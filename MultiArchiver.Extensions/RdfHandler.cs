﻿using IS4.MultiArchiver.Services;
using IS4.MultiArchiver.Tools.Xml;
using IS4.MultiArchiver.Vocabulary;
using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using VDS.RDF;

namespace IS4.MultiArchiver.Extensions
{
    public class RdfHandler : VocabularyCache<IUriNode, IRdfHandler>, ILinkedNodeFactory
    {
        readonly IRdfHandler defaultHandler;
        readonly IReadOnlyDictionary<Uri, IRdfHandler> graphHandlers;
        readonly IEntityAnalyzerProvider baseAnalyzer;

        public ILinkedNode Root { get; }

        public IDictionary<Uri, string> PrefixMap { get; }

        public RdfHandler(Uri root, IEntityAnalyzerProvider baseAnalyzer, IRdfHandler defaultHandler, IReadOnlyDictionary<Uri, IRdfHandler> graphHandlers)
            : base(defaultHandler.CreateUriNode, uri => graphHandlers.TryGetValue(uri, out var handler) ? handler : defaultHandler)
        {
            this.defaultHandler = defaultHandler;
            this.graphHandlers = graphHandlers;
            this.baseAnalyzer = baseAnalyzer;
            Root = Create(UriFormatter.Instance, root);

            PrefixMap = new ConcurrentDictionary<Uri, string>(Vocabulary.Vocabularies.Prefixes, new UriComparer());

            VocabularyAdded += (vocabulary) => {
                var uri = new Uri(vocabulary.Value, UriKind.Absolute);
                var prefix = GetNamespace(uri);
                AddNamespace(defaultHandler, prefix, uri);
                foreach(var handler in graphHandlers.Values)
                {
                    AddNamespace(handler, prefix, uri);
                }
            };
        }

        int namespaceCounter;

        private string GetNamespace(Uri uri)
        {
            return PrefixMap.TryGetValue(uri, out var prefix) ?
                prefix : $"ns{System.Threading.Interlocked.Increment(ref namespaceCounter)}";
        }

        private void AddNamespace(IRdfHandler handler, string prefix, Uri uri)
        {
            lock(handler)
            {
                handler.HandleNamespace(prefix, uri);
            }
        }

        public ILinkedNode Create<T>(IIndividualUriFormatter<T> formatter, T value)
        {
            var uri = formatter[value];
            if(uri == null)
            {
                return null;
            }
            return new UriNode(defaultHandler.CreateUriNode(uri), defaultHandler, this);
        }

        /// <summary>
        /// Detects a string that is unsafe for embedding or displaying.
        /// XML 1.0 prohibits C0 control codes and discourages the use of C1, with the exception of line separators;
        /// such characters cannot be encoded in RDF/XML and are semantically invalid.
        /// Unpaired surrogate characters are also prohibited (since the input must be a valid UTF-16 string).
        /// Additionally, a leading combining character or ZWJ could cause troubles
        /// when displayed.
        /// Other unassigned or invalid characters are detected later.
        /// </summary>
        static readonly Regex unsafeStringRegex = new Regex(@"^[\p{M}\u200D]|[\x00-\x08\x0B\x0C\x0E-\x1F\x7F-\x84\x86-\x9F]|(^|[^\uD800-\uDBFF])[\uDC00-\uDFFF]|[\uD800-\uDBFF]($|[^\uDC00-\uDFFF])", RegexOptions.Compiled | RegexOptions.Multiline);

        static bool IsSafeString(string str)
        {
            if(unsafeStringRegex.IsMatch(str)) return false;
            var e = StringInfo.GetTextElementEnumerator(str);
            while(e.MoveNext())
            {
                if(Char.GetUnicodeCategory(str, e.ElementIndex) == UnicodeCategory.OtherNotAssigned)
                {
                    return false;
                }
            }
            return true;
        }

        bool ILinkedNodeFactory.IsSafeString(string str)
        {
            return IsSafeString(str);
        }

        class UriNode : LinkedNode<INode, IRdfHandler>
        {
            public UriNode(INode subject, IRdfHandler handler, IVocabularyCache<INode, IRdfHandler> cache) : base(subject, handler, cache)
            {
                if(!(subject is IUriNode)) throw new ArgumentException(null, nameof(subject));
            }

            protected override void HandleTriple(INode subj, INode pred, INode obj)
            {
                var date = DateTime.UtcNow;
                lock(Graph)
                {
                    Graph.HandleTriple(new Triple(subj, pred, obj));
                }
                var meta = In(Graphs.Metadata);
                if(meta != null && (!Equals(meta) || !(pred is IUriNode uriNode) || uriNode.Uri.AbsoluteUri != Properties.Visited.Value))
                {
                    var dateString = XmlConvert.ToString(date, "yyyy-MM-dd\\THH:mm:ssK");
                    meta.Set(Properties.Visited, dateString, Datatypes.DateTime);
                }
            }

            private bool HandleExternalTriple(INode subj, INode pred, INode obj)
            {
                if(Subject.Equals(subj) && !(obj is IBlankNode))
                {
                    HandleTriple(subj, pred, obj);
                    return true;
                }
                return false;
            }

            protected override INode CreateNode(Uri uri)
            {
                return Graph.CreateUriNode(uri);
            }

            protected override INode CreateNode(string value)
            {
                if(IsSafeString(value)) return Graph.CreateLiteralNode(value);
                return Graph.CreateLiteralNode($@"{{""@value"":{HttpUtility.JavaScriptStringEncode(value, true)}}}", GetUri(Cache[Datatypes.Json]));
            }

            protected override INode CreateNode(string value, INode datatype)
            {
                var dt = GetUri(datatype);
                if(IsSafeString(value)) return Graph.CreateLiteralNode(value, dt);
                return Graph.CreateLiteralNode($@"{{""@value"":{HttpUtility.JavaScriptStringEncode(value, true)},""@type"":{HttpUtility.JavaScriptStringEncode(dt.IsAbsoluteUri ? dt.AbsoluteUri : dt.OriginalString, true)}}}", GetUri(Cache[Datatypes.Json]));
            }

            protected override INode CreateNode(string value, string language)
            {
                if(IsSafeString(value)) return Graph.CreateLiteralNode(value, language);
                return Graph.CreateLiteralNode($@"{{""@value"":{HttpUtility.JavaScriptStringEncode(value, true)},""@language"":{HttpUtility.JavaScriptStringEncode(language, true)}}}", GetUri(Cache[Datatypes.Json]));
            }

            protected override INode CreateNode(bool value)
            {
                return LiteralExtensions.ToLiteral(value, Graph);
            }

            protected override INode CreateNode<T>(T value)
            {
                try{
                    return LiteralExtensions.ToLiteral((dynamic)value, Graph);
                }catch(RuntimeBinderException e)
                {
                    throw new ArgumentException(null, nameof(value), e);
                }
            }

            protected override Uri GetUri(INode node)
            {
                return ((IUriNode)node).Uri;
            }

            protected override LinkedNode<INode, IRdfHandler> CreateNew(INode subject, IRdfHandler graph)
            {
                return new UriNode(subject, Graph, Cache);
            }

            protected override IRdfHandler CreateGraphNode(Uri uri)
            {
                var parent = (RdfHandler)Cache;
                return parent.graphHandlers.TryGetValue(uri, out var handler) ? handler : parent.defaultHandler;
            }

            private XmlDocument PrepareXmlDocument(XmlReader rdfXmlReader)
            {
                if(rdfXmlReader.NodeType != XmlNodeType.Element || rdfXmlReader.NamespaceURI != "http://www.w3.org/1999/02/22-rdf-syntax-ns#" || rdfXmlReader.LocalName != "RDF")
                {
                    throw new ArgumentException("The XML reader must be positioned on a rdf:RDF element.", nameof(rdfXmlReader));
                }
                return new BaseXmlDocument(GetUri(Subject).AbsoluteUri, rdfXmlReader.NameTable);
            }

            public override void Describe(XmlReader rdfXmlReader)
            {
                var doc = PrepareXmlDocument(rdfXmlReader);
                doc.Load(rdfXmlReader);
                Describe(doc);
            }

            public override async Task DescribeAsync(XmlReader rdfXmlReader)
            {
                var doc = PrepareXmlDocument(rdfXmlReader);
                await doc.LoadAsync(rdfXmlReader);
                Describe(doc);
            }

            public override void Describe(XmlDocument rdfXmlDocument)
            {
                if(rdfXmlDocument is BaseXmlDocument baseXmlDocument && baseXmlDocument.BaseURI == null)
                {
                    baseXmlDocument.SetBaseURI(GetUri(Subject).AbsoluteUri);
                }
                var graph = new DataGraph(this);
                var parser = new VDS.RDF.Parsing.RdfXmlParser();
                parser.Load(graph, rdfXmlDocument);
            }

            class DataGraph : Graph
            {
                readonly UriNode describingNode;

                public DataGraph(UriNode describingNode) : base(true)
                {
                    this.describingNode = describingNode;
                }

                public override bool Assert(Triple t)
                {
                    return describingNode.HandleExternalTriple(t.Subject, t.Predicate, t.Object);
                }
            }
        }
    }
}

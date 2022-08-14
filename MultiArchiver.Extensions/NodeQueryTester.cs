﻿using System;
using System.Collections.Generic;
using VDS.RDF;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;

namespace IS4.MultiArchiver
{
    /// <summary>
    /// Tests instances of <see cref="INode"/> whether they match
    /// any of the input SPARQL queries.
    /// </summary>
    public class NodeQueryTester
    {
        readonly IReadOnlyCollection<SparqlQuery> queries;
        readonly IRdfHandler rdfHandler;
        readonly LeviathanQueryProcessor processor;

        /// <summary>
        /// Creates a new instance of the tester.
        /// </summary>
        /// <param name="rdfHandler">The RDF handler to use to store the result of CONSTRUCT queries.</param>
        /// <param name="queryGraph">The graph to process with the queries.</param>
        /// <param name="queries">The collection of SPARQL queries to process.</param>
        public NodeQueryTester(IRdfHandler rdfHandler, Graph queryGraph, IReadOnlyCollection<SparqlQuery> queries)
        {
            this.queries = queries;
            this.rdfHandler = rdfHandler;
            var dataset = new InMemoryDataset(queryGraph);
            processor = new LeviathanQueryProcessor(dataset);
        }

        /// <summary>
        /// Matches an instance of <see cref="INode"/> against the stored SPARQL queries.
        /// In order for the match to be successful, the variables bound by the query
        /// must contain a "?node" variable equal to <paramref name="subject"/>.
        /// </summary>
        /// <param name="subject">The matching node to be identified by the queries.</param>
        /// <param name="properties">Additional variables from a successful match, as instances of <see cref="Uri"/> or <see cref="String"/>.</param>
        /// <returns>True if any of the queries successfully matched the node.</returns>
        public bool Match(INode subject, out IReadOnlyDictionary<string, object> properties)
        {
            Dictionary<string, object> variables = null;
            var success = false;
            foreach(var query in queries)
            {
                switch(processor.ProcessQuery(query))
                {
                    case IGraph resultsGraph:
                        foreach(var triple in resultsGraph.Triples)
                        {
                            rdfHandler.HandleTriple(triple);
                        }
                        break;
                    case IEnumerable<SparqlResult> resultSet:
                        foreach(var result in resultSet)
                        {
                            if(result.TryGetValue("node", out var node) && node.Equals(subject))
                            {
                                success = true;
                                if(variables == null)
                                {
                                    variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                                }
                                foreach(var pair in result)
                                {
                                    switch(pair.Value)
                                    {
                                        case IUriNode uriValue:
                                            variables[pair.Key] = uriValue.Uri;
                                            break;
                                        case ILiteralNode literalValue:
                                            variables[pair.Key] = literalValue.Value;
                                            break;
                                    }
                                }
                            }
                        }
                        break;
                }
            }
            properties = variables;
            return success;
        }
    }
}

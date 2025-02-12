﻿using IS4.SFI.Formats;
using IS4.SFI.Services;
using IS4.SFI.Tools;
using IS4.SFI.Vocabulary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace IS4.SFI.Analyzers
{
    /// <summary>
    /// An analyzer describing instances of <see cref="IDataObject"/>.
    /// </summary>
    [Description("An analyzer of data objects, describing their common properties and linking their formats.")]
    public class DataObjectAnalyzer : EntityAnalyzer<IDataObject>
    {
        /// <summary>
        /// Stores the number of digits used for <see cref="TextTools.SizeSuffix(long, int)"/>
        /// when creating the label.
        /// </summary>
        [Description("Stores the number of digits used when creating the label.")]
        public int LabelSizeSuffixDigits { get; set; } = 2;

        /// <inheritdoc/>
        public async override ValueTask<AnalysisResult> Analyze(IDataObject dataObject, AnalysisContext context, IEntityAnalyzers analyzers)
        {
            var node = GetNode(context);

            node.SetAsBase();

            var isBinary = dataObject.IsBinary;
            var charset = dataObject.Charset;

            if(charset != null)
            {
                node.Set(Properties.CharacterEncoding, charset);
            }

            if(dataObject.IsComplete)
            {
                if(isBinary)
                {
                    node.Set(Properties.Bytes, dataObject.ByteValue.ToBase64String(), Datatypes.Base64Binary);
                }else if(dataObject.StringValue != null)
                {
                    node.Set(Properties.Chars, DataTools.ReplaceControlCharacters(dataObject.StringValue, dataObject.Encoding), Datatypes.String);
                }
            }else if(!isBinary && dataObject.StringValue != null && dataObject.Encoding != null && DataTools.ExtractFirstLine(dataObject.StringValue) is string firstLine)
            {
                var firstLineNode = node["#line=,1"];
                if(firstLineNode != null)
                {
                    node.Set(Properties.HasPart, firstLineNode);
                    firstLineNode.Set(Properties.Value, firstLine);
                }
            }

            var sizeSuffix = TextTools.SizeSuffix(dataObject.ActualLength, LabelSizeSuffixDigits);

            var label = $"{(isBinary ? "binary data" : "text")} ({sizeSuffix})";

            node.Set(Properties.PrefLabel, label, LanguageCode.En);

            node.SetClass(isBinary ? Classes.ContentAsBase64 : Classes.ContentAsText);
            node.Set(Properties.Extent, dataObject.ActualLength, Datatypes.Byte);
            
            foreach(var (algorithm, value) in dataObject.Hashes)
            {
                await HashAlgorithm.AddHash(node, algorithm, value, context.NodeFactory, OnOutputFile);
            }

            KeyValuePair<IBinaryFormatObject, AnalysisResult>? primaryFormat = null;

            foreach(var pair in dataObject.Formats)
            {
                primaryFormat ??= pair;
                var formatNode = pair.Value.Node;
                if(formatNode != null)
                {
                    node.Set(Properties.HasFormat, formatNode);
                }
            }

            if(primaryFormat == null && !dataObject.IsPlain)
            {
                // Prepare an improvised format if no other format was recognized
                ImprovisedFormat.Format? improvisedFormat = null;

                if(isBinary && DataTools.ExtractSignature(dataObject.ByteValue) is string magicText)
                {
                    improvisedFormat = new SignatureFormat(magicText);
                }

                if(!isBinary && dataObject.StringValue != null && DataTools.ExtractInterpreter(dataObject.StringValue) is string interpreter)
                {
                    improvisedFormat = new InterpreterFormat(interpreter);
                }

                if(improvisedFormat != null)
                {
                    var formatObj = new BinaryFormatObject<ImprovisedFormat.Format>(dataObject, ImprovisedFormat.Instance, improvisedFormat);
                    await analyzers.Analyze(formatObj, context.WithParentLink(node, Properties.HasFormat));
                    primaryFormat = new(formatObj, default);
                }
            }

            label = primaryFormat?.Value.Label ?? label;

            foreach(var properties in node.Match())
            {
                var format = primaryFormat?.Key;
                if(format?.Extension is string extension)
                {
                    properties.Extension ??= "." + extension;
                }
                if(format?.MediaType is string mediaType)
                {
                    properties.MediaType ??= mediaType;
                }
                properties.Size ??= dataObject.ActualLength;
                if(label != null)
                {
                    properties.Name ??= label;
                }

                await OnOutputFile(dataObject.IsBinary, properties, async stream => {
                    using var input = dataObject.StreamFactory.Open();
                    await input.CopyToAsync(stream);
                });
            }

            return new AnalysisResult(node);
        }

        /// <summary>
        /// This improvised format is used for binary files when the signature can be extracted
        /// via <see cref="DataTools.ExtractSignature(ArraySegment{byte})"/>.
        /// </summary>
        class SignatureFormat : ImprovisedFormat.Format
        {
            /// <summary>
            /// The extension is the signature of the file.
            /// </summary>
            public override string Extension { get; }

            /// <summary>
            /// The media type is produced by <see cref="TextTools.GetImpliedMediaTypeFromSignature(string)"/>.
            /// </summary>
            public override string MediaType => TextTools.GetImpliedMediaTypeFromSignature(Extension);

            public SignatureFormat(string signature)
            {
                Extension = signature;
            }
        }

        /// <summary>
        /// This improvised format is used for text files when the interpreter command can be extracted
        /// via <see cref="DataTools.ExtractInterpreter(string)"/>.
        /// </summary>
        class InterpreterFormat : ImprovisedFormat.Format
        {
            /// <summary>
            /// The extension is the interpreter command.
            /// </summary>
            public override string Extension { get; }

            /// <summary>
            /// The media type is produced by <see cref="TextTools.GetImpliedMediaTypeFromInterpreter(string)"/>.
            /// </summary>
            public override string MediaType => TextTools.GetImpliedMediaTypeFromInterpreter(Extension);

            public InterpreterFormat(string interpreter)
            {
                Extension = interpreter;
            }
        }

        /// <summary>
        /// An improvised format is created when there are no other formats detectable from the input.
        /// Its properties are implied based on the data itself and serve to link data likely in the same format
        /// even when the format is unknown.
        /// </summary>
        class ImprovisedFormat : BinaryFileFormat<ImprovisedFormat.Format>
        {
            public static readonly ImprovisedFormat Instance = new();

            private ImprovisedFormat() : base(0, null, null)
            {

            }


            public override string? GetMediaType(Format value)
            {
                return value.MediaType;
            }

            public override string? GetExtension(Format value)
            {
                return value.Extension;
            }

            public override bool CheckHeader(ReadOnlySpan<byte> header, bool isBinary, IEncodingDetector? encodingDetector)
            {
                throw new NotSupportedException();
            }

            public async override ValueTask<TResult?> Match<TResult, TArgs>(Stream stream, MatchContext context, ResultFactory<Format, TResult, TArgs> resultFactory, TArgs args) where TResult : default
            {
                throw new NotSupportedException();
            }

            public abstract class Format
            {
                public abstract string Extension { get; }

                public abstract string MediaType { get; }
            }
        }
    }
}

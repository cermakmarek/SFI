﻿using DiscUtils.Iso9660;
using IS4.MultiArchiver.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace IS4.MultiArchiver.Formats
{
    /// <summary>
    /// Represents the ISO CD image format, as an instance of <see cref="CDReader"/>.
    /// </summary>
    public class IsoFormat : BinaryFileFormat<CDReader>
    {
        /// <inheritdoc cref="FileFormat{T}.FileFormat(string, string)"/>
        public IsoFormat() : base(0, "application/x-iso9660-image", "iso")
        {

        }

        public override bool CheckHeader(ArraySegment<byte> header, bool isBinary, IEncodingDetector encodingDetector)
        {
            return isBinary;
        }

        public override bool CheckHeader(Span<byte> header, bool isBinary, IEncodingDetector encodingDetector)
        {
            return isBinary;
        }

        public override async ValueTask<TResult> Match<TResult, TArgs>(Stream stream, MatchContext context, ResultFactory<CDReader, TResult, TArgs> resultFactory, TArgs args)
        {
            using(var reader = new CDReader(stream, true))
            {
                return await resultFactory(reader, args);
            }
        }
    }
}

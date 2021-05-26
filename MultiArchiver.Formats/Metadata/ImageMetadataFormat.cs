﻿using IS4.MultiArchiver.Services;
using MetadataExtractor;
using MetadataExtractor.Formats.FileType;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IS4.MultiArchiver.Formats
{
    public class ImageMetadataFormat : BinaryFileFormat<IReadOnlyList<MetadataExtractor.Directory>>
    {
        public ImageMetadataFormat() : base(0, null, null)
        {

        }

        public override string GetExtension(IReadOnlyList<MetadataExtractor.Directory> metadata)
        {
            return GetFileTag(metadata, FileTypeDirectory.TagExpectedFileNameExtension) ?? GetTypeFromName(GetFileTag(metadata, FileTypeDirectory.TagDetectedFileTypeName)).Item2 ?? base.GetExtension(metadata);
        }

        public override string GetMediaType(IReadOnlyList<MetadataExtractor.Directory> metadata)
        {
            return GetFileTag(metadata, FileTypeDirectory.TagDetectedFileMimeType) ?? GetTypeFromName(GetFileTag(metadata, FileTypeDirectory.TagDetectedFileTypeName)).Item1 ?? base.GetMediaType(metadata);
        }

        private string GetFileTag(IReadOnlyList<MetadataExtractor.Directory> metadata, int tag)
        {
            return metadata.OfType<FileTypeDirectory>().FirstOrDefault()?.GetString(tag);
        }

        private (string, string) GetTypeFromName(string name)
        {
            if(name == "RIFF") return ("application/x-riff", "riff");
            return default;
        }

        public override TResult Match<TResult>(Stream stream, ResultFactory<IReadOnlyList<MetadataExtractor.Directory>, TResult> resultFactory)
        {
            return resultFactory(ImageMetadataReader.ReadMetadata(stream));
        }

        public override bool CheckHeader(ArraySegment<byte> header, bool isBinary, IEncodingDetector encodingDetector)
        {
            return true;
        }

        public override bool CheckHeader(Span<byte> header, bool isBinary, IEncodingDetector encodingDetector)
        {
            return true;
        }
    }
}

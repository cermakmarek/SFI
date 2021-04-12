﻿using IS4.MultiArchiver.Services;
using System;
using System.IO;
using System.IO.Compression;

namespace IS4.MultiArchiver.Formats
{
    public class ZipFileFormat : FileFormat<ZipArchive>
    {
        public ZipFileFormat() : base(2, "application/zip", "zip")
        {

        }

        public override bool Match(Span<byte> header)
        {
            return header.Length >= 2 && header[0] == 0x50 && header[1] == 0x4B;
        }

        public override TResult Match<TResult>(Stream stream, Func<ZipArchive, TResult> resultFactory)
        {
            using(var archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                return resultFactory(archive);
            }
        }
    }
}

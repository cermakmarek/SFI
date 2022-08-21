﻿using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace IS4.MultiArchiver.Formats
{
    /// <summary>
    /// Represents the ZIP archive format, creating an instance of <see cref="ZipArchive"/>.
    /// </summary>
    public class ZipFileFormat : SignatureFormat<ZipArchive>
    {
        /// <inheritdoc cref="FileFormat{T}.FileFormat(string, string)"/>
        public ZipFileFormat() : base("PK", "application/zip", "zip")
        {

        }

        public override async ValueTask<TResult> Match<TResult, TArgs>(Stream stream, MatchContext context, ResultFactory<ZipArchive, TResult, TArgs> resultFactory, TArgs args)
        {
            using(var archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                return await resultFactory(archive, args);
            }
        }
    }
}

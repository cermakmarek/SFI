﻿using IS4.MultiArchiver.Services;
using SharpCompress.Archives.SevenZip;
using System.IO;

namespace IS4.MultiArchiver.Formats
{
    public class SevenZipFormat : ArchiveFormat<SevenZipArchive>
    {
        public SevenZipFormat() : base("7z", "application/x-7z-compressed", "7z")
        {

        }

        public override TResult Match<TResult, TArgs>(Stream stream, ResultFactory<SevenZipArchive, TResult, TArgs> resultFactory, TArgs args)
        {
            using(var archive = SevenZipArchive.Open(stream))
            {
                if(!CheckArchive(archive)) return default;
                return resultFactory(archive, args);
            }
        }
    }
}

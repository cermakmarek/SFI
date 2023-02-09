﻿using DiscUtils.Udf;
using System.IO;

namespace IS4.SFI.Formats
{
    /// <summary>
    /// Represents the Universal Disk Format, as an instance of <see cref="UdfReader"/>.
    /// </summary>
    public class UdfFormat : DiscFormat<UdfReader>
    {
        /// <inheritdoc cref="FileFormat{T}.FileFormat(string, string)"/>
        public UdfFormat() : base("application/x-udf", "udf")
        {

        }

        /// <inheritdoc/>
        protected override UdfReader Create(Stream stream)
        {
            return new UdfReader(stream);
        }
    }
}

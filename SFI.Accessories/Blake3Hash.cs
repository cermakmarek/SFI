﻿using Blake3;
using IS4.SFI.Services;
using IS4.SFI.Tools;
using IS4.SFI.Vocabulary;
using System;
using System.Threading.Tasks;

namespace IS4.SFI
{
    /// <summary>
    /// The BLAKE3 hash algorithm, using <see cref="Blake3.Hasher"/>.
    /// </summary>
    public class Blake3Hash : StreamDataHash<Hasher>
    {
        /// <inheritdoc/>
        public override int? NumericIdentifier => 0x1e;

        /// <inheritdoc cref="StreamDataHash{T}.StreamDataHash(IndividualUri, int, string, FormattingMethod)"/>
        public Blake3Hash() : base(Individuals.Blake3, 32, "urn:blake3:", FormattingMethod.Base32)
        {

        }

        /// <inheritdoc/>
        protected override Hasher Initialize()
        {
            return Hasher.New();
        }

        /// <inheritdoc/>
        protected override void Append(ref Hasher instance, ArraySegment<byte> segment)
        {
            instance.Update(segment.AsSpan());
        }

        /// <inheritdoc/>
        protected override byte[] Output(ref Hasher instance)
        {
            var hash = instance.Finalize();
            return hash.AsSpan().ToArray();
        }

        /// <inheritdoc/>
        protected override void Finalize(ref Hasher instance)
        {
            instance.Dispose();
        }

        /// <inheritdoc/>
        public async override ValueTask<byte[]> ComputeHash(byte[] data, IPersistentKey? key = null)
        {
            var hash = Hasher.Hash(new ReadOnlySpan<byte>(data));
            return hash.AsSpan().ToArray();
        }

        /// <inheritdoc/>
        public async override ValueTask<byte[]> ComputeHash(byte[] data, int offset, int count, IPersistentKey? key = null)
        {
            var hash = Hasher.Hash(new ReadOnlySpan<byte>(data, offset, count));
            return hash.AsSpan().ToArray();
        }
    }
}

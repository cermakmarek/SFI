﻿using IS4.MultiArchiver.Media;
using IS4.MultiArchiver.Services;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CryptographicException = System.Security.Cryptography.CryptographicException;
using SharpCompressCryptoException = SharpCompress.Common.CryptographicException;

namespace IS4.MultiArchiver.Formats.Archives
{
    public class ArchiveReaderAdapter : IArchiveReader
    {
        readonly IReader reader;
        readonly Dictionary<string, ArchiveDirectoryInfo> directories = new Dictionary<string, ArchiveDirectoryInfo>();
        IEnumerator<KeyValuePair<string, ArchiveDirectoryInfo>> directoryEnumerator;

        public ArchiveReaderAdapter(IReader reader)
        {
            this.reader = reader;
        }

        public IArchiveEntry Current { get; private set; }

        object IEnumerator.Current => Current;

        public void Dispose()
        {
            reader.Dispose();
        }

        public bool MoveNext()
        {
            if(MoveNextDirectories() is bool b) return b;
            try{
                if(reader.MoveToNextEntry())
                {
                    while(!UpdateNext())
                    {
                        if(!reader.MoveToNextEntry())
                        {
                            return StartNextDirectories();
                        }
                    }
                    return true;
                }
                return StartNextDirectories();
            }catch(SharpCompressCryptoException e)
            {
                throw new CryptographicException(e.Message, e);
            }
        }

        private bool StartNextDirectories()
        {
            directoryEnumerator = directories.GetEnumerator();
            return MoveNextDirectories() ?? false;
        }

        private bool? MoveNextDirectories()
        {
            if(directoryEnumerator == null) return null;
            while(directoryEnumerator.MoveNext())
            {
                Current = directoryEnumerator.Current.Value;
                var dir = GetDirectory(Current.Path);
                if(dir == null)
                {
                    return true;
                }
            }
            Current = null;
            return false;
        }

        private bool UpdateNext()
        {
            var entry = reader.Entry;
            IArchiveEntry info;
            string dir;
            if(entry.IsDirectory)
            {
                info = new ArchiveDirectoryInfo(reader, entry);
                if(!directories.TryGetValue(info.Path, out var dirInfo))
                {
                    directories[info.Path] = (ArchiveDirectoryInfo)info;
                }else{
                    dirInfo.Entry = entry;
                }
                dir = GetDirectory(info.Path);
                Current = null;
            }else{
                info = new ArchiveFileInfo(reader, entry);
                dir = GetDirectory(info.Path);
                Current = info;
                info = new ArchiveEntryInfo(reader, entry);
            }
            var exists = false;
            while(!exists && dir != null)
            {
                exists = directories.TryGetValue(dir, out var dirInfo);
                if(!exists)
                {
                    dirInfo = directories[dir] = new BlankDirectoryInfo(reader, dir);
                }
                dirInfo.Entries.Add(info);
                dir = GetDirectory(dir);
                info = dirInfo;
            }
            return Current != null;
        }

        public void Reset()
        {
            try{
                reader.Cancel();
            }catch(SharpCompressCryptoException e)
            {
                throw new CryptographicException(e.Message, e);
            }
        }

        public void Skip()
        {
            try{
                using(var stream = reader.OpenEntryStream())
                {
                    stream.SkipEntry();
                }
            }catch(SharpCompressCryptoException e)
            {
                throw new CryptographicException(e.Message, e);
            }
        }
        

        static string GetDirectory(string path)
        {
            if(path == null) return null;
            var nameIndex = path.LastIndexOf('/');
            if(nameIndex != -1) return path.Substring(0, nameIndex);
            return null;
        }
        
        class ArchiveEntryInfo : IArchiveEntry
        {
            protected IReader Reader { get; }
            public IEntry Entry { get; set; }

            public ArchiveEntryInfo(IReader reader, IEntry entry)
            {
                Reader = reader;
                Entry = entry;
            }

            public virtual string Name => Path != null ? System.IO.Path.GetFileName(Path) : null;

            public string SubName => null;

            public virtual string Path => ArchiveAdapter.ExtractPathSimple(Entry);

            public DateTime? CreationTime => Entry?.CreatedTime;

            public DateTime? LastWriteTime => Entry?.LastModifiedTime;

            public DateTime? LastAccessTime => Entry?.LastAccessedTime;

            public DateTime? ArchivedTime => Entry?.ArchivedTime;

            public int? Revision => null;

            object IPersistentKey.ReferenceKey => Reader;

            object IPersistentKey.DataKey => Path;

            public FileKind Kind => FileKind.ArchiveItem;

            public override string ToString()
            {
                return "/" + Path;
            }
        }

        class ArchiveFileInfo : ArchiveEntryInfo, IFileInfo
        {
            public ArchiveFileInfo(IReader reader, IEntry entry) : base(reader, entry)
            {

            }

            public long Length => Entry.Size;

            public bool IsEncrypted => Entry.IsEncrypted;

            public StreamFactoryAccess Access => StreamFactoryAccess.Single;

            public Stream Open()
            {
                return Reader.OpenEntryStream();
            }
        }

        class ArchiveDirectoryInfo : ArchiveEntryInfo, IDirectoryInfo
        {
            public List<IFileNodeInfo> Entries { get; } = new List<IFileNodeInfo>();

            public ArchiveDirectoryInfo(IReader reader, IEntry entry) : base(reader, entry)
            {

            }

            IEnumerable<IFileNodeInfo> IDirectoryInfo.Entries => Entries;
        }

        class BlankDirectoryInfo : ArchiveDirectoryInfo
        {
            public BlankDirectoryInfo(IReader reader, string path) : base(reader, null)
            {
                Path = path;
            }

            public override string Path { get; }
        }
    }
}

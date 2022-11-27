﻿using IS4.SFI.Analyzers;
using IS4.SFI.Application;
using IS4.SFI.Services;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace IS4.SFI.WebApp
{
    /// <summary>
    /// The specific implementation of <see cref="Inspector"/> for the web application.
    /// </summary>
    public class WebInspector : ExtensibleInspector
    {
        /// <inheritdoc/>
        public WebInspector()
        {
            DataAnalyzer.FileSizeToWriteToDisk = Int64.MaxValue;
        }

        /// <inheritdoc/>
        public async override ValueTask AddDefault()
        {
            BaseFormats.AddDefault(Analyzers, DataAnalyzer.DataFormats, XmlAnalyzer.XmlFormats, ContainerProviders);
            ExternalFormats.AddDefault(Analyzers, DataAnalyzer.DataFormats, XmlAnalyzer.XmlFormats, ContainerProviders);
            ExtensionsFormats.AddDefault(Analyzers, DataAnalyzer.DataFormats, XmlAnalyzer.XmlFormats, ContainerProviders);

            Plugins.Clear();
            await LoadPlugins();

            await base.AddDefault();
        }

        /// <inheritdoc/>
        protected override Assembly LoadFromFile(IFileInfo file, IDirectoryInfo mainDirectory)
        {
            var context = new PluginLoadContext();
            context.AddDirectory(mainDirectory);
            return context.LoadFromFile(file);
        }

        async ValueTask LoadPlugins()
        {
            var files = Pages.Index.PluginFiles.ToList();
            foreach(var (name, file) in files)
            {
                ArraySegment<byte> data;
                using(var stream = file.OpenReadStream(Int64.MaxValue))
                {
                    var buffer = new MemoryStream();
                    await stream.CopyToAsync(buffer);
                    if(!buffer.TryGetBuffer(out data))
                    {
                        data = new ArraySegment<byte>(buffer.ToArray());
                    }
                }
                ZipArchive archive;
                try{
                    var buffer = new MemoryStream(data.Array!, data.Offset, data.Count, false);
                    archive = new ZipArchive(buffer, ZipArchiveMode.Read);
                }catch(Exception e)
                {
                    OutputLog?.WriteLine($"An error occurred while opening plugin archive {name}: " + e);
                    continue;
                }
                Plugins.Add(new Plugin(GetDirectory(archive), Path.ChangeExtension(name, ".dll")));
            }
        }
    }
}

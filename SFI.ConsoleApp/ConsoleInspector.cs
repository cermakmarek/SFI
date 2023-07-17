﻿using IS4.SFI.Analyzers;
using IS4.SFI.Application;
using IS4.SFI.Application.Plugins;
using IS4.SFI.Application.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace IS4.SFI.ConsoleApp
{
    /// <summary>
    /// The specific implementation of <see cref="Inspector"/> for the console application.
    /// </summary>
    class ConsoleInspector : ExtensibleInspector
    {
        string baseDirectory = Path.Combine(AppContext.BaseDirectory, "plugins");

#if WINDOWS || NETFRAMEWORK
        /// <summary>
        /// The default image analyzer.
        /// </summary>
        public ImageAnalyzer? ImageAnalyzer { get; private set; }
#endif

        /// <inheritdoc/>
        public ConsoleInspector()
        {

        }

        public async override ValueTask AddDefault()
        {
#if WINDOWS || NETFRAMEWORK
            await LoadAssembly(Formats.All.Provider.Assembly);
            await LoadAssembly(Hashes.All.Provider.Assembly);

            ImageAnalyzer = Analyzers.OfType<ImageAnalyzer>().FirstOrDefault()!;

            var algorithms = ImageAnalyzer.DataHashAlgorithms;
            foreach(var algorithm in DataAnalyzer.HashAlgorithms)
            {
                if(!algorithms.Contains(algorithm))
                {
                    algorithms.Add(algorithm);
                }
            }
#else
            await LoadAssembly(Formats.AllPortable.Provider.Assembly);
            await LoadAssembly(Hashes.AllPortable.Provider.Assembly);
#endif

            Plugins.Clear();
            foreach(var plugin in LoadPlugins())
            {
                Plugins.Add(plugin);
            }

            await base.AddDefault();
        }

        IEnumerable<Plugin> LoadPlugins()
        {
            if(Directory.Exists(baseDirectory))
            {
                foreach(var dir in Directory.EnumerateDirectories(baseDirectory))
                {
                    yield return PluginResolvers.GetPluginFromDirectory(dir);
                }

                foreach(var zip in Directory.EnumerateFiles(baseDirectory, "*.zip"))
                {
                    yield return PluginResolvers.GetPluginFromZip(zip);
                }
            }
        }

        /// <inheritdoc/>
        protected override void AddLoadDirectories(PluginLoadContext context)
        {
            context.AddDirectory(baseDirectory);
            context.AddDirectory(AppContext.BaseDirectory);
        }
    }
}

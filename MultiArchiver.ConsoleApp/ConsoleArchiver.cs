﻿using IS4.MultiArchiver.Analyzers;
using IS4.MultiArchiver.Formats;
using IS4.MultiArchiver.Tools;

namespace IS4.MultiArchiver.ConsoleApp
{
    class ConsoleArchiver : Archiver
    {
        public ImageAnalyzer ImageAnalyzer { get; }

        public ConsoleArchiver()
        {
            Analyzers.Add(ImageAnalyzer = new ImageAnalyzer());

            ImageAnalyzer.LowFrequencyImageHashAlgorithms.Add(IS4.MultiArchiver.Analysis.Images.DHash.Instance);
            ImageAnalyzer.DataHashAlgorithms.Add(BuiltInHash.MD5);
            ImageAnalyzer.DataHashAlgorithms.Add(BuiltInHash.SHA1);
        }

        public override void AddDefault()
        {
            base.AddDefault();

            DataAnalyzer.DataFormats.Add(new ZipFormat());
            DataAnalyzer.DataFormats.Add(new RarFormat());
            DataAnalyzer.DataFormats.Add(new SevenZipFormat());
            DataAnalyzer.DataFormats.Add(new GZipFormat());
            DataAnalyzer.DataFormats.Add(new TarFormat());
            DataAnalyzer.DataFormats.Add(new SzFormat());
            DataAnalyzer.DataFormats.Add(new ImageMetadataFormat());
            DataAnalyzer.DataFormats.Add(new ImageFormat());
            DataAnalyzer.DataFormats.Add(new TagLibFormat());
            DataAnalyzer.DataFormats.Add(new IsoFormat());
            DataAnalyzer.DataFormats.Add(new DosModuleFormat());
            DataAnalyzer.DataFormats.Add(new GenericModuleFormat());
            DataAnalyzer.DataFormats.Add(new LinearModuleFormat());
            DataAnalyzer.DataFormats.Add(new Win16ModuleFormat());
            //DataAnalyzer.Formats.Add(new Win32ModuleFormat());
            DataAnalyzer.DataFormats.Add(new Win32ModuleFormatManaged());
            DataAnalyzer.DataFormats.Add(new WaveFormat());
            //DataAnalyzer.Formats.Add(new OggFormat());
            DataAnalyzer.DataFormats.Add(new WasapiFormat(false));
            DataAnalyzer.DataFormats.Add(new WasapiFormat(true));
            DataAnalyzer.DataFormats.Add(new DelphiFormFormat());
            DataAnalyzer.DataFormats.Add(new CabinetFormat());
            DataAnalyzer.DataFormats.Add(new OleStorageFormat());

            ContainerProviders.Add(new OpenPackageFormat());
            ContainerProviders.Add(new PackageDescriptionFormat());
            ContainerProviders.Add(new ExcelXmlDocumentFormat());
            ContainerProviders.Add(new ExcelDocumentFormat());
            ContainerProviders.Add(new WordXmlDocumentFormat());

            XmlAnalyzer.XmlFormats.Add(new SvgFormat());

            Analyzers.Add(new ArchiveAnalyzer());
            Analyzers.Add(new ArchiveReaderAnalyzer());
            Analyzers.Add(new FileSystemAnalyzer());
            Analyzers.Add(ImageMetadataAnalyzer.CreateDefault());
            Analyzers.Add(new TagLibAnalyzer());
            Analyzers.Add(new DosModuleAnalyzer());
            Analyzers.Add(new WinModuleAnalyzer());
            //Analyzers.Add(new WinVersionAnalyzer());
            Analyzers.Add(new WinVersionAnalyzerManaged());
            Analyzers.Add(new SvgAnalyzer());
            Analyzers.Add(new WaveAnalyzer());
            Analyzers.Add(new DelphiObjectAnalyzer());
            Analyzers.Add(new CabinetAnalyzer());
            Analyzers.Add(new OleStorageAnalyzer());
            Analyzers.Add(new PackageDescriptionAnalyzer());
        }
    }
}

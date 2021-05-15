﻿using IS4.MultiArchiver.Services;
using IS4.MultiArchiver.Vocabulary;
using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TagLib;
using Properties = IS4.MultiArchiver.Vocabulary.Properties;

namespace IS4.MultiArchiver.Analyzers
{
    public class TagLibAnalyzer : BinaryFormatAnalyzer<File>, IEntityAnalyzer<ICodec>, IPropertyUriFormatter<string>
    {
        static readonly ConditionalWeakTable<ICodec, string> codecPosition = new ConditionalWeakTable<ICodec, string>();

        public override string Analyze(ILinkedNode node, File file, ILinkedNodeFactory nodeFactory)
        {
            var properties = file.Properties;
            Set(node, Properties.Width, properties.PhotoWidth);
            Set(node, Properties.Height, properties.PhotoHeight);
            Set(node, Properties.Width, properties.VideoWidth);
            Set(node, Properties.Height, properties.VideoHeight);
            Set(node, Properties.BitsPerSample, properties.BitsPerSample);
            Set(node, Properties.SampleRate, properties.AudioSampleRate, Datatypes.Hertz);
            Set(node, Properties.Channels, properties.AudioChannels);
            Set(node, Properties.Duration, properties.Duration);

            if(properties.Codecs.Any())
            {
                if(!properties.Codecs.Skip(1).Any())
                {
                    var codec = properties.Codecs.First();
                    codecPosition.Add(codec, null);
                    nodeFactory.Create(node, codec);
                }else{
                    var codecCounters = new Dictionary<char, int>();

                    foreach(var codec in properties.Codecs)
                    {
                        char streamType;
                        switch(codec.MediaTypes)
                        {
                            case MediaTypes.Video:
                                streamType = 'v';
                                break;
                            case MediaTypes.Audio:
                                streamType = 'a';
                                break;
                            case MediaTypes.Photo:
                                streamType = 't';
                                break;
                            case MediaTypes.Text:
                                streamType = 's';
                                break;
                            default:
                                continue;
                        }
                        if(!codecCounters.TryGetValue(streamType, out int counter))
                        {
                            counter = 0;
                        }
                        codecCounters[streamType] = counter + 1;
                        codecPosition.Add(codec, streamType + ":" + counter);

                        var codecNode = nodeFactory.Create(node, codec);
                        if(codecNode != null)
                        {
                            node.Set(Properties.HasMediaStream, codecNode);
                        }
                    }
                }
            }

            if((file.TagTypes & (TagTypes.Id3v1 | TagTypes.Id3v2)) != 0)
            {
                node.SetClass(Classes.ID3Audio);
            }

            foreach(var prop in file.Tag.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if(!propertyNames.TryGetValue(prop.Name, out var name)) continue;
                var value = prop.GetValue(file.Tag);
                if(value is ICollection collection)
                {
                    foreach(var elem in collection)
                    {
                        try{
                            node.Set(this, name, (dynamic)elem);
                        }catch(RuntimeBinderException)
                        {

                        }
                    }
                    continue;
                }
                if(value == null || Double.NaN.Equals(value) || 0.Equals(value) || 0u.Equals(value) || false.Equals(value)) continue;
                try{
                    node.Set(this, name, (dynamic)value);
                }catch(RuntimeBinderException)
                {

                }
            }

            return null;
        }

        static readonly Dictionary<string, string> propertyNames = new Dictionary<string, string>
        {
            { nameof(Tag.Album), "albumTitle" },
            { nameof(Tag.BeatsPerMinute), "beatsPerMinute" },
            { nameof(Tag.Composers), "composer" },
            { nameof(Tag.Conductor), "conductor" },
            { nameof(Tag.Performers), "leadArtist" },
            { nameof(Tag.Genres), "contentType" },
            { nameof(Tag.Track), "trackNumber" },
            { nameof(Tag.Disc), "partOfSet" },
            { nameof(Tag.Grouping), "contentGroupDescription" },
            { nameof(Tag.AlbumArtists), "backgroundArtist" },
            { nameof(Tag.Comment), "comments" },
            { nameof(Tag.Copyright), "copyrightMessage" },
            { nameof(Tag.DateTagged), "comments" },
            { nameof(Tag.InitialKey), "initialKey" },
            { nameof(Tag.ISRC), "internationalStandardRecordingCode" },
            { nameof(Tag.RemixedBy), "interpretedBy" },
            { nameof(Tag.Publisher), "publisher" },
            { nameof(Tag.Subtitle), "subtitle" },
            { nameof(Tag.Title), "title" },
            { nameof(Tag.Year), "recordingYear" },

        };

        Uri IUriFormatter<string>.FormatUri(string name)
        {
            return new Uri($"http://www.semanticdesktop.org/ontologies/2007/05/10/nid3#{name}", UriKind.Absolute);
        }

        public ILinkedNode Analyze(ILinkedNode parent, ICodec codec, ILinkedNodeFactory nodeFactory)
        {
            if(!codecPosition.TryGetValue(codec, out var pos)) return null;
            var node = pos == null ? parent : parent[pos];
            if(node != null)
            {
                if(pos != null)
                {
                    node.SetClass(Classes.MediaStream);
                }
                Set(node, Properties.Duration, codec.Duration);
                if(codec is ILosslessAudioCodec losslessAudio)
                {
                    node.SetClass(Classes.Audio);
                    Set(node, Properties.BitsPerSample, losslessAudio.BitsPerSample);
                    node.Set(Properties.CompressionType, Individuals.LosslessCompressionType);
                }
                if(codec is IAudioCodec audio)
                {
                    node.SetClass(Classes.Audio);
                    Set(node, Properties.AverageBitrate, audio.AudioBitrate, Datatypes.KilobitPerSecond);
                    Set(node, Properties.Channels, audio.AudioChannels);
                    Set(node, Properties.SampleRate, audio.AudioSampleRate, Datatypes.Hertz);
                }
                if(codec is IVideoCodec video)
                {
                    node.SetClass(Classes.Video);
                    Set(node, Properties.Width, video.VideoWidth);
                    Set(node, Properties.Height, video.VideoHeight);
                }
                if(codec is IPhotoCodec photo)
                {
                    node.SetClass(Classes.Image);
                    Set(node, Properties.Width, photo.PhotoWidth);
                    Set(node, Properties.Height, photo.PhotoHeight);
                }
            }
            return node;
        }

        private void Set<T>(ILinkedNode node, Properties prop, T valueOrDefault) where T : struct, IEquatable<T>, IFormattable
        {
            if(valueOrDefault.Equals(default)) return;
            node.Set(prop, valueOrDefault);
        }

        private void Set<T>(ILinkedNode node, Properties prop, T valueOrDefault, Datatypes datatype) where T : struct, IEquatable<T>, IFormattable
        {
            if(valueOrDefault.Equals(default)) return;
            node.Set(prop, valueOrDefault, datatype);
        }
    }
}

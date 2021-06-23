﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace IS4.MultiArchiver
{
    public static class DataTools
    {
        static readonly string[] units = { "B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB", "ZiB", "YiB" };

        public static string SizeSuffix(long value, int decimalPlaces)
        {
            if(value < 0) return "-" + SizeSuffix(-value, decimalPlaces);
            if(value == 0) return String.Format(CultureInfo.InvariantCulture, $"{{0:0.{new string('#', decimalPlaces)}}} B", 0);

            int n = (int)Math.Log(value, 1024);
            decimal adjustedSize = (decimal)value / (1L << (n * 10));
            if(Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                n += 1;
                adjustedSize /= 1024;
            }
            return String.Format(CultureInfo.InvariantCulture, $"{{0:0.{new string('#', decimalPlaces)}}} {{1}}", adjustedSize, units[n]);
        }

        static readonly byte[][] knownBoms = new[]
        {
            Encoding.UTF8,
            Encoding.Unicode,
            Encoding.BigEndianUnicode,
            new UTF32Encoding(true, true),
            new UTF32Encoding(false, true)
        }.Select(e => e.GetPreamble()).ToArray();

        public static readonly int MaxBomLength = knownBoms.Max(b => b.Length);

        public static int FindBom(Span<byte> data)
        {
            foreach(var bom in knownBoms)
            {
                if(data.StartsWith(bom)) return bom.Length;
            }
            return 0;
        }

        static readonly Regex badCharacters = new Regex(@"://|//|[^a-zA-Z0-9._-]", RegexOptions.Compiled);

        public static string GetFakeMediaTypeFromXml(Uri ns, string publicId, string rootName)
        {
            if(ns == null)
            {
                if(publicId != null)
                {
                    ns = UriTools.CreatePublicId(publicId);
                }else{
                    return $"application/x.ns.{rootName}+xml";
                }
            }
            if(ns.HostNameType == UriHostNameType.Dns && !String.IsNullOrEmpty(ns.IdnHost))
            {
                var host = ns.IdnHost;
                var builder = new UriBuilder(ns);
                builder.Host = String.Join(".", host.Split('.').Reverse());
                if(!ns.Authority.EndsWith($":{builder.Port}", StringComparison.Ordinal))
                {
                    builder.Port = -1;
                }
                ns = builder.Uri;
            }
            var replaced = badCharacters.Replace(ns.OriginalString, m => {
                switch(m.Value)
                {
                    case "[":
                    case "]": return "";
                    case "%": return "&";
                    case ":":
                    case "/":
                    case "?":
                    case ";":
                    case "&":
                    case "=":
                    case "//":
                    case "://": return ".";
                    default: return String.Join("", Encoding.UTF8.GetBytes(m.Value).Select(b => $"&{b:X2}"));
                }
            });
            return $"application/x.ns.{replaced}.{rootName}+xml";
        }

        public static string GetFakeMediaTypeFromType<T>()
        {
            return FakeTypeNameCache<T>.Name;
        }

        static readonly Regex hyphenCharacters = new Regex(@"\p{Lu}+($|(?=\p{Lu}))|\p{Lu}(?!\p{Lu})", RegexOptions.Compiled);

        class FakeTypeNameCache<T>
        {
            public static readonly string Name = GetName();

            static string GetName()
            {
                return "application/x.obj." + GetTypeFriendlyName(typeof(T));
            }

            static string GetTypeFriendlyName(Type type)
            {
                var components = new List<string>();
                string name;
                if(String.IsNullOrEmpty(type.Namespace))
                {
                    name = type.Name;
                }else{
                    var similarTypes = type.Assembly.GetTypes().Where(t => t.IsPublic && !t.Equals(type) && t.Name.Equals(type.Name, StringComparison.OrdinalIgnoreCase));
                    int prefix = similarTypes.Select(t => CommonPrefix(t.Namespace, type.Namespace)).DefaultIfEmpty(type.Namespace.Length).Max();
                    name = (prefix == type.Namespace.Length ? "" : type.Namespace.Substring(prefix)) + type.Name;
                }
                int index = name.IndexOf('`');
                if(index != -1) name = name.Substring(0, index);
                components.Add(FormatName(name));
                components.AddRange(type.GetGenericArguments().Select(GetTypeFriendlyName));
                return String.Join(".", components);
            }

            static int CommonPrefix(string a, string b)
            {
                int max = Math.Min(a.Length, b.Length);
                for(int i = 0; i < max; i++)
                {
                    if(a[i] != b[i]) return i;
                }
                return max;
            }

            static string FormatName(string name)
            {
                name = hyphenCharacters.Replace(name, m => (m.Index > 0 ? "-" : "") + m.Value.ToLower());
                name = badCharacters.Replace(name, m =>  String.Join("", Encoding.UTF8.GetBytes(m.Value).Select(b => $"&{b:X2}")));
                return name;
            }
        }
        
        public static string GetFakeMediaTypeFromSignature(string signature)
        {
            return "application/x.sig." + signature.ToLowerInvariant();
        }

        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
        {
            key = pair.Key;
            value = pair.Value;
        }
        
        public static void Base32<TList>(TList bytes, StringBuilder sb) where TList : IReadOnlyList<byte>
        {
            const string chars = "QAZ2WSX3EDC4RFV5TGB6YHN7UJM8K9LP";

            byte index;
            int hi = 5;
            int currentByte = 0;

            while(currentByte < bytes.Count)
            {
                if(hi > 8)
                {
                    index = (byte)(bytes[currentByte++] >> (hi - 5));
                    if(currentByte != bytes.Count)
                    {
                        index = (byte)(((byte)(bytes[currentByte] << (16 - hi)) >> 3) | index);
                    }
                    hi -= 3;
                }else if(hi == 8)
                { 
                    index = (byte)(bytes[currentByte++] >> 3);
                    hi -= 3; 
                }else{
                    index = (byte)((byte)(bytes[currentByte] << (8 - hi)) >> 3);
                    hi += 5;
                }
                sb.Append(chars[index]);
            }
        }

        const string base58Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        static readonly BigInteger base58AlphabetLength = base58Alphabet.Length;

        public static void Base58<TList>(TList b, StringBuilder sb) where TList : IReadOnlyList<byte>
        {
            int pos = 0;
            while(b[pos] == 0)
            {
                sb.Append('1');
                pos++;
            }
            int len = b.Count - pos;
            if(len == 0) return;
            var data = new byte[len + (b[pos] > SByte.MaxValue ? 1 : 0)];
            for(int i = 0; i < len; i++)
            {
                data[len - 1 - i] = b[pos + i];
            }
            var num = new BigInteger(data);
            foreach(var c in Base58Bytes(num).Reverse())
            {
                sb.Append(base58Alphabet[c]);
            }
        }

        static IEnumerable<int> Base58Bytes(BigInteger num)
        {
            while(num > 0)
            {
                num = BigInteger.DivRem(num, base58AlphabetLength, out var rem);
                yield return (int)rem;
            }
        }

        public static void Base64(ArraySegment<byte> bytes, StringBuilder sb)
        {
            string str = Convert.ToBase64String(bytes.Array, bytes.Offset, bytes.Count);
            UriString(str, sb);
        }

        public static void Base64(byte[] bytes, StringBuilder sb)
        {
            string str = Convert.ToBase64String(bytes);
            UriString(str, sb);
        }

        static void UriString(string str, StringBuilder sb)
        {
            int end = 0;
            for(end = str.Length; end > 0; end--)
            {
                if(str[end - 1] != '=')
                {
                    break;
                }
            }

            for(int i = 0; i < end; i++)
            {
                char c = str[i];

                switch (c) {
                    case '+':
                        sb.Append('-');
                        break;
                    case '/':
                        sb.Append('_');
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
        }

        public static IEnumerable<byte> Varint(ulong value) 
        {
            while(value >= 0x80)
            {
                yield return (byte)(value | 0x80);
                value >>= 7;
            }
            yield return (byte)value;
        }
        
        static readonly ISet<byte> invalidSigBytes = new SortedSet<byte>(
            new byte[] { 0x09, 0x0A, 0x0D, (byte)' ', (byte)'-', (byte)'_' }
        );

        static readonly ISet<byte> recognizedSigBytes = new SortedSet<byte>(
            Enumerable.Range('a', 26).Concat(
                Enumerable.Range('A', 26)
            ).Concat(
                Enumerable.Range('0', 10)
            ).Select(i => (byte)i).Concat(invalidSigBytes)
        );

        const int maxSignatureLength = 8;

        public static string ExtractSignature(ArraySegment<byte> header)
        {
            var magicSig = header.Take(maxSignatureLength + 1).TakeWhile(b => recognizedSigBytes.Contains(b)).ToArray();
            if(magicSig.Length >= 2 && magicSig.Length <= maxSignatureLength && !magicSig.Any(b => invalidSigBytes.Contains(b)))
            {
                return Encoding.ASCII.GetString(magicSig);
            }
            return null;
        }

        static readonly Regex controlReplacement = new Regex(
            @"[\x00-\x08\x0B\x0C\x0E-\x1F]"
            , RegexOptions.Compiled);

        static int GetReplacementChar(char c)
        {
            switch(c)
            {
                case '\x7F':
                    return 0x2421;
                default:
                    return c + 0x2400;
            }
        }

        public static string ReplaceControlCharacters(string str, Encoding originalEncoding)
        {
            return controlReplacement.Replace(str, m => {
                var replacement = ((char)GetReplacementChar(m.Value[0])).ToString();
                try{
                    originalEncoding.GetBytes(replacement);
                }catch(ArgumentException)
                {
                    return replacement;
                }
                return m.Value;
            });
        }

        public static bool IsBinary(ArraySegment<byte> data)
        {
            int index = Array.IndexOf<byte>(data.Array, 0, data.Offset, data.Count);
            if(index != -1)
            {
                for(int i = index + 1; i < data.Offset + data.Count; i++)
                {
                    if(data.Array[i] != 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}

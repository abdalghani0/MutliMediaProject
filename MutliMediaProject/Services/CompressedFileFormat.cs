using System;
using System.IO;
using MutliMediaProject.Models;

namespace MutliMediaProject.Services
{
    /// <summary>
    /// Binary container for compressed audio with a tiny self-describing header.
    /// Each channel is stored as an independent encoded byte stream so each
    /// algorithm can be stateful per channel.
    ///
    /// Layout (little endian):
    ///   [0..3]   "AMCX"                magic bytes
    ///   [4]      version (1)
    ///   [5]      algorithm id          see CompressionAlgorithm
    ///   [6]      channels              1 or 2
    ///   [7]      original bits/sample  8 or 16
    ///   [8..11]  original sample rate
    ///   [12..15] encoded sample rate
    ///   [16..19] samples per channel   in encoded stream
    ///   [20]     bits per code         only meaningful for NLQ / DPCM
    ///   [21..24] param1 (float)        algorithm specific
    ///   [25..28] param2 (float)        algorithm specific
    ///   then per channel:
    ///     int32 length, byte[length]
    /// </summary>
    public static class CompressedFileFormat
    {
        public const string Extension = ".amcx";
        private static readonly byte[] Magic = { (byte)'A', (byte)'M', (byte)'C', (byte)'X' };
        private const byte Version = 1;

        public class Container
        {
            public CompressionAlgorithm Algorithm { get; set; }
            public int Channels { get; set; }
            public int OriginalBitsPerSample { get; set; }
            public int OriginalSampleRate { get; set; }
            public int EncodedSampleRate { get; set; }
            public int SamplesPerChannel { get; set; }
            public int BitsPerCode { get; set; }
            public float Param1 { get; set; }
            public float Param2 { get; set; }
            public byte[][] ChannelData { get; set; }
        }

        public static void Write(string path, Container c)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(Magic);
                bw.Write(Version);
                bw.Write((byte)c.Algorithm);
                bw.Write((byte)c.Channels);
                bw.Write((byte)c.OriginalBitsPerSample);
                bw.Write(c.OriginalSampleRate);
                bw.Write(c.EncodedSampleRate);
                bw.Write(c.SamplesPerChannel);
                bw.Write((byte)c.BitsPerCode);
                bw.Write(c.Param1);
                bw.Write(c.Param2);

                for (int ch = 0; ch < c.Channels; ch++)
                {
                    byte[] data = c.ChannelData[ch] ?? new byte[0];
                    bw.Write(data.Length);
                    bw.Write(data);
                }
            }
        }

        public static Container Read(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                var magic = br.ReadBytes(4);
                if (magic.Length != 4 || magic[0] != Magic[0] || magic[1] != Magic[1] || magic[2] != Magic[2] || magic[3] != Magic[3])
                    throw new InvalidDataException("Not an AMCX compressed file.");

                byte version = br.ReadByte();
                if (version != Version) throw new InvalidDataException("Unsupported AMCX version: " + version);

                var c = new Container
                {
                    Algorithm = (CompressionAlgorithm)br.ReadByte(),
                    Channels = br.ReadByte(),
                    OriginalBitsPerSample = br.ReadByte(),
                    OriginalSampleRate = br.ReadInt32(),
                    EncodedSampleRate = br.ReadInt32(),
                    SamplesPerChannel = br.ReadInt32(),
                    BitsPerCode = br.ReadByte(),
                    Param1 = br.ReadSingle(),
                    Param2 = br.ReadSingle()
                };

                c.ChannelData = new byte[c.Channels][];
                for (int ch = 0; ch < c.Channels; ch++)
                {
                    int len = br.ReadInt32();
                    c.ChannelData[ch] = br.ReadBytes(len);
                }
                return c;
            }
        }
    }
}

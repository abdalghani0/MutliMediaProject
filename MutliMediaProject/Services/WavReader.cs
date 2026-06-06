using System;
using System.IO;
using MutliMediaProject.Models;

namespace MutliMediaProject.Services
{
    /// <summary>
    /// Minimal RIFF/WAVE reader that supports 8-bit unsigned and 16-bit signed PCM,
    /// mono or stereo. Any unsupported format is rejected with a clear message so
    /// the UI can surface it.
    /// </summary>
    public static class WavReader
    {
        private const ushort WaveFormatPcm = 1;

        public static AudioFile Read(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("Audio file not found.", path);

            var info = new FileInfo(path);

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var br = new BinaryReader(fs))
            {
                if (new string(br.ReadChars(4)) != "RIFF")
                    throw new InvalidDataException("Not a RIFF file.");
                br.ReadInt32(); // overall size
                if (new string(br.ReadChars(4)) != "WAVE")
                    throw new InvalidDataException("Not a WAVE file.");

                ushort audioFormat = 0;
                ushort channels = 0;
                int sampleRate = 0;
                ushort bitsPerSample = 0;
                byte[] pcm = null;

                while (fs.Position < fs.Length)
                {
                    string chunkId = new string(br.ReadChars(4));
                    int chunkSize = br.ReadInt32();
                    long chunkEnd = fs.Position + chunkSize;

                    if (chunkId == "fmt ")
                    {
                        audioFormat = br.ReadUInt16();
                        channels = br.ReadUInt16();
                        sampleRate = br.ReadInt32();
                        br.ReadInt32(); // byte rate
                        br.ReadUInt16(); // block align
                        bitsPerSample = br.ReadUInt16();
                        fs.Position = chunkEnd;
                    }
                    else if (chunkId == "data")
                    {
                        pcm = br.ReadBytes(chunkSize);
                    }
                    else
                    {
                        fs.Position = chunkEnd;
                    }

                    if (chunkSize % 2 == 1 && fs.Position < fs.Length) fs.Position++;
                }

                if (pcm == null) throw new InvalidDataException("WAV file is missing data chunk.");
                if (audioFormat != WaveFormatPcm)
                    throw new InvalidDataException("Only PCM WAV files are supported (format code 1).");
                if (channels != 1 && channels != 2)
                    throw new InvalidDataException("Only mono or stereo WAV files are supported.");
                if (bitsPerSample != 8 && bitsPerSample != 16)
                    throw new InvalidDataException("Only 8-bit or 16-bit PCM WAV files are supported.");

                short[][] samples = ConvertToInt16Samples(pcm, channels, bitsPerSample);

                return new AudioFile
                {
                    FilePath = path,
                    FileSizeBytes = info.Length,
                    SampleRate = sampleRate,
                    Channels = channels,
                    BitsPerSample = bitsPerSample,
                    Encoding = "PCM",
                    Samples = samples
                };
            }
        }

        private static short[][] ConvertToInt16Samples(byte[] pcm, ushort channels, ushort bitsPerSample)
        {
            int bytesPerSample = bitsPerSample / 8;
            int totalSamples = pcm.Length / bytesPerSample;
            int samplesPerChannel = totalSamples / channels;

            short[][] result = new short[channels][];
            for (int c = 0; c < channels; c++)
                result[c] = new short[samplesPerChannel];

            for (int i = 0; i < samplesPerChannel; i++)
            {
                for (int c = 0; c < channels; c++)
                {
                    int idx = (i * channels + c) * bytesPerSample;
                    if (bitsPerSample == 16)
                    {
                        result[c][i] = (short)(pcm[idx] | (pcm[idx + 1] << 8));
                    }
                    else
                    {
                        // 8-bit PCM is unsigned (0..255). Centre and scale to signed 16-bit.
                        result[c][i] = (short)((pcm[idx] - 128) << 8);
                    }
                }
            }
            return result;
        }
    }
}

using System;
using System.IO;
using MutliMediaProject.Models;
using NAudio.Wave;

namespace MutliMediaProject.Services
{
    /// <summary>
    /// Loads PCM WAV files via NAudio's <see cref="WaveFileReader"/>.
    /// Supports 8-bit unsigned and 16-bit signed PCM, mono or stereo.
    /// </summary>
    public static class WavReader
    {
        public static AudioFile Read(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("Audio file not found.", path);

            var info = new FileInfo(path);

            using (var reader = new WaveFileReader(path))
            {
                WaveFormat format = reader.WaveFormat;
                if (format.Encoding != WaveFormatEncoding.Pcm)
                    throw new InvalidDataException("Only PCM WAV files are supported (format code 1).");
                if (format.Channels != 1 && format.Channels != 2)
                    throw new InvalidDataException("Only mono or stereo WAV files are supported.");
                if (format.BitsPerSample != 8 && format.BitsPerSample != 16)
                    throw new InvalidDataException("Only 8-bit or 16-bit PCM WAV files are supported.");

                int channels = format.Channels;
                int bitsPerSample = format.BitsPerSample;
                byte[] pcm = new byte[reader.Length];
                int bytesRead = reader.Read(pcm, 0, pcm.Length);
                if (bytesRead < pcm.Length)
                    Array.Resize(ref pcm, bytesRead);

                short[][] samples = ConvertToInt16Samples(pcm, channels, bitsPerSample);

                return new AudioFile
                {
                    FilePath = path,
                    FileSizeBytes = info.Length,
                    SampleRate = format.SampleRate,
                    Channels = channels,
                    BitsPerSample = bitsPerSample,
                    Encoding = "PCM",
                    Samples = samples
                };
            }
        }

        private static short[][] ConvertToInt16Samples(byte[] pcm, int channels, int bitsPerSample)
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
                        result[c][i] = (short)((pcm[idx] - 128) << 8);
                    }
                }
            }
            return result;
        }
    }
}

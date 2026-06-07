using System;
using NAudio.Wave;

namespace MutliMediaProject.Services
{
    /// <summary>
    /// Writes 16-bit signed PCM WAV files via NAudio's <see cref="WaveFileWriter"/>.
    /// </summary>
    public static class WavWriter
    {
        public static void Write(string path, short[][] samples, int sampleRate)
        {
            if (samples == null || samples.Length == 0) return;

            int channels = samples.Length;
            int sampleCount = samples[0].Length;
            var format = new WaveFormat(sampleRate, 16, channels);

            using (var writer = new WaveFileWriter(path, format))
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    for (int c = 0; c < channels; c++)
                        writer.WriteSample(samples[c][i] / 32768f);
                }
            }
        }
    }
}

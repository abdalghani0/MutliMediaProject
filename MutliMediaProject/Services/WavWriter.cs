using System.IO;

namespace MutliMediaProject.Services
{
    /// <summary>
    /// Writes 16-bit signed PCM WAV files. Used for decompression output and for the
    /// "save reconstructed audio" workflow.
    /// </summary>
    public static class WavWriter
    {
        public static void Write(string path, short[][] samples, int sampleRate)
        {
            if (samples == null || samples.Length == 0) return;
            int channels = samples.Length;
            int sampleCount = samples[0].Length;
            int byteRate = sampleRate * channels * 2;
            int dataSize = sampleCount * channels * 2;

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(new[] { 'R', 'I', 'F', 'F' });
                bw.Write(36 + dataSize);
                bw.Write(new[] { 'W', 'A', 'V', 'E' });

                bw.Write(new[] { 'f', 'm', 't', ' ' });
                bw.Write(16);                       // PCM fmt chunk size
                bw.Write((ushort)1);                // PCM
                bw.Write((ushort)channels);
                bw.Write(sampleRate);
                bw.Write(byteRate);
                bw.Write((ushort)(channels * 2));   // block align
                bw.Write((ushort)16);               // bits per sample

                bw.Write(new[] { 'd', 'a', 't', 'a' });
                bw.Write(dataSize);

                for (int i = 0; i < sampleCount; i++)
                {
                    for (int c = 0; c < channels; c++)
                    {
                        bw.Write(samples[c][i]);
                    }
                }
            }
        }
    }
}

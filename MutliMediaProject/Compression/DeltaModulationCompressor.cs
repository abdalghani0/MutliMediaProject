using System;
using System.Threading;
using MutliMediaProject.Models;

namespace MutliMediaProject.Compression
{
    /// <summary>
    /// Classic 1-bit delta modulation. For each sample we emit '1' if the
    /// input is above the running reconstruction and '0' otherwise. The
    /// reconstruction then takes a fixed step in that direction.
    /// </summary>
    public sealed class DeltaModulationCompressor : IAudioCompressor
    {
        public CompressionAlgorithm Algorithm => CompressionAlgorithm.DeltaModulation;

        public int GetBitsPerCode(CompressionSettings settings) => 1;

        public void GetParams(CompressionSettings settings, out float param1, out float param2)
        {
            param1 = Math.Max(1f, settings.StepSize);
            param2 = 0f;
        }

        public byte[] Compress(short[] samples, CompressionSettings settings,
            IProgress<long> samplesProcessedReporter, CancellationToken cancellationToken)
        {
            float step = Math.Max(1f, settings.StepSize);
            var bw = new BitWriter();
            double reconstruction = 0;
            const int reportEvery = 4096;

            for (int i = 0; i < samples.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (samples[i] >= reconstruction)
                {
                    bw.WriteBits(1, 1);
                    reconstruction += step;
                }
                else
                {
                    bw.WriteBits(0, 1);
                    reconstruction -= step;
                }
                if (reconstruction > 32767) reconstruction = 32767;
                if (reconstruction < -32768) reconstruction = -32768;

                if (((i + 1) % reportEvery) == 0 && samplesProcessedReporter != null)
                    samplesProcessedReporter.Report(reportEvery);
            }
            int remainder = samples.Length % reportEvery;
            if (remainder > 0 && samplesProcessedReporter != null)
                samplesProcessedReporter.Report(remainder);

            return bw.ToArray();
        }

        public short[] Decompress(byte[] encoded, int sampleCount, int bitsPerCode,
            float param1, float param2, CancellationToken cancellationToken)
        {
            float step = Math.Max(1f, param1);
            var br = new BitReader(encoded);
            var output = new short[sampleCount];
            double reconstruction = 0;

            for (int i = 0; i < sampleCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                uint bit = br.ReadBits(1);
                reconstruction += bit == 1 ? step : -step;
                if (reconstruction > 32767) reconstruction = 32767;
                if (reconstruction < -32768) reconstruction = -32768;
                output[i] = (short)reconstruction;
            }
            return output;
        }
    }
}

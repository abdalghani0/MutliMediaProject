using System;
using System.Threading;
using MutliMediaProject.Models;

namespace MutliMediaProject.Compression
{
    /// <summary>
    /// Continuously variable slope (Song) ADM. Step size doubles after two
    /// consecutive bits with the same sign (signal climbing fast) and halves
    /// after two consecutive opposite bits (signal nearly flat). Step is
    /// clamped to [MinStepSize, MaxStepSize] from the settings, so the same
    /// limits are used by the decoder.
    /// </summary>
    public sealed class AdaptiveDeltaModulationCompressor : IAudioCompressor
    {
        public CompressionAlgorithm Algorithm => CompressionAlgorithm.AdaptiveDeltaModulation;

        public int GetBitsPerCode(CompressionSettings settings) => 1;

        public void GetParams(CompressionSettings settings, out float param1, out float param2)
        {
            // param1 = initial step, param2 = max step. Min step is derived from initial / 4 to keep
            // the header minimal but predictable.
            param1 = Math.Max(1f, settings.StepSize);
            param2 = Math.Max(param1, settings.MaxStepSize);
        }

        public byte[] Compress(short[] samples, CompressionSettings settings,
            IProgress<long> samplesProcessedReporter, CancellationToken cancellationToken)
        {
            float initialStep = Math.Max(1f, settings.StepSize);
            float maxStep = Math.Max(initialStep, settings.MaxStepSize);
            float minStep = Math.Max(1f, Math.Min(initialStep, settings.MinStepSize));

            var bw = new BitWriter();
            double reconstruction = 0;
            double step = initialStep;
            int previousBit = -1;
            int sameBitRun = 0;
            const int reportEvery = 4096;

            for (int i = 0; i < samples.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int bit;
                if (samples[i] >= reconstruction)
                {
                    bit = 1;
                    reconstruction += step;
                }
                else
                {
                    bit = 0;
                    reconstruction -= step;
                }
                if (reconstruction > 32767) reconstruction = 32767;
                if (reconstruction < -32768) reconstruction = -32768;
                bw.WriteBits((uint)bit, 1);

                if (bit == previousBit) sameBitRun++; else sameBitRun = 1;
                step = sameBitRun >= 2 ? step * 1.5 : step * 0.75;
                if (step > maxStep) step = maxStep;
                if (step < minStep) step = minStep;
                previousBit = bit;

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
            float initialStep = Math.Max(1f, param1);
            float maxStep = Math.Max(initialStep, param2);
            float minStep = Math.Max(1f, initialStep / 4f);

            var br = new BitReader(encoded);
            var output = new short[sampleCount];
            double reconstruction = 0;
            double step = initialStep;
            int previousBit = -1;
            int sameBitRun = 0;

            for (int i = 0; i < sampleCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                uint bit = br.ReadBits(1);
                reconstruction += bit == 1 ? step : -step;
                if (reconstruction > 32767) reconstruction = 32767;
                if (reconstruction < -32768) reconstruction = -32768;
                output[i] = (short)reconstruction;

                int b = (int)bit;
                if (b == previousBit) sameBitRun++; else sameBitRun = 1;
                step = sameBitRun >= 2 ? step * 1.5 : step * 0.75;
                if (step > maxStep) step = maxStep;
                if (step < minStep) step = minStep;
                previousBit = b;
            }
            return output;
        }
    }
}

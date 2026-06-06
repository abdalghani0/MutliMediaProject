using System;
using System.Threading;
using MutliMediaProject.Models;

namespace MutliMediaProject.Compression
{
    /// <summary>
    /// Differential PCM. The encoder predicts each sample as the previous reconstructed
    /// sample, quantises the prediction error into a signed N-bit code, and stores it.
    /// The decoder reverses the process by adding the dequantised error back to the
    /// running reconstruction so encoder and decoder stay perfectly in sync.
    /// </summary>
    public sealed class DpcmCompressor : IAudioCompressor
    {
        public CompressionAlgorithm Algorithm => CompressionAlgorithm.DifferentialPcm;

        public int GetBitsPerCode(CompressionSettings settings)
        {
            return Math.Max(2, Math.Min(8, settings.QuantizationBits));
        }

        public void GetParams(CompressionSettings settings, out float param1, out float param2)
        {
            param1 = 0f;
            param2 = 0f;
        }

        public byte[] Compress(short[] samples, CompressionSettings settings,
            IProgress<long> samplesProcessedReporter, CancellationToken cancellationToken)
        {
            int bits = GetBitsPerCode(settings);
            int levels = 1 << bits;
            int half = levels / 2;
            int maxAbs = half - 1;            // representable error magnitude
            // Quantisation step sized so the dynamic range of a 16-bit signal maps onto N levels.
            double step = 65536.0 / levels;

            var bw = new BitWriter();
            int prediction = 0;
            const int reportEvery = 4096;

            for (int i = 0; i < samples.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int diff = samples[i] - prediction;
                int code = (int)Math.Round(diff / step);
                if (code > maxAbs) code = maxAbs;
                if (code < -half) code = -half;

                uint encoded = (uint)(code & (levels - 1)); // two's-complement in N bits
                bw.WriteBits(encoded, bits);

                int reconstructed = prediction + (int)(code * step);
                if (reconstructed > 32767) reconstructed = 32767;
                if (reconstructed < -32768) reconstructed = -32768;
                prediction = reconstructed;

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
            int levels = 1 << bitsPerCode;
            int half = levels / 2;
            double step = 65536.0 / levels;

            var br = new BitReader(encoded);
            var output = new short[sampleCount];
            int prediction = 0;

            for (int i = 0; i < sampleCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                uint code = br.ReadBits(bitsPerCode);
                int signed = (int)code;
                if (signed >= half) signed -= levels;

                int reconstructed = prediction + (int)(signed * step);
                if (reconstructed > 32767) reconstructed = 32767;
                if (reconstructed < -32768) reconstructed = -32768;

                output[i] = (short)reconstructed;
                prediction = reconstructed;
            }
            return output;
        }
    }
}

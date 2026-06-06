using System;
using System.Threading;
using MutliMediaProject.Models;

namespace MutliMediaProject.Compression
{
    /// <summary>
    /// \u03BC-law style nonlinear quantizer. The signal is normalised to [-1, 1],
    /// passed through the logarithmic compander F(x) = sign(x) * ln(1 + \u03BC|x|) / ln(1 + \u03BC),
    /// then uniformly quantised to N = 2^bits levels. Decoding inverts the compander.
    /// </summary>
    public sealed class NonlinearQuantizationCompressor : IAudioCompressor
    {
        public CompressionAlgorithm Algorithm => CompressionAlgorithm.NonlinearQuantization;

        public int GetBitsPerCode(CompressionSettings settings)
        {
            return Math.Max(1, Math.Min(8, settings.QuantizationBits));
        }

        public void GetParams(CompressionSettings settings, out float param1, out float param2)
        {
            param1 = settings.Mu;
            param2 = 0f;
        }

        public byte[] Compress(short[] samples, CompressionSettings settings,
            IProgress<long> samplesProcessedReporter, CancellationToken cancellationToken)
        {
            int bits = GetBitsPerCode(settings);
            int levels = 1 << bits;
            int half = levels / 2;
            float mu = Math.Max(1f, settings.Mu);
            double logMu = Math.Log(1.0 + mu);

            var bw = new BitWriter();
            const int reportEvery = 4096;

            for (int i = 0; i < samples.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double normalized = samples[i] / 32768.0;
                int sign = normalized < 0 ? -1 : 1;
                double magnitude = Math.Abs(normalized);
                double companded = sign * Math.Log(1.0 + mu * magnitude) / logMu; // [-1, 1]

                int code = (int)Math.Round((companded + 1.0) * 0.5 * (levels - 1));
                if (code < 0) code = 0;
                if (code >= levels) code = levels - 1;

                bw.WriteBits((uint)code, bits);

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
            float mu = Math.Max(1f, param1);
            double logMu = Math.Log(1.0 + mu);

            var br = new BitReader(encoded);
            var output = new short[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                uint code = br.ReadBits(bitsPerCode);
                double companded = (code / (double)(levels - 1)) * 2.0 - 1.0;
                int sign = companded < 0 ? -1 : 1;
                double magnitude = Math.Abs(companded);
                double restored = sign * (Math.Exp(magnitude * logMu) - 1.0) / mu;
                int s = (int)Math.Round(restored * 32767.0);
                if (s > 32767) s = 32767;
                if (s < -32768) s = -32768;
                output[i] = (short)s;
            }
            return output;
        }
    }
}

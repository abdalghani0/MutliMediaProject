using System;
using System.Threading;
using MutliMediaProject.Models;

namespace MutliMediaProject.Compression
{
    /// <summary>
    /// Contract every compression algorithm fulfils. Methods operate on a single
    /// channel so the form can drive multi-channel encoding by iterating channels
    /// and feeding deinterleaved sample streams.
    /// </summary>
    public interface IAudioCompressor
    {
        CompressionAlgorithm Algorithm { get; }

        /// <summary>Bits-per-encoded-sample for this run (after settings are applied).</summary>
        int GetBitsPerCode(CompressionSettings settings);

        /// <summary>Algorithm-specific parameter pair written into the container header.</summary>
        void GetParams(CompressionSettings settings, out float param1, out float param2);

        /// <summary>Encode a single channel of samples.</summary>
        byte[] Compress(
            short[] samples,
            CompressionSettings settings,
            IProgress<long> samplesProcessedReporter,
            CancellationToken cancellationToken);

        /// <summary>Decode a single channel of samples.</summary>
        short[] Decompress(
            byte[] encoded,
            int sampleCount,
            int bitsPerCode,
            float param1,
            float param2,
            CancellationToken cancellationToken);
    }
}

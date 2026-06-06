using System;
using MutliMediaProject.Models;

namespace MutliMediaProject.Compression
{
    public static class CompressorFactory
    {
        public static IAudioCompressor Create(CompressionAlgorithm algorithm)
        {
            switch (algorithm)
            {
                case CompressionAlgorithm.NonlinearQuantization:
                    return new NonlinearQuantizationCompressor();
                case CompressionAlgorithm.DifferentialPcm:
                    return new DpcmCompressor();
                case CompressionAlgorithm.DeltaModulation:
                    return new DeltaModulationCompressor();
                case CompressionAlgorithm.AdaptiveDeltaModulation:
                    return new AdaptiveDeltaModulationCompressor();
                default:
                    throw new NotSupportedException("Unsupported algorithm: " + algorithm);
            }
        }
    }
}

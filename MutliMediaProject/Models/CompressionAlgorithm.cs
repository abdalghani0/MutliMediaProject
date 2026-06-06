namespace MutliMediaProject.Models
{
    public enum CompressionAlgorithm
    {
        NonlinearQuantization = 0,
        DifferentialPcm = 1,
        DeltaModulation = 2,
        AdaptiveDeltaModulation = 3
    }

    public static class CompressionAlgorithmExtensions
    {
        public static string ToFriendlyString(this CompressionAlgorithm algorithm)
        {
            switch (algorithm)
            {
                case CompressionAlgorithm.NonlinearQuantization:
                    return "Nonlinear Quantization (\u03BC-law)";
                case CompressionAlgorithm.DifferentialPcm:
                    return "Differential PCM (DPCM)";
                case CompressionAlgorithm.DeltaModulation:
                    return "Delta Modulation (DM)";
                case CompressionAlgorithm.AdaptiveDeltaModulation:
                    return "Adaptive Delta Modulation (ADM)";
                default:
                    return algorithm.ToString();
            }
        }
    }
}

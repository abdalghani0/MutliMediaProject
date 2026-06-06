namespace MutliMediaProject.Models
{
    /// <summary>
    /// User-configurable parameters that drive a compression run.
    /// Different algorithms use different subsets of these fields.
    /// </summary>
    public class CompressionSettings
    {
        public CompressionAlgorithm Algorithm { get; set; } = CompressionAlgorithm.NonlinearQuantization;

        /// <summary>Target sample rate. If lower than the source rate, the audio is resampled (downsampled).</summary>
        public int TargetSampleRate { get; set; } = 44100;

        /// <summary>Bits per encoded sample (NLQ / DPCM). 1..8.</summary>
        public int QuantizationBits { get; set; } = 8;

        /// <summary>Mu parameter for nonlinear (\u03BC-law) quantization (typically 255).</summary>
        public float Mu { get; set; } = 255f;

        /// <summary>Step size used by Delta Modulation (and as the initial step for ADM).</summary>
        public float StepSize { get; set; } = 200f;

        /// <summary>Maximum step size for Adaptive Delta Modulation.</summary>
        public float MaxStepSize { get; set; } = 3000f;

        /// <summary>Minimum step size for Adaptive Delta Modulation.</summary>
        public float MinStepSize { get; set; } = 50f;

        public CompressionSettings Clone()
        {
            return (CompressionSettings)MemberwiseClone();
        }
    }
}

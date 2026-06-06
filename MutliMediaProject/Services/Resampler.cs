using System;

namespace MutliMediaProject.Services
{
    /// <summary>
    /// Very small linear-interpolation resampler. Good enough for the
    /// "lower the sample rate before encoding" knob that the assignment
    /// requires; we are not aiming for studio-grade SRC here.
    /// </summary>
    public static class Resampler
    {
        public static short[] Resample(short[] input, int sourceRate, int targetRate)
        {
            if (input == null || input.Length == 0 || sourceRate <= 0 || targetRate <= 0 || sourceRate == targetRate)
                return input;

            double ratio = (double)targetRate / sourceRate;
            int outLength = Math.Max(1, (int)Math.Round(input.Length * ratio));
            short[] output = new short[outLength];

            for (int i = 0; i < outLength; i++)
            {
                double srcPos = i / ratio;
                int idx = (int)srcPos;
                double frac = srcPos - idx;
                short s0 = input[Math.Min(idx, input.Length - 1)];
                short s1 = input[Math.Min(idx + 1, input.Length - 1)];
                output[i] = (short)Math.Round(s0 * (1.0 - frac) + s1 * frac);
            }
            return output;
        }
    }
}

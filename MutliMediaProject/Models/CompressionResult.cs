using System;

namespace MutliMediaProject.Models
{
    /// <summary>Final outcome of a compression run. Drives the on-screen report.</summary>
    public class CompressionResult
    {
        public CompressionSettings Settings { get; set; }
        public long OriginalSizeBytes { get; set; }
        public long CompressedSizeBytes { get; set; }
        public TimeSpan Elapsed { get; set; }
        public string OutputPath { get; set; }
        public bool WasCancelled { get; set; }

        public double SavingsPercent
        {
            get
            {
                if (OriginalSizeBytes == 0) return 0;
                return (1.0 - (double)CompressedSizeBytes / OriginalSizeBytes) * 100.0;
            }
        }

        public double CompressionRatio
        {
            get
            {
                if (CompressedSizeBytes == 0) return 0;
                return (double)OriginalSizeBytes / CompressedSizeBytes;
            }
        }
    }
}

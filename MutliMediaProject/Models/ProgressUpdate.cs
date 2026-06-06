namespace MutliMediaProject.Models
{
    /// <summary>
    /// Snapshot reported back to the UI while a compression / decompression job runs.
    /// </summary>
    public class ProgressUpdate
    {
        /// <summary>0..100 inclusive.</summary>
        public double Percent { get; set; }
        /// <summary>Original sample bytes processed so far (input side).</summary>
        public long ProcessedInputBytes { get; set; }
        /// <summary>Compressed bytes produced so far (output side).</summary>
        public long ProducedOutputBytes { get; set; }
        /// <summary>Elapsed milliseconds since the operation began.</summary>
        public double ElapsedMilliseconds { get; set; }
        /// <summary>Short human-readable status line.</summary>
        public string Status { get; set; }

        /// <summary>Running compression ratio (input / output) where larger is better.</summary>
        public double CurrentRatio
        {
            get
            {
                if (ProducedOutputBytes <= 0) return 0;
                return (double)ProcessedInputBytes / ProducedOutputBytes;
            }
        }

        /// <summary>Processing speed in MB/s based on input bytes consumed.</summary>
        public double SpeedMBps
        {
            get
            {
                if (ElapsedMilliseconds <= 0) return 0;
                return (ProcessedInputBytes / 1024.0 / 1024.0) / (ElapsedMilliseconds / 1000.0);
            }
        }
    }
}

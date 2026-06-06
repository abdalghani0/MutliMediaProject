using System;
using System.IO;

namespace MutliMediaProject.Models
{
    /// <summary>
    /// Represents an uncompressed PCM audio file loaded in memory.
    /// Samples are stored per channel as 16-bit signed integers regardless of
    /// the original bit depth; the original bit depth is preserved for reporting.
    /// </summary>
    public class AudioFile
    {
        public string FilePath { get; set; }
        public long FileSizeBytes { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public int BitsPerSample { get; set; }
        public string Encoding { get; set; }
        public short[][] Samples { get; set; }

        public int SampleCountPerChannel
        {
            get { return Samples != null && Samples.Length > 0 ? Samples[0].Length : 0; }
        }

        public TimeSpan Duration
        {
            get
            {
                if (SampleRate <= 0) return TimeSpan.Zero;
                return TimeSpan.FromSeconds((double)SampleCountPerChannel / SampleRate);
            }
        }

        public int BitRate
        {
            get { return SampleRate * Channels * BitsPerSample; }
        }

        public string FileName
        {
            get { return string.IsNullOrEmpty(FilePath) ? string.Empty : Path.GetFileName(FilePath); }
        }
    }
}

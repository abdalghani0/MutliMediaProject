using System;
using NAudio.Wave;

namespace MutliMediaProject.Services
{
    /// <summary>
    /// Preview playback using NAudio's <see cref="WaveFileReader"/> and <see cref="WaveOutEvent"/>.
    /// Uses 16-bit PCM for broad waveOut device compatibility.
    /// </summary>
    public sealed class AudioPlayer : IDisposable
    {
        private WaveOutEvent _output;
        private WaveFileReader _reader;

        public bool IsPlaying => _output != null && _output.PlaybackState == PlaybackState.Playing;
        public bool IsPaused => _output != null && _output.PlaybackState == PlaybackState.Paused;
        public bool IsLoaded => _reader != null;

        public void Load(string path)
        {
            Close();

            var reader = new WaveFileReader(path);
            var output = new WaveOutEvent();
            try
            {
                output.Init(reader);
                _reader = reader;
                _output = output;
            }
            catch
            {
                output.Dispose();
                reader.Dispose();
                throw;
            }
        }

        public void Play()
        {
            if (_output == null) return;
            _output.Play();
        }

        public void Pause()
        {
            if (_output == null || _output.PlaybackState != PlaybackState.Playing) return;
            _output.Pause();
        }

        public void Stop()
        {
            if (_output == null) return;
            _output.Stop();
            _reader.Position = 0;
        }

        public TimeSpan GetPosition() => _reader?.CurrentTime ?? TimeSpan.Zero;

        public TimeSpan GetLength() => _reader?.TotalTime ?? TimeSpan.Zero;

        public string GetMode()
        {
            return _output == null ? string.Empty : _output.PlaybackState.ToString();
        }

        public void Close()
        {
            if (_output != null)
            {
                _output.Stop();
                _output.Dispose();
                _output = null;
            }

            if (_reader != null)
            {
                _reader.Dispose();
                _reader = null;
            }
        }

        public void Dispose() => Close();
    }
}

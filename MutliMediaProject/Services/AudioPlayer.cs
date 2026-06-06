using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MutliMediaProject.Services
{
    /// <summary>
    /// Thin wrapper around the Windows Multimedia Control Interface (winmm.dll).
    /// Provides play / pause / resume / stop and reports playback position so the
    /// UI can show the current playhead without bundling third-party libraries.
    /// </summary>
    public sealed class AudioPlayer : IDisposable
    {
        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        private static extern int mciSendString(string command, StringBuilder buffer, int bufferSize, IntPtr hwndCallback);

        private string _alias;
        private bool _isOpen;
        private bool _isPlaying;
        private bool _isPaused;

        public bool IsPlaying => _isPlaying && !_isPaused;
        public bool IsPaused => _isPaused;
        public bool IsLoaded => _isOpen;

        public void Load(string path)
        {
            Close();
            _alias = "audio_" + Guid.NewGuid().ToString("N");
            string cmd = string.Format("open \"{0}\" type mpegvideo alias {1}", path, _alias);
            int code = mciSendString(cmd, null, 0, IntPtr.Zero);
            if (code != 0)
            {
                // Fallback: let MCI infer the type. Works for WAV and most common containers.
                cmd = string.Format("open \"{0}\" alias {1}", path, _alias);
                code = mciSendString(cmd, null, 0, IntPtr.Zero);
                if (code != 0) throw new InvalidOperationException("Could not load audio for playback (MCI code " + code + ").");
            }
            _isOpen = true;
        }

        public void Play()
        {
            if (!_isOpen) return;
            if (_isPaused)
            {
                mciSendString("resume " + _alias, null, 0, IntPtr.Zero);
                _isPaused = false;
            }
            else
            {
                mciSendString("play " + _alias, null, 0, IntPtr.Zero);
            }
            _isPlaying = true;
        }

        public void Pause()
        {
            if (!_isOpen || !_isPlaying) return;
            mciSendString("pause " + _alias, null, 0, IntPtr.Zero);
            _isPaused = true;
        }

        public void Stop()
        {
            if (!_isOpen) return;
            mciSendString("stop " + _alias, null, 0, IntPtr.Zero);
            mciSendString("seek " + _alias + " to start", null, 0, IntPtr.Zero);
            _isPlaying = false;
            _isPaused = false;
        }

        public TimeSpan GetPosition()
        {
            if (!_isOpen) return TimeSpan.Zero;
            var sb = new StringBuilder(128);
            mciSendString("status " + _alias + " position", sb, sb.Capacity, IntPtr.Zero);
            int ms;
            return int.TryParse(sb.ToString(), out ms) ? TimeSpan.FromMilliseconds(ms) : TimeSpan.Zero;
        }

        public TimeSpan GetLength()
        {
            if (!_isOpen) return TimeSpan.Zero;
            var sb = new StringBuilder(128);
            mciSendString("status " + _alias + " length", sb, sb.Capacity, IntPtr.Zero);
            int ms;
            return int.TryParse(sb.ToString(), out ms) ? TimeSpan.FromMilliseconds(ms) : TimeSpan.Zero;
        }

        public string GetMode()
        {
            if (!_isOpen) return string.Empty;
            var sb = new StringBuilder(128);
            mciSendString("status " + _alias + " mode", sb, sb.Capacity, IntPtr.Zero);
            return sb.ToString().Trim();
        }

        public void Close()
        {
            if (_isOpen)
            {
                mciSendString("close " + _alias, null, 0, IntPtr.Zero);
                _isOpen = false;
                _isPlaying = false;
                _isPaused = false;
                _alias = null;
            }
        }

        public void Dispose() { Close(); }
    }
}

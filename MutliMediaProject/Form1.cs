using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MutliMediaProject.Compression;
using MutliMediaProject.Models;
using MutliMediaProject.Services;

namespace MutliMediaProject
{
    public partial class Form1 : Form
    {
        // Loaded source state
        private AudioFile _loadedAudio;
        private CompressedFileFormat.Container _compressedOutput;
        private short[][] _decompressedSamples;
        private int _decompressedSampleRate;
        private CompressionResult _lastResult;

        private enum OutputKind { None, Compressed, Decompressed }
        private OutputKind _outputKind = OutputKind.None;

        // Default settings remembered for the "reset" button
        private CompressionSettings _defaultSettings;

        // Job control
        private CancellationTokenSource _cts;
        private bool _busy;

        private readonly AudioPlayer _player = new AudioPlayer();
        private readonly string _decompressedPreviewPath =
            Path.Combine(Path.GetTempPath(), "amcx_preview_" + Guid.NewGuid().ToString("N") + ".wav");

        // ===================================================================
        // Visual button styling: FlatStyle buttons don't gray themselves out
        // when disabled, so we toggle the colours manually.
        // ===================================================================
        private static readonly Color DisabledBack = Color.FromArgb(225, 228, 232);
        private static readonly Color DisabledFore = Color.FromArgb(150, 155, 165);

        private Color _btnOpenColor;
        private Color _btnOpenCompressedColor;
        private Color _btnPlayColor;
        private Color _btnPauseColor;
        private Color _btnStopColor;
        private Color _btnCompressColor;
        private Color _btnDecompressColor;
        private Color _btnCancelColor;
        private Color _btnSaveColor;
        private Color _btnResetColor;
        private Color _btnPauseFore;

        public Form1()
        {
            InitializeComponent();
            CaptureButtonColors();
            WireEvents();
            PopulateAlgorithmCombo();
            ApplyDefaultSettings();
            UpdateButtonState();
            UpdateFilePropertyDisplay();
        }

        private void CaptureButtonColors()
        {
            _btnOpenColor = btnOpen.BackColor;
            _btnOpenCompressedColor = btnOpenCompressed.BackColor;
            _btnPlayColor = btnPlay.BackColor;
            _btnPauseColor = btnPause.BackColor;
            _btnPauseFore = btnPause.ForeColor;
            _btnStopColor = btnStop.BackColor;
            _btnCompressColor = btnCompress.BackColor;
            _btnDecompressColor = btnDecompress.BackColor;
            _btnCancelColor = btnCancel.BackColor;
            _btnSaveColor = btnSave.BackColor;
            _btnResetColor = btnResetSettings.BackColor;
        }

        // ===================================================================
        // Wiring
        // ===================================================================
        private void WireEvents()
        {
            btnOpen.Click += BtnOpen_Click;
            btnOpenCompressed.Click += BtnOpenCompressed_Click;

            btnPlay.Click += BtnPlay_Click;
            btnPause.Click += BtnPause_Click;
            btnStop.Click += BtnStop_Click;
            playbackTimer.Tick += PlaybackTimer_Tick;

            cmbAlgorithm.SelectedIndexChanged += CmbAlgorithm_SelectedIndexChanged;
            btnResetSettings.Click += BtnResetSettings_Click;

            btnCompress.Click += BtnCompress_Click;
            btnDecompress.Click += BtnDecompress_Click;
            btnCancel.Click += BtnCancel_Click;
            btnSave.Click += BtnSave_Click;

            this.DragEnter += AnyControl_DragEnter;
            this.DragDrop += AnyControl_DragDrop;
            this.FormClosing += Form1_FormClosing;
        }

        private void PopulateAlgorithmCombo()
        {
            cmbAlgorithm.Items.Clear();
            foreach (CompressionAlgorithm alg in Enum.GetValues(typeof(CompressionAlgorithm)))
                cmbAlgorithm.Items.Add(alg.ToFriendlyString());
            cmbAlgorithm.SelectedIndex = 0;
        }

        private void ApplyDefaultSettings()
        {
            _defaultSettings = new CompressionSettings();
            ApplySettingsToUi(_defaultSettings);
        }

        // ===================================================================
        // File loading
        // ===================================================================
        private void BtnOpen_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = "WAV audio (*.wav)|*.wav|All files (*.*)|*.*",
                Title = "Select a WAV audio file"
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    LoadAudioFile(dlg.FileName);
            }
        }

        private void BtnOpenCompressed_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = "Compressed audio (*.amcx)|*.amcx|All files (*.*)|*.*",
                Title = "Select a compressed (.amcx) file"
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    LoadCompressedFile(dlg.FileName);
            }
        }

        private void AnyControl_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void AnyControl_DragDrop(object sender, DragEventArgs e)
        {
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (paths == null || paths.Length == 0) return;
            string path = paths[0];
            string ext = Path.GetExtension(path).ToLowerInvariant();
            try
            {
                if (ext == ".wav") LoadAudioFile(path);
                else if (ext == CompressedFileFormat.Extension) LoadCompressedFile(path);
                else MessageBox.Show(this, "Unsupported file type: " + ext + Environment.NewLine + "Drop a .wav or .amcx file.", "Unsupported file", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Cannot load file", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadAudioFile(string path)
        {
            try
            {
                StopPlayback();
                _player.Close();
                var audio = WavReader.Read(path);
                _loadedAudio = audio;
                _compressedOutput = null;
                _decompressedSamples = null;
                _decompressedSampleRate = 0;
                _lastResult = null;
                _outputKind = OutputKind.None;

                UpdateFilePropertyDisplay();
                ResetCharts();
                txtReport.Clear();
                progressBar.Value = 0;
                lblStatus.Text = "WAV loaded. Configure settings and click Compress.";
                statusLabel.Text = "Loaded: " + audio.FileName;

                // Cap the spinner so the user can't enter a sample rate higher than the source.
                // Upsampling would not improve compression; we only allow downsampling.
                if (audio.SampleRate < numSampleRate.Minimum) numSampleRate.Minimum = audio.SampleRate;
                numSampleRate.Maximum = audio.SampleRate;
                numSampleRate.Value = audio.SampleRate;

                try
                {
                    _player.Load(path);
                    lblPreviewSource.Text = "Source: original WAV - " + audio.FileName;
                }
                catch
                {
                    lblPreviewSource.Text = "Source: (playback unavailable)";
                }
                UpdatePlaybackLabel();
                UpdateButtonState();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Cannot load WAV", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadCompressedFile(string path)
        {
            try
            {
                StopPlayback();
                _player.Close();
                lblPreviewSource.Text = "Source: (decompress first to enable preview)";

                var container = CompressedFileFormat.Read(path);
                if (!Enum.IsDefined(typeof(CompressionAlgorithm), container.Algorithm))
                {
                    throw new InvalidDataException(
                        "This compressed file uses an unsupported algorithm (possibly an older ADM file). " +
                        "Please compress again with one of the current algorithms.");
                }
                _compressedOutput = container;
                _loadedAudio = null;
                _decompressedSamples = null;
                _decompressedSampleRate = 0;
                _lastResult = null;
                _outputKind = OutputKind.Compressed;

                lblFileNameValue.Text = Path.GetFileName(path);
                var fi = new FileInfo(path);
                lblSizeValue.Text = FormatBytes(fi.Length);
                lblDurationValue.Text = TimeSpan.FromSeconds((double)container.SamplesPerChannel / container.EncodedSampleRate).ToString(@"hh\:mm\:ss");
                lblSampleRateValue.Text = container.EncodedSampleRate + " Hz (original " + container.OriginalSampleRate + " Hz)";
                lblChannelsValue.Text = container.Channels == 1 ? "Mono (1)" : "Stereo (2)";
                lblBitsValue.Text = container.BitsPerCode + " bits/code (orig. " + container.OriginalBitsPerSample + " bits)";
                lblBitrateValue.Text = (container.EncodedSampleRate * container.Channels * container.BitsPerCode) + " bps";
                lblEncodingValue.Text = container.Algorithm.ToFriendlyString();

                // Lift the cap that LoadAudioFile may have applied previously so the
                // spinner can display the .amcx encoded rate regardless of any prior source.
                numSampleRate.Minimum = 4000;
                numSampleRate.Maximum = Math.Max(96000, container.EncodedSampleRate);

                // Wipe stale settings from a previous run so disabled fields don't show
                // leftover values that would confuse the user.
                ApplySettingsToUi(_defaultSettings);

                cmbAlgorithm.SelectedIndex = (int)container.Algorithm;
                numSampleRate.Value = Math.Min(numSampleRate.Maximum, Math.Max(numSampleRate.Minimum, container.EncodedSampleRate));
                numQuantBits.Value = Math.Min(numQuantBits.Maximum, Math.Max(numQuantBits.Minimum, container.BitsPerCode));
                if (container.Algorithm == CompressionAlgorithm.NonlinearQuantization)
                    numMu.Value = (decimal)Math.Min((float)numMu.Maximum, Math.Max((float)numMu.Minimum, container.Param1));
                else if (container.Algorithm == CompressionAlgorithm.DeltaModulation)
                    numStep.Value = (decimal)Math.Min((float)numStep.Maximum, Math.Max((float)numStep.Minimum, container.Param1));

                ResetCharts();
                txtReport.Clear();
                progressBar.Value = 0;
                lblStatus.Text = "Compressed file loaded. Click Decompress to reconstruct audio and enable preview.";
                statusLabel.Text = "Loaded: " + Path.GetFileName(path);
                UpdatePlaybackLabel();
                UpdateButtonState();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Cannot load AMCX", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateFilePropertyDisplay()
        {
            if (_loadedAudio == null && _compressedOutput == null)
            {
                lblFileNameValue.Text = "-";
                lblSizeValue.Text = "-";
                lblDurationValue.Text = "-";
                lblSampleRateValue.Text = "-";
                lblChannelsValue.Text = "-";
                lblBitsValue.Text = "-";
                lblBitrateValue.Text = "-";
                lblEncodingValue.Text = "-";
                return;
            }
            if (_loadedAudio != null)
            {
                lblFileNameValue.Text = _loadedAudio.FileName;
                lblSizeValue.Text = FormatBytes(_loadedAudio.FileSizeBytes);
                lblDurationValue.Text = _loadedAudio.Duration.ToString(@"hh\:mm\:ss\.ff");
                lblSampleRateValue.Text = _loadedAudio.SampleRate + " Hz";
                lblChannelsValue.Text = _loadedAudio.Channels == 1 ? "Mono (1)" : "Stereo (2)";
                lblBitsValue.Text = _loadedAudio.BitsPerSample + " bits";
                lblBitrateValue.Text = (_loadedAudio.BitRate / 1000.0).ToString("0.#") + " kbps";
                lblEncodingValue.Text = _loadedAudio.Encoding;
            }
        }

        // ===================================================================
        // Preview playback
        // ===================================================================
        private void BtnPlay_Click(object sender, EventArgs e)
        {
            if (!_player.IsLoaded) return;
            try
            {
                _player.Play();
                playbackTimer.Start();
                UpdatePlaybackLabel();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Cannot play audio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnPause_Click(object sender, EventArgs e)
        {
            _player.Pause();
            playbackTimer.Stop();
            UpdatePlaybackLabel();
        }

        private void BtnStop_Click(object sender, EventArgs e) => StopPlayback();

        private void StopPlayback()
        {
            _player.Stop();
            playbackTimer.Stop();
            UpdatePlaybackLabel();
        }

        private void PlaybackTimer_Tick(object sender, EventArgs e) => UpdatePlaybackLabel();

        private void UpdatePlaybackLabel()
        {
            TimeSpan pos = _player.IsLoaded ? _player.GetPosition() : TimeSpan.Zero;
            TimeSpan len = _player.IsLoaded ? _player.GetLength() : (_loadedAudio?.Duration ?? TimeSpan.Zero);
            lblPlaybackPosition.Text = pos.ToString(@"mm\:ss") + " / " + len.ToString(@"mm\:ss");
        }

        // ===================================================================
        // Settings
        // ===================================================================
        private void CmbAlgorithm_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateSettingsControlsEnabled();
        }

        private void UpdateSettingsControlsEnabled()
        {
            if (cmbAlgorithm.SelectedIndex < 0) return;
            var alg = (CompressionAlgorithm)cmbAlgorithm.SelectedIndex;
            bool isNlq = alg == CompressionAlgorithm.NonlinearQuantization;
            bool isDpcm = alg == CompressionAlgorithm.DifferentialPcm;
            bool isDm  = alg == CompressionAlgorithm.DeltaModulation;

            numQuantBits.Enabled = (isNlq || isDpcm) && !_busy;
            numMu.Enabled = isNlq && !_busy;
            numStep.Enabled = isDm && !_busy;
        }

        private void BtnResetSettings_Click(object sender, EventArgs e)
        {
            ApplySettingsToUi(_defaultSettings);
            if (_loadedAudio != null)
                numSampleRate.Value = Math.Min(numSampleRate.Maximum, Math.Max(numSampleRate.Minimum, _loadedAudio.SampleRate));
            statusLabel.Text = "Settings reset to defaults.";
        }

        private void ApplySettingsToUi(CompressionSettings s)
        {
            cmbAlgorithm.SelectedIndex = (int)s.Algorithm;
            numSampleRate.Value = Math.Min(numSampleRate.Maximum, Math.Max(numSampleRate.Minimum, s.TargetSampleRate));
            numQuantBits.Value = Math.Min(numQuantBits.Maximum, Math.Max(numQuantBits.Minimum, s.QuantizationBits));
            numMu.Value = (decimal)Math.Min((float)numMu.Maximum, Math.Max((float)numMu.Minimum, s.Mu));
            numStep.Value = (decimal)Math.Min((float)numStep.Maximum, Math.Max((float)numStep.Minimum, s.StepSize));
            UpdateSettingsControlsEnabled();
        }

        private CompressionSettings ReadSettingsFromUi()
        {
            return new CompressionSettings
            {
                Algorithm = (CompressionAlgorithm)cmbAlgorithm.SelectedIndex,
                TargetSampleRate = (int)numSampleRate.Value,
                QuantizationBits = (int)numQuantBits.Value,
                Mu = (float)numMu.Value,
                StepSize = (float)numStep.Value
            };
        }

        // ===================================================================
        // Compression
        // ===================================================================
        private async void BtnCompress_Click(object sender, EventArgs e)
        {
            if (_loadedAudio == null)
            {
                MessageBox.Show(this, "Open a WAV file first.", "No audio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_busy) return;

            var settings = ReadSettingsFromUi();
            settings.TargetSampleRate = Math.Min(settings.TargetSampleRate, _loadedAudio.SampleRate);

            SetBusy(true, "Compressing...");
            ResetCharts();
            txtReport.Clear();

            _cts = new CancellationTokenSource();
            var sw = Stopwatch.StartNew();
            var progress = new Progress<ProgressUpdate>(OnProgress);

            try
            {
                var container = await Task.Run(() => RunCompression(_loadedAudio, settings, sw, progress, _cts.Token));
                sw.Stop();

                _compressedOutput = container;
                _outputKind = OutputKind.Compressed;
                int compressedSize = EstimateContainerSize(container);

                _lastResult = new CompressionResult
                {
                    Settings = settings,
                    OriginalSizeBytes = _loadedAudio.FileSizeBytes,
                    CompressedSizeBytes = compressedSize,
                    Elapsed = sw.Elapsed,
                    WasCancelled = false
                };

                // Force a final chart point so even very fast jobs draw something.
                AddChartPointFinal(_lastResult, sw.Elapsed.TotalSeconds);

                progressBar.Value = 100;
                lblStatus.Text = "Compression complete.";
                statusLabel.Text = "Compression complete. Click Save Output to write the .amcx file.";
                WriteReport(_lastResult);
                UpdateButtonState();
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                lblStatus.Text = "Compression cancelled.";
                statusLabel.Text = "Compression cancelled by user.";
                progressBar.Value = 0;
                _compressedOutput = null;
                _outputKind = OutputKind.None;
                UpdateButtonState();
            }
            catch (Exception ex)
            {
                sw.Stop();
                MessageBox.Show(this, ex.ToString(), "Compression failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Compression failed.";
            }
            finally
            {
                SetBusy(false);
                _cts?.Dispose();
                _cts = null;
            }
        }

        private CompressedFileFormat.Container RunCompression(AudioFile audio, CompressionSettings settings,
            Stopwatch sw, IProgress<ProgressUpdate> progress, CancellationToken token)
        {
            var compressor = CompressorFactory.Create(settings.Algorithm);
            int bitsPerCode = compressor.GetBitsPerCode(settings);
            float p1, p2;
            compressor.GetParams(settings, out p1, out p2);

            short[][] resampled = new short[audio.Channels][];
            int targetSr = settings.TargetSampleRate;
            for (int c = 0; c < audio.Channels; c++)
            {
                resampled[c] = Resampler.Resample(audio.Samples[c], audio.SampleRate, targetSr);
            }
            int samplesPerChannel = resampled[0].Length;
            long totalInputSamples = (long)samplesPerChannel * audio.Channels;
            long processedSamples = 0;
            long producedBytes = 0;

            byte[][] channelData = new byte[audio.Channels][];
            for (int c = 0; c < audio.Channels; c++)
            {
                token.ThrowIfCancellationRequested();

                int currentChannel = c;
                IProgress<long> wrap = new Progress<long>(delta =>
                {
                    Interlocked.Add(ref processedSamples, delta);
                    double percent = totalInputSamples > 0
                        ? (processedSamples * 100.0) / totalInputSamples
                        : 100.0;
                    long bytesOut = Interlocked.Read(ref producedBytes);
                    progress?.Report(new ProgressUpdate
                    {
                        Percent = percent,
                        ProcessedInputBytes = Interlocked.Read(ref processedSamples) * 2,
                        ProducedOutputBytes = bytesOut,
                        ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
                        Status = "Encoding channel " + (currentChannel + 1) + " / " + audio.Channels + " - " + percent.ToString("0.0") + "%"
                    });
                });

                channelData[c] = compressor.Compress(resampled[c], settings, wrap, token);
                Interlocked.Add(ref producedBytes, channelData[c].Length);
            }

            return new CompressedFileFormat.Container
            {
                Algorithm = settings.Algorithm,
                Channels = audio.Channels,
                OriginalBitsPerSample = audio.BitsPerSample,
                OriginalSampleRate = audio.SampleRate,
                EncodedSampleRate = targetSr,
                SamplesPerChannel = samplesPerChannel,
                BitsPerCode = bitsPerCode,
                Param1 = p1,
                Param2 = p2,
                ChannelData = channelData
            };
        }

        // ===================================================================
        // Decompression (auto-loads the result into the preview player)
        // ===================================================================
        private async void BtnDecompress_Click(object sender, EventArgs e)
        {
            if (_compressedOutput == null)
            {
                MessageBox.Show(this, "Compress a file first, or open an existing .amcx file.", "No compressed data", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_busy) return;

            SetBusy(true, "Decompressing...");
            ResetCharts();
            _cts = new CancellationTokenSource();
            var sw = Stopwatch.StartNew();
            var progress = new Progress<ProgressUpdate>(OnProgress);

            try
            {
                var samples = await Task.Run(() => RunDecompression(_compressedOutput, sw, progress, _cts.Token));
                sw.Stop();
                _decompressedSamples = samples;
                _decompressedSampleRate = _compressedOutput.EncodedSampleRate;
                _outputKind = OutputKind.Decompressed;

                // Force a final point so the chart always has something to draw.
                long totalIn = 0;
                for (int i = 0; i < _compressedOutput.Channels; i++)
                    totalIn += _compressedOutput.ChannelData[i].Length;
                double finalMs = sw.Elapsed.TotalMilliseconds;
                double finalRatio = totalIn > 0
                    ? ((long)_compressedOutput.SamplesPerChannel * _compressedOutput.Channels * 2.0) / totalIn
                    : 0;
                double finalSpeed = sw.Elapsed.TotalSeconds > 0
                    ? (totalIn / 1024.0 / 1024.0) / sw.Elapsed.TotalSeconds
                    : 0;
                AddChartPoint(chartRatio, "ratio", finalMs, finalRatio);
                AddChartPoint(chartSpeed, "speed", finalMs, finalSpeed);

                // Write a temp WAV and load it for preview so the user can hear the decoded audio.
                try
                {
                    StopPlayback();
                    _player.Close();
                    WavWriter.Write(_decompressedPreviewPath, _decompressedSamples, _decompressedSampleRate);
                    _player.Load(_decompressedPreviewPath);
                    lblPreviewSource.Text = "Source: decompressed audio (preview lossy reconstruction)";
                }
                catch
                {
                    lblPreviewSource.Text = "Source: (playback unavailable)";
                }

                progressBar.Value = 100;
                lblStatus.Text = "Decompression complete (" + sw.Elapsed.TotalSeconds.ToString("0.00") + "s). Press Play to preview.";
                statusLabel.Text = "Decompression complete. Click Save Output to write a WAV file.";
                AppendReportLine("");
                AppendReportLine("Decompression completed in " + sw.Elapsed.TotalSeconds.ToString("0.00") + " seconds.");
                UpdatePlaybackLabel();
                UpdateButtonState();
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                lblStatus.Text = "Decompression cancelled.";
                progressBar.Value = 0;
                _decompressedSamples = null;
                UpdateButtonState();
            }
            catch (Exception ex)
            {
                sw.Stop();
                MessageBox.Show(this, ex.ToString(), "Decompression failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Decompression failed.";
            }
            finally
            {
                SetBusy(false);
                _cts?.Dispose();
                _cts = null;
            }
        }

        private short[][] RunDecompression(CompressedFileFormat.Container c, Stopwatch sw,
            IProgress<ProgressUpdate> progress, CancellationToken token)
        {
            var compressor = CompressorFactory.Create(c.Algorithm);
            short[][] output = new short[c.Channels][];
            long totalOutSamples = (long)c.SamplesPerChannel * c.Channels;
            long processedSamples = 0;
            long inputBytes = 0;
            for (int i = 0; i < c.Channels; i++) inputBytes += c.ChannelData[i].Length;

            for (int ch = 0; ch < c.Channels; ch++)
            {
                token.ThrowIfCancellationRequested();
                output[ch] = compressor.Decompress(c.ChannelData[ch], c.SamplesPerChannel, c.BitsPerCode, c.Param1, c.Param2, token);
                processedSamples += c.SamplesPerChannel;
                double percent = totalOutSamples > 0 ? (processedSamples * 100.0) / totalOutSamples : 100.0;
                progress?.Report(new ProgressUpdate
                {
                    Percent = percent,
                    ProcessedInputBytes = inputBytes,
                    ProducedOutputBytes = processedSamples * 2,
                    ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
                    Status = "Decoding channel " + (ch + 1) + " / " + c.Channels + " - " + percent.ToString("0.0") + "%"
                });
            }
            return output;
        }

        // ===================================================================
        // Save
        // ===================================================================
        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (_outputKind == OutputKind.Compressed && _compressedOutput != null)
            {
                using (var dlg = new SaveFileDialog
                {
                    Title = "Save compressed audio",
                    Filter = "Compressed audio (*.amcx)|*.amcx",
                    FileName = (_loadedAudio?.FileName ?? "audio") + ".amcx",
                    DefaultExt = "amcx",
                    AddExtension = true
                })
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        try
                        {
                            CompressedFileFormat.Write(dlg.FileName, _compressedOutput);
                            if (_lastResult != null)
                            {
                                var fi = new FileInfo(dlg.FileName);
                                _lastResult.CompressedSizeBytes = fi.Length;
                                _lastResult.OutputPath = dlg.FileName;
                                WriteReport(_lastResult);
                            }
                            statusLabel.Text = "Saved to " + dlg.FileName;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            else if (_outputKind == OutputKind.Decompressed && _decompressedSamples != null)
            {
                using (var dlg = new SaveFileDialog
                {
                    Title = "Save reconstructed WAV",
                    Filter = "WAV audio (*.wav)|*.wav",
                    FileName = "reconstructed.wav",
                    DefaultExt = "wav",
                    AddExtension = true
                })
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        try
                        {
                            WavWriter.Write(dlg.FileName, _decompressedSamples, _decompressedSampleRate);
                            statusLabel.Text = "Saved WAV to " + dlg.FileName;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show(this, "Nothing to save yet. Compress or decompress first.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            _cts?.Cancel();
            statusLabel.Text = "Cancellation requested...";
        }

        // ===================================================================
        // Progress / charts / reports
        // ===================================================================
        private double _lastChartAtMs = double.MinValue;
        private const double ChartThrottleMs = 25;

        private void OnProgress(ProgressUpdate p)
        {
            if (p == null) return;
            int percent = (int)Math.Max(0, Math.Min(100, Math.Round(p.Percent)));
            if (progressBar.Value != percent) progressBar.Value = percent;
            lblStatus.Text = p.Status ?? string.Empty;

            if (_lastChartAtMs < 0 || p.ElapsedMilliseconds - _lastChartAtMs >= ChartThrottleMs)
            {
                _lastChartAtMs = p.ElapsedMilliseconds;
                AddChartPoint(chartRatio, "ratio", p.ElapsedMilliseconds, p.CurrentRatio);
                AddChartPoint(chartSpeed, "speed", p.ElapsedMilliseconds, p.SpeedMBps);
            }
        }

        private void ResetCharts()
        {
            chartRatio.Series["ratio"].Points.Clear();
            chartSpeed.Series["speed"].Points.Clear();
            _lastChartAtMs = double.MinValue;
        }

        private void AddChartPointFinal(CompressionResult r, double seconds)
        {
            double elapsedMs = Math.Max(1, seconds * 1000.0);
            AddChartPoint(chartRatio, "ratio", elapsedMs, r.CompressionRatio);
            double speed = seconds > 0 ? (r.OriginalSizeBytes / 1024.0 / 1024.0) / seconds : 0;
            AddChartPoint(chartSpeed, "speed", elapsedMs, speed);
        }

        /// <summary>
        /// Append a (timeMs, value) point and rescale axes to readable whole-number ticks.
        /// </summary>
        private static void AddChartPoint(System.Windows.Forms.DataVisualization.Charting.Chart chart, string seriesName, double timeMs, double y)
        {
            var points = chart.Series[seriesName].Points;
            points.AddXY(timeMs, y);
            const int maxPoints = 600;
            while (points.Count > maxPoints) points.RemoveAt(0);
            RefreshChartScale(chart);
        }

        private static void RefreshChartScale(System.Windows.Forms.DataVisualization.Charting.Chart chart)
        {
            var area = chart.ChartAreas["MainArea"];
            var points = chart.Series[0].Points;
            if (points.Count == 0) return;

            double xMax = points[points.Count - 1].XValue;
            area.AxisX.Minimum = 0;
            area.AxisY.Minimum = 0;

            if (xMax <= 100)
            {
                area.AxisX.Maximum = 100;
                area.AxisX.Interval = 10;
            }
            else if (xMax <= 500)
            {
                area.AxisX.Maximum = Math.Ceiling(xMax / 50.0) * 50;
                area.AxisX.Interval = 50;
            }
            else if (xMax <= 2000)
            {
                area.AxisX.Maximum = Math.Ceiling(xMax / 200.0) * 200;
                area.AxisX.Interval = 200;
            }
            else
            {
                area.AxisX.Maximum = Math.Ceiling(xMax / 500.0) * 500;
                area.AxisX.Interval = 500;
            }
        }

        private int EstimateContainerSize(CompressedFileFormat.Container c)
        {
            int total = 29;
            for (int i = 0; i < c.Channels; i++)
                total += 4 + (c.ChannelData[i]?.Length ?? 0);
            return total;
        }

        private void WriteReport(CompressionResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("===== Compression Report =====");
            sb.AppendLine("Algorithm        : " + r.Settings.Algorithm.ToFriendlyString());
            sb.AppendLine("Sample rate      : " + r.Settings.TargetSampleRate + " Hz (input " + (_loadedAudio?.SampleRate ?? 0) + " Hz)");
            sb.AppendLine("Quantization bits: " + r.Settings.QuantizationBits);
            if (r.Settings.Algorithm == CompressionAlgorithm.NonlinearQuantization)
                sb.AppendLine("Mu (\u03BC)            : " + r.Settings.Mu);
            if (r.Settings.Algorithm == CompressionAlgorithm.DeltaModulation)
                sb.AppendLine("Step size        : " + r.Settings.StepSize);
            sb.AppendLine();
            sb.AppendLine("Original size    : " + FormatBytes(r.OriginalSizeBytes));
            sb.AppendLine("Compressed size  : " + FormatBytes(r.CompressedSizeBytes));
            sb.AppendLine("Size savings     : " + r.SavingsPercent.ToString("0.00") + " %");
            sb.AppendLine("Compression ratio: " + r.CompressionRatio.ToString("0.00") + " : 1");
            sb.AppendLine("Time taken       : " + r.Elapsed.TotalSeconds.ToString("0.00") + " s");
            if (!string.IsNullOrEmpty(r.OutputPath))
                sb.AppendLine("Output           : " + r.OutputPath);
            txtReport.Text = sb.ToString();
        }

        private void AppendReportLine(string line)
        {
            txtReport.AppendText(line + Environment.NewLine);
        }

        // ===================================================================
        // Button enable/disable + matching visual style
        // ===================================================================
        private void StyleButton(Button btn, Color activeBack, Color activeFore, bool enabled)
        {
            btn.Enabled = enabled;
            btn.BackColor = enabled ? activeBack : DisabledBack;
            btn.ForeColor = enabled ? activeFore : DisabledFore;
            btn.FlatAppearance.BorderColor = enabled ? activeBack : DisabledBack;
        }

        private void SetBusy(bool busy, string status = null)
        {
            _busy = busy;
            UpdateButtonState();
            UpdateSettingsControlsEnabled();
            cmbAlgorithm.Enabled = !busy;
            numSampleRate.Enabled = !busy;
            if (status != null) lblStatus.Text = status;
        }

        private void UpdateButtonState()
        {
            bool hasWav = _loadedAudio != null;
            bool hasCompressed = _compressedOutput != null;
            bool canPlay = _player.IsLoaded;

            StyleButton(btnOpen,           _btnOpenColor,           Color.White, !_busy);
            StyleButton(btnOpenCompressed, _btnOpenCompressedColor, Color.White, !_busy);

            StyleButton(btnPlay,  _btnPlayColor,  Color.White,    canPlay && !_busy);
            StyleButton(btnPause, _btnPauseColor, _btnPauseFore,  canPlay && !_busy);
            StyleButton(btnStop,  _btnStopColor,  Color.White,    canPlay && !_busy);

            StyleButton(btnCompress,   _btnCompressColor,   Color.White, hasWav && !_busy);
            StyleButton(btnDecompress, _btnDecompressColor, Color.White, hasCompressed && !_busy);
            StyleButton(btnCancel,     _btnCancelColor,     Color.White, _busy);
            StyleButton(btnSave,       _btnSaveColor,       Color.White, _outputKind != OutputKind.None && !_busy);
            StyleButton(btnResetSettings, _btnResetColor,   Color.White, !_busy);
        }

        // ===================================================================
        // Helpers
        // ===================================================================
        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            double v = bytes;
            string[] units = { "B", "KB", "MB", "GB" };
            int u = 0;
            while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
            return v.ToString("0.##") + " " + units[u];
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _cts?.Cancel();
            _player.Dispose();
            try { if (File.Exists(_decompressedPreviewPath)) File.Delete(_decompressedPreviewPath); }
            catch { /* best-effort cleanup */ }
        }
    }
}

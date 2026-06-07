using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace MutliMediaProject
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        // File area
        private GroupBox grpFile;
        private Button btnOpen;
        private Button btnOpenCompressed;
        private Label lblFileNameValue;
        private Label lblSizeValue;
        private Label lblDurationValue;
        private Label lblSampleRateValue;
        private Label lblChannelsValue;
        private Label lblBitsValue;
        private Label lblBitrateValue;
        private Label lblEncodingValue;

        // Preview
        private GroupBox grpPreview;
        private Button btnPlay;
        private Button btnPause;
        private Button btnStop;
        private Label lblPlaybackPosition;
        private Label lblPreviewSource;
        private Timer playbackTimer;

        // Settings
        private GroupBox grpSettings;
        private ComboBox cmbAlgorithm;
        private NumericUpDown numSampleRate;
        private NumericUpDown numQuantBits;
        private NumericUpDown numMu;
        private NumericUpDown numStep;
        private Label lblAlgorithm;
        private Label lblSampleRate;
        private Label lblQuantBits;
        private Label lblMu;
        private Label lblStep;
        private Button btnResetSettings;

        // Actions
        private Button btnCompress;
        private Button btnDecompress;
        private Button btnCancel;
        private Button btnSave;

        // Progress + charts
        private GroupBox grpProgress;
        private ProgressBar progressBar;
        private Label lblStatus;
        private Chart chartRatio;
        private Chart chartSpeed;

        // Report
        private GroupBox grpReport;
        private TextBox txtReport;

        // Status strip
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            this.SuspendLayout();
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1080, 900);
            this.Text = "Audio Compression Studio";
            this.MinimumSize = new Size(1000, 820);
            this.BackColor = Color.FromArgb(245, 247, 250);
            this.Font = new Font("Segoe UI", 9f);
            this.AllowDrop = true;

            // =============================================================
            // 1. File group
            // =============================================================
            this.grpFile = new GroupBox
            {
                Text = "1. Audio File",
                Location = new Point(12, 12),
                Size = new Size(1056, 160),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.White
            };
            this.Controls.Add(this.grpFile);

            this.btnOpen = new Button
            {
                Text = "Open WAV...",
                Location = new Point(14, 26),
                Size = new Size(150, 32),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            this.btnOpen.FlatAppearance.BorderSize = 0;
            this.grpFile.Controls.Add(this.btnOpen);

            this.btnOpenCompressed = new Button
            {
                Text = "Open .amcx...",
                Location = new Point(172, 26),
                Size = new Size(150, 32),
                BackColor = Color.FromArgb(100, 110, 130),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            this.btnOpenCompressed.FlatAppearance.BorderSize = 0;
            this.grpFile.Controls.Add(this.btnOpenCompressed);

            int col1X = 14;
            int col2X = 540;
            int propsLabelW = 110;
            int propsValueW1 = 410;
            int propsValueW2 = 380;
            int rowH = 21;
            int startY = 68;

            AddPropertyRow(this.grpFile, "File:",        out this.lblFileNameValue,   col1X, startY + 0 * rowH, propsLabelW, propsValueW1);
            AddPropertyRow(this.grpFile, "Size:",        out this.lblSizeValue,       col1X, startY + 1 * rowH, propsLabelW, propsValueW1);
            AddPropertyRow(this.grpFile, "Duration:",    out this.lblDurationValue,   col1X, startY + 2 * rowH, propsLabelW, propsValueW1);
            AddPropertyRow(this.grpFile, "Sample rate:", out this.lblSampleRateValue, col1X, startY + 3 * rowH, propsLabelW, propsValueW1);

            AddPropertyRow(this.grpFile, "Channels:",    out this.lblChannelsValue,   col2X, startY + 0 * rowH, propsLabelW, propsValueW2);
            AddPropertyRow(this.grpFile, "Bit depth:",   out this.lblBitsValue,       col2X, startY + 1 * rowH, propsLabelW, propsValueW2);
            AddPropertyRow(this.grpFile, "Bit rate:",    out this.lblBitrateValue,    col2X, startY + 2 * rowH, propsLabelW, propsValueW2);
            AddPropertyRow(this.grpFile, "Encoding:",    out this.lblEncodingValue,   col2X, startY + 3 * rowH, propsLabelW, propsValueW2);

            // =============================================================
            // 2. Preview group
            // =============================================================
            this.grpPreview = new GroupBox
            {
                Text = "2. Preview",
                Location = new Point(12, 180),
                Size = new Size(520, 96),
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                BackColor = Color.White
            };
            this.Controls.Add(this.grpPreview);

            this.btnPlay  = new Button { Text = "\u25B6  Play",       Location = new Point(14, 28),  Size = new Size(90, 32), BackColor = Color.FromArgb(40, 167, 69),  ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            this.btnPause = new Button { Text = "\u275A\u275A Pause", Location = new Point(110, 28), Size = new Size(90, 32), BackColor = Color.FromArgb(255, 193, 7), ForeColor = Color.Black, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            this.btnStop  = new Button { Text = "\u25A0  Stop",       Location = new Point(206, 28), Size = new Size(90, 32), BackColor = Color.FromArgb(220, 53, 69),  ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            this.btnPlay.FlatAppearance.BorderSize = 0;
            this.btnPause.FlatAppearance.BorderSize = 0;
            this.btnStop.FlatAppearance.BorderSize = 0;
            this.lblPlaybackPosition = new Label { Text = "00:00 / 00:00", Location = new Point(310, 35), Size = new Size(200, 20), Font = new Font("Consolas", 10f, FontStyle.Bold) };
            this.lblPreviewSource = new Label
            {
                Text = "Source: (none)",
                Location = new Point(14, 66),
                Size = new Size(496, 20),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = Color.FromArgb(90, 95, 110),
                AutoEllipsis = true
            };
            this.grpPreview.Controls.Add(this.btnPlay);
            this.grpPreview.Controls.Add(this.btnPause);
            this.grpPreview.Controls.Add(this.btnStop);
            this.grpPreview.Controls.Add(this.lblPlaybackPosition);
            this.grpPreview.Controls.Add(this.lblPreviewSource);

            this.playbackTimer = new Timer(this.components) { Interval = 250 };

            // =============================================================
            // 3. Settings group
            // =============================================================
            this.grpSettings = new GroupBox
            {
                Text = "3. Compression Settings",
                Location = new Point(540, 180),
                Size = new Size(528, 168),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.White
            };
            this.Controls.Add(this.grpSettings);

            int sCol1X = 14, sCol2X = 270;
            int lblW = 110, ctlW = 130;
            int rh = 32;

            this.lblAlgorithm = NewLabel("Algorithm:", sCol1X, 30, lblW);
            this.cmbAlgorithm = new ComboBox { Location = new Point(sCol1X + lblW, 27), Size = new Size(370, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            this.grpSettings.Controls.Add(this.lblAlgorithm);
            this.grpSettings.Controls.Add(this.cmbAlgorithm);

            this.lblSampleRate = NewLabel("Sample rate:", sCol1X, 30 + rh, lblW);
            this.numSampleRate = new NumericUpDown { Location = new Point(sCol1X + lblW, 27 + rh), Size = new Size(ctlW, 24), Minimum = 4000, Maximum = 96000, Value = 44100, Increment = 1000 };
            this.grpSettings.Controls.Add(this.lblSampleRate);
            this.grpSettings.Controls.Add(this.numSampleRate);

            this.lblQuantBits = NewLabel("Quant. bits:", sCol2X, 30 + rh, lblW);
            this.numQuantBits = new NumericUpDown { Location = new Point(sCol2X + lblW, 27 + rh), Size = new Size(ctlW, 24), Minimum = 1, Maximum = 8, Value = 8 };
            this.grpSettings.Controls.Add(this.lblQuantBits);
            this.grpSettings.Controls.Add(this.numQuantBits);

            this.lblMu = NewLabel("\u03BC (mu):", sCol1X, 30 + 2 * rh, lblW);
            this.numMu = new NumericUpDown { Location = new Point(sCol1X + lblW, 27 + 2 * rh), Size = new Size(ctlW, 24), Minimum = 1, Maximum = 1023, Value = 255 };
            this.grpSettings.Controls.Add(this.lblMu);
            this.grpSettings.Controls.Add(this.numMu);

            this.lblStep = NewLabel("Step size:", sCol2X, 30 + 2 * rh, lblW);
            this.numStep = new NumericUpDown { Location = new Point(sCol2X + lblW, 27 + 2 * rh), Size = new Size(ctlW, 24), Minimum = 1, Maximum = 10000, Value = 200, Increment = 10 };
            this.grpSettings.Controls.Add(this.lblStep);
            this.grpSettings.Controls.Add(this.numStep);

            this.btnResetSettings = new Button
            {
                Text = "Reset to Original",
                Location = new Point(sCol1X, 30 + 3 * rh + 8),
                Size = new Size(160, 30),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            this.btnResetSettings.FlatAppearance.BorderSize = 0;
            this.grpSettings.Controls.Add(this.btnResetSettings);

            // =============================================================
            // Action buttons (under preview, left-aligned, parallel to settings)
            // =============================================================
            this.btnCompress   = new Button { Text = "Compress",       Location = new Point(12,  290), Size = new Size(120, 38), BackColor = Color.FromArgb(0, 120, 215),  ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
            this.btnDecompress = new Button { Text = "Decompress",     Location = new Point(140, 290), Size = new Size(120, 38), BackColor = Color.FromArgb(0, 150, 136),  ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
            this.btnCancel     = new Button { Text = "Cancel",         Location = new Point(268, 290), Size = new Size(120, 38), BackColor = Color.FromArgb(220, 53, 69),  ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10f, FontStyle.Bold), Enabled = false };
            this.btnSave       = new Button { Text = "Save Output...", Location = new Point(396, 290), Size = new Size(120, 38), BackColor = Color.FromArgb(40, 167, 69),  ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10f, FontStyle.Bold), Enabled = false };
            this.btnCompress.FlatAppearance.BorderSize = 0;
            this.btnDecompress.FlatAppearance.BorderSize = 0;
            this.btnCancel.FlatAppearance.BorderSize = 0;
            this.btnSave.FlatAppearance.BorderSize = 0;
            this.Controls.Add(this.btnCompress);
            this.Controls.Add(this.btnDecompress);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);

            // =============================================================
            // 4. Progress + charts  (starts AFTER Settings ends at y=380)
            // =============================================================
            this.grpProgress = new GroupBox
            {
                Text = "4. Real-time Progress",
                Location = new Point(12, 340),
                Size = new Size(1056, 310),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.White
            };
            this.Controls.Add(this.grpProgress);

            this.progressBar = new ProgressBar
            {
                Location = new Point(14, 30),
                Size = new Size(1028, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Style = ProgressBarStyle.Continuous
            };
            this.grpProgress.Controls.Add(this.progressBar);

            this.lblStatus = new Label
            {
                Text = "Ready.",
                Location = new Point(14, 58),
                Size = new Size(1028, 18),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Color.FromArgb(60, 60, 70),
                Font = new Font("Segoe UI", 9f, FontStyle.Italic)
            };
            this.grpProgress.Controls.Add(this.lblStatus);

            this.chartRatio = CreateChart("ratio", "Compression ratio (input/output)", "Time (ms)", "Ratio", Color.FromArgb(0, 120, 215));
            this.chartRatio.Location = new Point(14, 82);
            this.chartRatio.Size = new Size(510, 218);
            this.chartRatio.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            this.grpProgress.Controls.Add(this.chartRatio);

            this.chartSpeed = CreateChart("speed", "Processing speed", "Time (ms)", "MB/s", Color.FromArgb(40, 167, 69));
            this.chartSpeed.Location = new Point(532, 82);
            this.chartSpeed.Size = new Size(510, 218);
            this.chartSpeed.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.grpProgress.Controls.Add(this.chartSpeed);

            // =============================================================
            // 5. Report
            // =============================================================
            this.grpReport = new GroupBox
            {
                Text = "5. Report",
                Location = new Point(12, 658),
                Size = new Size(1056, 162),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.White
            };
            this.Controls.Add(this.grpReport);

            this.txtReport = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(14, 26),
                Size = new Size(1028, 126),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Consolas", 9.5f),
                BackColor = Color.FromArgb(250, 251, 253),
                BorderStyle = BorderStyle.FixedSingle
            };
            this.grpReport.Controls.Add(this.txtReport);

            this.statusStrip = new StatusStrip();
            this.statusLabel = new ToolStripStatusLabel("Drop a WAV or .amcx file anywhere on the window to load it.");
            this.statusStrip.Items.Add(this.statusLabel);
            this.Controls.Add(this.statusStrip);

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private static Label NewLabel(string text, int x, int y, int width)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9f)
            };
        }

        private static void AddPropertyRow(GroupBox parent, string text, out Label valueLabel, int x, int y, int labelW, int valueW)
        {
            var lbl = new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(labelW, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 70)
            };
            valueLabel = new Label
            {
                Text = "-",
                Location = new Point(x + labelW, y),
                Size = new Size(valueW, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(30, 30, 40),
                AutoEllipsis = true
            };
            parent.Controls.Add(lbl);
            parent.Controls.Add(valueLabel);
        }

        /// <summary>
        /// Configures a System.Windows.Forms.DataVisualization Chart with a single
        /// spline-area series and a clean light theme that matches the rest of the UI.
        /// </summary>
        private static Chart CreateChart(string seriesName, string title, string xAxisTitle, string yAxisTitle, Color seriesColor)
        {
            var chart = new Chart
            {
                BackColor = Color.White,
                BorderlineColor = Color.FromArgb(200, 205, 212),
                BorderlineDashStyle = ChartDashStyle.Solid,
                BorderlineWidth = 1
            };

            chart.Titles.Add(new Title
            {
                Text = title,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 55, 65),
                Alignment = ContentAlignment.TopLeft,
                Docking = Docking.Top
            });

            var area = new ChartArea("MainArea")
            {
                BackColor = Color.White,
                BorderColor = Color.Transparent
            };
            area.AxisX.Title = xAxisTitle;
            area.AxisY.Title = yAxisTitle;
            area.AxisX.Minimum = 0;
            area.AxisY.Minimum = 0;
            area.AxisX.LabelStyle.Format = "0";
            area.AxisY.LabelStyle.Format = "0.##";
            area.AxisX.Interval = 10;
            area.AxisX.Maximum = 100;
            area.AxisX.TitleFont = new Font("Segoe UI", 8f);
            area.AxisY.TitleFont = new Font("Segoe UI", 8f);
            area.AxisX.LabelStyle.Font = new Font("Segoe UI", 8f);
            area.AxisY.LabelStyle.Font = new Font("Segoe UI", 8f);
            area.AxisX.LineColor = Color.FromArgb(180, 185, 195);
            area.AxisY.LineColor = Color.FromArgb(180, 185, 195);
            area.AxisX.MajorGrid.LineColor = Color.FromArgb(225, 228, 232);
            area.AxisY.MajorGrid.LineColor = Color.FromArgb(225, 228, 232);
            area.AxisX.MajorTickMark.LineColor = Color.FromArgb(180, 185, 195);
            area.AxisY.MajorTickMark.LineColor = Color.FromArgb(180, 185, 195);
            area.InnerPlotPosition.Auto = false;
            area.InnerPlotPosition.X = 10;
            area.InnerPlotPosition.Y = 8;
            area.InnerPlotPosition.Width = 86;
            area.InnerPlotPosition.Height = 82;
            chart.ChartAreas.Add(area);

            var series = new Series(seriesName)
            {
                ChartType = SeriesChartType.Area,
                Color = Color.FromArgb(140, seriesColor),
                BorderColor = seriesColor,
                BorderWidth = 2,
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 4,
                MarkerColor = seriesColor,
                XValueType = ChartValueType.Double,
                YValueType = ChartValueType.Double
            };
            chart.Series.Add(series);

            return chart;
        }
    }
}

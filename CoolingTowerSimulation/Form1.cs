using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.WindowsForms;
using OxyPlot.Axes;
using OxyPlot.Annotations;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;

namespace CoolingTowerSimulation
{
    public partial class Form1 : Form
    {
        // Komponen UI utama
        private TabControl tabControl;
        private TabPage tabParameters, tabTimeDomain, tabFrequencyDomain, tabLaplace, tabZDomain, tabFormulas, tab3DVisualization;
        private WebView2 webView3D;

        // Kontrol input parameter sensor
        private NumericUpDown numAmbientTemp, numHumidity, numWindSpeed, numMechanicalVib, numWaterTemp;
        private Button btnSimulate, btnStopSimulation, btnUpdateAnalysis;
        private Label lblStatus;

        // Plot time-domain & frequency-domain
        private readonly PlotView[] timePlots = new PlotView[5];
        private readonly PlotView[] freqPlots = new PlotView[5];
        private PlotView plotLaplace, plotZDomain;
        private RichTextBox rtbLaplace, rtbZDomain, rtbFormulas;

        // Penyimpanan data simulasi sensor
        private SensorData sensorData;

        // Multi-rate sampling untuk 5 sensor:
        // Index: 0=Temp, 1=Hum, 2=Air, 3=Vib, 4=DO
        // SHT85=400Hz, Testo=1.25Hz, PCB=10000Hz, DO=0.033Hz
        private readonly double[] samplingRates = new double[5] { 400, 400, 1.25, 10000, 0.033 };

        // Durasi default simulasi (diperpanjang agar sensor lambat seperti DO cukup sampel untuk FFT)
        private readonly double duration = 60.0;

        // Variabel untuk simulasi realtime
        private Timer simulationTimer;
        private double currentTime = 0;
        private readonly double timeWindow = 10.0;
        private bool isSimulating = false;
        private Random rand = new Random(42);

        // Parameter proses cooling tower (dipengaruhi dari input pengguna)
        private double ambientTemp, humidity, windSpeed, mechVib, waterTemp;

        // Konstanta waktu (time constant) masing-masing sensor
        private readonly double tempTau = 0.5;
        private readonly double humidityTau = 0.67;
        private readonly double airflowTau = 0.2;
        private readonly double vibrationTau = 0.02;
        private readonly double doTau = 1.25;

        public Form1()
        {
            InitializeComponent();
            InitializeUI();
            InitializeTimer();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new System.Drawing.Size(1400, 900);
            this.Text = "Cooling Tower Analysis - 3D Model Visualization Included";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ResumeLayout(false);
        }

        private void InitializeTimer()
        {
            // Timer untuk update data realtime (interval 50 ms)
            simulationTimer = new Timer { Interval = 50 };
            simulationTimer.Tick += SimulationTimer_Tick;
        }

        private void InitializeUI()
        {
            tabControl = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F) };

            // Inisialisasi tab utama
            tabParameters = new TabPage("Parameter Input");
            tabFormulas = new TabPage("Physics Models");
            tab3DVisualization = new TabPage("3D Model Visualization");
            tabTimeDomain = new TabPage("Time Domain (REALTIME)");
            tabFrequencyDomain = new TabPage("Frequency Domain (FFT)");
            tabLaplace = new TabPage("S-Domain (Laplace)");
            tabZDomain = new TabPage("Z-Domain (Discrete)");

            // Urutan tab yang ditampilkan
            tabControl.TabPages.AddRange(new[] {
                tabParameters,
                tabFormulas,
                tab3DVisualization,
                tabTimeDomain,
                tabFrequencyDomain,
                tabLaplace,
                tabZDomain
            });

            // Isi tiap tab
            InitializeParameterTab();
            InitializeFormulaTab();
            Initialize3DTab();
            InitializeTimeDomainTab();
            InitializeFrequencyTab();
            InitializeLaplaceTab();
            InitializeZDomainTab();

            this.Controls.Add(tabControl);
        }

        // --- TAB 3D VISUALIZATION ---
        private async void Initialize3DTab()
        {
            Panel panel3D = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };

            webView3D = new WebView2
            {
                Dock = DockStyle.Fill
            };

            try
            {
                // Inisialisasi WebView2 untuk menampilkan model 3D
                await webView3D.EnsureCoreWebView2Async(null);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Gagal memuat WebView2 Runtime.\nError: " + ex.Message);
                return;
            }

            panel3D.Controls.Add(webView3D);
            tab3DVisualization.Controls.Add(panel3D);

            Load3DModel();
        }

        private async void Load3DModel()
        {
            // Muat file .glb lokal dan tampilkan lewat <model-viewer> di WebView2
            if (webView3D != null && webView3D.CoreWebView2 == null)
            {
                await webView3D.EnsureCoreWebView2Async();
            }

            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            string glbPath = Path.Combine(currentDir, "cooling_tower.glb");

            if (!File.Exists(glbPath))
            {
                // Jika model 3D tidak ada, biarkan tab kosong (tanpa error)
                return;
            }

            try
            {
                byte[] modelBytes = File.ReadAllBytes(glbPath);
                string base64Model = Convert.ToBase64String(modelBytes);
                string modelUri = $"data:model/gltf-binary;base64,{base64Model}";

                string htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <title>3D Viewer</title>
    <meta charset=""utf-8"">
    <script type=""module"" src=""https://ajax.googleapis.com/ajax/libs/model-viewer/3.4.0/model-viewer.min.js""></script>
    <style>
        body {{ margin: 0; background-color: #333333; overflow: hidden; }}
        model-viewer {{ width: 100vw; height: 100vh; }}
    </style>
</head>
<body>
    <model-viewer 
        src=""{modelUri}"" 
        alt=""Cooling Tower""
        environment-image=""https://modelviewer.dev/shared-assets/environments/aircraft_workshop_01_1k.hdr""
        shadow-intensity=""1""
        exposure=""1""
        tone-mapping=""aces""
        camera-controls 
        auto-rotate
        camera-orbit=""45deg 55deg 105%"" 
        min-camera-orbit=""auto auto 5%"">
    </model-viewer>
</body>
</html>";
                webView3D.NavigateToString(htmlContent);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal memuat 3D Model: {ex.Message}");
            }
        }

        // --- TAB PHYSICS FORMULAS ---
        private void InitializeFormulaTab()
        {
            // Layout 2 baris: rumus input sensor & rumus simulasi program
            TableLayoutPanel mainTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(10),
                BackColor = Color.WhiteSmoke
            };

            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            mainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            mainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            AddFormulaSection(mainTable, "Rumus Input Sensor", "input_formulas.png", 0);
            AddFormulaSection(mainTable, "Rumus Simulasi Program", "simulation_formulas.png", 1);

            tabFormulas.Controls.Add(mainTable);
        }

        private void AddFormulaSection(TableLayoutPanel parentTable, string titleText, string imageName, int rowIndex)
        {
            // Panel berisi judul + gambar rumus
            Panel container = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(5),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblTitle = new Label
            {
                Text = titleText,
                Dock = DockStyle.Top,
                Height = 35,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(240, 240, 240),
                ForeColor = Color.Black
            };

            PictureBox pb = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White,
                Padding = new Padding(5)
            };

            string imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, imageName);
            if (File.Exists(imagePath))
            {
                try
                {
                    // Load gambar rumus dari file
                    using (var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                    {
                        pb.Image = Image.FromStream(fs);
                    }
                }
                catch (Exception ex)
                {
                    SetErrorImage(pb, "Gagal memuat gambar: " + ex.Message);
                }
            }
            else
            {
                SetErrorImage(pb, $"File '{imageName}' tidak ditemukan!");
            }

            container.Controls.Add(pb);
            container.Controls.Add(lblTitle);
            parentTable.Controls.Add(container, 0, rowIndex);
        }

        private void SetErrorImage(PictureBox pb, string msg)
        {
            // Tampilan placeholder jika gambar gagal dimuat
            pb.Image = null;
            pb.BackColor = Color.MistyRose;
            Label lblError = new Label
            {
                Text = "⚠️ " + msg,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Red,
                Font = new Font("Segoe UI", 9F, FontStyle.Italic)
            };
            pb.Controls.Add(lblError);
        }

        private void InitializeParameterTab()
        {
            // Tab konfigurasi parameter semua sensor
            Panel panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20) };
            int yPos = 20;

            Label title = new Label
            {
                Text = "Sensor Input Parameters Configuration",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                Location = new Point(20, yPos),
                AutoSize = true
            };
            panel.Controls.Add(title);
            yPos += 50;

            // Input numeric masing-masing parameter
            AddParameterControl(panel, "Ambient Temperature (°C):", 25m, 0m, 50m, ref numAmbientTemp, ref yPos);
            AddParameterControl(panel, "Relative Humidity (%):", 60m, 0m, 100m, ref numHumidity, ref yPos);
            AddParameterControl(panel, "Wind Speed (m/s):", 5m, 0m, 30m, ref numWindSpeed, ref yPos);
            AddParameterControl(panel, "Mechanical Vibration Base (g):", 0.5m, 0m, 5m, ref numMechanicalVib, ref yPos);
            AddParameterControl(panel, "Water Temperature (°C):", 30m, 10m, 60m, ref numWaterTemp, ref yPos);

            yPos += 20;

            // Tombol start simulasi realtime
            btnSimulate = new Button
            {
                Text = "▶ Start Realtime Simulation",
                Location = new Point(20, yPos),
                Size = new Size(200, 40),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 150, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSimulate.Click += BtnSimulate_Click;
            panel.Controls.Add(btnSimulate);

            // Tombol stop simulasi
            btnStopSimulation = new Button
            {
                Text = "⏹ Stop Simulation",
                Location = new Point(240, yPos),
                Size = new Size(180, 40),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = Color.FromArgb(200, 0, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            btnStopSimulation.Click += BtnStopSimulation_Click;
            panel.Controls.Add(btnStopSimulation);

            yPos += 50;

            // Tombol update analisis statis (FFT, Laplace, Z)
            btnUpdateAnalysis = new Button
            {
                Text = "🔄 Update FFT/Laplace/Z Analysis",
                Location = new Point(20, yPos),
                Size = new Size(260, 40),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnUpdateAnalysis.Click += (s, ev) => GenerateStaticAnalysis();
            panel.Controls.Add(btnUpdateAnalysis);

            yPos += 50;

            // Label status simulasi
            lblStatus = new Label
            {
                Text = "Status: Stopped",
                Location = new Point(20, yPos),
                Size = new Size(600, 30),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.Gray
            };
            panel.Controls.Add(lblStatus);

            yPos += 40;

            // Penjelasan strategi multi-rate sampling
            Label info = new Label
            {
                Text = "Multi-Rate Sampling Strategy:\n" +
                       "• Temperature (SHT85): 400 Hz - Time Sampling 12.7ms\n" +
                       "• Humidity (SHT85): 400 Hz - Time Sampling 12.7ms\n" +
                       "• Airflow (Testo 440): 1.25 Hz - Time Sampling 0.8s\n" +
                       "• Vibration (PCB 352C33): 10 kHz - Time Sampling 1ms\n" +
                       "• Dissolved Oxygen (Apera DO850): 0.033 Hz - Time Sampling 30s\n" +
                       "⚠️ UBAH PARAMETER SAAT SIMULASI untuk melihat efek real-time!",
                Location = new Point(20, yPos),
                Size = new Size(900, 180),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.DarkBlue
            };
            panel.Controls.Add(info);

            tabParameters.Controls.Add(panel);
        }

        private void AddParameterControl(Panel panel, string label, decimal defaultVal, decimal min, decimal max,
                                         ref NumericUpDown control, ref int yPos)
        {
            // Helper untuk membuat satu baris label + NumericUpDown
            Label lbl = new Label
            {
                Text = label,
                Location = new Point(20, yPos),
                Size = new Size(250, 25),
                Font = new Font("Segoe UI", 9F)
            };
            control = new NumericUpDown
            {
                Location = new Point(280, yPos),
                Size = new Size(120, 25),
                Minimum = min,
                Maximum = max,
                Value = defaultVal,
                DecimalPlaces = 2,
                Increment = 0.5m
            };
            panel.Controls.Add(lbl);
            panel.Controls.Add(control);
            yPos += 40;
        }

        private void InitializeTimeDomainTab()
        {
            // Tab untuk menampilkan sinyal realtime tiap sensor (time domain)
            Panel panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            string[] titles = {
                "Temperature (°C) - REALTIME [SHT85]",
                "Humidity (%) - REALTIME [SHT85]",
                "Airflow (m/s) - REALTIME [Testo 440]",
                "Vibration (g) - REALTIME [PCB 352C33]",
                "Dissolved Oxygen (mg/L) - REALTIME [Apera DO850]"
            };

            for (int i = 0; i < 5; i++)
            {
                timePlots[i] = new PlotView
                {
                    Location = new Point(10, 10 + i * 170),
                    Size = new Size(1350, 160),
                    BackColor = Color.White
                };
                var model = new PlotModel { Title = titles[i] };

                // Sumbu X (waktu)
                model.Axes.Add(new LinearAxis
                {
                    Position = AxisPosition.Bottom,
                    Title = "Time (s)",
                    Minimum = 0,
                    Maximum = timeWindow,
                    MajorGridlineStyle = LineStyle.Solid,
                    MinorGridlineStyle = LineStyle.Dot,
                    MajorGridlineColor = OxyColors.LightGray
                });

                // Sumbu Y (nilai sensor)
                model.Axes.Add(new LinearAxis
                {
                    Position = AxisPosition.Left,
                    Title = titles[i].Split('-')[0].Trim(),
                    MajorGridlineStyle = LineStyle.Solid,
                    MinorGridlineStyle = LineStyle.Dot,
                    MajorGridlineColor = OxyColors.LightGray
                });

                var series = new LineSeries
                {
                    Color = OxyColors.Blue,
                    StrokeThickness = 2,
                    LineStyle = LineStyle.Solid
                };
                model.Series.Add(series);
                timePlots[i].Model = model;
                panel.Controls.Add(timePlots[i]);
            }

            tabTimeDomain.Controls.Add(panel);
        }

        private void InitializeFrequencyTab()
        {
            // Tab untuk menampilkan spektrum FFT masing-masing sensor
            Panel panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            string[] titles = {
                "Temperature Spectrum",
                "Humidity Spectrum",
                "Airflow Spectrum",
                "Vibration Spectrum",
                "Dissolved Oxygen Spectrum"
            };

            for (int i = 0; i < 5; i++)
            {
                freqPlots[i] = new PlotView
                {
                    Location = new Point(10, 10 + i * 170),
                    Size = new Size(1350, 160),
                    BackColor = Color.White
                };
                var model = new PlotModel { Title = titles[i] };

                // Sumbu X (frekuensi)
                model.Axes.Add(new LinearAxis
                {
                    Position = AxisPosition.Bottom,
                    Title = "Frequency (Hz)",
                    MajorGridlineStyle = LineStyle.Solid,
                    MinorGridlineStyle = LineStyle.Dot,
                    MajorGridlineColor = OxyColors.LightGray
                });

                // Sumbu Y (magnitudo)
                model.Axes.Add(new LinearAxis
                {
                    Position = AxisPosition.Left,
                    Title = "Magnitude",
                    MajorGridlineStyle = LineStyle.Solid,
                    MinorGridlineStyle = LineStyle.Dot,
                    MajorGridlineColor = OxyColors.LightGray
                });

                freqPlots[i].Model = model;
                panel.Controls.Add(freqPlots[i]);
            }

            tabFrequencyDomain.Controls.Add(panel);
        }

        private void InitializeLaplaceTab()
        {
            // Tab analisis domain s (Laplace): teks + plot pole
            Panel mainPanel = new Panel { Dock = DockStyle.Fill };
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 600
            };

            rtbLaplace = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9F),
                ReadOnly = true,
                BackColor = Color.WhiteSmoke,
                Padding = new Padding(10)
            };
            split.Panel1.Controls.Add(rtbLaplace);

            Panel plotPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            Label lblPlot = new Label
            {
                Text = "Pole Plot (S-Plane)",
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Height = 30,
                TextAlign = ContentAlignment.MiddleCenter
            };
            plotLaplace = new PlotView { Dock = DockStyle.Fill, BackColor = Color.White };
            plotPanel.Controls.Add(plotLaplace);
            plotPanel.Controls.Add(lblPlot);
            split.Panel2.Controls.Add(plotPanel);

            mainPanel.Controls.Add(split);
            tabLaplace.Controls.Add(mainPanel);
        }

        private void InitializeZDomainTab()
        {
            // Tab analisis diskrit (Z-domain): teks + plot pole-zero
            Panel mainPanel = new Panel { Dock = DockStyle.Fill };
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 600
            };

            rtbZDomain = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9F),
                ReadOnly = true,
                BackColor = Color.WhiteSmoke,
                Padding = new Padding(10)
            };
            split.Panel1.Controls.Add(rtbZDomain);

            Panel plotPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            Label lblPlot = new Label
            {
                Text = "Pole-Zero Map (Z-Plane with Unit Circle)",
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Height = 30,
                TextAlign = ContentAlignment.MiddleCenter
            };
            plotZDomain = new PlotView { Dock = DockStyle.Fill, BackColor = Color.White };
            plotPanel.Controls.Add(plotZDomain);
            plotPanel.Controls.Add(lblPlot);
            split.Panel2.Controls.Add(plotPanel);

            mainPanel.Controls.Add(split);
            tabZDomain.Controls.Add(mainPanel);
        }

        private void BtnSimulate_Click(object sender, EventArgs e)
        {
            // Reset waktu & bersihkan plot realtime saat start ulang simulasi
            currentTime = 0;
            foreach (var plot in timePlots)
            {
                if (plot?.Model?.Series != null && plot.Model.Series.Count > 0)
                {
                    ((LineSeries)plot.Model.Series[0]).Points.Clear();
                }
            }
            isSimulating = true;
            simulationTimer.Start();
            btnSimulate.Enabled = false;
            btnStopSimulation.Enabled = true;
            lblStatus.Text = "Status: Running - Parameter dapat diubah real-time!";
            lblStatus.ForeColor = Color.Green;

            // Sekaligus update analisis statis awal (FFT, Laplace, Z)
            GenerateStaticAnalysis();
        }

        private void BtnStopSimulation_Click(object sender, EventArgs e)
        {
            // Hentikan simulasi realtime
            simulationTimer.Stop();
            isSimulating = false;
            btnSimulate.Enabled = true;
            btnStopSimulation.Enabled = false;
            lblStatus.Text = "Status: Stopped";
            lblStatus.ForeColor = Color.Gray;
        }

        private void SimulationTimer_Tick(object sender, EventArgs e)
        {
            if (!isSimulating) return;

            // Ambil parameter terkini dari UI (bisa berubah real-time)
            ambientTemp = (double)numAmbientTemp.Value;
            humidity = (double)numHumidity.Value;
            windSpeed = (double)numWindSpeed.Value;
            mechVib = (double)numMechanicalVib.Value;
            waterTemp = (double)numWaterTemp.Value;

            // Tambah waktu (step 0.05 s)
            currentTime += 0.05;

            // Update masing-masing sinyal sensor di time-domain
            UpdateRealtimePlot(timePlots[0], currentTime, CalculateTemperature(currentTime));
            UpdateRealtimePlot(timePlots[1], currentTime, CalculateHumidity(currentTime));
            UpdateRealtimePlot(timePlots[2], currentTime, CalculateAirflow(currentTime));
            UpdateRealtimePlot(timePlots[3], currentTime, CalculateVibration(currentTime));
            UpdateRealtimePlot(timePlots[4], currentTime, CalculateDissolvedOxygen(currentTime));
        }

        private void UpdateRealtimePlot(PlotView plot, double time, double value)
        {
            // Tambah titik baru ke plot realtime dan geser window waktu
            var series = (LineSeries)plot.Model.Series[0];
            series.Points.Add(new DataPoint(time, value));

            while (series.Points.Count > 0 && series.Points[0].X < time - timeWindow)
            {
                series.Points.RemoveAt(0);
            }

            var xAxis = plot.Model.Axes[0];
            if (time > timeWindow)
            {
                xAxis.Minimum = time - timeWindow;
                xAxis.Maximum = time;
            }
            plot.Model.InvalidatePlot(true);
        }

        // --- FUNGSI MODEL FISIK / SINYAL TIAP SENSOR ---

        private double CalculateTemperature(double t)
        {
            // Model suhu: baseline + gelombang periodik + efek angin + efek vibrasi + noise
            double baseTemp = ambientTemp;
            double periodic = 2 * Math.Sin(2 * Math.PI * 0.5 * t);
            double noise = 0.5 * (rand.NextDouble() - 0.5);
            double windEffect = -0.8 * (windSpeed - 5);
            double vibEffect = 0.3 * mechVib;
            return baseTemp + periodic + windEffect + vibEffect + noise;
        }

        private double CalculateHumidity(double t)
        {
            // Model kelembapan: dipengaruhi suhu, suhu air, angin, osilasi dan noise
            double baseHumidity = humidity;
            double tempEffect = -1.5 * (ambientTemp - 25);
            double waterEffect = 0.8 * (waterTemp - 30);
            double periodic = 5 * Math.Sin(2 * Math.PI * 0.3 * t);
            double noise = 1 * (rand.NextDouble() - 0.5);
            double windEffect = -0.5 * (windSpeed - 5);
            double result = baseHumidity + tempEffect + waterEffect + periodic + windEffect + noise;
            return Math.Max(0, Math.Min(100, result));
        }

        private double CalculateAirflow(double t)
        {
            // Model airflow: kombinasi beberapa frekuensi + efek suhu & humidity + noise
            double baseAirflow = windSpeed;
            double periodic1 = 1.5 * Math.Sin(2 * Math.PI * 1.0 * t);
            double periodic2 = 0.5 * Math.Sin(2 * Math.PI * 3.0 * t);
            double noise = 0.3 * (rand.NextDouble() - 0.5);
            double tempEffect = 0.3 * (ambientTemp - 25);
            double humidityEffect = -0.05 * (humidity - 60);
            return Math.Max(0, baseAirflow + periodic1 + periodic2 + tempEffect + humidityEffect + noise);
        }

        private double CalculateVibration(double t)
        {
            // Model vibrasi: fundamental 60 Hz + harmonik 120 Hz + efek angin + noise
            double fundamental = mechVib * Math.Sin(2 * Math.PI * 60 * t);
            double harmonic = 0.2 * Math.Sin(2 * Math.PI * 120 * t);
            double noise = 0.1 * (rand.NextDouble() - 0.5);
            double windEffect = 0.05 * windSpeed * Math.Sin(2 * Math.PI * 5 * t);
            return fundamental + harmonic + windEffect + noise;
        }

        private double CalculateDissolvedOxygen(double t)
        {
            // Model DO: fungsi suhu air, angin, suhu lingkungan, osilasi dan noise
            double tempFactor = (waterTemp - 20) / 10.0;
            double baseDO = 8.0 - 2.0 * tempFactor;
            double periodic = 0.5 * Math.Sin(2 * Math.PI * 0.2 * t);
            double noise = 0.2 * (rand.NextDouble() - 0.5);
            double aerationEffect = 0.5 * (windSpeed - 5);
            double ambientEffect = -0.15 * (ambientTemp - 25);
            return Math.Max(0, baseDO + periodic + aerationEffect + ambientEffect + noise);
        }

        private void GenerateStaticAnalysis()
        {
            // Ambil parameter saat ini dari UI
            ambientTemp = (double)numAmbientTemp.Value;
            humidity = (double)numHumidity.Value;
            windSpeed = (double)numWindSpeed.Value;
            mechVib = (double)numMechanicalVib.Value;
            waterTemp = (double)numWaterTemp.Value;

            sensorData = new SensorData();

            // Bangkitkan sinyal time-series untuk tiap sensor (dengan fs masing-masing)
            for (int sensorIdx = 0; sensorIdx < 5; sensorIdx++)
            {
                double fs = samplingRates[sensorIdx];

                // Pastikan minimal jumlah sampel (misal 128 titik) supaya FFT masuk akal
                double minSamples = 128.0;
                double calculatedDuration = minSamples / fs;

                // Ambil durasi yang lebih besar: duration default (60 s) atau hasil hitungan
                double localDuration = Math.Max(duration, calculatedDuration);

                // Batasi jumlah sampel maksimum untuk sensor dengan fs tinggi agar tidak berat
                if (localDuration * fs > 200000)
                {
                    localDuration = 200000.0 / fs;
                }

                int numPoints = (int)(fs * localDuration);
                double dt = 1.0 / fs;

                double[] timeData = new double[numPoints];
                double[] valueData = new double[numPoints];

                for (int i = 0; i < numPoints; i++)
                {
                    double t = i * dt;
                    timeData[i] = t;

                    // Pilih fungsi model sesuai sensor
                    switch (sensorIdx)
                    {
                        case 0: valueData[i] = CalculateTemperature(t); break;
                        case 1: valueData[i] = CalculateHumidity(t); break;
                        case 2: valueData[i] = CalculateAirflow(t); break;
                        case 3: valueData[i] = CalculateVibration(t); break;
                        case 4: valueData[i] = CalculateDissolvedOxygen(t); break;
                    }
                }
                sensorData.Time[sensorIdx] = timeData;
                sensorData.SensorValues[sensorIdx] = valueData;
            }

            // Update semua tab analisis
            UpdateFrequencyDomainPlots();
            UpdateLaplaceDomain();
            UpdateZDomain();
        }

        private void UpdateFrequencyDomainPlots()
        {
            // Hitung FFT dan update plot untuk semua sensor
            string[] names = { "Temperature", "Humidity", "Airflow", "Vibration", "DO" };
            for (int i = 0; i < 5; i++)
            {
                UpdateFFTPlot(freqPlots[i], sensorData.SensorValues[i], samplingRates[i], names[i]);
            }
        }

        // Plot FFT untuk satu sensor, fs dalam double
        private void UpdateFFTPlot(PlotView plot, double[] signal, double fs, string title)
        {
            // Pastikan panjang sinyal genap untuk FFT
            int n = signal.Length;
            if (n % 2 != 0)
            {
                Array.Resize(ref signal, n + 1);
                signal[n] = 0; // zero-padding jika ganjil
                n++;
            }

            var complex = signal.Select(x => new Complex(x, 0)).ToArray();
            Fourier.Forward(complex, FourierOptions.Matlab);

            int halfN = n / 2;
            double[] frequencies = new double[halfN];
            double[] magnitudes = new double[halfN];

            for (int i = 0; i < halfN; i++)
            {
                frequencies[i] = i * fs / (double)n;
                magnitudes[i] = complex[i].Magnitude / n * 2;
            }

            plot.Model.Series.Clear();
            var series = new LineSeries
            {
                Title = $"{title} FFT",
                Color = OxyColors.Red,
                StrokeThickness = 2
            };

            double nyquist = fs / 2.0;
            for (int i = 0; i < frequencies.Length && frequencies[i] <= nyquist; i++)
            {
                series.Points.Add(new DataPoint(frequencies[i], magnitudes[i]));
            }

            plot.Model.Series.Add(series);

            // Hitung dan tampilkan Ts di judul plot
            double Ts = 1.0 / fs;
            string tsLabel = Ts < 1.0 ? $"{Ts * 1000:F2} ms" : $"{Ts:F2} s";

            plot.Model.Title = $"{title} Spectrum\n(fs = {fs} Hz, Time Sampling (Ts) = {tsLabel})";

            plot.Model.Axes[0].Maximum = nyquist;
            plot.Model.InvalidatePlot(true);
        }

        private void UpdateLaplaceDomain()
        {
            // Bangun teks analisis domain s (model ekivalen first-order)
            rtbLaplace.Clear();
            rtbLaplace.AppendText("═══════════════════════════════════════════════════════════\n");
            rtbLaplace.AppendText("    S-DOMAIN STABILITY ANALYSIS - COOLING TOWER SYSTEM\n");
            rtbLaplace.AppendText("═══════════════════════════════════════════════════════════\n\n");

            // Parameter sistem (gain & feedback)
            double K_eff = 1.0;    // Total system gain
            double K_T = 1.0;      // Temperature sensor gain
            double K_F = 1.0;      // Airflow sensor gain
            double K_RH = 1.0;     // Humidity sensor gain
            double K_fb = 0.5;     // Feedback gain
            double tau_T = tempTau;      // Temperature time constant
            double tau_F = airflowTau;   // Airflow time constant
            double tau_RH = humidityTau; // Humidity time constant

            rtbLaplace.AppendText("COUPLED SYSTEM TRANSFER FUNCTION:\n");
            rtbLaplace.AppendText("─────────────────────────────────────────────────────────\n\n");

            rtbLaplace.AppendText("Individual Transfer Functions:\n");
            rtbLaplace.AppendText($"  G_T(s)  = K_T / (τ_T·s + 1)     where K_T = {K_T:F2}, τ_T = {tau_T:F2} s\n");
            rtbLaplace.AppendText($"  G_F(s)  = K_F / (τ_F·s + 1)     where K_F = {K_F:F2}, τ_F = {tau_F:F2} s\n");
            rtbLaplace.AppendText($"  G_RH(s) = K_RH / (τ_RH·s + 1)   where K_RH = {K_RH:F2}, τ_RH = {tau_RH:F2} s\n\n");

            rtbLaplace.AppendText("Cooling Tower Efficiency (with feedback):\n");
            rtbLaplace.AppendText("           K_eff · G_F(s) · G_RH(s)\n");
            rtbLaplace.AppendText("  G_eff(s) = ─────────────────────────────\n");
            rtbLaplace.AppendText("           1 + G_T(s) · G_F(s) · K_fb\n\n");

            // Hitung gain sistem ekivalen & dominan time constant
            double K_sys = (K_eff * K_F * K_RH) / (1 + K_T * K_F * K_fb);
            double tau_sys = Math.Max(Math.Max(tau_T, tau_F), tau_RH);

            rtbLaplace.AppendText("Equivalent System Parameters:\n");
            rtbLaplace.AppendText($"  K_sys = (K_eff · K_F · K_RH) / (1 + K_T · K_F · K_fb)\n");
            rtbLaplace.AppendText($"        = ({K_eff:F2} · {K_F:F2} · {K_RH:F2}) / (1 + {K_T:F2} · {K_F:F2} · {K_fb:F2})\n");
            rtbLaplace.AppendText($"        = {K_sys:F4}\n\n");

            rtbLaplace.AppendText("Simplified First-Order Equivalent:\n");
            rtbLaplace.AppendText("             K_sys\n");
            rtbLaplace.AppendText("  G_sys(s) = ───────────\n");
            rtbLaplace.AppendText("           τ_sys·s + 1\n\n");

            rtbLaplace.AppendText($"  τ_sys ≈ {tau_sys:F2} s (dominant time constant)\n\n");

            // Pole sistem
            double s_p = -1.0 / tau_sys;

            rtbLaplace.AppendText("─────────────────────────────────────────────────────────\n\n");

            rtbLaplace.AppendText("SYSTEM POLE (Stability Analysis):\n");
            rtbLaplace.AppendText("─────────────────────────────────────────────────────────\n\n");

            rtbLaplace.AppendText("Pole Location:\n");
            rtbLaplace.AppendText($"  s_p = -1/τ_sys = -1/{tau_sys:F2} = {s_p:F4} rad/s\n\n");

            rtbLaplace.AppendText("Pole Check:\n");
            rtbLaplace.AppendText($"  Real part: Re(s_p) = {s_p:F4}\n");
            rtbLaplace.AppendText($"  Imaginary part: Im(s_p) = 0 (real pole)\n\n");

            bool isStable = s_p < 0;

            rtbLaplace.AppendText("Stability Criterion:\n");
            rtbLaplace.AppendText("  For stability: Re(s) < 0 (Left Half Plane)\n\n");

            if (isStable)
            {
                rtbLaplace.AppendText($"  ✓ Re(s_p) = {s_p:F4} < 0  [STABLE]\n\n");
                rtbLaplace.AppendText("═══════════════════════════════════════════════════════════\n");
                rtbLaplace.AppendText("           ✓ SYSTEM IS STABLE ✓\n");
                rtbLaplace.AppendText("  (System pole located in Left Half Plane)\n");
                rtbLaplace.AppendText("═══════════════════════════════════════════════════════════\n\n");
            }
            else
            {
                rtbLaplace.AppendText($"  ✗ Re(s_p) = {s_p:F4} ≥ 0  [UNSTABLE]\n\n");
                rtbLaplace.AppendText("═══════════════════════════════════════════════════════════\n");
                rtbLaplace.AppendText("           ✗ SYSTEM IS UNSTABLE ✗\n");
                rtbLaplace.AppendText("  (System pole located in Right Half Plane)\n");
                rtbLaplace.AppendText("═══════════════════════════════════════════════════════════\n\n");
            }

            rtbLaplace.AppendText("SYSTEM RESPONSE CHARACTERISTICS:\n");
            rtbLaplace.AppendText("─────────────────────────────────────────────────────────\n\n");

            double settlingTime = -5.0 / s_p;
            double bandwidth = Math.Abs(s_p);
            double natFreq = Math.Abs(s_p) / (2 * Math.PI);

            rtbLaplace.AppendText($"  Time Constant (τ_sys):  {tau_sys:F2} s\n");
            rtbLaplace.AppendText($"  Settling Time (5τ):     {settlingTime:F2} s ({settlingTime / 60:F2} min)\n");
            rtbLaplace.AppendText($"  Bandwidth:              {bandwidth:F4} rad/s\n");
            rtbLaplace.AppendText($"  Natural Frequency:      {natFreq:F6} Hz\n");
            rtbLaplace.AppendText($"  System Gain:            {K_sys:F4}\n");
            rtbLaplace.AppendText($"  Decay Rate:             e^({s_p:F4}·t)\n\n");

            rtbLaplace.AppendText("PHYSICAL INTERPRETATION:\n");
            rtbLaplace.AppendText("─────────────────────────────────────────────────────────\n\n");
            rtbLaplace.AppendText("  • Pole represents cooling tower system dynamics\n");
            rtbLaplace.AppendText("  • Negative feedback ensures stability\n");
            rtbLaplace.AppendText($"  • Dominant time constant: {tau_sys:F2} s\n");
            rtbLaplace.AppendText("  • System response controlled by slowest sensor\n");
            rtbLaplace.AppendText("  • Closed-loop feedback reduces sensitivity to disturbances\n\n");

            rtbLaplace.AppendText("FEEDBACK EFFECT:\n");
            rtbLaplace.AppendText("─────────────────────────────────────────────────────────\n\n");
            rtbLaplace.AppendText($"  Open-loop gain:  K_F · K_RH = {K_F * K_RH:F2}\n");
            rtbLaplace.AppendText($"  Closed-loop gain: K_sys = {K_sys:F4}\n");
            rtbLaplace.AppendText($"  Feedback factor: 1 + K_T·K_F·K_fb = {1 + K_T * K_F * K_fb:F2}\n");
            rtbLaplace.AppendText($"  Gain reduction: {((1 - K_sys / (K_F * K_RH)) * 100):F1}%\n\n");

            rtbLaplace.AppendText("DESIGN GUIDELINES:\n");
            rtbLaplace.AppendText("─────────────────────────────────────────────────────────\n\n");
            rtbLaplace.AppendText($"  • Maintain negative feedback (K_fb > 0)\n");
            rtbLaplace.AppendText($"  • Ensure all sensor time constants positive\n");
            rtbLaplace.AppendText($"  • Sampling frequency >> {2 * natFreq:F6} Hz (Nyquist)\n");
            rtbLaplace.AppendText($"  • Control delay << {-0.5 / s_p:F2} s\n");
            rtbLaplace.AppendText($"  • Monitor dominant pole for stability margin\n");

            // Plot pole sistem di bidang s
            CreateSystemPolePlot(plotLaplace, s_p, tau_sys);
        }

        private void CreateSystemPolePlot(PlotView plotView, double pole, double tau)
        {
            // Plot satu pole sistem di bidang s (LHP/RHP + anotasi stabil/unstable)
            var model = new PlotModel
            {
                Title = "S-Plane: Cooling Tower System Pole",
                TitleFontSize = 14,
                TitleFontWeight = FontWeights.Bold
            };

            double poleAbs = Math.Abs(pole);
            double margin = Math.Max(0.5, poleAbs * 0.5);
            double xMin = pole - margin;
            double xMax = margin * 0.5;
            double yRange = (xMax - xMin) * 0.4;

            // Sumbu Real (σ)
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Real (σ) [rad/s]",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.LightGray,
                AxislineStyle = LineStyle.Solid,
                AxislineThickness = 2,
                Minimum = xMin,
                Maximum = xMax,
                FontSize = 11
            });

            // Sumbu Imaginer (jω)
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Imaginary (jω) [rad/s]",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.LightGray,
                AxislineStyle = LineStyle.Solid,
                AxislineThickness = 2,
                Minimum = -yRange,
                Maximum = yRange,
                FontSize = 11
            });

            // Garis batas stabilitas (sumbu imajiner)
            model.Annotations.Add(new LineAnnotation
            {
                Type = LineAnnotationType.Vertical,
                X = 0,
                Color = OxyColors.Red,
                StrokeThickness = 3,
                LineStyle = LineStyle.Dash,
                Text = "jω-axis"
            });

            // Region stabil (LHP)
            model.Annotations.Add(new RectangleAnnotation
            {
                MinimumX = xMin,
                MaximumX = 0,
                MinimumY = -yRange,
                MaximumY = yRange,
                Fill = OxyColor.FromArgb(60, 0, 200, 0),
                Text = "STABLE\n(LHP)",
                TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,
                TextVerticalAlignment = OxyPlot.VerticalAlignment.Middle,
                FontSize = 13,
                FontWeight = FontWeights.Bold
            });

            // Region tidak stabil (RHP)
            model.Annotations.Add(new RectangleAnnotation
            {
                MinimumX = 0,
                MaximumX = xMax,
                MinimumY = -yRange,
                MaximumY = yRange,
                Fill = OxyColor.FromArgb(60, 200, 0, 0),
                Text = "UNSTABLE\n(RHP)",
                TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,
                TextVerticalAlignment = OxyPlot.VerticalAlignment.Middle,
                FontSize = 13,
                FontWeight = FontWeights.Bold
            });

            // Titik pole
            var poleSeries = new ScatterSeries
            {
                MarkerType = MarkerType.Cross,
                MarkerSize = 20,
                MarkerStroke = OxyColors.DarkBlue,
                MarkerStrokeThickness = 5
            };
            poleSeries.Points.Add(new ScatterPoint(pole, 0));
            model.Series.Add(poleSeries);

            // Label informasi pole
            model.Annotations.Add(new TextAnnotation
            {
                Text = $"s_p = {pole:F4} rad/s\nτ_sys = {tau:F2} s\n(System Pole)",
                TextPosition = new DataPoint(pole, yRange * 0.25),
                Font = "Arial",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                TextColor = OxyColors.DarkBlue,
                Background = OxyColor.FromArgb(230, 255, 255, 200),
                Padding = new OxyThickness(6),
                Stroke = OxyColors.DarkBlue,
                StrokeThickness = 1
            });

            // Info stabilitas singkat
            model.Annotations.Add(new TextAnnotation
            {
                Text = $"✓ STABLE: Re(s_p) = {pole:F4} < 0",
                TextPosition = new DataPoint(xMin + (xMax - xMin) * 0.05, -yRange * 0.8),
                Font = "Arial",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                TextColor = OxyColors.DarkGreen,
                Background = OxyColor.FromArgb(220, 240, 255, 240),
                Padding = new OxyThickness(6)
            });

            plotView.Model = model;
            plotView.Model.InvalidatePlot(true);
        }

        private void UpdateZDomain()
        {
            // Teks analisis konversi pole DO dari s-domain ke z-domain (ZOH)
            rtbZDomain.Clear();
            rtbZDomain.AppendText("Z-Domain Transfer Function:\n");
            rtbZDomain.AppendText("G(z) = Z{G(s)} (ZOH)\n");
            rtbZDomain.AppendText("\n");
            rtbZDomain.AppendText("Conversion: z = e^(sT)\n");
            rtbZDomain.AppendText("T (sampling time) varies per sensor.\n");
            rtbZDomain.AppendText("\n");

            // T_DO diturunkan dari samplingRates[4] (sensor DO)
            double T_DO = 1.0 / samplingRates[4];

            rtbZDomain.AppendText($"Dominant Pole (s-domain): p5 = {-1.0 / doTau:F2}\n");
            rtbZDomain.AppendText($"Dominant Pole (z-domain): z_p5 = e^(p5 * T_DO)\n");
            rtbZDomain.AppendText($"  where T_DO = 1/{samplingRates[4]} Hz = {T_DO:F1} s\n");
            rtbZDomain.AppendText($"  z_p5 = e^({-1.0 / doTau:F2} * {T_DO:F1}) = {Math.Exp((-1.0 / doTau) * T_DO):F6}\n");
            rtbZDomain.AppendText("\n");
            rtbZDomain.AppendText("Example Zero (s-domain): z_s1 = -0.5\n");
            rtbZDomain.AppendText($"Example Zero (z-domain): z_z1 = e^(z_s1 * T_DO)\n");
            rtbZDomain.AppendText($"  z_z1 = e^(-0.5 * {T_DO:F1}) = {Math.Exp(-0.5 * T_DO):F6}\n");
            rtbZDomain.AppendText("\n");
            rtbZDomain.AppendText("Stability: |z| < 1 for all poles -> STABLE\n");

            double discretePole = Math.Exp((-1.0 / doTau) * T_DO);
            double discreteZero = Math.Exp(-0.5 * T_DO);
            CreatePoleZeroPlotWithUnitCircle(plotZDomain, discretePole, discreteZero);
        }

        private void CreatePoleOnlyPlot(PlotView plotView, double[] poles, string poleLabel)
        {
            // Plot hanya pole tunggal di s-plane (helper opsional)
            if (poles.Length != 1)
            {
                // Jika nanti ingin support multi-pole, tambahkan logika di sini
            }

            var model = new PlotModel
            {
                Title = "Pole Plot - S-Plane (Stability: LHP)",
                PlotAreaBorderColor = OxyColors.Black
            };

            double poleX = poles[0];
            double margin = 1.0;
            double range = Math.Max(5.0, Math.Abs(poleX) + margin);

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Real (σ)",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.LightGray,
                AxislineStyle = LineStyle.Solid,
                AxislineThickness = 2,
                Minimum = -range,
                Maximum = Math.Abs(poleX) > 0.5 ? poleX + margin : 1.0
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Imaginary (jω)",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.LightGray,
                AxislineStyle = LineStyle.Solid,
                AxislineThickness = 2,
                Minimum = -1.0,
                Maximum = 1.0
            });

            model.Annotations.Add(new LineAnnotation
            {
                Type = LineAnnotationType.Vertical,
                X = 0,
                Color = OxyColors.Red,
                StrokeThickness = 3,
                LineStyle = LineStyle.Dash,
                Text = "jω-axis (Stability Boundary)"
            });

            var stableRegion = new RectangleAnnotation
            {
                MinimumX = -range,
                MaximumX = 0,
                MinimumY = -1.0,
                MaximumY = 1.0,
                Fill = OxyColor.FromArgb(30, 0, 200, 0),
                Text = "STABLE\n(LHP)"
            };
            model.Annotations.Add(stableRegion);

            var unstableRegion = new RectangleAnnotation
            {
                MinimumX = 0,
                MaximumX = Math.Abs(poleX) > 0.5 ? poleX + margin : 1.0,
                MinimumY = -1.0,
                MaximumY = 1.0,
                Fill = OxyColor.FromArgb(30, 200, 0, 0),
                Text = "UNSTABLE\n(RHP)"
            };
            model.Annotations.Add(unstableRegion);

            var poleSeries = new ScatterSeries
            {
                MarkerType = MarkerType.Cross,
                MarkerSize = 15,
                MarkerStroke = OxyColors.Blue,
                MarkerStrokeThickness = 4,
                Title = "Pole (X)"
            };

            poleSeries.Points.Add(new ScatterPoint(poles[0], 0));
            model.Annotations.Add(new TextAnnotation
            {
                Text = poleLabel,
                TextPosition = new DataPoint(poles[0], 0.1),
                Font = "Arial",
                FontSize = 10,
                TextColor = OxyColors.DarkBlue
            });

            model.Series.Add(poleSeries);
            plotView.Model = model;
            plotView.Model.InvalidatePlot(true);
        }

        private void CreatePoleZeroPlotWithUnitCircle(PlotView plotView, double pole, double zero)
        {
            // Plot pole & zero di bidang z lengkap dengan unit circle
            var model = new PlotModel
            {
                Title = "Pole-Zero Map (Z-Plane) - Single Pole & Zero",
                PlotAreaBorderColor = OxyColors.Black
            };

            var xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Real",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.LightGray,
                Minimum = -1.5,
                Maximum = 1.5,
                AxislineStyle = LineStyle.Solid,
                AxislineThickness = 2
            };
            var yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Imaginary",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.LightGray,
                Minimum = -1.5,
                Maximum = 1.5,
                AxislineStyle = LineStyle.Solid,
                AxislineThickness = 2
            };

            model.Axes.Add(xAxis);
            model.Axes.Add(yAxis);

            // Unit circle |z| = 1
            var unitCircle = new FunctionSeries(
                t => Math.Cos(t),
                t => Math.Sin(t),
                0, 2 * Math.PI, 1000)
            {
                Color = OxyColors.Red,
                StrokeThickness = 2,
                LineStyle = LineStyle.Dash,
                Title = "Unit Circle (|z|=1)"
            };
            model.Series.Add(unitCircle);

            // Sumbu real & imaginer
            model.Annotations.Add(new LineAnnotation
            {
                Type = LineAnnotationType.Vertical,
                X = 0,
                Color = OxyColors.Black,
                StrokeThickness = 1,
                LineStyle = LineStyle.Solid
            });
            model.Annotations.Add(new LineAnnotation
            {
                Type = LineAnnotationType.Horizontal,
                Y = 0,
                Color = OxyColors.Black,
                StrokeThickness = 1,
                LineStyle = LineStyle.Solid
            });

            // Titik pole
            var poleSeries = new ScatterSeries
            {
                MarkerType = MarkerType.Cross,
                MarkerSize = 12,
                MarkerStroke = OxyColors.Red,
                MarkerStrokeThickness = 3,
                Title = "Pole (X)"
            };
            poleSeries.Points.Add(new ScatterPoint(pole, 0));
            model.Series.Add(poleSeries);

            // Titik zero
            var zeroSeries = new ScatterSeries
            {
                MarkerType = MarkerType.Circle,
                MarkerSize = 12,
                MarkerStroke = OxyColors.Blue,
                MarkerStrokeThickness = 3,
                MarkerFill = OxyColors.Transparent,
                Title = "Zero (O)"
            };
            zeroSeries.Points.Add(new ScatterPoint(zero, 0));
            model.Series.Add(zeroSeries);

            plotView.Model = model;
            plotView.Model.InvalidatePlot(true);
        }
    }

    public class SensorData
    {
        // Array time-series: [sensorIndex][sampleIndex]
        public double[][] Time { get; set; }

        // Array nilai sensor: [sensorIndex][sampleIndex]
        public double[][] SensorValues { get; set; }

        public SensorData()
        {
            Time = new double[5][];
            SensorValues = new double[5][];
        }
    }
}

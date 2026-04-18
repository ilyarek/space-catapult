using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.Management;
using System.Net.NetworkInformation;
using static System.Windows.Forms.AxHost;
using System.Windows.Forms.DataVisualization.Charting;
using System.Net;
using System.Timers;
using System.IO;

namespace OpenCM_9_04_GUI
{
    public partial class Form1 : Form
    {
        // Структура для хранения TLE данных
        public class TLEData
        {
            public string Name { get; set; }
            public string Line1 { get; set; }
            public string Line2 { get; set; }
            public double Inclination { get; set; }
            public double RAAN { get; set; }
            public double Eccentricity { get; set; }
            public double ArgPerigee { get; set; }
            public double MeanAnomaly { get; set; }
            public double MeanMotion { get; set; }
            public DateTime Epoch { get; set; }

            public bool ParseTLE(string line1, string line2, Action<string> errorLogger = null)
            {
                Line1 = line1;
                Line2 = line2;

                try
                {
                    // Парсим Line 2 с правильной обработкой пробелов
                    string incStr = line2.Substring(8, 8).Trim();
                    if (string.IsNullOrEmpty(incStr))
                        throw new Exception("Inclination cannot be empty");
                    Inclination = double.Parse(incStr, System.Globalization.CultureInfo.InvariantCulture) * Math.PI / 180.0;

                    string raanStr = line2.Substring(17, 8).Trim();
                    if (string.IsNullOrEmpty(raanStr))
                        throw new Exception("RAAN cannot be empty");
                    RAAN = double.Parse(raanStr, System.Globalization.CultureInfo.InvariantCulture) * Math.PI / 180.0;

                    string eccStr = line2.Substring(26, 7).Trim();
                    if (!string.IsNullOrEmpty(eccStr))
                        Eccentricity = double.Parse("0." + eccStr, System.Globalization.CultureInfo.InvariantCulture);
                    else
                        throw new Exception("Eccentricity cannot be empty");

                    string argStr = line2.Substring(34, 8).Trim();
                    if (string.IsNullOrEmpty(argStr))
                        throw new Exception("Argument perigee cannot be empty");
                    ArgPerigee = double.Parse(argStr, System.Globalization.CultureInfo.InvariantCulture) * Math.PI / 180.0;

                    string meanStr = line2.Substring(43, 8).Trim();
                    if (string.IsNullOrEmpty(meanStr))
                        throw new Exception("Mean anomaly cannot be empty");
                    MeanAnomaly = double.Parse(meanStr, System.Globalization.CultureInfo.InvariantCulture) * Math.PI / 180.0;

                    string motionStr = line2.Substring(52, 11).Trim();
                    if (string.IsNullOrEmpty(motionStr))
                        throw new Exception("Mean motion cannot be empty");
                    MeanMotion = double.Parse(motionStr, System.Globalization.CultureInfo.InvariantCulture);

                    // Парсим эпоху из Line 1
                    int year = int.Parse("20" + line1.Substring(18, 2));
                    string dayStr = line1.Substring(20, 12).Trim();
                    double dayOfYear = double.Parse(dayStr, System.Globalization.CultureInfo.InvariantCulture);
                    Epoch = new DateTime(year, 1, 1).AddDays(dayOfYear - 1);

                    return true;
                }
                catch (Exception ex)
                {
                    if (errorLogger != null)
                    {
                        errorLogger($"Error parsing TLE: {ex.Message}");
                        errorLogger($"Line1: {line1}");
                        errorLogger($"Line2: {line2}");
                    }
                    return false;
                }
            }

            public void SetDefaultValues()
            {
                Inclination = 51.6390 * Math.PI / 180.0;
                RAAN = 125.2481 * Math.PI / 180.0;
                Eccentricity = 0.0002447;
                ArgPerigee = 48.1017 * Math.PI / 180.0;
                MeanAnomaly = 70.2224 * Math.PI / 180.0;
                MeanMotion = 15.50205123;
                Epoch = DateTime.UtcNow.Date;
            }
        }

        // Для работы с орбитой
        private Bitmap earthMap;
        private TLEData issTLE;
        private System.Timers.Timer updateTimer;
        private List<PointF> currentTrack = new List<PointF>();
        private PointF issPosition;
        private string cachePath;
        private DateTime lastCelestrakAttempt = DateTime.MinValue;
        private const int CELESTRAK_COOLDOWN_MINUTES = 120;

        public Form1()
        {
            InitializeComponent();

            // Инициализация пути к кешу
            string cacheFolder = Path.Combine(Application.StartupPath, "Cache");
            if (!Directory.Exists(cacheFolder))
            {
                Directory.CreateDirectory(cacheFolder);
            }
            cachePath = Path.Combine(cacheFolder, "iss_tle_cache.txt");

            // Инициализация карты Земли
            try
            {
                earthMap = Properties.Resources.ModifiedBlueMarble;
            }
            catch (Exception ex)
            {
                earthMap = new Bitmap(pictureBox3.Width, pictureBox3.Height);
                using (Graphics g = Graphics.FromImage(earthMap))
                {
                    g.Clear(Color.LightBlue);
                }
            }

            pictureBox3.Paint += PictureBox3_Paint;

            updateTimer = new System.Timers.Timer(1000);
            updateTimer.Elapsed += UpdateTimer_Elapsed;
            updateTimer.AutoReset = true;
        }

        private void LogError(string message)
        {
            if (textBox1 == null) return;

            string logMessage = $"[{DateTime.Now:HH:mm:ss}] ERROR: {message}{Environment.NewLine}";

            if (textBox1.InvokeRequired)
            {
                textBox1.Invoke((MethodInvoker)delegate {
                    textBox1.AppendText(logMessage);
                });
            }
            else
            {
                textBox1.AppendText(logMessage);
            }
        }

        private void LogInfo(string message)
        {
            if (textBox1 == null) return;

            string logMessage = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";

            if (textBox1.InvokeRequired)
            {
                textBox1.Invoke((MethodInvoker)delegate {
                    textBox1.AppendText(logMessage);
                });
            }
            else
            {
                textBox1.AppendText(logMessage);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string[] portnames = SerialPort.GetPortNames();
            comboBox1.Items.AddRange(portnames);

            // Логируем только после полной загрузки формы
            LogInfo("Program is running");
            LoadISSTle();
            updateTimer.Start();
        }

        // ========== КЕШИРОВАНИЕ TLE ==========
        private bool TryLoadFromCache(out TLEData tleData)
        {
            tleData = new TLEData();

            try
            {
                if (File.Exists(cachePath))
                {
                    string[] lines = File.ReadAllLines(cachePath);

                    if (lines.Length < 4)
                    {
                        LogError("Cache-file is damaged (not enough strings)");
                        return false;
                    }

                    DateTime cacheTime = DateTime.Parse(lines[0]);
                    TimeSpan cacheAge = DateTime.Now - cacheTime;

                    LogInfo($"Found TLE cache age {cacheAge.TotalHours:F1} hours");

                    if (cacheAge.TotalHours < 24)
                    {
                        tleData.Name = lines[1];
                        if (tleData.ParseTLE(lines[2], lines[3], LogError))
                        {
                            LogInfo("TLE successfully loaded from cache");
                            return true;
                        }
                        else
                        {
                            LogError("TLE cache parsing failed");
                        }
                    }
                    else
                    {
                        LogInfo("TLE cache is obsolete (more than 24 hours)");
                    }
                }
                else
                {
                    LogInfo("Cache-file is not found");
                }
            }
            catch (Exception ex)
            {
                LogError($"Cache reading error: {ex.Message}");
            }

            return false;
        }

        private void SaveToCache(TLEData tleData)
        {
            try
            {
                string[] lines = new string[]
                {
                    DateTime.Now.ToString("O"),
                    tleData.Name,
                    tleData.Line1,
                    tleData.Line2
                };

                File.WriteAllLines(cachePath, lines);
                LogInfo($"TLE saved in cache: {cachePath}");
            }
            catch (Exception ex)
            {
                LogError($"Cache saving error: {ex.Message}");
            }
        }

        private void LoadISSTle()
        {
            // Пробуем загрузить из кеша
            if (TryLoadFromCache(out TLEData cachedTLE))
            {
                issTLE = cachedTLE;
                return;
            }

            // Проверяем кулдаун для Celestrak
            TimeSpan timeSinceLastAttempt = DateTime.Now - lastCelestrakAttempt;
            if (timeSinceLastAttempt.TotalMinutes < CELESTRAK_COOLDOWN_MINUTES)
            {
                LogInfo($"Skipping Celestrak request (cooldown {CELESTRAK_COOLDOWN_MINUTES} minutes, lasted {timeSinceLastAttempt.TotalMinutes:F1})");
                UseFallbackTLE();
                return;
            }

            // Пробуем загрузить с Celestrak
            if (TryLoadFromCelestrak(out TLEData webTLE))
            {
                issTLE = webTLE;
                SaveToCache(issTLE);
                return;
            }

            // Если не удалось - используем резервные параметры
            UseFallbackTLE();
        }

        private bool TryLoadFromCelestrak(out TLEData tleData)
        {
            tleData = new TLEData();
            lastCelestrakAttempt = DateTime.Now;

            try
            {
                LogInfo("Pulling TLE с Celestrak...");

                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) SpaceCatapult/1.0");
                    client.Headers.Add("Accept", "text/plain, */*");

                    string url = "https://celestrak.org/NORAD/elements/gp.php?CATNR=25544&FORMAT=TLE";
                    LogInfo($"URL: {url}");

                    string tleDataStr = client.DownloadString(url);

                    if (string.IsNullOrEmpty(tleDataStr))
                    {
                        LogError("Celestrak returned null or empty");
                        return false;
                    }

                    string[] lines = tleDataStr.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    LogInfo($"Got {lines.Length} strings from Celestrak");

                    if (lines.Length >= 3)
                    {
                        tleData.Name = lines[0].Trim();
                        LogInfo($"Satellite name: {tleData.Name}");

                        if (tleData.ParseTLE(lines[1].Trim(), lines[2].Trim(), LogError))
                        {
                            LogInfo("TLE is successfully loaded and parsed Celestrak");
                            return true;
                        }
                        else
                        {
                            LogError("Failed parsing TLE from Celestrak");
                        }
                    }
                    else
                    {
                        LogError($"Not enough strings in Celestrak aswer (got {lines.Length}, minimum 3)");
                    }
                }
            }
            catch (WebException webEx)
            {
                if (webEx.Response is HttpWebResponse response)
                {
                    LogError($"HTTP error {(int)response.StatusCode}: {response.StatusDescription}");

                    if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        LogError("Access denied! IP might be blocked for excessive downloads.");
                        LogError("Recomendation: wait 2+ hours before next try.");
                    }
                }
                else
                {
                    LogError($"Web exception: {webEx.Message}");
                }

                if (webEx.Status == WebExceptionStatus.Timeout)
                    LogError("Connection timeout Celestrak");
                else if (webEx.Status == WebExceptionStatus.NameResolutionFailure)
                    LogError("Failed resolutioning DNS name celestrak.org");
                else if (webEx.Status == WebExceptionStatus.ConnectFailure)
                    LogError("Failed connecting celestrak.org");
            }
            catch (Exception ex)
            {
                LogError($"Unexpected error parsing from Celestrak: {ex.Message}");
                LogError($"Error type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                    LogError($"Internal error: {ex.InnerException.Message}");
            }

            return false;
        }

        private void UseFallbackTLE()
        {
            LogInfo("Using backup ISS parameters");

            issTLE = new TLEData();
            issTLE.Name = "ISS (ZARYA) - FALLBACK";
            issTLE.SetDefaultValues();

            LogInfo($"Orbit parameters by default:");
            LogInfo($"  Inclination: {issTLE.Inclination * 180.0 / Math.PI:F4}°");
            LogInfo($"  RAAN: {issTLE.RAAN * 180.0 / Math.PI:F4}°");
            LogInfo($"  Eccentricity: {issTLE.Eccentricity:F7}");
            LogInfo($"  Argument perigee: {issTLE.ArgPerigee * 180.0 / Math.PI:F4}°");
            LogInfo($"  Mean anomaly: {issTLE.MeanAnomaly * 180.0 / Math.PI:F4}°");
            LogInfo($"  Mean motion: {issTLE.MeanMotion:F8} rot/day");
            LogInfo($"  Epoch: {issTLE.Epoch:yyyy-MM-dd HH:mm:ss}");
        }

        private void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (issTLE != null)
            {
                try
                {
                    UpdateISSPosition();
                    CalculateGroundTrack();

                    pictureBox3.Invoke((MethodInvoker)delegate {
                        pictureBox3.Invalidate();
                    });
                }
                catch (Exception ex)
                {
                    // Используем Debug.WriteLine вместо LogError чтобы избежать рекурсии
                    System.Diagnostics.Debug.WriteLine($"Timer error: {ex.Message}");
                }
            }
        }

        private void UpdateISSPosition()
        {
            try
            {
                DateTime now = DateTime.UtcNow;
                double[] pos = CalculatePosition(now);
                double lat = pos[0];
                double lon = pos[1];

                while (lon > 180) lon -= 360;
                while (lon < -180) lon += 360;

                float x = (float)((lon + 180) * pictureBox3.Width / 360.0);
                float y = (float)((90 - lat) * pictureBox3.Height / 180.0);

                issPosition = new PointF(x, y);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ISS position calculating error: {ex.Message}");
            }
        }

        private double CalculateGMST(DateTime utcTime)
        {
            // Юлианская дата
            double jd = (utcTime - new DateTime(2000, 1, 1, 12, 0, 0)).TotalDays + 2451545.0;
            double tu = (jd - 2451545.0) / 36525.0;

            // GMST в секундах (формула из Astronomical Almanac)
            double gmst_sec = 24110.54841 + tu * (8640184.812866 + tu * (0.093104 - tu * 6.2e-6));
            gmst_sec += 1.00273790934 * 86400.0 * (utcTime.Hour * 3600 + utcTime.Minute * 60 + utcTime.Second) / 86400.0;

            // Приведение к диапазону [0, 2π)
            gmst_sec = gmst_sec % 86400.0;
            if (gmst_sec < 0) gmst_sec += 86400.0;

            return gmst_sec * 2.0 * Math.PI / 86400.0; // в радианах
        }

        private double[] CalculatePosition(DateTime time)
        {
            const double MU = 398600.4418; // км³/с²
            const double RE = 6371.0; // км

            TimeSpan ts = time - issTLE.Epoch;
            double minutes = ts.TotalMinutes;

            // 1. Расчёт большой полуоси из mean motion
            double n_rad_per_min = issTLE.MeanMotion * 2.0 * Math.PI / 1440.0;
            double a = Math.Pow(MU / Math.Pow(n_rad_per_min / 60.0, 2), 1.0 / 3.0);

            // 2. Решение уравнения Кеплера
            double M = issTLE.MeanAnomaly + n_rad_per_min * minutes;
            double E = SolveKepler(M, issTLE.Eccentricity);

            // 3. Истинная аномалия и радиус
            double v = 2 * Math.Atan(Math.Sqrt((1 + issTLE.Eccentricity) / (1 - issTLE.Eccentricity)) * Math.Tan(E / 2));
            double r = a * (1 - issTLE.Eccentricity * Math.Cos(E));

            // 4. Позиция в орбитальной плоскости
            double u = v + issTLE.ArgPerigee;
            double xOrb = r * Math.Cos(u);
            double yOrb = r * Math.Sin(u);

            // 5. Преобразование в ECI (без коррекции RAAN!)
            double inc = issTLE.Inclination;
            double raan = issTLE.RAAN;

            double x_eci = xOrb * Math.Cos(raan) - yOrb * Math.Cos(inc) * Math.Sin(raan);
            double y_eci = xOrb * Math.Sin(raan) + yOrb * Math.Cos(inc) * Math.Cos(raan);
            double z_eci = yOrb * Math.Sin(inc);

            // 6. Преобразование ECI → ECEF через GMST
            double gmst = CalculateGMST(time);
            double x_ecef = x_eci * Math.Cos(gmst) + y_eci * Math.Sin(gmst);
            double y_ecef = -x_eci * Math.Sin(gmst) + y_eci * Math.Cos(gmst);
            double z_ecef = z_eci;

            // 7. Географические координаты
            double lat = Math.Asin(z_ecef / r) * 180.0 / Math.PI;
            double lon = Math.Atan2(y_ecef, x_ecef) * 180.0 / Math.PI;

            return new double[] { lat, lon };
        }

        private double SolveKepler(double M, double e)
        {
            double E = M;
            for (int i = 0; i < 50; i++)
            {
                double dE = (E - e * Math.Sin(E) - M) / (1 - e * Math.Cos(E));
                E -= dE;
                if (Math.Abs(dE) < 1e-8) break;
            }
            return E;
        }

        private void CalculateGroundTrack()
        {
            try
            {
                currentTrack.Clear();

                DateTime startTime = DateTime.UtcNow.AddMinutes(-30);
                DateTime endTime = DateTime.UtcNow.AddMinutes(90);

                double previousLon = 0;
                bool firstPoint = true;

                for (DateTime time = startTime; time <= endTime; time = time.AddSeconds(30))
                {
                    double[] pos = CalculatePosition(time);
                    double lat = pos[0];
                    double lon = pos[1];

                    while (lon > 180) lon -= 360;
                    while (lon < -180) lon += 360;

                    float x = (float)((lon + 180) * pictureBox3.Width / 360.0);
                    float y = (float)((90 - lat) * pictureBox3.Height / 180.0);

                    if (!firstPoint && Math.Abs(lon - previousLon) > 180)
                    {
                        currentTrack.Add(PointF.Empty);
                    }

                    currentTrack.Add(new PointF(x, y));
                    previousLon = lon;
                    firstPoint = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Trajectory calculating error: {ex.Message}");
            }
        }

        private void PictureBox3_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            if (earthMap != null)
            {
                g.DrawImage(earthMap, 0, 0, pictureBox3.Width, pictureBox3.Height);
            }

            // Отрисовка трассы
            if (currentTrack.Count > 0)
            {
                using (Pen trackPen = new Pen(Color.FromArgb(200, 255, 165, 0), 2))
                {
                    List<PointF> segment = new List<PointF>();

                    foreach (PointF point in currentTrack)
                    {
                        if (point == PointF.Empty && segment.Count > 1)
                        {
                            g.DrawLines(trackPen, segment.ToArray());
                            segment.Clear();
                        }
                        else if (point != PointF.Empty)
                        {
                            segment.Add(point);
                        }
                    }

                    if (segment.Count > 1)
                    {
                        g.DrawLines(trackPen, segment.ToArray());
                    }
                }
            }

            // Отрисовка позиции МКС
            if (issPosition != PointF.Empty)
            {
                using (Brush issBrush = new SolidBrush(Color.FromArgb(200, 50, 205, 50)))
                using (Pen outlinePen = new Pen(Color.White, 2))
                {
                    int pointSize = 8;
                    g.FillEllipse(issBrush, issPosition.X - pointSize / 2, issPosition.Y - pointSize / 2,
                        pointSize, pointSize);
                    g.DrawEllipse(outlinePen, issPosition.X - pointSize / 2, issPosition.Y - pointSize / 2,
                        pointSize, pointSize);
                }

                using (Brush glowBrush = new SolidBrush(Color.FromArgb(80, 50, 205, 50)))
                {
                    int glowSize = 20;
                    g.FillEllipse(glowBrush, issPosition.X - glowSize / 2, issPosition.Y - glowSize / 2,
                        glowSize, glowSize);
                }
            }

            // Координатная сетка
            using (Pen gridPen = new Pen(Color.FromArgb(100, 200, 200, 200), 1))
            {
                for (int lon = -180; lon <= 180; lon += 30)
                {
                    float x = (float)((lon + 180) * pictureBox3.Width / 360.0);
                    g.DrawLine(gridPen, x, 0, x, pictureBox3.Height);
                }

                for (int lat = -90; lat <= 90; lat += 30)
                {
                    float y = (float)((90 - lat) * pictureBox3.Height / 180.0);
                    g.DrawLine(gridPen, 0, y, pictureBox3.Width, y);
                }
            }

            // Информация о позиции
            if (issPosition != PointF.Empty)
            {
                DateTime now = DateTime.UtcNow;
                double[] pos = CalculatePosition(now);
                double lat = pos[0];
                double lon = pos[1];

                while (lon > 180) lon -= 360;
                while (lon < -180) lon += 360;

                string info = $"ISS: {lat:F2}°, {lon:F2}° | Time: {now:HH:mm:ss} UTC";

                using (Font font = new Font("Arial", 10, FontStyle.Bold))
                using (Brush bgBrush = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
                using (Brush textBrush = new SolidBrush(Color.White))
                {
                    SizeF textSize = g.MeasureString(info, font);
                    g.FillRectangle(bgBrush, 5, pictureBox3.Height - 30, textSize.Width + 10, 25);
                    g.DrawString(info, font, textBrush, 10, pictureBox3.Height - 25);
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                serialPort1.PortName = comboBox1.Text;
                serialPort1.BaudRate = Convert.ToInt32(comboBox2.Text);
                serialPort1.Open();

                byte[] buffer = new byte[1];
                buffer[0] = (byte)2;
                serialPort1.Write(buffer, 0, buffer.Length);

                comboBox1.Enabled = false;
                comboBox2.Enabled = false;
                comboBox3.Enabled = true;
                button1.Enabled = false;
                button2.Enabled = true;
                button3.Enabled = true;
                button4.Enabled = true;
                button6.Enabled = true;
                numericUpDown1.Enabled = true;
                numericUpDown2.Enabled = true;
                numericUpDown3.Enabled = true;
                numericUpDown4.Enabled = true;

                LogInfo($"Port {serialPort1.PortName.ToString()} is open {Environment.NewLine}");
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                serialPort1.Close();
                button1.Enabled = true;
                comboBox1.Enabled = true;
                comboBox2.Enabled = true;
                comboBox3.Enabled = false;
                button2.Enabled = false;
                button3.Enabled = false;
                button4.Enabled = false;
                button6.Enabled = false;
                numericUpDown1.Enabled = false;
                numericUpDown2.Enabled = false;
                numericUpDown3.Enabled = false;
                numericUpDown4.Enabled = false;

                LogInfo($"Port {serialPort1.PortName.ToString()} is closed");
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            servo_move((float)numericUpDown1.Value, (float)numericUpDown2.Value, (float)numericUpDown3.Value, (float)numericUpDown4.Value);
            LogInfo($"Moving to {numericUpDown1.Value} {numericUpDown1.Value} {numericUpDown1.Value} {numericUpDown1.Value}");
        }

        private void button6_Click(object sender, EventArgs e)
        {
            try
            {
                if (!serialPort1.IsOpen)
                {
                    MessageBox.Show("Port is closed. Check connection to MC", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                byte[] buffer = new byte[1];
                buffer[0] = (byte)0;
                serialPort1.Write(buffer, 0, buffer.Length);
                LogInfo($"Pinging... {Environment.NewLine}");
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show("Error occured while working with port: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error occured: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                serialPort1.Close();
            }

            updateTimer?.Stop();
            updateTimer?.Dispose();
        }

        private void servo_ping(byte[] buffer)
        {
            this.Invoke((MethodInvoker)delegate
            {
                LogInfo($"Done!");
                for (int servo = 1; servo < 8; servo++)
                {
                    if (buffer[servo] == (byte)1)
                    {
                        LogInfo($"Servo {servo} OK");
                    }
                    else
                    {
                        LogInfo($"Servo {servo} not connected");
                    }
                }
            });
        }

        private void initial_move(byte[] buffer)
        {
            ushort rotate = BitConverter.ToUInt16(buffer, 1);
            ushort joint1 = BitConverter.ToUInt16(buffer, 3);
            ushort joint2 = BitConverter.ToUInt16(buffer, 5);
            ushort joint3 = BitConverter.ToUInt16(buffer, 7);

            this.Invoke((MethodInvoker)delegate
            {
                numericUpDown1.Value = (decimal)((Math.Round((rotate / 1023.0) * 3000)) / 10);
                numericUpDown2.Value = (decimal)((Math.Round((joint1 / 1023.0) * 3000)) / 10);
                numericUpDown3.Value = (decimal)((Math.Round((joint2 / 1023.0) * 3000)) / 10);
                numericUpDown4.Value = (decimal)((Math.Round((joint3 / 1023.0) * 3000)) / 10);
            });

            pictureBox1.Invalidate();
            pictureBox2.Invalidate();
        }

        private void servo_move(float rotate, float joint1, float joint2, float joint3)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show("Port is closed. Check connection to MC", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            byte[] buffer = new byte[9];
            buffer[0] = (byte)1;
            byte[] rotate_arr = BitConverter.GetBytes((Int16)Math.Round((rotate * 1023) / 300));
            byte[] joint1_arr = BitConverter.GetBytes((Int16)Math.Round((joint1 * 1023) / 300));
            byte[] joint2_arr = BitConverter.GetBytes((Int16)Math.Round((joint2 * 1023) / 300));
            byte[] joint3_arr = BitConverter.GetBytes((Int16)Math.Round((joint3 * 1023) / 300));

            buffer[1] = rotate_arr[0];
            buffer[2] = rotate_arr[1];
            buffer[3] = joint1_arr[0];
            buffer[4] = joint1_arr[1];
            buffer[5] = joint2_arr[0];
            buffer[6] = joint2_arr[1];
            buffer[7] = joint3_arr[0];
            buffer[8] = joint3_arr[1];

            try
            {
                serialPort1.Write(buffer, 0, buffer.Length);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show("Error occured while writing in port: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error occured: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            pictureBox1.Invalidate();
            pictureBox2.Invalidate();
        }

        private void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            byte[] buffer = new byte[serialPort1.BytesToRead];
            serialPort1.Read(buffer, 0, buffer.Length);
            switch (buffer[0])
            {
                default:
                    LogError($"Buffer error {buffer[0]}");
                    break;
                case ((byte)0):
                    servo_ping(buffer);
                    break;
                case ((byte)2):
                    initial_move(buffer);
                    break;
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            textBox1.SelectionStart = textBox1.Text.Length;
            textBox1.ScrollToCaret();
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.White);

            float baseX = pictureBox1.Width / 2;
            float baseY = pictureBox1.Height / 2;
            float radius1 = 150;

            float rotation_angle = (float)numericUpDown1.Value;

            float x1 = baseX + radius1 * (float)0.4 * (float)Math.Cos((rotation_angle - 60) * Math.PI / 180);
            float y1 = baseY + radius1 * (float)0.4 * (float)Math.Sin((rotation_angle - 60) * Math.PI / 180);
            float x2 = baseX + radius1 * (float)1.1 * (float)Math.Cos((rotation_angle + 120) * Math.PI / 180);
            float y2 = baseY + radius1 * (float)1.1 * (float)Math.Sin((rotation_angle + 120) * Math.PI / 180);

            Pen RotationPen = new Pen(Brushes.Red, 5);
            g.DrawLine(RotationPen, x1, y1, x2, y2);

            for (float i = 120; i < 360; i += 30)
            {
                g.DrawLine(Pens.Black, baseX, baseY, baseX + radius1 * (float)1.05 * (float)Math.Cos(i * Math.PI / 180), baseY + radius1 * (float)1.05 * (float)Math.Sin(i * Math.PI / 180));
            }
            for (float i = 0; i < 90; i += 30)
            {
                g.DrawLine(Pens.Black, baseX, baseY, baseX + radius1 * (float)1.05 * (float)Math.Cos(i * Math.PI / 180), baseY + radius1 * (float)1.05 * (float)Math.Sin(i * Math.PI / 180));
            }

            float start_angle = 120;
            float sweep_angle = 300;

            g.DrawArc(Pens.Black, baseX - radius1, baseY - radius1, radius1 * 2, radius1 * 2, start_angle, sweep_angle);
            g.DrawArc(Pens.Black, baseX - radius1 / 2, baseY - radius1 / 2, radius1, radius1, start_angle, sweep_angle);
        }

        private void pictureBox2_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.White);

            float baseX = pictureBox2.Width / 2;
            float baseY = pictureBox2.Height / 2;
            float radius2 = 85;
            float radius3 = 55;
            float radius4 = 35;

            g.DrawLine(Pens.Black, baseX - 85, baseY, baseX + 115, baseY);
            g.DrawLine(Pens.Black, baseX, baseY, baseX, baseY - 130);

            float joint1_angle = (float)numericUpDown2.Value;
            float joint2_angle = (float)numericUpDown3.Value;
            float joint3_angle = (float)numericUpDown4.Value;

            float x1 = baseX + radius2 * (float)Math.Cos((joint1_angle + 120) * Math.PI / 180);
            float y1 = baseY + radius2 * (float)Math.Sin((joint1_angle + 120) * Math.PI / 180);
            float x2 = x1 + radius3 * (float)Math.Cos((joint1_angle + joint2_angle - 30) * Math.PI / 180);
            float y2 = y1 + radius3 * (float)Math.Sin((joint1_angle + joint2_angle - 30) * Math.PI / 180);
            float x3 = x2 + radius4 * (float)Math.Cos((joint1_angle + joint2_angle + joint3_angle - 180) * Math.PI / 180);
            float y3 = y2 + radius4 * (float)Math.Sin((joint1_angle + joint2_angle + joint3_angle - 180) * Math.PI / 180);

            Pen Joint1Pen = new Pen(Brushes.Blue, 3);
            Pen Joint2Pen = new Pen(Brushes.Orange, 3);
            Pen Joint3Pen = new Pen(Brushes.Green, 3);

            g.DrawLine(Joint1Pen, baseX, baseY, x1, y1);
            g.DrawLine(Joint2Pen, x1, y1, x2, y2);
            g.DrawLine(Joint3Pen, x2, y2, x3, y3);

            g.DrawLine(Pens.Black, baseX, baseY, baseX + radius2 * (float)0.55 * (float)Math.Cos(120 * Math.PI / 180), baseY + radius2 * (float)0.55 * (float)Math.Sin(120 * Math.PI / 180));
            g.DrawLine(Pens.Black, baseX, baseY, baseX + radius2 * (float)0.55 * (float)Math.Cos(60 * Math.PI / 180), baseY + radius2 * (float)0.55 * (float)Math.Sin(60 * Math.PI / 180));

            for (float i = 45; i < 360; i += 90)
            {
                g.DrawLine(Pens.Black, baseX, baseY, baseX + radius2 * (float)0.55 * (float)Math.Cos(i * Math.PI / 180), baseY + radius2 * (float)0.55 * (float)Math.Sin(i * Math.PI / 180));
            }

            g.DrawLine(Pens.Black, x1, y1, x1 + radius3 * (float)0.55 * (float)Math.Cos((joint1_angle - 30) * Math.PI / 180), y1 + radius3 * (float)0.55 * (float)Math.Sin((joint1_angle - 30) * Math.PI / 180));
            g.DrawLine(Pens.Black, x1, y1, x1 + radius3 * (float)0.55 * (float)Math.Cos((joint1_angle - 90) * Math.PI / 180), y1 + radius3 * (float)0.55 * (float)Math.Sin((joint1_angle - 90) * Math.PI / 180));

            for (float i = 45; i < 360; i += 90)
            {
                g.DrawLine(Pens.Black, x1, y1, x1 + radius3 * (float)0.55 * (float)Math.Cos((i + joint1_angle - 150) * Math.PI / 180), y1 + radius3 * (float)0.55 * (float)Math.Sin((i + joint1_angle - 150) * Math.PI / 180));
            }

            g.DrawLine(Pens.Black, x2, y2, x2 + radius4 * (float)0.55 * (float)Math.Cos((joint1_angle + joint2_angle + 180) * Math.PI / 180), y2 + radius4 * (float)0.55 * (float)Math.Sin((joint1_angle + joint2_angle + 180) * Math.PI / 180));
            g.DrawLine(Pens.Black, x2, y2, x2 + radius4 * (float)0.55 * (float)Math.Cos((joint1_angle + joint2_angle + 120) * Math.PI / 180), y2 + radius4 * (float)0.55 * (float)Math.Sin((joint1_angle + joint2_angle + 120) * Math.PI / 180));

            for (float i = 45; i < 360; i += 90)
            {
                g.DrawLine(Pens.Black, x2, y2, x2 + radius4 * (float)0.55 * (float)Math.Cos((i + joint1_angle + joint2_angle + 60) * Math.PI / 180), y2 + radius4 * (float)0.55 * (float)Math.Sin((i + joint1_angle + joint2_angle + 60) * Math.PI / 180));
            }

            float start_angle = 120;
            float sweep_angle = 300;

            g.DrawArc(Pens.Black, baseX - radius2 * (float)0.5, baseY - radius2 * (float)0.5, radius2 * 2 * (float)0.5, radius2 * 2 * (float)0.5, start_angle, sweep_angle);
            g.DrawArc(Pens.Black, x1 - radius3 * (float)0.5, y1 - radius3 * (float)0.5, radius3 * 2 * (float)0.5, radius3 * 2 * (float)0.5, start_angle + joint1_angle - 150, sweep_angle);
            g.DrawArc(Pens.Black, x2 - radius4 * (float)0.5, y2 - radius4 * (float)0.5, radius4 * 2 * (float)0.5, radius4 * 2 * (float)0.5, start_angle + joint1_angle + joint2_angle + 60, sweep_angle);

            g.DrawString("front", new Font("Microsoft Sans Serif", 10), Brushes.Black, baseX + 50, baseY + 5);
            g.DrawString("back", new Font("Microsoft Sans Serif", 10), Brushes.Black, baseX - 80, baseY + 5);
        }
    }
}
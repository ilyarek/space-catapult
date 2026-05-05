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
        private const double MU = 398600.4418;     // км³/с² - гравитационный параметр Земли
        private const double RE = 6371.0;           // км - радиус Земли
        private const double DEPLOY_DELTA_V = 2.0;  // м/с - скорость отделения кубсата (настраиваемая)

        // ВЕКТОР 3D ДЛЯ РАСЧЁТОВ
private struct Vector3
{
    public double X, Y, Z;
    
    public Vector3(double x, double y, double z) 
    { 
        X = x; Y = y; Z = z; 
    }
    
    // Операторы
    public static Vector3 operator +(Vector3 a, Vector3 b) => 
        new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    
    public static Vector3 operator -(Vector3 a, Vector3 b) => 
        new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    
    public static Vector3 operator *(Vector3 v, double s) => 
        new Vector3(v.X * s, v.Y * s, v.Z * s);
    
    public static Vector3 operator *(double s, Vector3 v) => 
        new Vector3(v.X * s, v.Y * s, v.Z * s);
    
    public static Vector3 operator /(Vector3 v, double s) => 
        new Vector3(v.X / s, v.Y / s, v.Z / s);
    
    public static Vector3 operator -(Vector3 v) => 
        new Vector3(-v.X, -v.Y, -v.Z);

    // Методы
    public double Norm() => Math.Sqrt(X*X + Y*Y + Z*Z);
    
    public double NormSq() => X*X + Y*Y + Z*Z;
    
    public Vector3 Normalize() 
    { 
        var n = Norm(); 
        return n > 1e-10 ? this / n : new Vector3(0, 0, 1); 
    }
    
    public static double Dot(Vector3 a, Vector3 b) => 
        a.X*b.X + a.Y*b.Y + a.Z*b.Z;
    
    public static Vector3 Cross(Vector3 a, Vector3 b) =>
        new Vector3(
            a.Y*b.Z - a.Z*b.Y, 
            a.Z*b.X - a.X*b.Z, 
            a.X*b.Y - a.Y*b.X
        );
    
    public override string ToString() => 
        $"({X:F3}, {Y:F3}, {Z:F3})";
}

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
        private List<PointF> deployedOrbitTrack = new List<PointF>();
        private PointF issPosition;
        private string cachePath;
        private DateTime lastCelestrakAttempt = DateTime.MinValue;
        private const int CELESTRAK_COOLDOWN_MINUTES = 120;

        public Form1()
        {
            InitializeComponent();

            string cacheFolder = Path.Combine(Application.StartupPath, "Cache");
            if (!Directory.Exists(cacheFolder))
            {
                Directory.CreateDirectory(cacheFolder);
            }
            cachePath = Path.Combine(cacheFolder, "iss_tle_cache.txt");

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

            this.Resize += (s, e) => {
                pictureBox1.Invalidate(); // Перерисовываем при изменении размера
                pictureBox2.Invalidate();
            };
        }

        private void LogError(string message)
        {
            if (textBox1 == null) return;
            string logMessage = $"[{DateTime.Now:HH:mm:ss}] ERROR: {message}{Environment.NewLine}";
            if (textBox1.InvokeRequired)
            {
                textBox1.Invoke((MethodInvoker)delegate { textBox1.AppendText(logMessage); });
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
                textBox1.Invoke((MethodInvoker)delegate { textBox1.AppendText(logMessage); });
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
            LogInfo("Program is running");
            LoadISSTle();
            updateTimer.Start();
        }

        // КЕШИРОВАНИЕ TLE
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
            if (TryLoadFromCache(out TLEData cachedTLE))
            {
                issTLE = cachedTLE;
                return;
            }
            TimeSpan timeSinceLastAttempt = DateTime.Now - lastCelestrakAttempt;
            if (timeSinceLastAttempt.TotalMinutes < CELESTRAK_COOLDOWN_MINUTES)
            {
                LogInfo($"Skipping Celestrak request (cooldown {CELESTRAK_COOLDOWN_MINUTES} minutes, lasted {timeSinceLastAttempt.TotalMinutes:F1})");
                UseFallbackTLE();
                return;
            }
            if (TryLoadFromCelestrak(out TLEData webTLE))
            {
                issTLE = webTLE;
                SaveToCache(issTLE);
                return;
            }
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

        // ОРБИТАЛЬНАЯ МЕХАНИКА 

        // Вычисляет единичный вектор направления "конца" манипулятора в системе LVLH МКС
        // Платформа направлена ОТ Земли → используем -Z_LVLH (т.к. +Z_LVLH = к Земле/nadir)
        private Vector3 CalculateLaunchDirectionLVLH()
        {
            
            double rotation = (double)numericUpDown1.Value;
            double j1 = (double)numericUpDown2.Value;
            double j2 = (double)numericUpDown3.Value;
            double j3 = (double)numericUpDown4.Value;
            
            // 1. Суммарный угол наклона (тангаж) от вертикали
            // 150 = 0° (строго вверх, зенит)
            // < 150 → отрицательный угол → наклон НАЗАД (против вектора скорости)
            // > 150 → положительный угол → наклон ВПЕРЁД (по вектору скорости)
            double pitchDeg = (j1 - 150.0) + (j2 - 150.0) + (j3 - 150.0);
            double pitchRad = pitchDeg * Math.PI / 180.0;

            // 2. Азимут в горизонтальной плоскости
            // 150 = 0° (по курсу МКС)
            // 240 = +90° (вправо от курса)
            // 60  = -90° (влево от курса)
            double azimuthRad = (rotation - 150.0) * Math.PI / 180.0;

            // 3. Построение единичного вектора в системе LVLH
            // X_LVLH : направление движения (вперёд)
            // Y_LVLH : нормаль орбиты (вправо)
            // Z_LVLH : надир (к центру Земли), поэтому ВВЕРХ = -Z_LVLH

            // Горизонтальная проекция зависит от тангажа
            double hMag = Math.Sin(pitchRad);      // 0 при 0°, -1 при -90°, +1 при +90°

            // Базовый вектор в вертикальной плоскости XZ
            double vx_base = hMag;                 // +X = вперёд, -X = назад
            double vy_base = 0.0;
            double vz = -Math.Cos(pitchRad);       // -Z = вверх (при pitch=0 → vz=-1)

            // Применяем азимутальный поворот вокруг оси Z_LVLH
            double cosA = Math.Cos(azimuthRad);
            double sinA = Math.Sin(azimuthRad);

            double finalX = vx_base * cosA - vy_base * sinA;
            double finalY = vx_base * sinA + vy_base * cosA;
            double finalZ = vz;

            return new Vector3(finalX, finalY, finalZ).Normalize();
     
        }

        // Преобразует вектор из LVLH в ECI
        private Vector3 LVLHtoECI(Vector3 v_lvlh, Vector3 r_iss_eci, Vector3 v_iss_eci)
        {
            Vector3 z_lvlh = -r_iss_eci.Normalize();
            Vector3 y_lvlh = Vector3.Cross(r_iss_eci, v_iss_eci).Normalize();
            Vector3 x_lvlh = Vector3.Cross(y_lvlh, z_lvlh).Normalize();
            return x_lvlh * v_lvlh.X + y_lvlh * v_lvlh.Y + z_lvlh * v_lvlh.Z;
        }

        // Вычисляет орбитальные элементы из вектора состояния [r, v] в ECI
        // Возвращает: [a, e, i, Ω, ω, ν]
        private double[] StateVectorToElements(Vector3 r, Vector3 v)
        {
            double rmag = r.Norm(), vmag = v.Norm();
            Vector3 h = Vector3.Cross(r, v);
            double hmag = h.Norm();
            Vector3 K = new Vector3(0, 0, 1);
            Vector3 N = Vector3.Cross(K, h);
            double Nmag = N.Norm();
            Vector3 e_vec = Vector3.Cross(v, h) / MU - r / rmag;
            double e = e_vec.Norm();
            double energy = vmag * vmag / 2 - MU / rmag;
            double a = energy < -1e-10 ? -MU / (2 * energy) : 1e10;
            double inc = Math.Acos(h.Z / hmag);
            double raan = Nmag > 1e-10 ? Math.Acos(N.X / Nmag) : 0;
            if (N.Y < 0) raan = 2 * Math.PI - raan;
            double argPer = (Nmag > 1e-10 && e > 1e-10) ? Math.Acos(Vector3.Dot(N, e_vec) / (Nmag * e)) : 0;
            if (e > 1e-10 && e_vec.Z < 0) argPer = 2 * Math.PI - argPer;
            double nu = e > 1e-10 ? Math.Acos(Vector3.Dot(e_vec, r) / (e * rmag)) : 0;
            double vr = Vector3.Dot(r, v) / rmag;
            if (vr < 0) nu = 2 * Math.PI - nu;
            return new double[] { a, e, inc, raan, argPer, nu };
        }

        // Расширенная версия: возвращает полный вектор состояния МКС в ECI
        private (Vector3 r, Vector3 v) CalculateISSStateVector(DateTime time)
        {
            TimeSpan ts = time - issTLE.Epoch;
            double minutes = ts.TotalMinutes;

            double n_rad_per_min = issTLE.MeanMotion * 2.0 * Math.PI / 1440.0;
            double a = Math.Pow(MU / Math.Pow(n_rad_per_min / 60.0, 2), 1.0 / 3.0);

            double M = issTLE.MeanAnomaly + n_rad_per_min * minutes;
            double E = SolveKepler(M, issTLE.Eccentricity);

            double v_true = 2 * Math.Atan(Math.Sqrt((1 + issTLE.Eccentricity) / (1 - issTLE.Eccentricity)) * Math.Tan(E / 2));
            double r_mag = a * (1 - issTLE.Eccentricity * Math.Cos(E));

            double argLat = v_true + issTLE.ArgPerigee;
            double xOrb = r_mag * Math.Cos(argLat);
            double yOrb = r_mag * Math.Sin(argLat);

            double inc = issTLE.Inclination, raan = issTLE.RAAN;
            double x_eci = xOrb * Math.Cos(raan) - yOrb * Math.Cos(inc) * Math.Sin(raan);
            double y_eci = xOrb * Math.Sin(raan) + yOrb * Math.Cos(inc) * Math.Cos(raan);
            double z_eci = yOrb * Math.Sin(inc);

            Vector3 r_eci = new Vector3(x_eci, y_eci, z_eci);

            // Скорость (упрощённый расчёт через численное дифференцирование)
            double dt_small = 0.1; // секунды
            DateTime t1 = time.AddSeconds(-dt_small / 2);
            DateTime t2 = time.AddSeconds(dt_small / 2);
            var pos1 = CalculatePositionRaw(t1);
            var pos2 = CalculatePositionRaw(t2);
            Vector3 r1 = new Vector3(pos1[0], pos1[1], pos1[2]);
            Vector3 r2 = new Vector3(pos2[0], pos2[1], pos2[2]);
            Vector3 v_eci = (r2 - r1) / dt_small;

            return (r_eci, v_eci);
        }

        // Raw-версия CalculatePosition, возвращающая ECI координаты (км)
        private double[] CalculatePositionRaw(DateTime time)
        {
            TimeSpan ts = time - issTLE.Epoch;
            double minutes = ts.TotalMinutes;

            double n_rad_per_min = issTLE.MeanMotion * 2.0 * Math.PI / 1440.0;
            double a = Math.Pow(MU / Math.Pow(n_rad_per_min / 60.0, 2), 1.0 / 3.0);

            double M = issTLE.MeanAnomaly + n_rad_per_min * minutes;
            double E = SolveKepler(M, issTLE.Eccentricity);
            double v_true = 2 * Math.Atan(Math.Sqrt((1 + issTLE.Eccentricity) / (1 - issTLE.Eccentricity)) * Math.Tan(E / 2));
            double r = a * (1 - issTLE.Eccentricity * Math.Cos(E));

            double u = v_true + issTLE.ArgPerigee;
            double xOrb = r * Math.Cos(u);
            double yOrb = r * Math.Sin(u);

            double inc = issTLE.Inclination, raan = issTLE.RAAN;
            double x_eci = xOrb * Math.Cos(raan) - yOrb * Math.Cos(inc) * Math.Sin(raan);
            double y_eci = xOrb * Math.Sin(raan) + yOrb * Math.Cos(inc) * Math.Cos(raan);
            double z_eci = yOrb * Math.Sin(inc);

            return new double[] { x_eci, y_eci, z_eci };
        }

        // Основная функция расчёта орбиты запускаемого аппарата
        private List<PointF> CalculateDeployedOrbit()
        {
            var track = new List<PointF>();
            if (issTLE == null) return track;

            try
            {
                DateTime now = DateTime.UtcNow;
                var issState = CalculateISSStateVector(now);
                Vector3 r_iss = issState.r;
                Vector3 v_iss = issState.v;

                // Направление запуска в ECI
                Vector3 dirLVLH = CalculateLaunchDirectionLVLH();
                Vector3 dirECI = LVLHtoECI(dirLVLH, r_iss, v_iss);

                // Добавляем delta-V
                double dv_kms = DEPLOY_DELTA_V / 1000.0;
                Vector3 v_sat = v_iss + dirECI * dv_kms;

                // Новые орбитальные элементы
                double[] satElements = StateVectorToElements(r_iss, v_sat);
                double a = satElements[0], e = satElements[1], inc = satElements[2];
                double raan = satElements[3], argPer = satElements[4], nu0 = satElements[5];

                if (a < RE + 100 || a > 50000 || double.IsNaN(a))
                {
                    LogInfo("Warning: Deployed orbit is unbound (escape trajectory).");
                    return track;
                }

                // Среднее движение (рад/с)
                double n = Math.Sqrt(MU / (a * a * a));

                // Начальная средняя аномалия для текущего момента
                double E0 = SolveKepler(nu0, e); // Истинная -> Эксцентрическая
                double M0 = E0 - e * Math.Sin(E0); // Средняя

                DateTime endTime = now.AddMinutes(90);
                double prevLon = 0;
                bool first = true;

                for (DateTime t = now; t <= endTime; t = t.AddSeconds(30))
                {
                    double dt = (t - now).TotalSeconds;
                    double M = M0 + n * dt;
                    double E = SolveKepler(M, e);
                    double nu = 2 * Math.Atan(Math.Sqrt((1 + e) / (1 - e)) * Math.Tan(E / 2));
                    double r = a * (1 - e * Math.Cos(E));

                    double xOrb = r * Math.Cos(nu);
                    double yOrb = r * Math.Sin(nu);

                    // Поворот в ECI
                    double x_eci = xOrb * (Math.Cos(raan) * Math.Cos(argPer) - Math.Sin(raan) * Math.Sin(argPer) * Math.Cos(inc))
                                 - yOrb * (Math.Cos(raan) * Math.Sin(argPer) + Math.Sin(raan) * Math.Cos(argPer) * Math.Cos(inc));
                    double y_eci = xOrb * (Math.Sin(raan) * Math.Cos(argPer) + Math.Cos(raan) * Math.Sin(argPer) * Math.Cos(inc))
                                 - yOrb * (Math.Sin(raan) * Math.Sin(argPer) - Math.Cos(raan) * Math.Cos(argPer) * Math.Cos(inc));
                    double z_eci = xOrb * (Math.Sin(argPer) * Math.Sin(inc)) + yOrb * (Math.Cos(argPer) * Math.Sin(inc));

                    double lat = Math.Asin(z_eci / Math.Sqrt(x_eci * x_eci + y_eci * y_eci + z_eci * z_eci)) * 180.0 / Math.PI;
                    double lon = Math.Atan2(y_eci, x_eci) * 180.0 / Math.PI;

                    // Учёт вращения Земли (ECEF)
                    double gmst = CalculateGMST(t);
                    lon = (lon * Math.PI / 180.0 - gmst) * 180.0 / Math.PI;
                    while (lon > 180) lon -= 360;
                    while (lon < -180) lon += 360;

                    // Проекция на карту
                    float x = (float)((lon + 180) * pictureBox3.Width / 360.0);
                    float y = (float)((90 - lat) * pictureBox3.Height / 180.0);

                    // Разрыв линии при переходе через ±180° долготы
                    if (!first && Math.Abs(lon - prevLon) > 180)
                        track.Add(PointF.Empty);

                    track.Add(new PointF(x, y));
                    prevLon = lon;
                    first = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Deployed orbit error: {ex.Message}");
            }
            return track;
        }

        private void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (issTLE != null)
            {
                try
                {
                    UpdateISSPosition();
                    CalculateGroundTrack();
                    if(serialPort1.IsOpen)
                    {
                        deployedOrbitTrack = CalculateDeployedOrbit();
                        UpdateOrbitDetailsPanel();
                    }
                    
                    pictureBox3.Invoke((MethodInvoker)delegate {
                        pictureBox3.Invalidate();
                    });
                }
                catch (Exception ex)
                {
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

        private void UpdateOrbitDetailsPanel()
        {
            if (issTLE == null || deployedOrbitTrack?.Count == 0)
            {
                string msg = $"Error: {Environment.NewLine} Escape trajectory";
                if (lblOrbitDiff.InvokeRequired)
                    lblOrbitDiff.Invoke((MethodInvoker)(() => lblOrbitDiff.Text = msg));
                else
                    lblOrbitDiff.Text = msg;
                return;
            }

            try
            {
                // 1. Расчет углов из текущих значений NumericUpDown
                double j1 = (double)numericUpDown2.Value;
                double j2 = (double)numericUpDown3.Value;
                double j3 = (double)numericUpDown4.Value;
                double pitchDeg = (j1 - 150.0) + (j2 - 150.0) + (j3 - 150.0);

                double rotation = (double)numericUpDown1.Value;
                double azimuthDeg = rotation - 150.0;

                // 2. Расчет параметров орбит
                var now = DateTime.UtcNow;
                var issState = CalculateISSStateVector(now);
                double[] issEl = StateVectorToElements(issState.r, issState.v);

                Vector3 dirLVLH = CalculateLaunchDirectionLVLH();
                Vector3 dirECI = LVLHtoECI(dirLVLH, issState.r, issState.v);
                Vector3 v_sat = issState.v + dirECI * (DEPLOY_DELTA_V / 1000.0);
                double[] satEl = StateVectorToElements(issState.r, v_sat);

                double da = satEl[0] - issEl[0];
                double di = (satEl[2] - issEl[2]) * 180.0 / Math.PI;
                double de = satEl[1] - issEl[1];

                double T_iss = 2.0 * Math.PI * Math.Sqrt(Math.Pow(issEl[0], 3) / MU);
                double T_sat = 2.0 * Math.PI * Math.Sqrt(Math.Pow(satEl[0], 3) / MU);
                double dT = T_sat - T_iss;

                // Упрощенная оценка дрейфа трассы за виток (км)
                double driftPerOrbit = Math.Abs(dT) * 465.1 * Math.Cos(issEl[2]) / 1000.0;

                // 3. Формирование текста
                string detailsMan = $"Launch parameters:\n";
                detailsMan += $"   Pitch:  {pitchDeg:+0.0;-0.0;0.0}°\n";
                detailsMan += $"   Azimuth: {azimuthDeg:+0.0;-0.0;0.0}° from course\n";
                string detailsOrbit = $"Orbit difference:\n";
                detailsOrbit += $"   Δa (semiaxis):  {da:+0.0;-0.0;0.0} km\n";
                detailsOrbit += $"   Δe (exccent.): {de:+0.00000;-0.00000;0.00000}\n";
                detailsOrbit += $"   Δi (inc.):  {di:+0.000;-0.000;0.000}°\n";
                detailsOrbit += $"   ΔT (period):   {dT:+0.00;-0.00;0.00} s/rot\n";
                detailsOrbit += $"   Drift: ~{driftPerOrbit:F1} km/rot\n";

                // 4. Безопасное обновление UI
                if (lblOrbitDiff.InvokeRequired)
                    lblOrbitDiff.Invoke((MethodInvoker)(() => lblOrbitDiff.Text = detailsOrbit));
                else
                    lblOrbitDiff.Text = detailsOrbit;
                if (lblManipulatorPar.InvokeRequired)
                    lblManipulatorPar.Invoke((MethodInvoker)(() => lblManipulatorPar.Text = detailsMan));
                else
                    lblManipulatorPar.Text = detailsMan;
            }
            catch (Exception ex)
            {
                string errMsg = $"Calculation error: {ex.Message}";
                if (lblOrbitDiff.InvokeRequired)
                    lblOrbitDiff.Invoke((MethodInvoker)(() => lblOrbitDiff.Text = errMsg));
                else
                    lblOrbitDiff.Text = errMsg;
            }
        }

        // Вычисляет позицию МКА через заданное количество витков
        private PointF? CalculateMarkerPosition(int orbitsAhead)
        {
            if (issTLE == null || deployedOrbitTrack?.Count == 0) return null;

            try
            {
                var now = DateTime.UtcNow;
                var issState = CalculateISSStateVector(now);

                Vector3 dirLVLH = CalculateLaunchDirectionLVLH();
                Vector3 dirECI = LVLHtoECI(dirLVLH, issState.r, issState.v);
                Vector3 v_sat = issState.v + dirECI * (DEPLOY_DELTA_V / 1000.0);
                double[] satEl = StateVectorToElements(issState.r, v_sat);

                double a = satEl[0], e = satEl[1], inc = satEl[2];
                double raan = satEl[3], argPer = satEl[4], nu0 = satEl[5];

                if (a < RE + 100 || double.IsNaN(a)) return null;

                double n = Math.Sqrt(MU / (a * a * a));
                double E0 = 2.0 * Math.Atan(Math.Sqrt((1.0 - e) / (1.0 + e)) * Math.Tan(nu0 / 2.0));
                double M0 = E0 - e * Math.Sin(E0);

                // Время вперёд: N витков
                double T = 2 * Math.PI / n;
                double dt = orbitsAhead * T;
                double M = M0 + n * dt;
                M = M % (2.0 * Math.PI); if (M < 0) M += 2.0 * Math.PI;

                double E = SolveKepler(M, e);
                double nu = 2 * Math.Atan(Math.Sqrt((1 + e) / (1 - e)) * Math.Tan(E / 2));
                double r = a * (1 - e * Math.Cos(E));

                double xOrb = r * Math.Cos(nu), yOrb = r * Math.Sin(nu);
                double x_eci = xOrb * (Math.Cos(raan) * Math.Cos(argPer) - Math.Sin(raan) * Math.Sin(argPer) * Math.Cos(inc))
                             - yOrb * (Math.Cos(raan) * Math.Sin(argPer) + Math.Sin(raan) * Math.Cos(argPer) * Math.Cos(inc));
                double y_eci = xOrb * (Math.Sin(raan) * Math.Cos(argPer) + Math.Cos(raan) * Math.Sin(argPer) * Math.Cos(inc))
                             - yOrb * (Math.Sin(raan) * Math.Sin(argPer) - Math.Cos(raan) * Math.Cos(argPer) * Math.Cos(inc));
                double z_eci = xOrb * Math.Sin(argPer) * Math.Sin(inc) + yOrb * Math.Cos(argPer) * Math.Sin(inc);

                double lat = Math.Asin(z_eci / Math.Sqrt(x_eci * x_eci + y_eci * y_eci + z_eci * z_eci)) * 180 / Math.PI;
                double lon = Math.Atan2(y_eci, x_eci) * 180 / Math.PI;

                double gmst = CalculateGMST(now.AddSeconds(dt));
                lon = (lon * Math.PI / 180 - gmst) * 180 / Math.PI;
                while (lon > 180) lon -= 360; while (lon < -180) lon += 360;

                float x = (float)((lon + 180) * pictureBox3.Width / 360.0);
                float y = (float)((90 - lat) * pictureBox3.Height / 180.0);

                return new PointF(x, y);
            }
            catch { return null; }
        }

        private double CalculateGMST(DateTime utcTime)
        {
            double jd = (utcTime - new DateTime(2000, 1, 1, 12, 0, 0)).TotalDays + 2451545.0;
            double tu = (jd - 2451545.0) / 36525.0;
            double gmst_sec = 24110.54841 + tu * (8640184.812866 + tu * (0.093104 - tu * 6.2e-6));
            gmst_sec += 1.00273790934 * 86400.0 * (utcTime.Hour * 3600 + utcTime.Minute * 60 + utcTime.Second) / 86400.0;
            gmst_sec = gmst_sec % 86400.0;
            if (gmst_sec < 0) gmst_sec += 86400.0;
            return gmst_sec * 2.0 * Math.PI / 86400.0;
        }

        private double[] CalculatePosition(DateTime time)
        {
            const double MU = 398600.4418;
            const double RE = 6371.0;
            TimeSpan ts = time - issTLE.Epoch;
            double minutes = ts.TotalMinutes;
            double n_rad_per_min = issTLE.MeanMotion * 2.0 * Math.PI / 1440.0;
            double a = Math.Pow(MU / Math.Pow(n_rad_per_min / 60.0, 2), 1.0 / 3.0);
            double M = issTLE.MeanAnomaly + n_rad_per_min * minutes;
            double E = SolveKepler(M, issTLE.Eccentricity);
            double v = 2 * Math.Atan(Math.Sqrt((1 + issTLE.Eccentricity) / (1 - issTLE.Eccentricity)) * Math.Tan(E / 2));
            double r = a * (1 - issTLE.Eccentricity * Math.Cos(E));
            double u = v + issTLE.ArgPerigee;
            double xOrb = r * Math.Cos(u);
            double yOrb = r * Math.Sin(u);
            double inc = issTLE.Inclination;
            double raan = issTLE.RAAN;
            double x_eci = xOrb * Math.Cos(raan) - yOrb * Math.Cos(inc) * Math.Sin(raan);
            double y_eci = xOrb * Math.Sin(raan) + yOrb * Math.Cos(inc) * Math.Cos(raan);
            double z_eci = yOrb * Math.Sin(inc);
            double gmst = CalculateGMST(time);
            double x_ecef = x_eci * Math.Cos(gmst) + y_eci * Math.Sin(gmst);
            double y_ecef = -x_eci * Math.Sin(gmst) + y_eci * Math.Cos(gmst);
            double z_ecef = z_eci;
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

            // Отрисовка трассы МКС
            if (currentTrack.Count > 0)
            {
                using (Pen trackPen = new Pen(Color.FromArgb(200, 255, 165, 0), 2))
                {
                    try {
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
                    catch { }
                }
            }

            // Отрисовка орбиты запускаемого аппарата (НОВОЕ)
            if (deployedOrbitTrack.Count > 1)
            {
                using (Pen deployPen = new Pen(Color.FromArgb(200, 255, 50, 50), 2))
                {
                    deployPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    List<PointF> segment = new List<PointF>();
                    foreach (PointF point in deployedOrbitTrack)
                    {
                        if (point == PointF.Empty && segment.Count > 1)
                        {
                            g.DrawLines(deployPen, segment.ToArray());
                            segment.Clear();
                        }
                        else if (point != PointF.Empty)
                        {
                            segment.Add(point);
                        }
                    }
                    if (segment.Count > 1)
                    {
                        g.DrawLines(deployPen, segment.ToArray());
                    }
                }
                // Подпись
                using (Font font = new Font("Arial", 9, FontStyle.Italic))
                using (Brush textBrush = new SolidBrush(Color.Red))
                {
                    g.DrawString("Deployed sat. orbit", font, textBrush, 10, 30);
                }
            }

            // Отрисовка позиции МКС
            if (issPosition != PointF.Empty)
            {
                using (Brush issBrush = new SolidBrush(Color.FromArgb(200, 50, 205, 50)))
                using (Pen outlinePen = new Pen(Color.White, 2))
                {
                    int pointSize = 8;
                    g.FillEllipse(issBrush, issPosition.X - pointSize / 2, issPosition.Y - pointSize / 2, pointSize, pointSize);
                    g.DrawEllipse(outlinePen, issPosition.X - pointSize / 2, issPosition.Y - pointSize / 2, pointSize, pointSize);
                }
                using (Brush glowBrush = new SolidBrush(Color.FromArgb(80, 50, 205, 50)))
                {
                    int glowSize = 20;
                    g.FillEllipse(glowBrush, issPosition.X - glowSize / 2, issPosition.Y - glowSize / 2, glowSize, glowSize);
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

            if (chkShowMarkers?.Checked == true && deployedOrbitTrack?.Count > 0)
            {
                string[] labels = { "T+1", "T+2", "T+3" };
                Color[] colors = { Color.FromArgb(180, 255, 100, 100),
                       Color.FromArgb(180, 255, 150, 50),
                       Color.FromArgb(180, 255, 200, 50) };

                for (int i = 0; i < 3; i++)
                {
                    var marker = CalculateMarkerPosition(i + 1);
                    if (marker.HasValue)
                    {
                        // Кружок маркера
                        using (Brush markerBrush = new SolidBrush(colors[i]))
                        using (Pen markerPen = new Pen(Color.White, 1))
                        {
                            int size = 6;
                            g.FillEllipse(markerBrush, marker.Value.X - size / 2, marker.Value.Y - size / 2, size, size);
                            g.DrawEllipse(markerPen, marker.Value.X - size / 2, marker.Value.Y - size / 2, size, size);
                        }

                        // Подпись
                        using (Font font = new Font("Arial", 8, FontStyle.Bold))
                        using (Brush textBrush = new SolidBrush(Color.Black))
                        using (Brush bgBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                        {
                            var textSize = g.MeasureString(labels[i], font);
                            g.FillRectangle(bgBrush, marker.Value.X + 8, marker.Value.Y - 8, textSize.Width + 4, textSize.Height + 2);
                            g.DrawString(labels[i], font, textBrush, marker.Value.X + 10, marker.Value.Y - 10);
                        }
                    }
                }
            }
        }

        // СЕРИЙНЫЙ ПОРТ И УПРАВЛЕНИЕ

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                serialPort1.PortName = comboBox1.Text;
                serialPort1.BaudRate = Convert.ToInt32(comboBox2.Text);
                comboBox1.Enabled = false;
                comboBox2.Enabled = false;
                numericUpDown5.Enabled = true;
                button1.Enabled = false;
                button2.Enabled = true;
                button3.Enabled = true;
                button4.Enabled = true;
                button6.Enabled = true;
                numericUpDown1.Enabled = true;
                numericUpDown2.Enabled = true;
                numericUpDown3.Enabled = true;
                numericUpDown4.Enabled = true;
                serialPort1.Open();
                LogInfo($"Port {serialPort1.PortName.ToString()} is open");
                byte[] buffer = new byte[1];
                buffer[0] = (byte)2;
                serialPort1.Write(buffer, 0, buffer.Length);
                setVelocity((float)50);
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
                numericUpDown5.Enabled = false;
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
            // Отправляем команду на манипулятор
            servo_move((float)numericUpDown1.Value, (float)numericUpDown2.Value, (float)numericUpDown3.Value, (float)numericUpDown4.Value);
            LogInfo($"Move to: R={numericUpDown1.Value}° J1={numericUpDown2.Value}° J2={numericUpDown3.Value}° J3={numericUpDown4.Value}°");

            // Отладочный расчёт вектора и параметров для проверки
            var issState = CalculateISSStateVector(DateTime.UtcNow);
            Vector3 dir = CalculateLaunchDirectionLVLH();
            double pitch = (double)((float)numericUpDown2.Value - 150) + ((float)numericUpDown3.Value - 150) + ((float)numericUpDown4.Value - 150);
            double azimuth = (double)((float)numericUpDown1.Value - 150);

            if (pictureBox3.InvokeRequired)
                pictureBox3.Invoke((MethodInvoker)(() => pictureBox3.Invalidate()));
            else
                pictureBox3.Invalidate();
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
            if (serialPort1.IsOpen) serialPort1.Close();
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
                    if (buffer[servo] == (byte)1) LogInfo($"Servo {servo} OK");
                    else LogInfo($"Servo {servo} not connected");
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
            buffer[1] = rotate_arr[0]; buffer[2] = rotate_arr[1];
            buffer[3] = joint1_arr[0]; buffer[4] = joint1_arr[1];
            buffer[5] = joint2_arr[0]; buffer[6] = joint2_arr[1];
            buffer[7] = joint3_arr[0]; buffer[8] = joint3_arr[1];
            try { serialPort1.Write(buffer, 0, buffer.Length); }
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
                default: LogError($"Buffer error {buffer[0]}"); break;
                case ((byte)0): servo_ping(buffer); break;
                case ((byte)2): initial_move(buffer); break;
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

            // Получаем размеры pictureBox
            float width = pictureBox1.Width;
            float height = pictureBox1.Height;

            // Определяем минимальный размер для корректного отображения
            float minSize = Math.Min(width, height);

            // Центр pictureBox (всегда 50%)
            float baseX = width / 2;
            float baseY = height / 2;

            // Радиусы в процентах от минимального размера
            float radius1 = minSize * 0.35f;      // Основной радиус (было 150)

            float rotation_angle = (float)numericUpDown1.Value;

            // Координаты для линии ротации (в процентах)
            float x1 = baseX + radius1 * 0.4f * (float)Math.Cos((rotation_angle - 60) * Math.PI / 180);
            float y1 = baseY + radius1 * 0.4f * (float)Math.Sin((rotation_angle - 60) * Math.PI / 180);
            float x2 = baseX + radius1 * 1.1f * (float)Math.Cos((rotation_angle + 120) * Math.PI / 180);
            float y2 = baseY + radius1 * 1.1f * (float)Math.Sin((rotation_angle + 120) * Math.PI / 180);

            // Рисуем линию ротации (толщина 5 как было)
            Pen RotationPen = new Pen(Brushes.Red, 5);
            g.DrawLine(RotationPen, x1, y1, x2, y2);

            // Рисуем риски (от 120° до 360°) - как было
            for (float i = 120; i < 360; i += 30)
            {
                g.DrawLine(Pens.Black, baseX, baseY, baseX + radius1 * 1.05f * (float)Math.Cos(i * Math.PI / 180), baseY + radius1 * 1.05f * (float)Math.Sin(i * Math.PI / 180));
            }

            // Рисуем риски (от 0° до 90°) - как было
            for (float i = 0; i < 90; i += 30)
            {
                g.DrawLine(Pens.Black, baseX, baseY, baseX + radius1 * 1.05f * (float)Math.Cos(i * Math.PI / 180), baseY + radius1 * 1.05f * (float)Math.Sin(i * Math.PI / 180));
            }

            // Рисуем дуги
            float start_angle = 120;
            float sweep_angle = 300;

            // Внешняя дуга
            g.DrawArc(Pens.Black, baseX - radius1, baseY - radius1, radius1 * 2, radius1 * 2, start_angle, sweep_angle);

            // Внутренняя дуга
            g.DrawArc(Pens.Black, baseX - radius1 / 2, baseY - radius1 / 2, radius1, radius1, start_angle, sweep_angle);
        }

        private void pictureBox2_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.White);

            // Получаем размеры pictureBox
            float width = pictureBox2.Width;
            float height = pictureBox2.Height;
            float minSize = Math.Min(width, height);

            // Центр
            float baseX = width / 2;
            float baseY = height / 2;

            // Радиусы в процентах от минимального размера
            float radius2 = minSize * 0.25f;     // Основной радиус (было 85)
            float radius3 = radius2 * 0.65f;     // Средний радиус (было 55)
            float radius4 = radius3 * 0.65f;     // Маленький радиус (было 35)

            // Оси координат
            g.DrawLine(Pens.Black, baseX - radius2, baseY, baseX + radius2 * 1.35f, baseY);
            g.DrawLine(Pens.Black, baseX, baseY, baseX, baseY - radius2 * 1.53f);

            // Углы из NumericUpDown
            float joint1_angle = (float)numericUpDown2.Value;
            float joint2_angle = (float)numericUpDown3.Value;
            float joint3_angle = (float)numericUpDown4.Value;

            // Вычисляем координаты сочленений
            float x1 = baseX + radius2 * (float)Math.Cos((joint1_angle + 120) * Math.PI / 180);
            float y1 = baseY + radius2 * (float)Math.Sin((joint1_angle + 120) * Math.PI / 180);

            float x2 = x1 + radius3 * (float)Math.Cos((joint1_angle + joint2_angle - 30) * Math.PI / 180);
            float y2 = y1 + radius3 * (float)Math.Sin((joint1_angle + joint2_angle - 30) * Math.PI / 180);

            float x3 = x2 + radius4 * (float)Math.Cos((joint1_angle + joint2_angle + joint3_angle - 180) * Math.PI / 180);
            float y3 = y2 + radius4 * (float)Math.Sin((joint1_angle + joint2_angle + joint3_angle - 180) * Math.PI / 180);

            // Рисуем основные линии сочленений (толщина 3 как было)
            Pen Joint1Pen = new Pen(Brushes.Blue, 3);
            Pen Joint2Pen = new Pen(Brushes.Orange, 3);
            Pen Joint3Pen = new Pen(Brushes.Green, 3);

            g.DrawLine(Joint1Pen, baseX, baseY, x1, y1);
            g.DrawLine(Joint2Pen, x1, y1, x2, y2);
            g.DrawLine(Joint3Pen, x2, y2, x3, y3);

            // Вспомогательные линии для первого сочленения
            g.DrawLine(Pens.Black, baseX, baseY, baseX + radius2 * 0.55f * (float)Math.Cos(120 * Math.PI / 180), baseY + radius2 * 0.55f * (float)Math.Sin(120 * Math.PI / 180));
            g.DrawLine(Pens.Black, baseX, baseY, baseX + radius2 * 0.55f * (float)Math.Cos(60 * Math.PI / 180), baseY + radius2 * 0.55f * (float)Math.Sin(60 * Math.PI / 180));

            for (float i = 45; i < 360; i += 90)
            {
                g.DrawLine(Pens.Black, baseX, baseY, baseX + radius2 * 0.55f * (float)Math.Cos(i * Math.PI / 180), baseY + radius2 * 0.55f * (float)Math.Sin(i * Math.PI / 180));
            }

            // Вспомогательные линии для второго сочленения
            g.DrawLine(Pens.Black, x1, y1, x1 + radius3 * 0.55f * (float)Math.Cos((joint1_angle - 30) * Math.PI / 180), y1 + radius3 * 0.55f * (float)Math.Sin((joint1_angle - 30) * Math.PI / 180));
            g.DrawLine(Pens.Black, x1, y1, x1 + radius3 * 0.55f * (float)Math.Cos((joint1_angle - 90) * Math.PI / 180), y1 + radius3 * 0.55f * (float)Math.Sin((joint1_angle - 90) * Math.PI / 180));

            for (float i = 45; i < 360; i += 90)
            {
                g.DrawLine(Pens.Black, x1, y1, x1 + radius3 * 0.55f * (float)Math.Cos((i + joint1_angle - 150) * Math.PI / 180), y1 + radius3 * 0.55f * (float)Math.Sin((i + joint1_angle - 150) * Math.PI / 180));
            }

            // Вспомогательные линии для третьего сочленения
            g.DrawLine(Pens.Black, x2, y2, x2 + radius4 * 0.55f * (float)Math.Cos((joint1_angle + joint2_angle + 180) * Math.PI / 180), y2 + radius4 * 0.55f * (float)Math.Sin((joint1_angle + joint2_angle + 180) * Math.PI / 180));
            g.DrawLine(Pens.Black, x2, y2, x2 + radius4 * 0.55f * (float)Math.Cos((joint1_angle + joint2_angle + 120) * Math.PI / 180), y2 + radius4 * 0.55f * (float)Math.Sin((joint1_angle + joint2_angle + 120) * Math.PI / 180));

            for (float i = 45; i < 360; i += 90)
            {
                g.DrawLine(Pens.Black, x2, y2, x2 + radius4 * 0.55f * (float)Math.Cos((i + joint1_angle + joint2_angle + 60) * Math.PI / 180), y2 + radius4 * 0.55f * (float)Math.Sin((i + joint1_angle + joint2_angle + 60) * Math.PI / 180));
            }

            // Рисуем дуги
            float start_angle = 120;
            float sweep_angle = 300;

            // Первая дуга
            g.DrawArc(Pens.Black, baseX - radius2 * 0.5f, baseY - radius2 * 0.5f, radius2, radius2, start_angle, sweep_angle);

            // Вторая дуга
            g.DrawArc(Pens.Black, x1 - radius3 * 0.5f, y1 - radius3 * 0.5f, radius3, radius3, start_angle + joint1_angle - 150, sweep_angle);

            // Третья дуга
            g.DrawArc(Pens.Black, x2 - radius4 * 0.5f, y2 - radius4 * 0.5f, radius4, radius4, start_angle + joint1_angle + joint2_angle + 60, sweep_angle);

            // Подписи
            g.DrawString("front", new Font("Microsoft Sans Serif", 10), Brushes.Black, baseX + 50, baseY + 5);
            g.DrawString("back", new Font("Microsoft Sans Serif", 10), Brushes.Black, baseX - 80, baseY + 5);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            try
            {
                LogInfo("Forcing TLE update...");
                if (TryLoadFromCelestrak(out TLEData webTLE))
                {
                    issTLE = webTLE;
                    SaveToCache(issTLE);
                    LogInfo("TLE successfully updated from Celestrak");
                    UpdateISSPosition();
                    CalculateGroundTrack();
                    pictureBox3.Invalidate();
                }
                else
                {
                    LogError("Failed to load TLE from Celestrak");
                }
            }
            catch (Exception ex)
            {
                LogError($"Critical error during TLE update: {ex.Message}");
                MessageBox.Show($"Error during TLE update: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void chkShowMarkers_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void button4_Click(object sender, EventArgs e)
        {
            try
            {
                if (!serialPort1.IsOpen)
                {
                    MessageBox.Show("Port is closed. Check connection to MC", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                setVelocity((float) numericUpDown5.Value);
            }
            catch (Exception ex)
            {
                LogError($"Critical error during velocity setting: {ex.Message}");
                MessageBox.Show($"Error during velocity setting: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
                
        }

        private void setVelocity(float velocity)
        {
            byte[] buffer = new byte[3];
            buffer[0] = (byte)3;
            byte[] velocity_arr = BitConverter.GetBytes((Int16)Math.Round((velocity / 0.111)));
            buffer[1] = velocity_arr[0]; buffer[2] = velocity_arr[1];
            try 
            {
                serialPort1.Write(buffer, 0, buffer.Length);
                LogInfo($"Manipulator velocity is set to {velocity} rpm");
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show("Error occured while writing in port: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error occured: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
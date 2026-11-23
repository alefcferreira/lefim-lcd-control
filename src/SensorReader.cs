using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

internal sealed class SensorReader : IDisposable
{
    object computer;
    Type computerType, hardwareType, sensorType;
    bool triedLibrary;

    public string CpuSource { get; private set; }
    public string GpuSource { get; private set; }

    ulong previousIdle, previousTotal;

    public string[] Read(bool[] enabled)
    {
        var values = new string[8];
        if (enabled[0] || enabled[1] || enabled[4] || enabled[5] || enabled[6])
        {
            if (!triedLibrary) OpenMonitor();
            if (computer != null)
            {
                try { ReadHardware((IEnumerable)computerType.GetProperty("Hardware").GetValue(computer, null), enabled, values); }
                catch (Exception) { Dispose(); }
            }
        }
        if ((enabled[1] && values[1] == null) || (enabled[4] && values[4] == null) || (enabled[5] && values[5] == null))
            ReadNvidia(enabled, values);
        if (enabled[2])
        {
            var memory = new MemoryInfo { Length = (uint)Marshal.SizeOf(typeof(MemoryInfo)) };
            if (GlobalMemoryStatusEx(ref memory)) values[2] = Format(memory.Load, "0", "%");
        }
        if (enabled[3]) values[3] = ReadCpuUse();
        else previousTotal = 0;
        if (enabled[7]) values[7] = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        return values;
    }

    void ReadHardware(IEnumerable hardwareList, bool[] enabled, string[] values)
    {
        foreach (var hardware in hardwareList)
        {
            string kind = hardwareType.GetProperty("HardwareType").GetValue(hardware, null).ToString();
            bool cpu = kind == "Cpu";
            bool gpu = kind.StartsWith("Gpu", StringComparison.Ordinal);
            if ((!cpu || !enabled[0]) && (!gpu || !(enabled[1] || enabled[4] || enabled[5])) && !enabled[6]) continue;
            hardwareType.GetMethod("Update").Invoke(hardware, null);
            foreach (var sensor in (IEnumerable)hardwareType.GetProperty("Sensors").GetValue(hardware, null))
            {
                string type = sensorType.GetProperty("SensorType").GetValue(sensor, null).ToString();
                string name = (string)sensorType.GetProperty("Name").GetValue(sensor, null);
                var reading = sensorType.GetProperty("Value").GetValue(sensor, null) as float?;
                if (!reading.HasValue || reading.Value < 0 || float.IsNaN(reading.Value) || float.IsInfinity(reading.Value)) continue;
                float value = reading.Value;
                if (cpu && enabled[0] && values[0] == null && type == "Temperature" && value > 0 &&
                    (name.IndexOf("Package", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.IndexOf("Tctl", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.IndexOf("Tdie", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    values[0] = Format(value, "0", " °C");
                    CpuSource = name;
                }
                if (gpu && name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase))
                {
                    if (enabled[1] && values[1] == null && type == "Temperature" && value > 0)
                    {
                        values[1] = Format(value, "0", " °C");
                        GpuSource = name;
                    }
                    if (enabled[4] && values[4] == null && type == "Load") values[4] = Format(value, "0", "%");
                }
                if (gpu && enabled[5] && values[5] == null && name.Equals("GPU Memory Used", StringComparison.OrdinalIgnoreCase))
                {
                    if (type == "SmallData") values[5] = Format(value / 1024, "0.0", " GB");
                    if (type == "Data") values[5] = Format(value, "0.0", " GB");
                }
                if (enabled[6] && type == "Fan" && value > 0 && values[6] == null)
                    values[6] = Format(value, "0", " RPM");
            }
            if (enabled[6] && values[6] == null)
                ReadHardware((IEnumerable)hardwareType.GetProperty("SubHardware").GetValue(hardware, null), enabled, values);
        }
    }

    string ReadCpuUse()
    {
        ulong idle, kernel, user;
        if (!GetSystemTimes(out idle, out kernel, out user)) return null;
        ulong total = kernel + user;
        string result = null;
        if (previousTotal != 0 && total > previousTotal && idle >= previousIdle)
        {
            double idleShare = (double)(idle - previousIdle) / (total - previousTotal);
            result = Format(Math.Max(0, Math.Min(100, (1 - idleShare) * 100)), "0", "%");
        }
        previousIdle = idle;
        previousTotal = total;
        return result;
    }

    void OpenMonitor()
    {
        triedLibrary = true;
        string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LibreHardwareMonitorLib.dll");
        string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads", "LibreHardwareMonitor", "LibreHardwareMonitorLib.dll");
        string file = new[] { local, downloads }.FirstOrDefault(File.Exists);
        if (file == null) return;
        try
        {
            var assembly = Assembly.LoadFrom(file);
            computerType = assembly.GetType("LibreHardwareMonitor.Hardware.Computer", true);
            hardwareType = assembly.GetType("LibreHardwareMonitor.Hardware.IHardware", true);
            sensorType = assembly.GetType("LibreHardwareMonitor.Hardware.ISensor", true);
            computer = Activator.CreateInstance(computerType);
            computerType.GetProperty("IsCpuEnabled").SetValue(computer, true, null);
            computerType.GetProperty("IsGpuEnabled").SetValue(computer, true, null);
            computerType.GetProperty("IsMotherboardEnabled").SetValue(computer, true, null);
            computerType.GetMethod("Open").Invoke(computer, null);
        }
        catch (Exception) { Dispose(); }
    }

    void ReadNvidia(bool[] enabled, string[] values)
    {
        try
        {
            string exe = Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe");
            if (!File.Exists(exe)) return;
            using (var process = Process.Start(new ProcessStartInfo(exe,
                "--query-gpu=temperature.gpu,utilization.gpu,memory.used --format=csv,noheader,nounits") {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true
            }))
            {
                if (!process.WaitForExit(900)) { process.Kill(); return; }
                string line = process.StandardOutput.ReadLine();
                if (process.ExitCode != 0 || line == null) return;
                var fields = line.Split(',');
                if (fields.Length != 3) return;
                int[] indices = { 1, 4, 5 };
                for (int index = 0; index < fields.Length; index++)
                {
                    int slot = indices[index];
                    float value;
                    if (!enabled[slot] || values[slot] != null || !float.TryParse(fields[index],
                        NumberStyles.Float, CultureInfo.InvariantCulture, out value)) continue;
                    values[slot] = slot == 1 ? Format(value, "0", " °C") :
                        slot == 4 ? Format(value, "0", "%") : Format(value / 1024, "0.0", " GB");
                    if (slot == 1) GpuSource = "NVIDIA GPU core";
                }
            }
        }
        catch (Exception) { }
    }

    static string Format(double value, string format, string unit)
    {
        return value.ToString(format, CultureInfo.InvariantCulture) + unit;
    }

    [DllImport("kernel32.dll")]
    static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);

    public void Dispose()
    {
        if (computer == null) return;
        try { computerType.GetMethod("Close").Invoke(computer, null); }
        catch (Exception) { }
        finally { computer = null; }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MemoryInfo
    {
        public uint Length, Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile;
        public ulong TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    [DllImport("kernel32.dll")]
    static extern bool GlobalMemoryStatusEx(ref MemoryInfo memory);
}

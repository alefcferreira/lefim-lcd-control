using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

internal static class LcdPort
{
    // Fallback mapping observed on this PC using both the healthy and failed device nodes.
    const string KnownHub = @"USB\VID_05E3&PID_0608\6&3365fbaf&0&11";
    const string FailedLcd = @"USB\VID_0000&PID_0002\7&1951ffa0&0&1";
    const uint GetConnectionInformation = 0x220448;
    const uint CycleUsbPort = 0x220444;
    internal const int UnidentifiedPort = 20001, UnavailableHub = 20002, UnexpectedDevice = 20003;
    internal const int HubRejected = 20004, PlayerInUse = 20005;

    internal sealed class Target
    {
        public string Hub;
        public uint Port;
        public bool FailedEnumeration;
    }

    internal static Target Locate()
    {
        return LocateNode(Usb.Device, false) ?? LocateNode(FailedLcd, true);
    }

    static Target LocateNode(string instance, bool failed)
    {
        uint node, parent, status, problem, type;
        var id = new StringBuilder(512);
        var address = new byte[4];
        uint size = (uint)address.Length;
        if (CM_Locate_DevNodeW(out node, instance, 0) != 0 ||
            CM_Get_DevNode_Status(out status, out problem, node, 0) != 0 ||
            CM_Get_Parent(out parent, node, 0) != 0 ||
            CM_Get_Device_IDW(parent, id, id.Capacity, 0) != 0 ||
            CM_Get_DevNode_Registry_PropertyW(node, 0x1D, out type, address, ref size, 0) != 0 ||
            type != 4 || size != 4) return null;
        var target = new Target { Hub = id.ToString(), Port = BitConverter.ToUInt32(address, 0),
            FailedEnumeration = failed };
        if (target.Port < 1 || target.Port > 255) return null;
        if (failed && (problem != 43 || target.Port != 1 ||
            !string.Equals(target.Hub, KnownHub, StringComparison.OrdinalIgnoreCase))) return null;
        return target;
    }

    internal static int Cycle(bool inspectOnly = false)
    {
        try { Usb.EnsureAvailable(); }
        catch (System.IO.IOException) { return PlayerInUse; }
        var target = Locate();
        if (target == null) return UnidentifiedPort;
        var hubClass = new Guid("f18a0e88-c30c-11d0-8815-00a0c906bed8");
        var interfaces = new byte[4096];
        if (CM_Get_Device_Interface_ListW(ref hubClass, target.Hub, interfaces, interfaces.Length / 2, 0) != 0)
            return UnavailableHub;
        string[] paths = Encoding.Unicode.GetString(interfaces).Split(new[] {'\0'}, StringSplitOptions.RemoveEmptyEntries);
        if (paths.Length != 1) return UnavailableHub;

        using (var hub = CreateFileW(paths[0], 0x40000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
        {
            if (hub.IsInvalid) return Marshal.GetLastWin32Error();
            byte[] index = BitConverter.GetBytes(target.Port);
            byte[] connection = new byte[1024];
            Array.Copy(index, connection, index.Length);
            uint received;
            if (!DeviceIoControl(hub, GetConnectionInformation, connection, connection.Length,
                connection, connection.Length, out received, IntPtr.Zero))
                return Marshal.GetLastWin32Error();
            if (received < 22 || !MatchesDescriptor(target.FailedEnumeration, BitConverter.ToUInt16(connection, 12),
                BitConverter.ToUInt16(connection, 14))) return UnexpectedDevice;
            if (inspectOnly) return 0;

            // Re-resolve before issuing the only mutating operation, in case the port changed.
            var current = Locate();
            if (current == null || current.Port != target.Port || current.FailedEnumeration != target.FailedEnumeration ||
                !string.Equals(current.Hub, target.Hub, StringComparison.OrdinalIgnoreCase)) return UnexpectedDevice;
            byte[] cycle = new byte[8];
            Array.Copy(index, cycle, index.Length);
            if (!DeviceIoControl(hub, CycleUsbPort, cycle, cycle.Length, cycle, cycle.Length, out received, IntPtr.Zero))
                return Marshal.GetLastWin32Error();
            return received >= 8 && BitConverter.ToUInt32(cycle, 4) != 0 ? HubRejected : 0;
        }
    }

    internal static bool MatchesDescriptor(bool failed, ushort vendor, ushort product)
    {
        return (vendor == 0x2A55 && product == 0x9990) ||
            (failed && vendor == 0 && (product == 0 || product == 2));
    }

    internal static string Explain(int code)
    {
        switch (code)
        {
            case UnidentifiedPort: return Words.Get("A porta do LCD não pôde ser identificada com segurança.");
            case UnavailableHub: return Words.Get("O Windows não informou a interface do hub do LCD.");
            case UnexpectedDevice: return Words.Get("O dispositivo da porta mudou ou não corresponde ao LCD.");
            case HubRejected: return Words.Get("O hub não confirmou o ciclo da porta USB.");
            case PlayerInUse: return Words.Get("Feche o outro aplicativo do LCD para liberar o USB.");
            default: return "Windows " + code + ": " + new Win32Exception(code).Message;
        }
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern uint CM_Locate_DevNodeW(out uint node, string instance, uint flags);
    [DllImport("cfgmgr32.dll")]
    static extern uint CM_Get_DevNode_Status(out uint status, out uint problem, uint node, uint flags);
    [DllImport("cfgmgr32.dll")]
    static extern uint CM_Get_Parent(out uint parent, uint node, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern uint CM_Get_Device_IDW(uint node, StringBuilder buffer, int length, uint flags);
    [DllImport("cfgmgr32.dll", ExactSpelling = true)]
    static extern uint CM_Get_DevNode_Registry_PropertyW(uint node, uint property, out uint type, [Out] byte[] buffer,
        ref uint length, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern uint CM_Get_Device_Interface_ListW(ref Guid guid, string instance, [Out] byte[] buffer, int chars, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security,
        uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(SafeFileHandle file, uint code, byte[] input, int inputLength,
        [Out] byte[] output, int outputLength, out uint received, IntPtr overlapped);
}

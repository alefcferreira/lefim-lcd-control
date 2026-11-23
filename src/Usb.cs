using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

internal static class Usb
{
    internal const string Device = @"USB\VID_2A55&PID_9990\20211205";
    static readonly string[] Interfaces = {
        "12345678-1234-1344-1234-123456789abc",
        "dee824ef-729b-4a0e-9c14-b7117d33a817"
    };

    public static bool Detected()
    {
        uint node, status, problem;
        return CM_Locate_DevNode(out node, Device, 0) == 0 &&
            CM_Get_DevNode_Status(out status, out problem, node, 0) == 0 && (status & 8) != 0;
    }

    public static string Probe()
    {
        try
        {
            using (var usb = Open(false)) return Describe(usb.Handle, usb.Guid);
        }
        catch (IOException ex)
        {
            var port = LcdPort.Locate();
            string topology = port == null ? "" : Environment.NewLine + Words.Get("Hub: ") + port.Hub +
                Words.Get(" • Porta: ") + port.Port + (port.FailedEnumeration ? Words.Get(" • falha de descritor (43)") : "");
            return Device + Environment.NewLine + ex.Message + topology;
        }
    }

    sealed class Connection : IDisposable
    {
        public SafeFileHandle File;
        public IntPtr Handle;
        public string Guid;
        public void Dispose() { WinUsb_Free(Handle); File.Dispose(); }
    }

    static Connection Open(bool exclusive)
    {
        var errors = new StringBuilder();
        foreach (string id in Interfaces)
        {
            string path = @"\\?\USB#VID_2A55&PID_9990#20211205#{" + id + "}";
            var file = CreateFile(path, 0xC0000000, exclusive ? 0u : 3u, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);
            if (file.IsInvalid)
            {
                errors.AppendLine(id + ": " + LastError());
                file.Dispose();
                continue;
            }
            IntPtr handle;
            if (WinUsb_Initialize(file, out handle))
                return new Connection { File = file, Handle = handle, Guid = id };
            errors.AppendLine(id + ": " + LastError());
            file.Dispose();
        }
        throw new IOException(Words.Get("A interface WinUSB não pôde ser aberta.") + Environment.NewLine + errors.ToString());
    }

    static string LastError() { return new Win32Exception(Marshal.GetLastWin32Error()).Message; }

    internal static void EnsureAvailable()
    {
        var processes = Process.GetProcessesByName("WebBrowserV22Port2020");
        foreach (var process in processes) process.Dispose();
        if (processes.Length != 0) throw new IOException(Words.Get("Feche o outro aplicativo do LCD pela bandeja para liberar o USB."));
    }

    static byte[] Descriptor(IntPtr handle, byte type, byte index, ushort language)
    {
        byte[] bytes = new byte[512];
        uint length;
        if (!WinUsb_GetDescriptor(handle, type, index, language, bytes, (uint)bytes.Length, out length))
            return new byte[0];
        Array.Resize(ref bytes, (int)length);
        return bytes;
    }

    static ushort Word(byte[] bytes, int offset)
    {
        return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
    }

    static string UsbString(IntPtr handle, byte index, ushort lang)
    {
        if (index == 0) return Words.Get("indisponível");
        var data = Descriptor(handle, 3, index, lang);
        return data.Length < 2 ? Words.Get("indisponível") : Encoding.Unicode.GetString(data, 2, (data.Length - 2) & ~1);
    }

    static string Describe(IntPtr handle, string id)
    {
        var output = new StringBuilder();
        output.AppendLine(Device).AppendLine("Driver: WinUSB • GUID: " + id);
        var device = Descriptor(handle, 1, 0, 0);
        if (device.Length >= 18)
        {
            var langs = Descriptor(handle, 3, 0, 0);
            ushort lang = langs.Length < 4 ? (ushort)0x409 : Word(langs, 2);
            output.AppendFormat(Words.Get("VID:PID {0:X4}:{1:X4} • revisão {2:X4} • USB {3:X4}\r\n"),
                Word(device, 8), Word(device, 10), Word(device, 12), Word(device, 2));
            output.AppendLine(Words.Get("Fabricante: ") + UsbString(handle, device[14], lang));
            output.AppendLine(Words.Get("Produto: ") + UsbString(handle, device[15], lang));
            output.AppendLine(Words.Get("Série: ") + UsbString(handle, device[16], lang));
            output.AppendLine(Words.Get("Descritor de dispositivo: ") + BitConverter.ToString(device));
        }
        var config = Descriptor(handle, 2, 0, 0);
        if (config.Length > 0) output.AppendLine(Words.Get("Descritor de configuração: ") + BitConverter.ToString(config));
        for (int at = 0; at + 2 <= config.Length && config[at] >= 2 && at + config[at] <= config.Length; at += config[at])
        {
            int size = config[at];
            switch (config[at + 1])
            {
                case 4:
                    if (size >= 9) output.AppendFormat(Words.Get("Interface {0}, alternativa {1} • classe {2:X2}/{3:X2}/{4:X2}\r\n"),
                        config[at + 2], config[at + 3], config[at + 5], config[at + 6], config[at + 7]);
                    break;
                case 5:
                    if (size >= 7) output.AppendFormat(Words.Get("Endpoint 0x{0:X2} {1} • {2} • pacote de até {3} bytes\r\n"),
                        config[at + 2], (config[at + 2] & 0x80) != 0 ? "IN" : "OUT",
                        new[] { "Control", "Isochronous", "Bulk", "Interrupt" }[config[at + 3] & 3], Word(config, at + 4) & 0x7FF);
                    break;
            }
        }
        output.AppendLine(Words.Get("Protocolo identificado: SmartCool, JPEG em transferências bulk."));
        return output.ToString();
    }

    const byte Out = 0x01, In = 0x81;

    static void SetTimeout(IntPtr handle, byte pipe, uint timeout)
    {
        if (!WinUsb_SetPipePolicy(handle, pipe, 3, 4, ref timeout))
            throw new IOException(Words.Get("Tempo limite WinUSB: ") + LastError());
    }

    static void Write(IntPtr handle, byte[] data, string stage)
    {
        uint written;
        if (!WinUsb_WritePipe(handle, Out, data, (uint)data.Length, out written, IntPtr.Zero))
        {
            var error = new Win32Exception(Marshal.GetLastWin32Error());
            throw new IOException(string.Format(Words.Get("{0} — erro de escrita WinUSB {1} ao enviar {2} bytes: {3}"),
                stage, error.NativeErrorCode, data.Length, error.Message), error);
        }
        if (written != data.Length)
            throw new IOException(string.Format(Words.Get("{0} — enviados {1} de {2} bytes."), stage, written, data.Length));
    }

    static byte[] Read(IntPtr handle, int packetSize, string stage)
    {
        byte[] data = new byte[packetSize];
        uint received;
        if (!WinUsb_ReadPipe(handle, In, data, (uint)data.Length, out received, IntPtr.Zero))
        {
            var error = new Win32Exception(Marshal.GetLastWin32Error());
            throw new IOException(stage + Words.Get(" — erro de leitura WinUSB ") + error.NativeErrorCode + ": " + error.Message, error);
        }
        Array.Resize(ref data, (int)received);
        return data;
    }

    internal static byte[] Command(uint tag, int length, bool input, bool query)
    {
        var data = new byte[31];
        Array.Copy(Encoding.ASCII.GetBytes("USBC"), data, 4);
        Array.Copy(BitConverter.GetBytes(tag), 0, data, 4, 4);
        Array.Copy(BitConverter.GetBytes(length), 0, data, 8, 4);
        data[12] = input ? (byte)0x80 : (byte)0;
        data[14] = 16;
        data[15] = 0xCD;
        data[20] = query ? (byte)3 : (byte)6;
        if (!query)
        {
            data[21] = 0x30;
            var size = BitConverter.GetBytes((ushort)(Media.Size - 1));
            Array.Copy(size, 0, data, 26, 2);
            Array.Copy(size, 0, data, 28, 2);
        }
        return data;
    }

    internal static void ValidateStatus(byte[] data, uint tag, string stage)
    {
        // The original firmware client validates length, command tag and final status.
        if (data.Length != 13)
            throw new IOException(string.Format(Words.Get("{0} — status USB com {1} bytes, esperados 13. Dados: {2}"),
                stage, data.Length, BitConverter.ToString(data)));
        uint actualTag = BitConverter.ToUInt32(data, 4);
        if (actualTag != tag)
            throw new IOException(string.Format(Words.Get("{0} — tag USB {1} recebida, esperada {2}. Dados: {3}"),
                stage, actualTag, tag, BitConverter.ToString(data)));
        if (data[12] != 0)
            throw new IOException(string.Format(Words.Get("{0} — LCD retornou código 0x{1:X2}. Dados: {2}"),
                stage, data[12], BitConverter.ToString(data)));
    }

    static bool IsEcho(byte[] packet, byte[] command)
    {
        // Affected units can echo a prefix of the outgoing command before replying.
        if (packet.Length < 13 || packet.Length > command.Length) return false;
        for (int i = 0; i < packet.Length; i++)
            if (packet[i] != command[i]) return false;
        return true;
    }

    internal static byte[] ReadReply(Func<string, byte[]> readPacket, byte[] command, uint tag,
        bool resolution, Action<string> trace, out byte[] payload)
    {
        const int statusSize = 13;
        const int maxResolutionSize = 12;
        string stage = resolution ? Words.Get("Consulta de resolução") : Words.Get("Status do quadro JPEG");
        var buffer = new byte[maxResolutionSize + statusSize];
        int count = 0;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            byte[] packet = readPacket(stage);
            if (resolution) trace(Words.Get("Pacote IN: ") + BitConverter.ToString(packet));
            if (packet.Length == 0) continue;
            if (count == 0 && IsEcho(packet, command))
            {
                if (resolution) trace(Words.Get("Eco do comando ignorado (") + packet.Length + " bytes).");
                continue;
            }
            if (!resolution)
            {
                ValidateStatus(packet, tag, stage);
                payload = new byte[0];
                return packet;
            }

            if (count + packet.Length > buffer.Length)
                throw new IOException(stage + Words.Get(" — tamanho inesperado. Último pacote: ") + BitConverter.ToString(packet));
            Buffer.BlockCopy(packet, 0, buffer, count, packet.Length);
            count += packet.Length;

            int statusOffset = count - statusSize;
            bool hasSignature = statusOffset >= 0 && BitConverter.ToUInt32(buffer, statusOffset) == 0x53425355;
            if (hasSignature && statusOffset == 0)
            {
                ValidateStatus(packet, tag, stage);
                throw new IOException(stage + Words.Get(" — o painel devolveu o status sem informar a resolução."));
            }
            bool separateStatus = packet.Length == statusSize && statusOffset >= 5 &&
                BitConverter.ToUInt32(packet, 4) == tag;
            if (statusOffset >= 5 && (hasSignature || separateStatus))
            {
                payload = new byte[statusOffset];
                Buffer.BlockCopy(buffer, 0, payload, 0, payload.Length);
                var status = new byte[statusSize];
                Buffer.BlockCopy(buffer, statusOffset, status, 0, status.Length);
                ValidateStatus(status, tag, stage);
                return status;
            }
        }
        throw new IOException(stage + Words.Get(" — resposta completa não encontrada. Dados: ") + BitConverter.ToString(buffer, 0, count));
    }

    static int BulkPacketSize(IntPtr handle)
    {
        var config = Descriptor(handle, 2, 0, 0);
        int input = 0;
        bool output = false, primary = false;
        for (int at = 0; at + 2 <= config.Length && config[at] >= 2 && at + config[at] <= config.Length; at += config[at])
        {
            if (config[at + 1] == 4 && config[at] >= 9)
                primary = config[at + 2] == 0 && config[at + 3] == 0;
            if (!primary || config[at + 1] != 5 || config[at] < 7 || (config[at + 3] & 3) != 2) continue;
            if (config[at + 2] == In) input = Word(config, at + 4) & 0x7FF;
            output |= config[at + 2] == Out;
        }
        return output ? input : 0;
    }

    internal static byte[] EncodeFrame(Bitmap bitmap)
    {
        var jpeg = Array.Find(ImageCodecInfo.GetImageEncoders(), codec => codec.FormatID == ImageFormat.Jpeg.Guid);
        using (var stream = new MemoryStream())
        using (var settings = new EncoderParameters(1))
        {
            // Matches the GDI+ JPEG quality setting in WebBrowserV22Port2020.exe.
            settings.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 75L);
            lock (bitmap) bitmap.Save(stream, jpeg, settings);
            return stream.ToArray();
        }
    }

    static void TraceAfterWriteError(IntPtr handle, int packetSize, Action<string> trace)
    {
        try
        {
            SetTimeout(handle, In, 500);
            byte[] reply = Read(handle, packetSize, Words.Get("Status após falha de escrita"));
            trace(Words.Get("Pacote IN após falha de escrita: ") + BitConverter.ToString(reply));
        }
        catch (IOException ex) { trace(ex.Message); }
    }

    static bool RecoverPipes(IntPtr handle, Action<string> trace)
    {
        bool success = true;
        foreach (byte pipe in new[] { In, Out })
        {
            bool aborted = WinUsb_AbortPipe(handle, pipe);
            if (!aborted) trace(string.Format(Words.Get("Cancelar endpoint 0x{0:X2}: {1}"), pipe, LastError()));
            bool reset = WinUsb_ResetPipe(handle, pipe);
            string result = reset ? "OK" : LastError();
            trace(string.Format(Words.Get("Recuperar endpoint 0x{0:X2}: {1}"), pipe, result));
            success &= aborted && reset;
        }
        if (WinUsb_FlushPipe(handle, In)) return success;
        trace(Words.Get("Limpar buffer de recepção: ") + LastError());
        return false;
    }

    internal static bool CanRetryResolution(IOException failure)
    {
        var native = failure.InnerException as Win32Exception;
        return native != null && (native.NativeErrorCode == 121 || native.NativeErrorCode == 31);
    }

    public static void Send(Media media, CancellationToken stop, Action<int> frameSent, Action<string> trace = null, Func<Media> nextMedia = null, Func<SensorOverlay> getSensors = null)
    {
        WithExclusiveAccess(() => SendCore(media, stop, frameSent, trace ?? delegate { }, nextMedia, getSensors));
    }

    static void WithExclusiveAccess(Action action)
    {
        using (var exclusive = new Mutex(false, @"Local\LeFiM-LCD-2A55-9990"))
        {
            bool locked;
            try { locked = exclusive.WaitOne(0); }
            catch (AbandonedMutexException) { locked = true; }
            if (!locked) throw new IOException(Words.Get("Outra janela do LeFiM LCD Control está usando a tela."));
            try { action(); }
            finally { exclusive.ReleaseMutex(); }
        }
    }

    static void SendCore(Media media, CancellationToken stop, Action<int> frameSent, Action<string> trace, Func<Media> nextMedia, Func<SensorOverlay> getSensors)
    {
        EnsureAvailable();

        using (var usb = Open(true))
        {
            var handle = usb.Handle;
            trace(Words.Get("Interface aberta: ") + usb.Guid);
            var device = Descriptor(handle, 1, 0, 0);
            int packetSize = BulkPacketSize(handle);
            if (device.Length < 18 || Word(device, 8) != 0x2A55 || Word(device, 10) != 0x9990 ||
                Word(device, 12) != 0x0200 || packetSize < 25)
                throw new IOException(Words.Get("Os descritores não correspondem ao perfil SmartCool deste equipamento."));
            SetTimeout(handle, Out, 2000);
            SetTimeout(handle, In, 2000);
            if (stop.IsCancellationRequested) return;
            uint tag = 1;
            Func<string, byte[]> readPacket = stage => Read(handle, packetSize, stage);
            byte[] dimensions;
            byte[] queryStatus = QueryResolution(handle, readPacket, tag++, trace, out dimensions);
            trace(Words.Get("Resposta de resolução: ") + BitConverter.ToString(dimensions));
            trace(Words.Get("Status da resolução: ") + BitConverter.ToString(queryStatus));
            if (Word(dimensions, 0) != Media.Size || Word(dimensions, 2) != Media.Size)
                throw new IOException(string.Format(Words.Get("O LCD informou {0} × {1}. O editor está configurado em 480 × 480."),
                    Word(dimensions, 0), Word(dimensions, 2)));

            // The reference application waits one second after the resolution exchange.
            if (stop.WaitHandle.WaitOne(1000)) return;
            SetTimeout(handle, Out, 5000);
            byte[] still = media.Frames.Count == 1 ? EncodeFrame(media.Frames[0]) : null;
            bool firstFrameSent = false;
            SensorOverlay cachedOverlay = null;
            byte[] cachedStillOverlay = null;

            var playbackTime = Stopwatch.StartNew();
            long confirmed = 0;
            int switches = 0;
            int lastFrameBytes = 0;
            try
            {
                do
                {
                    for (int i = 0; i < media.Frames.Count; i++)
                    {
                        if (stop.IsCancellationRequested) return;
                        var duration = Stopwatch.StartNew();
                        Media replacement = nextMedia == null ? null : nextMedia();
                        if (replacement != null)
                        {
                            media = replacement;
                            i = 0;
                            still = media.Frames.Count == 1 ? EncodeFrame(media.Frames[0]) : null;
                            firstFrameSent = false;
                            cachedOverlay = null;
                            cachedStillOverlay = null;
                            switches++;
                            trace(string.Format(Words.Get("Troca {0} na conexão aberta após {1} quadros confirmados. Próxima tag: {2}."),
                                switches, confirmed, tag));
                            // Leave an interval between streams before sending the replacement's first frame.
                            if (stop.WaitHandle.WaitOne(120)) return;
                            duration.Restart();
                        }
                        var overlay = getSensors == null ? null : getSensors();
                        byte[] frame;
                        if (overlay == null) frame = still ?? EncodeFrame(media.Frames[i]);
                        else if (still == null) frame = overlay.Encode(media.Frames[i]);
                        else
                        {
                            if (!ReferenceEquals(cachedOverlay, overlay))
                            {
                                cachedStillOverlay = overlay.Encode(media.Frames[0]);
                                cachedOverlay = overlay;
                            }
                            frame = cachedStillOverlay;
                        }
                        if (stop.IsCancellationRequested) return;
                        byte[] frameCommand = Command(tag, frame.Length, false, false);
                        if (!firstFrameSent)
                            trace(Words.Get("Primeiro quadro: JPEG de ") + frame.Length + Words.Get(" bytes. Cabeçalho: ") + BitConverter.ToString(frameCommand));
                        lastFrameBytes = frame.Length;
                        Write(handle, frameCommand, Words.Get("Cabeçalho do quadro"));
                        try { Write(handle, frame, Words.Get("Envio do JPEG")); }
                        catch (IOException)
                        {
                            trace(Words.Get("Tag do quadro com falha: ") + tag);
                            TraceAfterWriteError(handle, packetSize, trace);
                            throw;
                        }
                        byte[] ignoredPayload;
                        var frameStatus = ReadReply(readPacket, frameCommand, tag++, false, trace, out ignoredPayload);
                        if (!firstFrameSent) trace(Words.Get("Primeiro JPEG: ") + frame.Length + Words.Get(" bytes. Status: ") + BitConverter.ToString(frameStatus));
                        firstFrameSent = true;
                        confirmed++;
                        frameSent(i);
                        int interval = still == null ? media.Delays[i] : 120;
                        stop.WaitHandle.WaitOne(Math.Max(0, interval - (int)duration.ElapsedMilliseconds));
                    }
                } while (!stop.IsCancellationRequested);
            }
            finally
            {
                trace(string.Format(Words.Get("Tempo de envio: {0:F1} s. Quadros confirmados: {1}. Último JPEG preparado: {2} bytes. Próxima tag: {3}."),
                    playbackTime.Elapsed.TotalSeconds, confirmed, lastFrameBytes, tag));
            }
        }
    }

    static byte[] QueryResolution(IntPtr handle, Func<string, byte[]> readPacket, uint tag, Action<string> trace, out byte[] dimensions)
    {
        byte[] query = Command(tag, 5, true, true);
        Write(handle, query, Words.Get("Comando de resolução"));
        return ReadReply(readPacket, query, tag, true, trace, out dimensions);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security,
        uint disposition, uint flags, IntPtr template);
    [DllImport("winusb.dll", SetLastError = true)]
    static extern bool WinUsb_Initialize(SafeFileHandle file, out IntPtr handle);
    [DllImport("winusb.dll")]
    static extern bool WinUsb_Free(IntPtr handle);
    [DllImport("winusb.dll", SetLastError = true)]
    static extern bool WinUsb_GetDescriptor(IntPtr handle, byte type, byte index, ushort lang,
        [Out] byte[] buffer, uint size, out uint read);
    [DllImport("winusb.dll", SetLastError = true)]
    static extern bool WinUsb_SetPipePolicy(IntPtr handle, byte pipe, uint policy, uint length, ref uint value);
    [DllImport("winusb.dll", SetLastError = true)]
    static extern bool WinUsb_WritePipe(IntPtr handle, byte pipe, byte[] data, uint length, out uint written, IntPtr overlapped);
    [DllImport("winusb.dll", SetLastError = true)]
    static extern bool WinUsb_ReadPipe(IntPtr handle, byte pipe, [Out] byte[] data, uint length, out uint read, IntPtr overlapped);
    [DllImport("winusb.dll", SetLastError = true)]
    static extern bool WinUsb_AbortPipe(IntPtr handle, byte pipe);
    [DllImport("winusb.dll", SetLastError = true)]
    static extern bool WinUsb_ResetPipe(IntPtr handle, byte pipe);
    [DllImport("winusb.dll", SetLastError = true)]
    static extern bool WinUsb_FlushPipe(IntPtr handle, byte pipe);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    static extern uint CM_Locate_DevNode(out uint node, string device, uint flags);
    [DllImport("cfgmgr32.dll")]
    static extern uint CM_Get_DevNode_Status(out uint status, out uint problem, uint node, uint flags);
}

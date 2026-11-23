using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

internal sealed partial class Editor
{
    readonly Window window;
    readonly DispatcherTimer timer = new DispatcherTimer();
    BitmapImage[] previewFrames;
    Media media;
    string path;
    int theme, rotation, current;
    bool fill = true, circular, busy, paused;
    CancellationTokenSource playback;
    const string Version = "1.0.20";
    const string InstanceName = @"Local\LeFiM.LCD.Control.";
    string lastUsbSend;
    StringBuilder activeUsbTrace;
    bool? lcdDetected;
    string lcdState = Words.Get("Verificando LCD");
    string MediaName { get { return path == null ? Words.Get(ThemeNames[theme]) : Path.GetFileName(path); } }

    void SetLcdState(string state, bool includeMedia = false)
    {
        lcdState = state + (includeMedia ? " · " + MediaName : "");
        var indicator = Find<TextBlock>("UsbStatus");
        indicator.Text = lcdState;
        indicator.ToolTip = lcdState;
    }

    void StopSending()
    {
        if (playback == null || playback.IsCancellationRequested) return;
        playback.Cancel();
        Find<Button>("Send").IsEnabled = false;
        SetLcdState(Words.Get("Encerrando envio"), true);
    }
    string sendSummary;
    Media pendingMedia;
    string pendingPath;
    bool importing;
    Media AcceptReplacement()
    {
        if (Interlocked.CompareExchange(ref pendingMedia,null,null)==null) return null;
        return window.Dispatcher.Invoke(new Func<Media>(() => {
            var next=Interlocked.Exchange(ref pendingMedia,null);
            if(next==null) return null;
            media.Dispose();
            media=next; path=pendingPath; zoom=1; offsetX=offsetY=0;
            current=0; paused=false; previewFrames=new BitmapImage[media.Frames.Count];
            SetZoomControl(); UpdateDetails(); ShowFrame();
            Remember();
            SetLcdState(Words.Get("Enviando"), true);
            Status(Words.Get("Enviando ") + MediaName + Words.Get(" ao LCD."));
            return media;
        }));
    }

    static readonly string[] ThemeNames = { "Órbita", "Prisma", "Aurora" };

    static Window CreateWindow()
    {
        using (var xaml = typeof(Editor).Assembly.GetManifestResourceStream("Main.xaml"))
            return (Window)XamlReader.Load(xaml);
    }

    Editor()
    {
        window = CreateWindow();
        using (var logo = typeof(Editor).Assembly.GetManifestResourceStream("Logo.png"))
        {
            var source = new BitmapImage();
            source.BeginInit();
            source.StreamSource = logo;
            source.DecodePixelWidth = 96;
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.EndInit();
            Find<Image>("HeaderLogo").Source = source;
        }
        var area = SystemParameters.WorkArea;
        window.MinWidth = Math.Min(window.MinWidth, area.Width);
        window.MinHeight = Math.Min(window.MinHeight, area.Height);
        window.Width = Math.Min(window.Width, area.Width);
        window.Height = Math.Min(window.Height, area.Height);
        using (var icon = typeof(Editor).Assembly.GetManifestResourceStream("iconeLcd.ico"))
        {
            if (icon != null) window.Icon = BitmapFrame.Create(icon, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }
        Find<Button>("Import").Click += Import;
        Find<Button>("Export").Click += Export;
        Find<Button>("Send").Click += async (s, e) => {
            if (playback != null) StopSending();
            else if (!busy) await Send();
        };
        Find<Button>("Probe").Click += async (s, e) => await Probe();
        Find<Button>("Fill").Click += async (s, e) => { if (!fill) await Load(path, theme, true, rotation); };
        Find<Button>("Fit").Click += async (s, e) => { if (fill) await Load(path, theme, false, rotation); };
        Find<Button>("Mask").Click += (s, e) => { ToggleMask(); Remember(); };
        Find<Button>("Play").Click += (s, e) => { paused = !paused; ShowFrame(); };
        Find<Border>("Drop").DragOver += (s, e) => {
            e.Effects = !busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        Find<Border>("Drop").Drop += async (s, e) => {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length == 1) await Load(files[0], 0, fill, rotation);
            else Status(Words.Get("Arraste uma imagem ou um GIF por vez."));
        };
        timer.Tick += (s, e) => { current = (current + 1) % media.Frames.Count; ShowFrame(); };
        window.Closing += (sender, e) => {
            if (exiting || checking) return;
            e.Cancel = true;
            HideWindow();
        };
        window.Closed += (sender, e) => {
            timer.Stop();
            CloseSensors();
            if (tray != null) tray.Dispose();
            if (media != null) media.Dispose();
        };
        SetupOptions();
        SetupSensors();
        AddThemes();
        media = Media.Theme(0, 0);
        previewFrames = new BitmapImage[1];
        UpdateDetails();
        SetZoomControl();
        ShowFrame();

    }

    T Find<T>(string name) where T : class { return (T)window.FindName(name); }
    void Status(string text) { Find<TextBlock>("Feedback").Text = text; }
    static Brush Brush(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }

    void AddThemes()
    {
        var colors = new[] { "#6FECD3", "#ABA6FF", "#FFA679" };
        for (int i = 0; i < ThemeNames.Length; i++)
        {
            int selected = i;
            using (var sample = Media.Theme(i, 0))
            {
                var row = new StackPanel();
                row.Children.Add(new Image { Source = WpfBitmap(sample.Frames[0]), Width = 55, Height = 55 });
                row.Children.Add(new TextBlock { Text = ThemeNames[i], Foreground = Brush(colors[i]),
                    Margin = new Thickness(0, 5, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, FontSize = 12 });
                var button = new Button { Content = row, Margin = new Thickness(2),
                    Padding = new Thickness(4, 6, 4, 6), ToolTip = Words.Get("Aplicar tema ") + ThemeNames[i] };
                button.Click += async (s, e) => await Load(null, selected, fill, rotation);
                Find<System.Windows.Controls.Primitives.UniformGrid>("Themes").Children.Add(button);
            }
        }
    }

    async void Import(object sender, RoutedEventArgs e)
    {
        if (importing || (busy && playback==null)) return;
        var picker = new OpenFileDialog { Filter = Words.Get("Imagens e GIFs|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.tif;*.tiff|Todos os arquivos|*.*") };
        var session=playback;
        importing=true;
        try {
            if (picker.ShowDialog(window)!=true) return;
            if (session==null) { await Load(picker.FileName,0,fill,rotation); return; }
            var next=await Task.Run(() => Media.Load(picker.FileName,fill,rotation));
            if (session!=playback || session.IsCancellationRequested) { next.Dispose(); return; }
            pendingPath=picker.FileName;
            var previous=Interlocked.Exchange(ref pendingMedia,next);
            if(previous!=null) previous.Dispose();
        } catch(Exception ex) { Status(Words.Get("Falha ao importar: ")+ex.Message); }
        finally { importing=false; }
    }

    async Task<bool> Load(string next, int selected, bool cover, int degrees, float scale = 1, float dx = 0, float dy = 0, bool quietAngle = false)
    {
        if (busy) return false;
        if (quietAngle) busy = true;
        else SetBusy(true, Words.Get("Preparando mídia em 480 × 480…"));
        timer.Stop();
        try
        {
            var result = await Task.Run(() => next == null ? Media.Theme(selected, degrees) : Media.Load(next, cover, degrees, scale, dx, dy));
            media.Dispose();
            media = result;
            previewFrames = new BitmapImage[media.Frames.Count];
            path = next;
            zoom = scale; offsetX = dx; offsetY = dy;
            SetZoomControl();
            theme = selected;
            fill = cover;
            rotation = degrees;
            current = 0;
            if (!quietAngle) paused = false;
            UpdateDetails();
            if (!quietAngle) Remember();
            if (!quietAngle) Status(Words.Get("Prévia pronta para exportar."));
            return true;
        }
        catch (Exception ex) { Status(Words.Get("Falha ao importar: ") + ex.Message); SetZoomControl(); return false; }
        finally
        {
            if (quietAngle) busy = false;
            else { SetBusy(false); ShowFrame(); }
        }
    }

    void UpdateDetails()
    {
        if (!applyingAngle) DisplayAngle(rotation);
        string name = path == null ? Words.Get(ThemeNames[theme]) : Path.GetFileName(path);
        Find<TextBlock>("MediaInfo").Text = string.Format(Words.Get("{0}\n{1} × {2} original · {3}{4}"), name,
            media.OriginalSize.Width, media.OriginalSize.Height, rotation + "°",
            media.Frames.Count > 1 ? " · " + media.Frames.Count + Words.Get(" quadros") : "");
        Find<Button>("Fill").BorderBrush = Brush(fill ? "#63E5CF" : "#2E4059");
        Find<Button>("Fit").BorderBrush = Brush(fill ? "#2E4059" : "#63E5CF");
        Find<Button>("Play").IsEnabled = media.Frames.Count > 1;
    }

    void ToggleMask()
    {
        circular = !circular;
        Find<Grid>("PreviewCanvas").Clip = circular ? new EllipseGeometry(new Rect(0, 0, 476, 476)) : null;
        Find<System.Windows.Shapes.Ellipse>("CropRing").Visibility = circular ? Visibility.Visible : Visibility.Collapsed;
        Find<Button>("Mask").Content = circular ? Words.Get("Prévia quadrada") : Words.Get("Prévia circular");
    }

    void ShowFrame()
    {
        timer.Stop();
        if (previewFrames[current] == null) previewFrames[current] = WpfBitmap(media.Frames[current]);
        Find<Image>("Preview").Source = previewFrames[current];
        Find<Button>("Play").Content = paused ? Words.Get("Reproduzir") : Words.Get("Pausar");
        if (!window.IsVisible || paused || media.Frames.Count < 2) return;
        timer.Interval = TimeSpan.FromMilliseconds(media.Delays[current]);
        timer.Start();
    }

    static BitmapImage WpfBitmap(System.Drawing.Bitmap frame)
    {
        using (var stream = new MemoryStream())
        {
            lock (frame) frame.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }

    void SetBusy(bool value, string message = null)
    {
        busy = value;
        Find<StackPanel>("AngleControls").IsEnabled = !value || applyingAngle;
        Find<Slider>("Zoom").IsEnabled = !value && path != null;
        Find<MenuItem>("SaveConfig").IsEnabled = !value || playback != null;
        foreach (var name in new[] {"Import", "Export", "Fill", "Fit"})
            Find<Button>(name).IsEnabled = !value;
        Find<Button>("Send").IsEnabled = !value;
        Find<System.Windows.Controls.Primitives.UniformGrid>("Themes").IsEnabled = !value;
        if (message != null) Status(message);
    }

    async void Export(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        bool animated = media.Frames.Count > 1;
        var picker = new SaveFileDialog { Filter = animated ? Words.Get("GIF animado|*.gif|PNG do primeiro quadro|*.png") : Words.Get("Imagem PNG|*.png"),
            FileName = (path == null ? Words.Get(ThemeNames[theme]) : Path.GetFileNameWithoutExtension(path)) + "-480x480" };
        if (picker.ShowDialog(window) != true) return;
        SetBusy(true, Words.Get("Exportando…"));
        timer.Stop();
        Find<Button>("Play").IsEnabled = false;
        try
        {
            await Task.Run(() => media.Export(picker.FileName));
            Status(Words.Get("Exportado: ") + Path.GetFileName(picker.FileName));
        }
        catch (Exception ex) { Status(Words.Get("Falha ao exportar: ") + ex.Message); }
        finally
        {
            SetBusy(false);
            Find<Button>("Play").IsEnabled = media.Frames.Count>1;
            ShowFrame();
        }
    }

    async Task Probe()
    {
        if (busy && playback == null) return;
        bool duringSend = playback != null;
        if (!duringSend) SetBusy(true, Words.Get("Consultando a interface WinUSB."));
        try
        {
            string trace = lastUsbSend;
            var live = activeUsbTrace;
            if (live != null) lock (live) trace = live.ToString();
            string result = "LeFiM LCD Control " + Version + Words.Get("\nUSB esperado: 2A55:9990") +
                Words.Get("\nEstado: ") + lcdState + Words.Get("\nMídia: ") + MediaName + "\n\n" +
                (duringSend ? Words.Get("Envio em andamento.") : sendSummary ?? Words.Get("Ainda não há resultado de envio nesta sessão."));
            if (!duringSend) result += Words.Get("\n\nDetalhes técnicos\n") + await Task.Run(() => Usb.Probe());
            if (!string.IsNullOrEmpty(trace)) result += Words.Get("\n\nRegistro de envio\n") + trace;
            var output = new TextBox { Text = result, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(12),
                FontFamily = new FontFamily("Consolas"), Background = Brush("#141D2B"), Foreground = Brushes.White };
            var copy = new Button { Content = Words.Get("Copiar diagnóstico"), Margin = new Thickness(12) };
            copy.Click += (s, e) => {
                try { Clipboard.SetText(result); copy.Content = Words.Get("Diagnóstico copiado"); }
                catch (System.Runtime.InteropServices.ExternalException) { copy.Content = Words.Get("Selecione o texto e pressione Ctrl+C"); }
            };
            var panel = new DockPanel();
            DockPanel.SetDock(copy, Dock.Bottom);
            panel.Children.Add(copy);
            panel.Children.Add(output);
            new Window { Owner = window, Title = Words.Get("Diagnóstico USB · ") + Version, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Width = 690, Height = 425, Content = panel }.ShowDialog();
            if (!duringSend) await RefreshDetection();
        }
        catch (Exception ex) { Status(Words.Get("Falha no diagnóstico: ") + ex.Message); }
        finally { if (!duringSend) SetBusy(false); }
    }

    async Task RefreshDetection()
    {
        lcdDetected = await Task.Run(() => Usb.Detected());
        if (playback == null && !lcdState.StartsWith(Words.Get("Sem resposta do LCD"), StringComparison.Ordinal))
            SetLcdState(lcdDetected == true ? Words.Get("LCD detectada") : Words.Get("LCD indisponível"));
    }

    async Task Send()
    {
        SetBusy(true, Words.Get("Conectando e conferindo a resolução do LCD…"));
        var send = Find<Button>("Send");
        send.Content = Words.Get("Parar exibição no LCD");
        send.IsEnabled = true;
        var cancel = new CancellationTokenSource();
        var trace = new StringBuilder();
        activeUsbTrace = trace;
        trace.AppendLine(Words.Get("Mídia inicial: ") + MediaName);
        playback = cancel;
        SetLcdState(Words.Get("Enviando"), true);
        Find<Button>("Import").IsEnabled=true;
        Find<Button>("Import").Content=Words.Get("Trocar imagem ou GIF no LCD");
        Find<MenuItem>("SaveConfig").IsEnabled = true;
        try
        {
            bool reported = false;
            bool awaitingFrame = true;
            await Task.Run(() => Usb.Send(media, cancel.Token, frame => {
                if (!awaitingFrame) return;
                awaitingFrame = false;
                reported = true;
                window.Dispatcher.BeginInvoke(new Action(() => {
                    if (playback != cancel || cancel.IsCancellationRequested) return;
                    SetLcdState(media.Frames.Count > 1 ? Words.Get("Reproduzindo") : Words.Get("Exibindo"), true);
                    Status(Words.Get("O LCD confirmou o recebimento do quadro."));
                }));
            }, line => { lock (trace) trace.AppendLine(line); }, () => {
                var replacement = AcceptReplacement();
                if (replacement != null) awaitingFrame = true;
                return replacement;
            }, () => sensorOverlay));
            string result = cancel.IsCancellationRequested ? Words.Get("Envio encerrado.") : Words.Get("Imagem aceita pelo LCD.");
            sendSummary = reported ? Words.Get("O LCD confirmou o recebimento de quadros. A exibição foi encerrada.") : Words.Get("Envio encerrado antes da confirmação do primeiro quadro.");
            trace.AppendLine(result);
            if (reported) lcdDetected = true;
            SetLcdState(lcdDetected == true ? Words.Get("LCD detectada") : Words.Get("Envio encerrado"));
            Status(result);
        }
        catch (Exception ex)
        {
            sendSummary = (ex.Message.Contains(Words.Get("Consulta de resolução")) || ex.Message.Contains(Words.Get("Comando de resolução"))) && ex.Message.Contains("121")
                ? (ex.Message.Contains(Words.Get("Comando de resolução")) ? Words.Get("O Windows detectou o LCD, mas o envio da consulta de tamanho expirou.") : Words.Get("A consulta de tamanho foi enviada, mas a resposta do LCD não chegou a tempo.")) + Words.Get(" Nenhuma imagem foi enviada. Feche o controlador antes de recuperar a tela.")
                : Words.Get("A transmissão foi interrompida. O visor pode continuar mostrando o último quadro recebido.");
            trace.AppendLine(Words.Get("Falha no envio: ") + ex.Message);
            SetLcdState(ex.InnerException is System.ComponentModel.Win32Exception &&
                ((System.ComponentModel.Win32Exception)ex.InnerException).NativeErrorCode == 121
                ? Words.Get("Sem resposta do LCD") : Words.Get("Falha no envio"), true);
            Status(Words.Get("Falha no envio: ") + ex.Message + Words.Get("\nVeja os detalhes em Diagnóstico USB."));
        }
        finally
        {
            lastUsbSend = trace.ToString();
            activeUsbTrace = null;
            playback = null;
            var abandoned=Interlocked.Exchange(ref pendingMedia,null);
            if(abandoned!=null) abandoned.Dispose();
            Find<Button>("Import").Content=Words.Get("Importar imagem ou GIF");
            cancel.Dispose();
            send.Content = Words.Get("Enviar ao LCD");
            SetBusy(false);
            Find<Button>("Play").IsEnabled = media.Frames.Count>1;
            ShowFrame();
        }
    }

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--check-ui")
        {
            checking = true;
            var app = new Application();
            var editor = new Editor();
            editor.window.Show();
            editor.window.UpdateLayout();
            editor.window.Close();
            return 0;
        }
        bool startup = Array.IndexOf(args, "--startup") >= 0;
        bool primary;
        using (var signal = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceName + "Open"))
        using (var instanceLock = new Mutex(true, InstanceName + "Primary", out primary))
        {
            if (!primary)
            {
                if (!startup) signal.Set();
                return 0;
            }

            try
            {
                var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var instance = new Editor();
                application.MainWindow = instance.window;
                instance.SetupTray();
                var registration = ThreadPool.RegisterWaitForSingleObject(signal, (state, timedOut) =>
                    application.Dispatcher.BeginInvoke(new Action(() => {
                        if (!instance.exiting) instance.OpenWindow(true);
                    })), null, Timeout.Infinite, false);
                try
                {
                    if (!startup) instance.OpenWindow();
                    application.Dispatcher.BeginInvoke(new Action(async () => {
                        await instance.RefreshDetection();
                        await instance.Restore(startup);
                    }));
                    return application.Run();
                }
                finally { registration.Unregister(null); }
            }
            finally { instanceLock.ReleaseMutex(); }
        }
    }
}

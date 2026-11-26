using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Serialization;
using Microsoft.Win32;

public sealed class SavedDisplay
{
    public string FilePath;
    public int Theme, Rotation;
    public bool Fill = true, Circular;
    public float Zoom = 1, X, Y;
    public SensorPlacement[] Sensors;
    public bool English;
}

internal sealed partial class Editor
{
    static bool checking;
    float zoom = 1, offsetX, offsetY, draftX, draftY;
    Point anchor;
    bool dragging, syncing;
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string AppName = "LeFiM LCD Control";
    static string Exe { get { return typeof(Editor).Assembly.Location; } }
    static string SettingsFile { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LeFiM LCD Control", "display.xml"); } }

    void SetZoomControl()
    {
        syncing = true;
        Find<Slider>("Zoom").Value = zoom;
        Find<Slider>("Zoom").IsEnabled = !busy && path != null;
        Find<TextBlock>("ZoomValue").Text = zoom.ToString("P0");
        syncing = false;
    }

    void LimitOffsets(float scale, ref float x, ref float y)
    {
        var size = media.OriginalSize;
        double factor = (fill ? 480.0 / Math.Min(size.Width,size.Height) : 480.0 / Math.Max(size.Width,size.Height)) * scale;
        double radians = rotation * Math.PI / 180;
        double cosine = Math.Abs(Math.Cos(radians)), sine = Math.Abs(Math.Sin(radians));
        double width = size.Width * cosine + size.Height * sine;
        double height = size.Width * sine + size.Height * cosine;
        float maxX = (float)(width * factor / 2);
        float maxY = (float)(height * factor / 2);
        x = Math.Max(-maxX, Math.Min(maxX,x));
        y = Math.Max(-maxY, Math.Min(maxY,y));
    }

    void Draft(float scale,float x,float y)
    {
        timer.Stop();
        LimitOffsets(scale,ref x,ref y);
        draftX=x; draftY=y;
        using (var frame = Media.Render(media.DragSource, fill, rotation, scale, x, y))
            Find<Image>("Preview").Source = WpfBitmap(frame);
    }

    async Task CommitZoom()
    {
        float scale = (float)Find<Slider>("Zoom").Value;
        if (!busy && path != null && scale != zoom) await Load(path,theme,fill,rotation,scale,draftX,draftY);
    }

    int draftAngle;
    int requestedAngle;
    bool applyingAngle;
    void DisplayAngle(int angle)
    {
        draftAngle = angle;
        Find<System.Windows.Media.RotateTransform>("AnglePointer").Angle = angle;
        Find<TextBlock>("AngleText").Text = angle + "°";
    }

    void PreviewAngle(int angle)
    {
        if (angle == draftAngle) return;
        DisplayAngle(angle);
        timer.Stop();
        using (var frame = Media.Render(media.DragSource, fill, angle, zoom, offsetX, offsetY))
            Find<Image>("Preview").Source = WpfBitmap(frame);
    }

    void SetupAngle()
    {
        var dial = Find<Grid>("AngleDial");
        Find<Button>("AngleMinus").Click += async (s,e) => await StepAngle(-1);
        Find<Button>("AnglePlus").Click += async (s,e) => await StepAngle(1);
        Find<StackPanel>("AngleControls").PreviewMouseWheel += async (s,e) => {
            if (e.Delta == 0) return;
            e.Handled = true;
            await StepAngle(Math.Sign(e.Delta));
        };
        Action<Point> pointAngle = point => {
            double x = point.X - dial.ActualWidth / 2, y = point.Y - dial.ActualHeight / 2;
            if (x*x + y*y < 9) return;
            double angle = Math.Atan2(x,-y)*180/Math.PI;
            PreviewAngle(WrapAngle((int)Math.Round(angle)));
        };
        dial.MouseLeftButtonDown += (s,e) => {
            if (busy) return;
            dial.Focus(); dial.CaptureMouse(); pointAngle(e.GetPosition(dial));
        };
        dial.MouseMove += (s,e) => { if (dial.IsMouseCaptured) pointAngle(e.GetPosition(dial)); };
        dial.MouseLeftButtonUp += async (s,e) => {
            if (!dial.IsMouseCaptured) return;
            int angle = draftAngle;
            dial.ReleaseMouseCapture();
            await ApplyAngle(angle);
        };
        dial.LostMouseCapture += (s,e) => { if (!applyingAngle) { DisplayAngle(rotation); ShowFrame(); } };
        dial.KeyDown += async (s,e) => {
            int angle;
            switch(e.Key) {
                case Key.Left: case Key.Down: angle = SteppedAngle(-1); break;
                case Key.Right: case Key.Up: angle = SteppedAngle(1); break;
                case Key.Home: angle = 0; break;
                case Key.End: angle = 360; break;
                default: return;
            }
            e.Handled = true;
            await ApplyAngle(angle);
        };
    }

    static int WrapAngle(int angle)
    {
        return (angle % 360 + 360) % 360;
    }

    int SteppedAngle(int step)
    {
        return WrapAngle((applyingAngle ? requestedAngle : rotation) + step);
    }

    Task StepAngle(int step)
    {
        return ApplyAngle(SteppedAngle(step));
    }

    async Task ApplyAngle(int angle)
    {
        if (busy && !applyingAngle) return;
        requestedAngle = Math.Max(0, Math.Min(360, angle));
        DisplayAngle(requestedAngle);
        timer.Stop();
        if (applyingAngle) return;
        applyingAngle = true;
        try
        {
            while (requestedAngle != rotation)
            {
                busy = true;
                try { await Task.Delay(100); }
                finally { busy = false; }
                if (requestedAngle == rotation) break;
                if (!await Load(path, theme, fill, requestedAngle, zoom, offsetX, offsetY, quietAngle: true)) break;
            }
        }
        finally
        {
            applyingAngle = false;
            DisplayAngle(rotation);
            ShowFrame();
            Remember();
        }
    }

    void SetupOptions()
    {
        SetupAngle();
        Find<MenuItem>("LanguagePt").Click += (s,e) => { ApplyLanguage(false); Remember(); };
        Find<MenuItem>("LanguageEn").Click += (s,e) => { ApplyLanguage(true); Remember(); };
        Find<MenuItem>("MenuAbout").Click += (s,e) => MessageBox.Show(window,
            "LeFiM LCD Control " + Version + Words.Get("\nDesenvolvido por LeFiM Scripts\nAplicativo gratuito para telas USB compatíveis."),
            Words.Get("Sobre o LeFiM LCD Control"), MessageBoxButton.OK, MessageBoxImage.Information);
        Find<MenuItem>("MenuImport").Click += Import;
        Find<MenuItem>("MenuExport").Click += Export;
        Find<MenuItem>("MenuHide").Click += (s,e) => HideWindow();
        Find<MenuItem>("MenuExit").Click += async (s,e) => await Exit();
        Find<MenuItem>("MenuProbe").Click += async (s,e) => await Probe();
        var canvas = Find<Grid>("PreviewCanvas");
        var slider = Find<Slider>("Zoom");
        slider.ValueChanged += (s,e) => {
            if (syncing || busy || path == null) return;
            Find<TextBlock>("ZoomValue").Text = e.NewValue.ToString("P0");
            Draft((float)e.NewValue,offsetX,offsetY);
        };
        slider.AddHandler(Mouse.MouseUpEvent, new MouseButtonEventHandler(async (s,e) => await CommitZoom()),true);
        slider.KeyUp += async (s,e) => await CommitZoom();
        canvas.MouseLeftButtonDown += (s,e) => {
            if (busy || path == null) return;
            anchor=e.GetPosition(canvas); draftX=offsetX; draftY=offsetY;
            dragging=canvas.CaptureMouse();
        };
        canvas.MouseMove += (s,e) => {
            if (!dragging) return;
            var delta=e.GetPosition(canvas)-anchor;
            Draft(zoom,offsetX+(float)(delta.X*480/canvas.ActualWidth),offsetY+(float)(delta.Y*480/canvas.ActualHeight));
        };
        canvas.MouseLeftButtonUp += async (s,e) => {
            if (!dragging) return;
            dragging=false; canvas.ReleaseMouseCapture();
            if (draftX!=offsetX || draftY!=offsetY) await Load(path,theme,fill,rotation,zoom,draftX,draftY);
            else ShowFrame();
        };
        canvas.LostMouseCapture += (s,e) => { if (dragging) { dragging=false; ShowFrame(); } };
        Find<MenuItem>("SaveConfig").Click += (s,e) => SafeOption(() => { Save(); Status(Words.Get("Configuração salva.")); });
        Find<MenuItem>("DonateKoFi").Click += (s, e) => OpenDonationPage("https://ko-fi.com/alefcferreira");
        Find<MenuItem>("DonateMercadoPago").Click += (s, e) => OpenDonationPage("https://link.mercadopago.com.br/lefimscripts");
        var startup=Find<MenuItem>("Startup");
        try { using (var key=Registry.CurrentUser.OpenSubKey(RunKey))
            startup.IsChecked=key != null && key.GetValue(AppName) != null; } catch (System.Security.SecurityException) { startup.IsEnabled=false; }
        startup.Click += (s,e) => {
            bool enabled=startup.IsChecked;
            try {
                using (var key=Registry.CurrentUser.CreateSubKey(RunKey)) {
                    if (enabled) { Save(); key.SetValue(AppName,"\"" + Exe + "\" --startup"); }
                    else key.DeleteValue(AppName,false);
                }
                Status(enabled ? Words.Get("Configuração salva. Reprodução automática ativada.") : Words.Get("Inicialização automática desativada."));
            } catch(Exception ex) { startup.IsChecked=!enabled; Status(ex.Message); }
        };
        Find<MenuItem>("Shortcut").Click += (s,e) => SafeOption(() => {
            dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            dynamic shortcut=shell.CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName+".lnk"));
            shortcut.TargetPath=Exe; shortcut.WorkingDirectory=Path.GetDirectoryName(Exe);
            string icon=Path.Combine(Path.GetDirectoryName(Exe),"iconeLcd.ico");
            shortcut.IconLocation=(File.Exists(icon)?icon:Exe)+",0";
            shortcut.Save(); Status(Words.Get("Atalho criado na área de trabalho."));
        });
    }
    void ApplyLanguage(bool english)
    {
        Words.English = english;
        Words.Apply(window);
        Find<MenuItem>("LanguagePt").IsChecked = !english;
        Find<MenuItem>("LanguageEn").IsChecked = english;
        UpdateTrayLanguage();
        var parts = lcdState.Split(new[] { " · " }, 2, StringSplitOptions.None);
        lcdState = Words.InLanguage(parts[0]) + (parts.Length == 2 ? " · " + parts[1] : "");
        Find<TextBlock>("UsbStatus").Text = lcdState;
        Find<TextBlock>("UsbStatus").ToolTip = lcdState;
        var sensorStatus = Find<TextBlock>("SensorStatus");
        sensorStatus.Text = Words.InLanguage(sensorStatus.Text);
        Status(english ? "Ready." : "Pronto.");
        UpdateDetails();
    }

    void OpenDonationPage(string url)
    {
        SafeOption(() => {
            using (Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })) { }
        });
    }

    void SafeOption(Action action) { try { action(); } catch(Exception ex) { Status(ex.Message); } }
    void Save()
    {
        var state=new SavedDisplay { FilePath=path, Theme=theme, Rotation=rotation, Fill=fill, Circular=circular, Zoom=zoom, X=offsetX, Y=offsetY, Sensors=sensors, English=Words.English };
        string target=SettingsFile, temp=target+"."+Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        try {
            using(var stream=File.Create(temp)) new XmlSerializer(typeof(SavedDisplay)).Serialize(stream,state);
            if(File.Exists(target)) File.Replace(temp,target,null); else File.Move(temp,target);
        } finally { if(File.Exists(temp)) File.Delete(temp); }
        savePending = false;
    }
    async Task Restore(bool startup)
    {
        if (!File.Exists(SettingsFile)) return;
        restoring = true;
        try {
            SavedDisplay state;
            using(var stream=File.OpenRead(SettingsFile)) state=(SavedDisplay)new XmlSerializer(typeof(SavedDisplay)).Deserialize(stream);
            ApplyLanguage(state.English);
            if(state.FilePath!=null && !File.Exists(state.FilePath)) { Status(Words.Get("Arquivo salvo indisponível: ")+state.FilePath); return; }
            if(!await Load(state.FilePath,state.Theme,state.Fill,state.Rotation,state.Zoom,state.X,state.Y)) return;
            if(circular!=state.Circular) ToggleMask();
            RestoreSensors(state.Sensors);
            if (!startup) return;
            restoring = false;
            SetBusy(true,Words.Get("Aguardando o LCD para reproduzir a configuração salva…"));
            bool ready=false;
            try {
                for(int i=0;i<30 && !exiting;i++) {
                    ready=await Task.Run(() => Usb.Detected());
                    if(ready) break;
                    await Task.Delay(1000);
                }
            } finally { SetBusy(false); }
            if(ready && !exiting) await Send();
            else Status(Words.Get("LCD indisponível. Clique em Enviar ao LCD quando ele estiver conectado."));
        } catch(Exception ex) { Status(Words.Get("Falha ao restaurar configuração: ")+ex.Message); }
        finally { restoring = false; }
    }
}

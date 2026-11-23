using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

internal sealed partial class Editor
{
    SensorPlacement[] sensors = Enumerable.Range(0, SensorOverlay.Labels.Length).Select(i => new SensorPlacement { X = 148, Y = 64 + 45 * i }).ToArray();
    readonly string[] sensorNames = { Words.Get("Temperatura da CPU"), Words.Get("Temperatura da GPU"), Words.Get("Uso de RAM"),
        Words.Get("Utilização da CPU"), Words.Get("Utilização da GPU"), Words.Get("VRAM em uso"), Words.Get("Rotação da ventoinha"), Words.Get("Relógio") };
    readonly Border[] sensorCards = new Border[SensorOverlay.Labels.Length];
    readonly CheckBox[] sensorChecks = new CheckBox[SensorOverlay.Labels.Length];
    readonly TextBlock[] sensorSizes = new TextBlock[SensorOverlay.Labels.Length];
    readonly TextBlock[] sensorTexts = new TextBlock[SensorOverlay.Labels.Length];
    readonly SensorReader sensorReader = new SensorReader();
    readonly DispatcherTimer sensorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
    string[] sensorValues = new string[SensorOverlay.Labels.Length];
    volatile SensorOverlay sensorOverlay;
    SensorOverlay previewSensors;
    bool readingSensors, updatingSensors, sensorsClosed;

    void SetupSensors()
    {
        var rows = Find<StackPanel>("SensorRows");
        var layer = Find<Canvas>("SensorLayer");
        for (int i = 0; i < sensors.Length; i++)
        {
            int index = i;
            var check = new CheckBox { Content = sensorNames[index], Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            sensorChecks[index] = check;
            var color = new Button { Content = Words.Get("Cor…"), Margin = new Thickness(6, 0, 0, 0) };
            var row = new DockPanel { Margin = new Thickness(0, 4, 0, 6) };
            DockPanel.SetDock(color, Dock.Right);
            row.Children.Add(color);
            row.Children.Add(check);
            rows.Children.Add(row);
            var sizeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var smaller = new Button { Content = "−", Padding = new Thickness(10, 3, 10, 3), ToolTip = Words.Get("Diminuir texto") };
            var larger = new Button { Content = "+", Padding = new Thickness(10, 3, 10, 3), ToolTip = Words.Get("Aumentar texto") };
            var size = new TextBlock { Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0), MinWidth = 42, TextAlignment = TextAlignment.Center };
            sensorSizes[index] = size;
            smaller.Click += (s,e) => ResizeSensor(index, -2);
            larger.Click += (s,e) => ResizeSensor(index, 2);
            sizeRow.Children.Add(smaller);
            sizeRow.Children.Add(size);
            sizeRow.Children.Add(larger);
            rows.Children.Add(sizeRow);
            check.Checked += async (s,e) => await SetSensorEnabled(index, true);
            check.Unchecked += async (s,e) => await SetSensorEnabled(index, false);
            color.Click += (s,e) => {
                using (var picker = new Forms.ColorDialog { Color = SensorOverlay.TextColor(sensors[index].Color), FullOpen = true })
                {
                    if (picker.ShowDialog(new WindowOwner(window)) != Forms.DialogResult.OK) return;
                    sensors[index].Color = "#" + (picker.Color.ToArgb() & 0xFFFFFF).ToString("X6");
                }
                PublishSensors();
                Remember();
            };
            var text = new TextBlock { FontSize = 24, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8,0,0,0) };
            var card = new Border {
                Background = Brush("#B409101B"),
                Child = text, Cursor = Cursors.SizeAll
            };
            Point origin = new Point();
            card.MouseLeftButtonDown += (s,e) => {
                origin = e.GetPosition(card);
                card.CaptureMouse();
                e.Handled = true;
            };
            card.MouseMove += (s,e) => {
                if (!card.IsMouseCaptured) return;
                var position = e.GetPosition(layer);
                sensors[index].X = SensorOverlay.Clamp(position.X - origin.X, previewSensors.Width(index));
                sensors[index].Y = SensorOverlay.Clamp(position.Y - origin.Y, sensors[index].BoxHeight);
                PublishSensors();
                e.Handled = true;
            };
            card.MouseLeftButtonUp += (s,e) => {
                if (!card.IsMouseCaptured) return;
                card.ReleaseMouseCapture();
                e.Handled = true;
            };
            card.PreviewMouseWheel += (s,e) => {
                if (e.Delta == 0) return;
                ResizeSensor(index, e.Delta > 0 ? 2 : -2);
                e.Handled = true;
            };
            card.LostMouseCapture += (s,e) => Remember();
            layer.Children.Add(card);
            sensorCards[index] = card;
            sensorTexts[index] = text;
        }
        sensorTimer.Tick += async (s,e) => await SampleSensors();
        PublishSensors();
    }

    sealed class WindowOwner : Forms.IWin32Window
    {
        public IntPtr Handle { get; private set; }
        public WindowOwner(Window window) { Handle = new System.Windows.Interop.WindowInteropHelper(window).Handle; }
    }

    void ResizeSensor(int index, int step)
    {
        var item = sensors[index];
        item.FontSize = Math.Max(12, Math.Min(42, item.FontSize + step));
        PublishSensors();
        item.X = previewSensors.Left(index);
        item.Y = previewSensors.Top(index);
        Remember();
    }

    async Task SetSensorEnabled(int index, bool enabled)
    {
        if (updatingSensors) return;
        sensors[index].Enabled = enabled;
        PublishSensors();
        if (sensors.Any(item => item.Enabled))
        {
            sensorTimer.Start();
            Remember();
            await SampleSensors();
            return;
        }
        else sensorTimer.Stop();
        Remember();
    }

    void RestoreSensors(SensorPlacement[] saved)
    {
        if (saved == null) return;
        for (int i = 0; i < Math.Min(saved.Length, sensors.Length); i++)
            if (saved[i] != null) sensors[i] = saved[i];
        updatingSensors = true;
        for (int i = 0; i < sensors.Length; i++)
        {
            var item = sensors[i];
            item.FontSize = Math.Max(12, Math.Min(42, item.FontSize));
            sensorChecks[i].IsChecked = sensors[i].Enabled;
        }
        updatingSensors = false;
        PublishSensors();
        if (sensors.Any(item => item.Enabled)) sensorTimer.Start();
    }

    async Task SampleSensors()
    {
        if (readingSensors || sensorsClosed) return;
        readingSensors = true;
        try
        {
            bool[] enabled = Array.ConvertAll(sensors, item => item.Enabled);
            sensorValues = await Task.Run(() => sensorReader.Read(enabled));
            if (sensorsClosed) return;
            PublishSensors();
            Find<TextBlock>("SensorStatus").Text = enabled[0] && sensorValues[0] == null
                ? Words.Get("CPU sem leitura. Confira o sensor do processador no Libre Hardware Monitor e as permissões do Windows.")
                : Words.Get("Dados atualizados a cada segundo. — indica uma leitura indisponível.");
        }
        finally
        {
            readingSensors = false;
            if (sensorsClosed) sensorReader.Dispose();
        }
    }

    void PublishSensors()
    {
        var snapshot = new SensorOverlay(sensors, sensorValues);
        previewSensors = snapshot;
        sensorOverlay = sensors.Any(item => item.Enabled) ? snapshot : null;
        for (int i = 0; i < sensors.Length; i++)
        {
            var item = sensors[i];
            var color = SensorOverlay.TextColor(item.Color);
            sensorSizes[i].Text = item.FontSize + " px";
            sensorCards[i].Width = snapshot.Width(i);
            sensorCards[i].Height = item.BoxHeight;
            sensorTexts[i].FontSize = item.FontSize;
            sensorTexts[i].Margin = new Thickness(SensorOverlay.Padding(item), 0, 0, 0);
            sensorTexts[i].Text = snapshot.Text(i);
            sensorTexts[i].Foreground = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
            sensorCards[i].Visibility = item.Enabled ? Visibility.Visible : Visibility.Collapsed;
            Canvas.SetLeft(sensorCards[i], snapshot.Left(i));
            Canvas.SetTop(sensorCards[i], snapshot.Top(i));
        }
    }

    void CloseSensors()
    {
        sensorsClosed = true;
        sensorTimer.Stop();
        if (!readingSensors) sensorReader.Dispose();
    }
}

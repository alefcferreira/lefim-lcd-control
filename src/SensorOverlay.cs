using System;
using System.Drawing;
using System.Drawing.Text;

public sealed class SensorPlacement
{
    public bool Enabled;
    public double X, Y;
    public int FontSize = 24;
    public string Color = "#FFFFFF";

    internal int BoxHeight { get { return (int)Math.Ceiling(FontSize * 1.5); } }

    internal SensorPlacement Copy()
    {
        return new SensorPlacement { Enabled = Enabled, X = X, Y = Y, Color = Color, FontSize = FontSize };
    }
}

internal sealed class SensorOverlay
{
    public const int PreviewSize = Media.Size - 4;
    public static readonly string[] Labels = { "CPU", "GPU", "RAM", "CPU", "GPU", "VRAM", "FAN", "" };
    readonly SensorPlacement[] placements;
    readonly string[] values;
    readonly int[] widths;

    public SensorOverlay(SensorPlacement[] placements, string[] values)
    {
        this.placements = Array.ConvertAll(placements, p => p.Copy());
        this.values = (string[])values.Clone();
        widths = new int[placements.Length];
        for (int index = 0; index < placements.Length; index++)
        {
            var item = placements[index];
            using (var font = new Font("Segoe UI", item.FontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                widths[index] = System.Windows.Forms.TextRenderer.MeasureText(Text(index), font,
                    System.Drawing.Size.Empty, System.Windows.Forms.TextFormatFlags.NoPadding |
                    System.Windows.Forms.TextFormatFlags.SingleLine).Width + Padding(item) * 2 + 2;
        }
    }

    public string Text(int index)
    {
        string value = values[index] ?? "—";
        return Labels[index].Length == 0 ? value : Labels[index] + " " + value;
    }

    public int Width(int index) { return widths[index]; }
    public double Left(int index) { return Clamp(placements[index].X, Width(index)); }
    public double Top(int index) { return Clamp(placements[index].Y, placements[index].BoxHeight); }
    public static int Padding(SensorPlacement item) { return (int)Math.Ceiling(item.FontSize * 0.22); }
    public static double Clamp(double value, int extent)
    {
        return double.IsNaN(value) ? 0 : Math.Max(0, Math.Min(PreviewSize - extent, value));
    }

    public static Color TextColor(string color)
    {
        try { return ColorTranslator.FromHtml(color); }
        catch (ArgumentException) { return Color.White; }
    }

    public byte[] Encode(Bitmap frame)
    {
        using (var composite = new Bitmap(Media.Size, Media.Size))
        {
            using (var graphics = Graphics.FromImage(composite))
            using (var background = new SolidBrush(Color.FromArgb(180, 9, 16, 27)))
            {
                lock (frame) graphics.DrawImageUnscaled(frame, 0, 0);
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                graphics.ScaleTransform(Media.Size / (float)PreviewSize, Media.Size / (float)PreviewSize);
                for (int index = 0; index < placements.Length; index++)
                {
                    var item = placements[index];
                    if (!item.Enabled) continue;
                    float x = (float)Left(index), y = (float)Top(index);
                    graphics.FillRectangle(background, x, y, Width(index), item.BoxHeight);
                    using (var font = new Font("Segoe UI", item.FontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var ink = new SolidBrush(TextColor(item.Color)))
                        graphics.DrawString(Text(index), font, ink, x + Padding(item), y + 1, StringFormat.GenericTypographic);
                }
            }
            return Usb.EncodeFrame(composite);
        }
    }
}

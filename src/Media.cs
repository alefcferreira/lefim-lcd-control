using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

internal sealed class Media : IDisposable
{
    public const int Size = 480;
    public readonly List<Bitmap> Frames = new List<Bitmap>();
    public readonly List<int> Delays = new List<int>();
    public readonly Size OriginalSize;
    public Bitmap DragSource;

    Media(Size originalSize) { OriginalSize = originalSize; }

    public static Media Load(string path, bool fill, int angle, float zoom = 1, float x = 0, float y = 0)
    {
        var file = new FileInfo(path);
        if (file.Length > 48 * 1024 * 1024) throw new FormatException(Words.Get("O arquivo excede 48 MB."));
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 6 && Encoding.ASCII.GetString(bytes, 0, 3) == "GIF")
            return Gif.Decode(bytes, fill, angle, zoom, x, y);
        using (var stream = new MemoryStream(bytes))
        using (var image = Image.FromStream(stream, true, true))
        {
            CheckSize(image.Width, image.Height);
            ApplyExifOrientation(image);
            var media = new Media(image.Size);
            media.Add(image, 100, fill, angle, zoom, x, y);
            return media;
        }
    }

    static void ApplyExifOrientation(Image image)
    {
        if (Array.IndexOf(image.PropertyIdList, 0x0112) < 0) return;
        var property = image.GetPropertyItem(0x0112);
        if (property.Value.Length < 2) return;
        int orientation = property.Value[0] | property.Value[1] << 8;
        if (orientation < 2 || orientation > 8) return;
        var operations = new[] { RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipX,
            RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate90FlipX,
            RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate270FlipNone };
        image.RotateFlip(operations[orientation - 1]);
    }

    static void CheckSize(int width, int height)
    {
        if (width < 1 || height < 1 || (long)width * height > 25000000)
            throw new FormatException(Words.Get("A imagem excede o limite de 25 megapixels."));
    }

    void Add(Image image, int delay, bool fill, int angle, float zoom = 1, float x = 0, float y = 0)
    {
        if (Frames.Count == 0)
        {
            float ratio = Math.Min(1f, 960f / Math.Max(image.Width, image.Height));
            DragSource = new Bitmap(image, Math.Max(1, (int)Math.Round(image.Width * ratio)), Math.Max(1, (int)Math.Round(image.Height * ratio)));
        }
        Frames.Add(Render(image, fill, angle, zoom, x, y));
        Delays.Add(delay);
    }

    public static Bitmap Render(Image image, bool fill, int angle, float zoom, float x, float y)
    {
        var bitmap = new Bitmap(Size, Size, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Black);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float sx = Size / (float)image.Width, sy = Size / (float)image.Height;
            float scale = fill ? Math.Max(sx, sy) : Math.Min(sx, sy);
            float w = image.Width * scale * zoom, h = image.Height * scale * zoom;
            g.TranslateTransform(Size / 2f + x, Size / 2f + y);
            g.RotateTransform(angle);
            using (var edge = new ImageAttributes())
            {
                edge.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(image, Rectangle.Round(new RectangleF(-w/2, -h/2, w, h)),
                    0, 0, image.Width, image.Height, GraphicsUnit.Pixel, edge);
            }
        }
        return bitmap;
    }

    public static Media Theme(int theme, int angle)
    {
        var media = new Media(new Size(Size, Size));
        using (var bitmap = new Bitmap(Size, Size))
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var colors = new[] { "#51E9C6", "#9BA0FF", "#FFA16E" };
            Color accent = ColorTranslator.FromHtml(colors[theme]);
            using (var bg = new LinearGradientBrush(new Rectangle(0, 0, Size, Size),
                Color.FromArgb(31, 38, 49), Color.FromArgb(6, 10, 17), 65f))
                g.FillRectangle(bg, 0, 0, Size, Size);
            using (var grid = new Pen(Color.FromArgb(25, accent)))
            using (var line = new Pen(accent, 4))
            using (var soft = new Pen(Color.FromArgb(95, accent), 2))
            using (var ink = new SolidBrush(accent))
            using (var large = new Font("Segoe UI", 54, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var small = new Font("Consolas", 19, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var center = new StringFormat { Alignment = StringAlignment.Center })
            {
                for (int i = 0; i < Size; i += 24)
                {
                    g.DrawLine(grid, i, 0, i, Size);
                    g.DrawLine(grid, 0, i, Size, i);
                }
                if (theme == 0)
                {
                    g.DrawEllipse(soft, 64, 64, 352, 352);
                    g.DrawEllipse(soft, 90, 90, 300, 300);
                    g.DrawArc(line, 76, 76, 328, 328, 200, 122);
                    g.DrawArc(line, 76, 76, 328, 328, 50, 92);
                }
                else if (theme == 1)
                {
                    for (int i = 75; i < 165; i += 22)
                        g.DrawPolygon(soft, new[] {new Point(240, i), new Point(480-i, 240),
                            new Point(240, 480-i), new Point(i, 240)});
                }
                else
                {
                    g.DrawArc(line, 102, 98, 276, 276, 193, 154);
                    for (int y = 325; y <= 397; y += 18) g.DrawLine(soft, 104, y, 376, y);
                }
                g.DrawString(new[] {"ORBITA", "PRISMA", "AURORA"}[theme], large, Brushes.White, 240, 190, center);
                g.DrawString("LeFiM", small, ink, 240, 267, center);
            }
            media.Add(bitmap, 100, true, angle);
        }
        return media;
    }

    public void Export(string path)
    {
        string temp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), ".lefim-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew))
            {
                if (Path.GetExtension(path).Equals(".gif", StringComparison.OrdinalIgnoreCase))
                {
                    Gif.Encode(this, stream);
                }
                else
                    Frames[0].Save(stream, ImageFormat.Png);
            }
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public void Dispose() { if (DragSource != null) DragSource.Dispose(); foreach (var frame in Frames) frame.Dispose(); }

    static class Gif
    {
        sealed class Frame
        {
            public int X, Y, Width, Height, Bits, Delay, Disposal, Transparent = -1;
            public bool Interlaced;
            public byte[] Palette, Pixels;
        }

        static byte[] Exact(BinaryReader reader, int count)
        {
            byte[] bytes = reader.ReadBytes(count);
            if (bytes.Length != count) throw new EndOfStreamException(Words.Get("GIF incompleto."));
            return bytes;
        }

        static byte[] Blocks(BinaryReader reader)
        {
            using (var data = new MemoryStream())
            {
                int count;
                do
                {
                    count = reader.ReadByte();
                    data.WriteByte((byte)count);
                    var bytes = Exact(reader, count);
                    data.Write(bytes, 0, bytes.Length);
                } while (count != 0);
                return data.ToArray();
            }
        }

        static Frame ReadFrame(BinaryReader reader, int bits, byte[] palette)
        {
            var f = new Frame { X = reader.ReadUInt16(), Y = reader.ReadUInt16(),
                Width = reader.ReadUInt16(), Height = reader.ReadUInt16() };
            CheckSize(f.Width, f.Height);
            int flags = reader.ReadByte();
            f.Interlaced = (flags & 64) != 0;
            f.Bits = (flags & 128) != 0 ? flags & 7 : bits;
            f.Palette = (flags & 128) != 0 ? Exact(reader, 3 << (f.Bits+1)) : palette;
            if (f.Palette == null) throw new FormatException(Words.Get("GIF sem paleta."));
            byte lzw = reader.ReadByte();
            var blocks = Blocks(reader);
            f.Pixels = new byte[blocks.Length + 1];
            f.Pixels[0] = lzw;
            Buffer.BlockCopy(blocks, 0, f.Pixels, 1, blocks.Length);
            return f;
        }

        static List<Frame> Parse(byte[] bytes, out Size size, out Color background)
        {
            using (var reader = new BinaryReader(new MemoryStream(bytes)))
            {
                string signature = Encoding.ASCII.GetString(Exact(reader, 6));
                if (signature != "GIF87a" && signature != "GIF89a") throw new FormatException(Words.Get("GIF inválido."));
                size = new Size(reader.ReadUInt16(), reader.ReadUInt16());
                CheckSize(size.Width, size.Height);
                int flags = reader.ReadByte(), backdrop = reader.ReadByte();
                reader.ReadByte();
                int bits = flags & 7;
                byte[] palette = (flags & 128) != 0 ? Exact(reader, 3 << (bits+1)) : null;
                int offset = 3 * backdrop;
                background = palette != null && offset + 2 < palette.Length
                    ? Color.FromArgb(palette[offset], palette[offset+1], palette[offset+2]) : Color.Transparent;
                int delay = 0, disposal = 0, transparent = -1;
                var frames = new List<Frame>();
                while (true)
                {
                    int marker = reader.ReadByte();
                    if (marker == 0x3B) break;
                    if (marker == 0x21)
                    {
                        int label = reader.ReadByte();
                        var ext = Blocks(reader);
                        if (label == 0xF9 && ext.Length == 6 && ext[0] == 4)
                        {
                            disposal = (ext[1] >> 2) & 7;
                            delay = ext[2] | ext[3] << 8;
                            transparent = (ext[1] & 1) != 0 ? ext[4] : -1;
                        }
                        if (label == 1) { disposal = delay = 0; transparent = -1; }
                        continue;
                    }
                    if (marker == 0) continue;
                    if (marker != 0x2C || frames.Count >= 300)
                        throw new FormatException(Words.Get("GIF inválido ou com mais de 300 quadros."));
                    var f = ReadFrame(reader, bits, palette);
                    if (f.X + f.Width > size.Width || f.Y + f.Height > size.Height)
                        throw new FormatException(Words.Get("Quadro fora das dimensões do GIF."));
                    f.Delay = delay;
                    f.Disposal = disposal;
                    f.Transparent = transparent;
                    frames.Add(f);
                    delay = disposal = 0;
                    transparent = -1;
                }
                if (frames.Count == 0) throw new FormatException(Words.Get("GIF sem quadros."));
                if (frames[0].Transparent >= 0) background = Color.Transparent;
                return frames;
            }
        }

        static void Header(BinaryWriter writer, int w, int h, byte[] palette, int bits)
        {
            writer.Write(Encoding.ASCII.GetBytes("GIF89a"));
            writer.Write((ushort)w);
            writer.Write((ushort)h);
            writer.Write((byte)(palette == null ? 0x70 : 0xF0 | bits));
            writer.Write((ushort)0);
            if (palette != null) writer.Write(palette);
        }

        static void WriteFrame(BinaryWriter writer, Frame frame, bool localPalette)
        {
            writer.Write((byte)0x21);
            writer.Write((byte)0xF9);
            writer.Write((byte)4);
            writer.Write((byte)((frame.Disposal << 2) | (frame.Transparent >= 0 ? 1 : 0)));
            writer.Write((ushort)frame.Delay);
            writer.Write((byte)Math.Max(0, frame.Transparent));
            writer.Write((byte)0);
            writer.Write((byte)0x2C);
            writer.Write(0);
            writer.Write((ushort)frame.Width);
            writer.Write((ushort)frame.Height);
            writer.Write((byte)((frame.Interlaced ? 64 : 0) | (localPalette ? 128 | frame.Bits : 0)));
            if (localPalette) writer.Write(frame.Palette);
            writer.Write(frame.Pixels);
        }

        public static Media Decode(byte[] bytes, bool fill, int angle, float zoom = 1, float x = 0, float y = 0)
        {
            Size size;
            Color background;
            var frames = Parse(bytes, out size, out background);
            var media = new Media(size);
            try
            {
                using (var canvas = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(canvas)) g.Clear(background);
                    foreach (var frame in frames)
                    using (var restore = frame.Disposal == 3 ? (Bitmap)canvas.Clone() : null)
                    using (var stream = new MemoryStream())
                    {
                        var writer = new BinaryWriter(stream);
                        Header(writer, frame.Width, frame.Height, frame.Palette, frame.Bits);
                        WriteFrame(writer, frame, false);
                        writer.Write((byte)0x3B);
                        stream.Position = 0;
                        using (var image = Image.FromStream(stream, false, true))
                        using (var g = Graphics.FromImage(canvas))
                            g.DrawImageUnscaled(image, frame.X, frame.Y);
                        media.Add(canvas, frame.Delay <= 1 ? 100 : frame.Delay * 10, fill, angle, zoom, x, y);
                        using (var g = Graphics.FromImage(canvas))
                        {
                            g.CompositingMode = CompositingMode.SourceCopy;
                            if (restore != null) g.DrawImageUnscaled(restore, 0, 0);
                            else if (frame.Disposal == 2)
                                using (var brush = new SolidBrush(frame.Transparent >= 0 ? Color.Transparent : background))
                                    g.FillRectangle(brush, frame.X, frame.Y, frame.Width, frame.Height);
                        }
                    }
                }
                return media;
            }
            catch { media.Dispose(); throw; }
        }

        public static void Encode(Media media, Stream stream)
        {
            var writer = new BinaryWriter(stream);
            Header(writer, Size, Size, null, 0);
            writer.Write(new byte[] { 0x21, 0xFF, 11 });
            writer.Write(Encoding.ASCII.GetBytes("NETSCAPE2.0"));
            writer.Write(new byte[] { 3, 1, 0, 0, 0 });
            for (int i = 0; i < media.Frames.Count; i++)
                using (var frameStream = new MemoryStream())
                {
                    media.Frames[i].Save(frameStream, ImageFormat.Gif);
                    Size ignoredSize;
                    Color ignoredBackground;
                    var frame = Parse(frameStream.ToArray(), out ignoredSize, out ignoredBackground)[0];
                    frame.Delay = media.Delays[i] / 10;
                    frame.Disposal = 1;
                    frame.Transparent = -1;
                    WriteFrame(writer, frame, true);
                }
            writer.Write((byte)0x3B);
            writer.Flush();
        }
    }
}

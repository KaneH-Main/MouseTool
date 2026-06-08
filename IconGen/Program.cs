using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace IconGen;

// 一次性图标生成器：渲染一个圆角渐变底 + 白色光标 + 点击波纹的图标，
// 输出多尺寸 PNG 组成的 app.ico。
internal static class Program
{
    private static void Main(string[] args)
    {
        string outPath = args.Length > 0
            ? args[0]
            : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "app.ico");
        outPath = Path.GetFullPath(outPath);

        int[] sizes = { 256, 128, 64, 48, 32, 16 };
        var dibs = sizes.Select(RenderDib).ToArray();
        WriteIco(outPath, sizes, dibs);
        Console.WriteLine($"图标已生成: {outPath}");
    }

    private static Bitmap Render(int s)
    {
        var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            // 圆角渐变背景
            var rect = new RectangleF(s * 0.05f, s * 0.05f, s * 0.90f, s * 0.90f);
            float radius = s * 0.24f;
            using (var path = RoundedRect(rect, radius))
            using (var brush = new LinearGradientBrush(
                       new PointF(rect.Left, rect.Top),
                       new PointF(rect.Right, rect.Bottom),
                       ColorTranslator.FromHtml("#5B8DEF"),
                       ColorTranslator.FromHtml("#7B5BE8")))
            {
                g.FillPath(brush, path);

                // 顶部柔光高光
                using var glossBrush = new LinearGradientBrush(
                    new PointF(rect.Left, rect.Top),
                    new PointF(rect.Left, rect.Top + rect.Height * 0.55f),
                    Color.FromArgb(60, 255, 255, 255),
                    Color.FromArgb(0, 255, 255, 255));
                using var glossPath = RoundedRect(
                    new RectangleF(rect.Left, rect.Top, rect.Width, rect.Height * 0.55f), radius);
                g.FillPath(glossBrush, glossPath);
            }

            // 点击波纹（以光标点击点为中心的三段同心弧）
            float cx = s * 0.62f, cy = s * 0.62f;
            using (var ripplePen = new Pen(Color.FromArgb(150, 255, 255, 255), s * 0.022f))
            {
                ripplePen.StartCap = LineCap.Round;
                ripplePen.EndCap = LineCap.Round;
                DrawArc(g, ripplePen, cx, cy, s * 0.16f, -20, 90);
                using var pen2 = new Pen(Color.FromArgb(95, 255, 255, 255), s * 0.020f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
                DrawArc(g, pen2, cx, cy, s * 0.24f, -20, 90);
                using var pen3 = new Pen(Color.FromArgb(55, 255, 255, 255), s * 0.018f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
                DrawArc(g, pen3, cx, cy, s * 0.32f, -20, 90);
            }

            // 白色光标箭头（指向右下的点击点）
            DrawCursor(g, s);
        }

        return bmp;
    }

    // 渲染为 ICO 内嵌的 32bpp BMP/DIB（BITMAPINFOHEADER + 自下而上 BGRA + AND 掩码）。
    private static byte[] RenderDib(int s)
    {
        using var bmp = Render(s);
        var data = bmp.LockBits(new Rectangle(0, 0, s, s),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        var xor = new byte[s * s * 4];
        try
        {
            // 自下而上逐行拷贝
            for (int y = 0; y < s; y++)
            {
                IntPtr src = data.Scan0 + (s - 1 - y) * data.Stride;
                System.Runtime.InteropServices.Marshal.Copy(src, xor, y * s * 4, s * 4);
            }
        }
        finally
        {
            bmp.UnlockBits(data);
        }

        int maskRow = ((s + 31) / 32) * 4; // AND 掩码每行按 4 字节对齐
        var andMask = new byte[maskRow * s]; // 全 0 = 全不透明，由 alpha 决定显示

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        // BITMAPINFOHEADER
        bw.Write(40);              // biSize
        bw.Write(s);               // biWidth
        bw.Write(s * 2);           // biHeight = XOR + AND
        bw.Write((short)1);        // biPlanes
        bw.Write((short)32);       // biBitCount
        bw.Write(0);               // biCompression = BI_RGB
        bw.Write(0);               // biSizeImage
        bw.Write(0);               // biXPelsPerMeter
        bw.Write(0);               // biYPelsPerMeter
        bw.Write(0);               // biClrUsed
        bw.Write(0);               // biClrImportant
        bw.Write(xor);
        bw.Write(andMask);
        return ms.ToArray();
    }

    private static void DrawCursor(Graphics g, int s)
    {
        // 经典箭头，归一化坐标（自有包围盒），指向左上角的尖。
        PointF[] norm =
        {
            new(0.10f, 0.00f),
            new(0.10f, 0.78f),
            new(0.30f, 0.60f),
            new(0.45f, 0.96f),
            new(0.57f, 0.90f),
            new(0.42f, 0.55f),
            new(0.64f, 0.55f),
        };

        float scale = s * 0.42f;
        float ox = s * 0.24f, oy = s * 0.20f;
        var pts = norm.Select(p => new PointF(ox + p.X * scale, oy + p.Y * scale)).ToArray();

        using var shape = new GraphicsPath();
        shape.AddPolygon(pts);

        // 阴影
        using (var shadow = new GraphicsPath())
        {
            float d = s * 0.02f;
            shadow.AddPolygon(pts.Select(p => new PointF(p.X + d, p.Y + d)).ToArray());
            using var sb = new PathGradientBrush(shadow) { CenterColor = Color.FromArgb(70, 0, 0, 0) };
            sb.SurroundColors = new[] { Color.FromArgb(0, 0, 0, 0) };
            g.FillPath(sb, shadow);
        }

        using var fill = new SolidBrush(Color.White);
        g.FillPath(fill, shape);
        using var outline = new Pen(Color.FromArgb(40, 40, 60, 120), s * 0.014f) { LineJoin = LineJoin.Round };
        g.DrawPath(outline, shape);
    }

    private static void DrawArc(Graphics g, Pen pen, float cx, float cy, float r, float start, float sweep)
        => g.DrawArc(pen, cx - r, cy - r, r * 2, r * 2, start, sweep);

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void WriteIco(string path, int[] sizes, byte[][] images)
    {
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);

        bw.Write((short)0);             // reserved
        bw.Write((short)1);             // type = icon
        bw.Write((short)sizes.Length);  // count

        int offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            byte dim = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
            bw.Write(dim);              // width
            bw.Write(dim);              // height
            bw.Write((byte)0);          // palette
            bw.Write((byte)0);          // reserved
            bw.Write((short)1);         // color planes
            bw.Write((short)32);        // bits per pixel
            bw.Write(images[i].Length); // size of image data
            bw.Write(offset);           // offset
            offset += images[i].Length;
        }

        foreach (var img in images)
            bw.Write(img);
    }
}

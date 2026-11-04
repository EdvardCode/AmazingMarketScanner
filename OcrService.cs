using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using Tesseract;

namespace com.edvardcode.AmazingMarketScanner;

public sealed class OcrService : System.IDisposable
{
    private readonly TesseractEngine _engine = new("tessdata", "rus+eng", EngineMode.Default);

    public string Recognize(Bitmap crop, int scale = 4, string? whitelist = null,
                            PageSegMode psm = PageSegMode.SingleBlock)
    {
        using var bin = BinarizeAuto(crop, scale);
        return Process(bin, whitelist, psm);
    }

    public string RecognizePriceGreen(Bitmap pillCrop)
    {
        int cut = 6;
        if (pillCrop.Width > cut * 2 + 10 && pillCrop.Height > cut * 2 + 6)
        {
            using var inner = pillCrop.Clone(
                new Rectangle(cut, cut, pillCrop.Width - cut * 2, pillCrop.Height - cut * 2),
                pillCrop.PixelFormat);
            using var mask = GreenMask(inner);
            using var big = Upscale(mask, 4);
            return Process(big, "0123456789 ", PageSegMode.SingleLine);
        }

        using var fallback = Upscale(BinarizeAuto(pillCrop, 4), 1);
        return Process(fallback, "0123456789 ", PageSegMode.SingleLine);
    }

    private string Process(Bitmap bin, string? whitelist, PageSegMode psm)
    {
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            bin.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            bytes = ms.ToArray();
        }
        using var pix = Pix.LoadFromMemory(bytes);

        if (whitelist != null) _engine.SetVariable("tessedit_char_whitelist", whitelist);
        else _engine.SetVariable("tessedit_char_whitelist", "");

        using var page = _engine.Process(pix, psm);
        return page.GetText();
    }

    private static Bitmap GreenMask(Bitmap src)
    {
        var bmp = new Bitmap(src.Width, src.Height);
        for (int y = 0; y < src.Height; y++)
            for (int x = 0; x < src.Width; x++)
            {
                var c = src.GetPixel(x, y);
                bool text = c.G > 130 && c.G > c.R + 40 && c.G > c.B + 40;
                bmp.SetPixel(x, y, text ? Color.Black : Color.White);
            }
        return bmp;
    }

    private static Bitmap Upscale(Bitmap src, int scale)
    {
        if (scale <= 1) return new Bitmap(src);
        var big = new Bitmap(src.Width * scale, src.Height * scale);
        using (var g = Graphics.FromImage(big))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, big.Width, big.Height);
        }
        for (int y = 0; y < big.Height; y++)
            for (int x = 0; x < big.Width; x++)
            {
                var c = big.GetPixel(x, y);
                byte v = (byte)((c.R + c.G + c.B) / 3);
                big.SetPixel(x, y, v < 128 ? Color.Black : Color.White);
            }
        return big;
    }

    private static Bitmap BinarizeAuto(Bitmap src, int scale)
    {
        var big = new Bitmap(src.Width * scale, src.Height * scale);
        using (var g = Graphics.FromImage(big))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, big.Width, big.Height);
        }

        var outBmp = new Bitmap(big.Width, big.Height);
        int bright = 0, total = big.Width * big.Height;
        var gray = new byte[total];

        for (int y = 0; y < big.Height; y++)
            for (int x = 0; x < big.Width; x++)
            {
                var c = big.GetPixel(x, y);
                byte v = (byte)((c.R + c.G + c.B) / 3);
                gray[y * big.Width + x] = v;
                if (v > 128) bright++;
            }
        bool textIsBright = bright < total / 2;

        for (int y = 0; y < big.Height; y++)
            for (int x = 0; x < big.Width; x++)
            {
                byte v = gray[y * big.Width + x];
                bool isText = textIsBright ? v > 128 : v <= 128;
                outBmp.SetPixel(x, y, isText ? Color.Black : Color.White);
            }
        big.Dispose();
        return outBmp;
    }

    public void Dispose() => _engine.Dispose();
}
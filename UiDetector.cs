using System.Drawing;
using System.Drawing.Imaging;

namespace com.edvardcode.AmazingMarketScanner;

public static class UiDetector
{
    public record Pixels(byte[] Data, int Stride, int Width, int Height);

    public static Pixels Lock(Bitmap bmp)
    {
        var d = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var buf = new byte[Math.Abs(d.Stride) * d.Height];
        System.Runtime.InteropServices.Marshal.Copy(d.Scan0, buf, 0, buf.Length);
        bmp.UnlockBits(d);
        return new Pixels(buf, d.Stride, bmp.Width, bmp.Height);
    }

    private static (byte R, byte G, byte B) At(Pixels p, int x, int y)
    {
        int i = y * p.Stride + x * 4;
        return (p.Data[i + 2], p.Data[i + 1], p.Data[i]);
    }

    public static List<Rectangle> FindPricePills(Pixels p)
    {
        var mask = new bool[p.Width * p.Height];
        for (int y = 0; y < p.Height; y++)
            for (int x = 0; x < p.Width; x++)
            {
                var (r, g, b) = At(p, x, y);
                bool greenText = g > 120 && g > r + 40 && g > b + 40;
                bool greenBg = g >= 40 && g <= 150 && g > r + 12 && g > b + 12;
                mask[y * p.Width + x] = greenText || greenBg;
            }

        Dilate(mask, p.Width, p.Height, 6, 2); 

        return ConnectedComponents(mask, p.Width, p.Height)
            .Where(r => r.Width >= 30 && r.Width <= 500 && r.Height >= 12 && r.Height <= 70)
            .OrderBy(r => r.Y / 40).ThenBy(r => r.X)
            .ToList();
    }

    public static Rectangle? FindNameArea(Pixels p, Rectangle pill)
    {
        int top = Math.Max(0, pill.Top - 95), bottom = pill.Top - 6;
        int left = Math.Max(0, pill.Left - 12), right = Math.Min(p.Width, pill.Left + 280);

        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = top; y < bottom; y++)
            for (int x = left; x < right; x++)
            {
                var (r, g, b) = At(p, x, y);
                if (r > 170 && g > 170 && b > 170)
                {
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
            }
        if (maxX < 0 || maxY - minY < 6) return null;
        return Rectangle.FromLTRB(minX - 4, minY - 4, maxX + 4, maxY + 4);
    }

    private static void Dilate(bool[] mask, int w, int h, int kx, int ky)
    {
        var src = (bool[])mask.Clone();
        Array.Clear(mask, 0, mask.Length);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!src[y * w + x]) continue;
                for (int dy = -ky; dy <= ky; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= h) continue;
                    int row = ny * w;
                    for (int dx = -kx; dx <= kx; dx++)
                    {
                        int nx = x + dx;
                        if (nx < 0 || nx >= w) continue;
                        mask[row + nx] = true;
                    }
                }
            }
    }

    private static List<Rectangle> ConnectedComponents(bool[] mask, int w, int h)
    {
        var visited = new bool[w * h];
        var result = new List<Rectangle>();
        var stack = new Stack<int>();

        for (int start = 0; start < mask.Length; start++)
        {
            if (!mask[start] || visited[start]) continue;
            stack.Push(start);
            visited[start] = true;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1, count = 0;

            while (stack.Count > 0)
            {
                int i = stack.Pop();
                int x = i % w, y = i / w;
                count++;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);

                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int ni = ny * w + nx;
                        if (mask[ni] && !visited[ni]) { visited[ni] = true; stack.Push(ni); }
                    }
            }
            if (count > 200) result.Add(Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1));
        }
        return result;
    }
}
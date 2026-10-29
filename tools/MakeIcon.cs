using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class MakeIconProgram
{
    public static int Main(string[] args)
    {
        string path = args.Length > 0
            ? args[0]
            : Path.Combine("Assets", "app.ico");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        WriteIco(path);
        Console.WriteLine(path);
        return 0;
    }

    private static void WriteIco(string path)
    {
        int[] sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
        var pngs = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++)
        {
            using var bmp = Draw(sizes[i]);
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            pngs[i] = ms.ToArray();
        }

        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        bw.Write((ushort)0);
        bw.Write((ushort)1);
        bw.Write((ushort)sizes.Length);

        int offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            int s = sizes[i];
            bw.Write((byte)(s >= 256 ? 0 : s));
            bw.Write((byte)(s >= 256 ? 0 : s));
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((ushort)1);
            bw.Write((ushort)32);
            bw.Write(pngs[i].Length);
            bw.Write(offset);
            offset += pngs[i].Length;
        }

        foreach (byte[] png in pngs)
        {
            bw.Write(png);
        }
    }

    private static Bitmap Draw(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        float pad = Math.Max(1f, size / 16f);
        using var bg = new SolidBrush(Color.FromArgb(255, 18, 22, 26));
        using var teal = new SolidBrush(Color.FromArgb(255, 47, 158, 140));
        using var core = new SolidBrush(Color.FromArgb(255, 22, 28, 32));

        g.FillEllipse(bg, 0.5f, 0.5f, size - 1.5f, size - 1.5f);
        float outer = pad * 1.6f;
        g.FillEllipse(teal, outer, outer, size - outer * 2, size - outer * 2);
        float shift = size * 0.22f;
        float inner = outer + size * 0.06f;
        g.FillEllipse(
            core,
            inner + shift,
            inner - size * 0.05f,
            size - inner * 2 - shift * 0.35f,
            size - inner * 2 + size * 0.04f);
        return bmp;
    }
}

// Dev utility: converts an image into a 256x256 PNG-in-ICO app icon.
// Usage: MakeIco.exe <input image> <output .ico>
// Compiled with the .NET Framework in-box csc.exe (see build.ps1), needs System.Drawing.
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

class MakeIco
{
    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("usage: MakeIco <input image> <output .ico>");
            return 2;
        }

        using (Bitmap src = new Bitmap(args[0]))
        {
            Color corner = src.GetPixel(0, 0);
            Console.WriteLine("source: " + src.Width + "x" + src.Height + " cornerAlpha=" + corner.A);

            // Center-crop to square, scale down to 256x256 on a transparent canvas
            using (Bitmap dst = new Bitmap(256, 256, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                int size = Math.Min(src.Width, src.Height);
                Rectangle srcRect = new Rectangle(
                    (src.Width - size) / 2, (src.Height - size) / 2, size, size);
                g.DrawImage(src, new Rectangle(0, 0, 256, 256), srcRect, GraphicsUnit.Pixel);

                using (MemoryStream ms = new MemoryStream())
                {
                    dst.Save(ms, ImageFormat.Png);
                    byte[] png = ms.ToArray();

                    // Wrap the PNG in an ICO container (single 256px PNG entry, Vista+)
                    using (FileStream fs = File.Create(args[1]))
                    using (BinaryWriter w = new BinaryWriter(fs))
                    {
                        w.Write((ushort)0);          // reserved
                        w.Write((ushort)1);          // type: icon
                        w.Write((ushort)1);          // image count
                        w.Write((byte)0);            // width (0 = 256)
                        w.Write((byte)0);            // height (0 = 256)
                        w.Write((byte)0);            // palette size
                        w.Write((byte)0);            // reserved
                        w.Write((ushort)1);          // color planes
                        w.Write((ushort)32);         // bits per pixel
                        w.Write((uint)png.Length);   // data size
                        w.Write((uint)22);           // data offset (6 + 16)
                        w.Write(png);
                    }
                    Console.WriteLine("ico written, png payload = " + png.Length + " bytes");
                }
            }
        }
        return 0;
    }
}

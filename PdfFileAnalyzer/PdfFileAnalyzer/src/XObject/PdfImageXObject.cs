using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using PdfFileAnalyzer;
using PdfFileAnalyzer.Element;

namespace PdfFileAnalyzer.XObject
{
    /// <summary>
    /// Конкретная реализация для /XObject /Image.
    /// Декодирует поток, применяет фильтры, отдаёт System.Drawing.Bitmap.
    /// </summary>
    public class PdfImageXObject : PdfXObject
    {
        public PdfImageXObject(PdfIndirectObject obj, PdfReader reader)
            : base(obj, reader) { }

        /// <summary>
        /// Возвращает Bitmap, декодировав и распаковав поток.
        /// Поддерживает JPEG (/DCTDecode) и «сырые» потоки DeviceGray/DeviceRGB/DeviceCMYK с BPC=8.
        /// </summary>
        public Bitmap GetBitmap()
        {
            var dict = _obj.Dictionary;

            // 1) читаем и декомпрессим байты
            byte[] data = _obj.ReadStream();
            data = _obj.DecompressStream(data);

            // 2) проверяем наличие DCTDecode
            bool isJpeg = false;
            var filterVal = dict.FindValue("/Filter");
            if (filterVal.IsName && filterVal.ToName == "/DCTDecode")
            {
                isJpeg = true;
            }
            else if (filterVal.IsArray)
            {
                var arr = filterVal.ToArrayItems;
                for (int i = 0; i < arr.Length; i++)
                {
                    var f = arr[i];
                    if (f.IsName && f.ToName == "/DCTDecode")
                    {
                        isJpeg = true;
                        break;
                    }
                }
            }

            if (isJpeg)
            {
                using (var ms = new MemoryStream(data))
                {
                    return new Bitmap(ms);
                }
            }

            // 3) «сырые» пиксели
            int width, height;
            if (!dict.FindValue("/Width").GetInteger(out width) ||
                !dict.FindValue("/Height").GetInteger(out height))
            {
                throw new ArgumentException("Image width/height missing");
            }

            int bpc;
            if (!dict.FindValue("/BitsPerComponent").GetInteger(out bpc))
                bpc = 8;
            if (bpc != 8)
                throw new NotSupportedException("BitsPerComponent=" + bpc + " not supported");

            // 4) цветовое пространство
            string cs = dict.FindValue("/ColorSpace").ToName ?? "/DeviceGray";
            int comps;
            switch (cs)
            {
                case "/DeviceGray":
                    comps = 1;
                    break;
                case "/DeviceRGB":
                    comps = 3;
                    break;
                case "/DeviceCMYK":
                    comps = 4;
                    break;
                default:
                    throw new NotSupportedException("ColorSpace " + cs + " not supported");
            }

            // 5) выбираем PixelFormat
            PixelFormat pf;
            switch (comps)
            {
                case 1:
                    pf = PixelFormat.Format8bppIndexed;
                    break;
                case 3:
                    pf = PixelFormat.Format24bppRgb;
                    break;
                case 4:
                    pf = PixelFormat.Format32bppArgb;
                    break;
                default:
                    throw new InvalidOperationException();
            }

            var bmp = new Bitmap(width, height, pf);

            // если DeviceGray, задаём палитру градаций серого
            if (comps == 1)
            {
                ColorPalette pal = bmp.Palette;
                for (int i = 0; i < 256; i++)
                    pal.Entries[i] = Color.FromArgb(i, i, i);
                bmp.Palette = pal;
            }

            // 6) копируем байты в bitmap
            var rect = new Rectangle(0, 0, width, height);
            BitmapData bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, pf);
            try
            {
                int stride = bd.Stride;
                IntPtr dst = bd.Scan0;
                for (int y = 0; y < height; y++)
                {
                    int srcOff = y * width * comps;
                    IntPtr rowPtr = IntPtr.Add(dst, y * stride);
                    System.Runtime.InteropServices.Marshal.Copy(data, srcOff, rowPtr, width * comps);
                }
            }
            finally
            {
                bmp.UnlockBits(bd);
            }

            return bmp;
        }

        /// <summary>
        /// Для обратной совместимости: возвращает Bitmap как Image.
        /// </summary>
        public Image GetImage()
        {
            return GetBitmap();
        }

        public override string Summary()
        {
            Bitmap img = GetBitmap();
            var sb = new StringBuilder(base.Summary());
            sb.AppendLine("Image size: " + img.Width + "×" + img.Height);
            return sb.ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Media.Imaging;
using System.Drawing;

namespace RawBayer2DNG
{
    class Helpers
    {
        static public BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }

        static public int getStride(int byteWidth)
        {
        
            int modulo4 = byteWidth % 4;
            return byteWidth + (modulo4 > 0 ? (4 - modulo4) : 0);
        }

        static public byte[] PadLines(byte[] bytes, int height, int width,int newStride,int bytesPerPixel)
        {
            //The old and new offsets could be passed through parameters,
            //but I hardcoded them here as a sample.
            //var newStride = width + (4-width%4);
            var newBytes = new byte[newStride * height];
            for (var i = 0; i < height; i++)
                Buffer.BlockCopy(bytes, width *bytesPerPixel* i, newBytes, newStride * i, width*bytesPerPixel);
            return newBytes;
        }
    }
}

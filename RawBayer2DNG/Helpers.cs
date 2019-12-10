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

        internal static byte[] DrawPreview(byte[] buff, int height, int width, int srcHeight, int srcWidth, int newStride, int byteDepth, int subsample = 4, bool previewGamma = true)
        {

            var newBytes = new byte[newStride * height];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    double fullValue = (double)BitConverter.ToUInt16(buff,y * subsample * srcWidth * byteDepth + x * subsample * byteDepth)/(double)UInt16.MaxValue;
                    if(previewGamma) fullValue = fullValue > 0.0031308 ? 1.055 * Math.Pow(fullValue, 1 / 2.4) - 0.055 : 12.92 * fullValue;
                    fullValue *= 256;
                    newBytes[y * newStride + x * 3] =(byte)(int)(fullValue);
                    newBytes[y * newStride + x * 3+1] =(byte)(int)(fullValue);
                    newBytes[y * newStride + x * 3+2] =(byte)(int)(fullValue);
                }
            }
            return newBytes;
        }


        internal static byte[] DrawBayerPreview(byte[] buff, int height, int width, int srcHeight, int srcWidth, int newStride, int byteDepth, int subsample, bool previewGamma,byte[,] bayerPattern)
        {

            byte[] newBytes = new byte[newStride * height];


            //input bayer pattern: 0=Red, 1=Green,   2=Blue,
            // output mapping: BGR
            byte[] bayerSubstitution = { 2, 1, 0 };
            byte[,] mappedBayer = { { bayerSubstitution[bayerPattern[0,0]],bayerSubstitution[bayerPattern[0,1]] },
                {bayerSubstitution[bayerPattern[1,0]],bayerSubstitution[bayerPattern[1,1]] } };


            //
            // Bayer interpreted like this (Variable names)
            // A B
            // C D
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    double fullValueA = (double)BitConverter.ToUInt16(buff, y * subsample * srcWidth * byteDepth + x * subsample * byteDepth) / (double)UInt16.MaxValue;
                    double fullValueB = (double)BitConverter.ToUInt16(buff, y * subsample * srcWidth * byteDepth + (x * subsample + 1) * byteDepth) / (double)UInt16.MaxValue;
                    double fullValueC = (double)BitConverter.ToUInt16(buff, (y * subsample + 1) * srcWidth * byteDepth + x * subsample * byteDepth) / (double)UInt16.MaxValue;
                    double fullValueD = (double)BitConverter.ToUInt16(buff, (y * subsample + 1) * srcWidth * byteDepth + (x * subsample + 1) * byteDepth) / (double)UInt16.MaxValue;
                    if (previewGamma)
                    {
                        fullValueA = fullValueA > 0.0031308 ? 1.055 * Math.Pow(fullValueA, 1 / 2.4) - 0.055 : 12.92 * fullValueA;
                        fullValueB = fullValueB > 0.0031308 ? 1.055 * Math.Pow(fullValueB, 1 / 2.4) - 0.055 : 12.92 * fullValueB;
                        fullValueC = fullValueC > 0.0031308 ? 1.055 * Math.Pow(fullValueC, 1 / 2.4) - 0.055 : 12.92 * fullValueC;
                        fullValueD = fullValueD > 0.0031308 ? 1.055 * Math.Pow(fullValueD, 1 / 2.4) - 0.055 : 12.92 * fullValueD;
                    }
                    fullValueA *= 256;
                    fullValueB *= 256;
                    fullValueC *= 256;
                    fullValueD *= 256;


                    newBytes[y * newStride + x * 3 + mappedBayer[0, 0]] = (byte)(int)(fullValueA);
                    newBytes[y * newStride + x * 3 + mappedBayer[0, 1]] = (byte)(int)(fullValueB);
                    newBytes[y * newStride + x * 3 + mappedBayer[1, 0]] = (byte)(int)(fullValueC);
                    newBytes[y * newStride + x * 3 + mappedBayer[1, 1]] = (byte)(int)(fullValueD);
                }
            }
            return newBytes;
        }
    }
}

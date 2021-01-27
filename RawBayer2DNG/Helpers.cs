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
    static class Helpers
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


        // Source: https://stackoverflow.com/a/42078952
        public static unsafe TDest ReinterpretCast<TSource, TDest>(TSource source)
        {
            var sourceRef = __makeref(source);
            var dest = default(TDest);
            var destRef = __makeref(dest);
            *(IntPtr*)&destRef = *(IntPtr*)&sourceRef;
            return __refvalue(destRef, TDest);
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

        // 
        internal static byte[] DrawPreview(byte[] buff, int height, int width, int srcHeight, int srcWidth, int newStride, int byteDepth, double[] RGBAmplify, int subsample = 4, bool previewGamma = true)
        {

            var newBytes = new byte[newStride * height];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    double fullValue = (double)BitConverter.ToUInt16(buff,y * subsample * srcWidth * byteDepth + x * subsample * byteDepth)/(double)UInt16.MaxValue;
                    if(previewGamma) fullValue = fullValue > 0.0031308 ? 1.055 * Math.Pow(fullValue, 1 / 2.4) - 0.055 : 12.92 * fullValue;
                    fullValue *= 255;
                    newBytes[y * newStride + x * 3] =(byte)(int)(fullValue);
                    newBytes[y * newStride + x * 3+1] =(byte)(int)(fullValue);
                    newBytes[y * newStride + x * 3+2] =(byte)(int)(fullValue);
                }
            }
            return newBytes;
        }

        static public Bitmap ResizeBitmapNN(Bitmap sourceBMP, int width, int height)
        {
            Bitmap result = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(sourceBMP, 0, 0, width, height);
            }
            return result;
        }

        public static byte[] drawRectangle(byte[] image, int width, int height, Rectangle rectangle)
        {
            // Top line
            int bottom = rectangle.Y + rectangle.Height;
            int i;
            for(i=rectangle.X; i < rectangle.X+rectangle.Width; i++)
            {
                image[rectangle.Y * width * 3 + i * 3] = 255;
                image[rectangle.Y * width * 3 + i * 3+1] = 255;
                image[rectangle.Y * width * 3 + i * 3+2] = 255;
            }
            //Bottom line
            for(i=rectangle.X; i < rectangle.X+rectangle.Width; i++)
            {
                image[(bottom-1) * width * 3 + i * 3] = 255;
                image[(bottom - 1) * width * 3 + i * 3+1] = 255;
                image[(bottom - 1) * width * 3 + i * 3+2] = 255;
            }
            //Left line
            for(i=rectangle.Y; i < rectangle.Y+rectangle.Height; i++)
            {
                image[i * width * 3 + rectangle.X * 3] = 255;
                image[i * width * 3 + rectangle.X*3 + 1] = 255;
                image[i * width * 3 + rectangle.X*3 + 2] = 255;
            }
            //Right line
            for(i=rectangle.Y; i < rectangle.Y+rectangle.Height; i++)
            {
                image[i * width * 3 + (rectangle.X+rectangle.Width-1) * 3] = 255;
                image[i * width * 3 + (rectangle.X + rectangle.Width-1) * 3 + 1] = 255;
                image[i * width * 3 + (rectangle.X + rectangle.Width-1) * 3 + 2] = 255;
            }
            return image;
        }

        public static int MinMultipleOfTwo(int input)
        {
            return input % 2 > 0 ? input - 1 : input;
        }


        public static byte[] DrawMagnifier(byte[] source,Rectangle rectangle, int sourceWidth,bool previewGamma, int byteDepth,double[] RGBAmplify,byte[,] bayerPattern)
        {
            byte[] newbytes = new byte[rectangle.Width * rectangle.Height * 3];

            for(int x = 0; x < rectangle.Width; x++)
            {
                for(int y = 0; y < rectangle.Height; y++)
                {
                    //0=Red, 1=Green,   2=Blue
                    byte currentColor = bayerPattern[x % 2, y % 2];
                    double srcData = (double)BitConverter.ToUInt16(source, (rectangle.Y+y)*sourceWidth*byteDepth + (rectangle.X+x)*byteDepth) / (double)UInt16.MaxValue;
                    srcData *= RGBAmplify[currentColor];
                    if (previewGamma) // converts linear values to sRGB
                    {
                        srcData = srcData > 0.0031308 ? 1.055 * Math.Pow(srcData, 1 / 2.4) - 0.055 : 12.92 * srcData;
                    }
                    srcData = srcData * 255;

                    newbytes[y * rectangle.Width * 3 + x * 3] = (byte)(int)srcData;
                    newbytes[y * rectangle.Width * 3 + x * 3+1] = (byte)(int)srcData;
                    newbytes[y * rectangle.Width * 3 + x * 3+2] = (byte)(int)srcData;
                }
            }

            return newbytes;
        }


        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }

        internal static byte[] DrawBayerPreview(byte[] buff, int height, int width, int srcHeight, int srcWidth, int newStride, int byteDepth, int subsample, bool previewGamma,byte[,] bayerPattern,double[] RGBAmplify)
        {

            byte[] newBytes = new byte[newStride * height];


            //input bayer pattern: 0=Red, 1=Green,   2=Blue,
            // output mapping: BGR
            byte[] bayerSubstitution = { 2, 1, 0 };
            byte[,] mappedBayer = { { bayerSubstitution[bayerPattern[0,0]],bayerSubstitution[bayerPattern[0,1]] },
                {bayerSubstitution[bayerPattern[1,0]],bayerSubstitution[bayerPattern[1,1]] } };

            byte[] bayerSubstitutionForAmplify = { 0,1,2 };
            byte[,] mappedBayerForAmplify = { { bayerSubstitutionForAmplify[bayerPattern[0,0]],bayerSubstitutionForAmplify[bayerPattern[0,1]] },
                {bayerSubstitutionForAmplify[bayerPattern[1,0]],bayerSubstitutionForAmplify[bayerPattern[1,1]] } };

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

                    {// RGB amplify
                        fullValueA = fullValueA * RGBAmplify[mappedBayerForAmplify[0, 0]];
                        fullValueB = fullValueB * RGBAmplify[mappedBayerForAmplify[0, 1]];
                        fullValueC = fullValueC * RGBAmplify[mappedBayerForAmplify[1, 0]];
                        fullValueD = fullValueD * RGBAmplify[mappedBayerForAmplify[1, 1]];
                    }
                    if (previewGamma) // converts linear values to sRGB
                    {
                        fullValueA = fullValueA > 0.0031308 ? 1.055 * Math.Pow(fullValueA, 1 / 2.4) - 0.055 : 12.92 * fullValueA;
                        fullValueB = fullValueB > 0.0031308 ? 1.055 * Math.Pow(fullValueB, 1 / 2.4) - 0.055 : 12.92 * fullValueB;
                        fullValueC = fullValueC > 0.0031308 ? 1.055 * Math.Pow(fullValueC, 1 / 2.4) - 0.055 : 12.92 * fullValueC;
                        fullValueD = fullValueD > 0.0031308 ? 1.055 * Math.Pow(fullValueD, 1 / 2.4) - 0.055 : 12.92 * fullValueD;
                    }
                    fullValueA *= 255;
                    fullValueB *= 255;
                    fullValueC *= 255;
                    fullValueD *= 255;


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

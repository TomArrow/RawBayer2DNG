﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Globalization;

namespace RawBayer2DNG
{
    static class Helpers
    {


        public static void streamToFileAndDispose(string path, Stream sourceStream)
        {
            sourceStream.Seek(0, SeekOrigin.Begin);
            using(FileStream fs = new FileStream(path, FileMode.CreateNew))
            {
                sourceStream.CopyTo(fs);
            }
            sourceStream.Close();
            sourceStream.Dispose();
        }

        public static string findUnoccupiedFileName(string filename, string fileEnding, string countSeparator= "_")
        {
            if (File.Exists(filename + fileEnding))
            {
                int index = 2;
                while (File.Exists(filename+countSeparator+index+fileEnding))
                {
                    index++;
                }
                return filename + countSeparator + index + fileEnding;
            } else
            {
                return filename + fileEnding;
            }
        }

        public static string byteArrayToHexString(byte[] data)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in data)
            {

                sb.AppendFormat("{0:x2}", b);
            }
            return sb.ToString();
        }

        public static string byteArrayToString<T>(byte[] input, string separator) where T : unmanaged
        {
            return arrayToString<T>(byteArrayTo<T>(input), separator);
        }

        // Obsolete:
        public static float[] byteArrayToFloats(byte[] input)
        {
            float[] rVal = new float[input.Length / 4];
            for (int i = 0; i < rVal.Length; i++)
            {
                rVal[i] = BitConverter.ToSingle(input, i * 4);
            }
            return rVal;
        }

        // More generic.
        public unsafe static T[] byteArrayTo<T>(byte[] input) where T : unmanaged
        {

            T[] rVal = new T[input.Length / sizeof(T)];
            //for(int i = 0; i < rVal.Length; i++)
            //{
            //    rVal[i] = BitConverter.(input, i * 4);
            //}
            Buffer.BlockCopy(input, 0, rVal, 0, input.Length);
            return rVal;
        }

        public static string arrayToString<T>(T[] input, string separator)
        {
            StringBuilder sb = new StringBuilder();
            int i = 0;
            foreach (T inVal in input)
            {
                if (i++ != 0)
                {

                    sb.Append(separator);
                }
                sb.Append(inVal.ToString());
            }
            return sb.ToString();
        }


        static string numberFormat = "0." + new string('#', 339);

        public static string doubleToString(double number)
        {
            return number.ToString(numberFormat, CultureInfo.InvariantCulture);
        }
        public static string floatToString(float number)
        {
            return number.ToString(numberFormat, CultureInfo.InvariantCulture);
        }
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

        static public byte[] PadLines(byte[] bytes, int height, int width, int newStride, int bytesPerPixel)
        {
            //The old and new offsets could be passed through parameters,
            //but I hardcoded them here as a sample.
            //var newStride = width + (4-width%4);
            var newBytes = new byte[newStride * height];
            for (var i = 0; i < height; i++)
                Buffer.BlockCopy(bytes, width * bytesPerPixel * i, newBytes, newStride * i, width * bytesPerPixel);
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
                    double fullValue = (double)BitConverter.ToUInt16(buff, y * subsample * srcWidth * byteDepth + x * subsample * byteDepth) / (double)UInt16.MaxValue;
                    if (previewGamma) fullValue = fullValue > 0.0031308 ? 1.055 * Math.Pow(fullValue, 1 / 2.4) - 0.055 : 12.92 * fullValue;
                    fullValue *= 255;
                    newBytes[y * newStride + x * 3] = (byte)(int)(fullValue);
                    newBytes[y * newStride + x * 3 + 1] = (byte)(int)(fullValue);
                    newBytes[y * newStride + x * 3 + 2] = (byte)(int)(fullValue);
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
            for (i = rectangle.X; i < rectangle.X + rectangle.Width; i++)
            {
                image[rectangle.Y * width * 3 + i * 3] = 255;
                image[rectangle.Y * width * 3 + i * 3 + 1] = 255;
                image[rectangle.Y * width * 3 + i * 3 + 2] = 255;
            }
            //Bottom line
            for (i = rectangle.X; i < rectangle.X + rectangle.Width; i++)
            {
                image[(bottom - 1) * width * 3 + i * 3] = 255;
                image[(bottom - 1) * width * 3 + i * 3 + 1] = 255;
                image[(bottom - 1) * width * 3 + i * 3 + 2] = 255;
            }
            //Left line
            for (i = rectangle.Y; i < rectangle.Y + rectangle.Height; i++)
            {
                image[i * width * 3 + rectangle.X * 3] = 255;
                image[i * width * 3 + rectangle.X * 3 + 1] = 255;
                image[i * width * 3 + rectangle.X * 3 + 2] = 255;
            }
            //Right line
            for (i = rectangle.Y; i < rectangle.Y + rectangle.Height; i++)
            {
                image[i * width * 3 + (rectangle.X + rectangle.Width - 1) * 3] = 255;
                image[i * width * 3 + (rectangle.X + rectangle.Width - 1) * 3 + 1] = 255;
                image[i * width * 3 + (rectangle.X + rectangle.Width - 1) * 3 + 2] = 255;
            }
            return image;
        }

        public static int MinMultipleOfTwo(int input)
        {
            return input % 2 > 0 ? input - 1 : input;
        }


        public static byte[] DrawMagnifier(byte[] source, Rectangle rectangle, int sourceWidth, bool previewGamma, int byteDepth, double[] RGBAmplify, byte[,] bayerPattern)
        {
            byte[] newbytes = new byte[rectangle.Width * rectangle.Height * 3];

            for (int x = 0; x < rectangle.Width; x++)
            {
                for (int y = 0; y < rectangle.Height; y++)
                {
                    //0=Red, 1=Green,   2=Blue
                    byte currentColor = bayerPattern[x % 2, y % 2];
                    double srcData = (double)BitConverter.ToUInt16(source, (rectangle.Y + y) * sourceWidth * byteDepth + (rectangle.X + x) * byteDepth) / (double)UInt16.MaxValue;
                    srcData *= RGBAmplify[currentColor];
                    if (previewGamma) // converts linear values to sRGB
                    {
                        srcData = srcData > 0.0031308 ? 1.055 * Math.Pow(srcData, 1 / 2.4) - 0.055 : 12.92 * srcData;
                    }
                    srcData = srcData * 255;

                    newbytes[y * rectangle.Width * 3 + x * 3] = (byte)(int)srcData;
                    newbytes[y * rectangle.Width * 3 + x * 3 + 1] = (byte)(int)srcData;
                    newbytes[y * rectangle.Width * 3 + x * 3 + 2] = (byte)(int)srcData;
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

        internal static byte[] DrawScope(byte[] buff, int height, int width, int srcHeight, int srcWidth, int newStride, int byteDepth, int subsample, bool previewGamma, byte[,] bayerPattern, double[] RGBAmplify)
        {

            byte[] newBytes = new byte[newStride * height];

            int paddingH = 10;
            int paddingV = 10;
            int paddingTotal = paddingH * 4 + 1;
            int singleChannelWidth = (int)Math.Floor(0.1 + (((double)width - (double)paddingTotal) / 4.0)); // 4 channels: green is twice, but each is shown separately. The 0.1 is so it doesn't accidentally round down too much. Not sure if C# needs that
            int singleChannelHeight = height - paddingV - paddingV;


            //input bayer pattern: 0=Red, 1=Green,   2=Blue,
            // output mapping: BGR
            byte[] bayerSubstitution = { 2, 1, 0 };
            byte[,] mappedBayer = { { bayerSubstitution[bayerPattern[0,0]],bayerSubstitution[bayerPattern[0,1]] },
                {bayerSubstitution[bayerPattern[1,0]],bayerSubstitution[bayerPattern[1,1]] } };

            byte[] bayerSubstitutionForAmplify = { 0, 1, 2 };
            byte[,] mappedBayerForAmplify = { { bayerSubstitutionForAmplify[bayerPattern[0,0]],bayerSubstitutionForAmplify[bayerPattern[0,1]] },
                {bayerSubstitutionForAmplify[bayerPattern[1,0]],bayerSubstitutionForAmplify[bayerPattern[1,1]] } };


            // Draw gridlines
            int gridLineDistance = 20;
            for (int y = 0 + paddingV; y < singleChannelHeight + paddingV; y += gridLineDistance)
            {
                for (int x = paddingH; x < width - paddingH; x++)
                {

                    newBytes[y * newStride + x * 3 + 0] = 30;
                    newBytes[y * newStride + x * 3 + 1] = 30;
                    newBytes[y * newStride + x * 3 + 2] = 30;
                }
            }
            // Top and bottom line
            for (int x = paddingH; x < width - paddingH; x++)
            {

                newBytes[paddingV * newStride + x * 3 + 0] = 255;
                newBytes[paddingV * newStride + x * 3 + 1] = 255;
                newBytes[paddingV * newStride + x * 3 + 2] = 255;
            }
            for (int x = paddingH; x < width - paddingH; x++)
            {

                newBytes[(paddingV + singleChannelHeight) * newStride + x * 3 + 0] = 255;
                newBytes[(paddingV + singleChannelHeight) * newStride + x * 3 + 1] = 255;
                newBytes[(paddingV + singleChannelHeight) * newStride + x * 3 + 2] = 255;
            }


            double fullValueA, fullValueB, fullValueC, fullValueD;
            double highestValueA = 0, highestValueB = 0, highestValueC = 0, highestValueD = 0;
            int yA, yB, yC, yD, xA, xB, xC, xD, offsetA, offsetB, offsetC, offsetD;
            for (var y = 0; y < srcHeight; y += 2)
            {
                for (var x = 0; x < srcWidth; x += 2)
                {
                    fullValueA = (double)BitConverter.ToUInt16(buff, y * srcWidth * byteDepth + x * byteDepth) / (double)UInt16.MaxValue;
                    fullValueB = (double)BitConverter.ToUInt16(buff, y * srcWidth * byteDepth + (x + 1) * byteDepth) / (double)UInt16.MaxValue;
                    fullValueC = (double)BitConverter.ToUInt16(buff, (y + 1) * srcWidth * byteDepth + x * byteDepth) / (double)UInt16.MaxValue;
                    fullValueD = (double)BitConverter.ToUInt16(buff, (y + 1) * srcWidth * byteDepth + (x + 1) * byteDepth) / (double)UInt16.MaxValue;

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

                    highestValueA = Math.Max(highestValueA, fullValueA);
                    highestValueB = Math.Max(highestValueB, fullValueB);
                    highestValueC = Math.Max(highestValueC, fullValueC);
                    highestValueD = Math.Max(highestValueD, fullValueD);

                    fullValueA = Math.Max(0, Math.Min(1, fullValueA));
                    fullValueB = Math.Max(0, Math.Min(1, fullValueB));
                    fullValueC = Math.Max(0, Math.Min(1, fullValueC));
                    fullValueD = Math.Max(0, Math.Min(1, fullValueD));


                    yA = paddingV + (singleChannelHeight - (int)(fullValueA * (double)(singleChannelHeight - 1)));
                    yB = paddingV + (singleChannelHeight - (int)(fullValueB * (double)(singleChannelHeight - 1)));
                    yC = paddingV + (singleChannelHeight - (int)(fullValueC * (double)(singleChannelHeight - 1)));
                    yD = paddingV + (singleChannelHeight - (int)(fullValueD * (double)(singleChannelHeight - 1)));

                    xA = (int)(paddingH + ((double)x / (double)srcWidth) * (double)singleChannelWidth);
                    xB = (int)(paddingH + (paddingH + singleChannelWidth * 1) + ((double)x / (double)srcWidth) * (double)singleChannelWidth);
                    xC = (int)(paddingH + (paddingH + singleChannelWidth * 2) + ((double)x / (double)srcWidth) * (double)singleChannelWidth);
                    xD = (int)(paddingH + (paddingH + singleChannelWidth * 3) + ((double)x / (double)srcWidth) * (double)singleChannelWidth);

                    offsetA = yA * newStride + xA * 3 + mappedBayer[0, 0];
                    offsetB = yB * newStride + xB * 3 + mappedBayer[0, 1];
                    offsetC = yC * newStride + xC * 3 + mappedBayer[1, 0];
                    offsetD = yD * newStride + xD * 3 + mappedBayer[1, 1];

                    newBytes[offsetA] = (byte)Math.Min(255, newBytes[offsetA] == 0 ? 1 : newBytes[offsetA] + 1);
                    newBytes[offsetB] = (byte)Math.Min(255, newBytes[offsetB] == 0 ? 1 : newBytes[offsetB] + 1);
                    newBytes[offsetC] = (byte)Math.Min(255, newBytes[offsetC] == 0 ? 1 : newBytes[offsetC] + 1);
                    newBytes[offsetD] = (byte)Math.Min(255, newBytes[offsetD] == 0 ? 1 : newBytes[offsetD] + 1);
                }
            }

            yA = paddingV + (singleChannelHeight - (int)(highestValueA * (double)(singleChannelHeight - 1)));
            yB = paddingV + (singleChannelHeight - (int)(highestValueB * (double)(singleChannelHeight - 1)));
            yC = paddingV + (singleChannelHeight - (int)(highestValueC * (double)(singleChannelHeight - 1)));
            yD = paddingV + (singleChannelHeight - (int)(highestValueD * (double)(singleChannelHeight - 1)));

            yA = Math.Max(0,Math.Min(height-1,yA)); 
            yB = Math.Max(0,Math.Min(height-1,yB)); 
            yC = Math.Max(0,Math.Min(height-1,yC)); 
            yD = Math.Max(0,Math.Min(height-1,yD)); 

            // Draw peak values
            for (var x = 0; x < width; x ++)
            {
                offsetA = yA * newStride + x * 3 + mappedBayer[0, 0];
                offsetB = yB * newStride + x * 3 + mappedBayer[0, 1];
                offsetC = yC * newStride + x * 3 + mappedBayer[1, 0];
                offsetD = yD * newStride + x * 3 + mappedBayer[1, 1];

                newBytes[offsetA] = 255;
                newBytes[offsetB] = 255;
                newBytes[offsetC] = 255;
                newBytes[offsetD] = 255;
            }

            // Apply a bit of gamma.
            for (int abc=0;abc<newBytes.Length;abc++)
            {
                newBytes[abc] = (byte)(Math.Pow(((double)newBytes[abc])/255.0,1.0/2.4)*255.0);
            }
            return newBytes;
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
            double fullValueA, fullValueB, fullValueC, fullValueD;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    fullValueA = (double)BitConverter.ToUInt16(buff, y * subsample * srcWidth * byteDepth + x * subsample * byteDepth) / (double)UInt16.MaxValue;
                    fullValueB = (double)BitConverter.ToUInt16(buff, y * subsample * srcWidth * byteDepth + (x * subsample + 1) * byteDepth) / (double)UInt16.MaxValue;
                    fullValueC = (double)BitConverter.ToUInt16(buff, (y * subsample + 1) * srcWidth * byteDepth + x * subsample * byteDepth) / (double)UInt16.MaxValue;
                    fullValueD = (double)BitConverter.ToUInt16(buff, (y * subsample + 1) * srcWidth * byteDepth + (x * subsample + 1) * byteDepth) / (double)UInt16.MaxValue;

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
                    

                    newBytes[y * newStride + x * 3 + mappedBayer[0, 0]] = (byte)(int)(Math.Min(byte.MaxValue,Math.Max(fullValueA,0)));
                    newBytes[y * newStride + x * 3 + mappedBayer[0, 1]] = (byte)(int)(Math.Min(byte.MaxValue, Math.Max(fullValueB, 0)));
                    newBytes[y * newStride + x * 3 + mappedBayer[1, 0]] = (byte)(int)(Math.Min(byte.MaxValue, Math.Max(fullValueC, 0)));
                    newBytes[y * newStride + x * 3 + mappedBayer[1, 1]] = (byte)(int)(Math.Min(byte.MaxValue, Math.Max(fullValueD, 0)));
                }
            }
            return newBytes;
        }

        // cropAmounts has to be array of 4 numbers for:left, top, right, bottom.
        public static byte[] cropBuffer16bitMono(byte[] buffer, uint inputWidth, uint inputHeight, uint[] cropAmounts)
        {

            uint outputWidth = inputWidth - cropAmounts[0] - cropAmounts[2];
            uint outputHeight = inputHeight - cropAmounts[1] - cropAmounts[3];
            byte[] outputBuffer = new byte[2 * outputWidth * outputHeight];

            for (int y = 0; y < outputHeight; y++)
            {
                for(int x = 0; x < outputWidth; x++)
                {
                    outputBuffer[y * outputWidth * 2 + x * 2] = buffer[(y+cropAmounts[1]) * inputWidth *2 + (x+cropAmounts[0]) * 2];
                    outputBuffer[y * outputWidth * 2 + x * 2 +1] = buffer[(y+cropAmounts[1]) * inputWidth *2 + (x+cropAmounts[0]) * 2+1];
                }
            }

            return outputBuffer;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawBayer2DNG
{
    public static class DataFormatConverter
    {

        //
        // INPUT FORMAT SECTION
        //
        /* ignore this function, I just ended up integrating the unpacking into CRISequenceSource, borrowing the code from ffmpeg.
        public static byte[] tryConvertCintel10Inputto16bit(byte[] input)
        {
            //return ImageSequenceSources.CRISequenceSource.unpack_10bit(input);
            byte[] inputBits = new byte[input.Length * 8];
            for (int i = 0, bb = 0; bb < inputBits.Length; i += 1, bb += 8)
            {
                for (int bit = 0; bit < 8; bit++)
                {
                    inputBits[bb + bit] = (byte)(0b0000_0001 & (input[i] >> (7 - (bit % 8))));
                }
                //bit1 = 0b0000_0001 & (input[i + a / 8] >> (7 - (a % 8)));
                //bit2 = 0b0000_0001 & (input[i + b / 8] >> (7 - (b % 8)));
                //bit3 = 0b0000_0001 & (input[i + c / 8] >> (7 - (c % 8)));
                //bit4 = 0b0000_0001 & (input[i + d / 8] >> (7 - (d % 8)));
            }
            long inputLength = input.Length * 8;
            //long outputLength = inputLength * 6 / 4;
            long outputLength = inputLength * 8 / 9;
            long inputLengthBytes = inputLength / 8;
            long outputLengthBytes = outputLength;
            byte[] output = new byte[outputLengthBytes];

            for (long i = 0, o = 0; i < inputLengthBytes; i += 16, o += 18)
            {
                output[o] = (byte)((input[i + 2] & 0b1100_0000)); // not sure?
                output[o+1] = input[i];
                //output[o + 2] = input[i];
                output[o + 3] = (byte)(((input[i+1]&0b0011_1100)<<2) | ((input[i+2]&0b1111_0000)>>4));
                //output[o + 4] = input[i];
                output[o + 5] = (byte)((input[i + 2] & 0b0000_0011) << 6);
                //output[o + 6] = input[i];
                //output[o + 7] = input[i];
                //output[o + 8] = input[i];
                //output[o + 9] = input[i];
                //output[o + 10] = input[i];
                //output[o + 11] = input[i];
                //output[o + 12] = input[i];
                //output[o + 13] = input[i];
                //output[o + 14] = input[i];
                //output[o + 15] = input[i];
                //output[o + 16] = input[i];
                //output[o + 17] = input[i];
            }

            return output;
        }*/
        // This gave me roughly that 4/3 ratio and alignin lines every 4 lines.
        /*public static byte[] tryConvertCintel10Inputto16bit(byte[] input)
        {
            long inputLength = input.Length * 8;
            long outputLength = inputLength * 6 / 4;
            long inputLengthBytes = inputLength / 8;
            long outputLengthBytes = outputLength;
            byte[] output = new byte[outputLengthBytes];

            for (long i = 0, o = 0; i < inputLengthBytes; i += 4, o += 6)
            {
                //output[o] = input[i];
                output[o+1] = input[i];
                //output[o + 2] = input[i];
                output[o + 3] = input[i];
                //output[o + 4] = input[i];
                output[o + 5] = input[i];
            }

            return output;
        }*/
        public static byte[] convert12pInputto16bit(byte[] input)
        {
            long inputlength = input.Length * 8;
            long outputLength = inputlength / 12 * 16;
            long inputlengthBytes = inputlength / 8;
            long outputLengthBytes = outputLength / 8;

            byte[] output = new byte[outputLengthBytes];

            // For each 3 bytes in input, we write 4 bytes in output
            for (long i = 0, o = 0; i < inputlengthBytes; i += 3, o += 4)
            {

                output[o + 1] = (byte)((input[i] & 0b1111_0000) >> 4 | ((input[i + 1] & 0b0000_1111) << 4));
                output[o] = (byte)((input[i] & 0b0000_1111) << 4);
                output[o + 3] = (byte)input[i + 2];
                output[o + 2] = (byte)((input[i + 1] & 0b1111_0000));
            }

            return output;
        }
        public static byte[] convert10p1Inputto16bit(byte[] input)
        {
            long inputlength = input.Length * 8;
            long outputLength = inputlength / 10 * 16;
            long inputlengthBytes = inputlength / 8;
            long outputLengthBytes = outputLength / 8;

            byte[] output = new byte[outputLengthBytes];

            // For each 5 bytes in input, we write 8 bytes in output
            for (long i = 0, o = 0; i < inputlengthBytes; i += 5, o += 8)
            {
                output[o + 1] = input[i]; // Seems correct
                output[o] = (byte)((input[i + 4] & 0b0000_0011) << 6);
                output[o + 3] = input[i + 1];
                output[o + 2] = (byte)((input[i + 4] & 0b0000_1100) << 4);
                output[o + 5] = input[i + 2];
                output[o + 4] = (byte)((input[i + 4] & 0b0011_0000) << 2);
                output[o + 7] = input[i + 3];
                output[o + 6] = (byte)(input[i + 4] & 0b1100_0000);
                /*Try 1: 1,2,3,4 and then the least significant bits of each in right order. Wasnt quite correct I think.
                 * output[o+1] = input[i]; // Seems correct
                output[o] = (byte)(input[i+4] & 0b1100_0000); 
                output[o+3] = input[i + 1];
                output[o+2] = (byte)((input[i + 4] & 0b0011_0000) << 2);
                output[o+5] = input[i + 2];
                output[o+4] = (byte)((input[i + 4] & 0b0000_11000) << 4);
                output[o+7] = input[i + 3];
                output[o+6] = (byte)((input[i + 4] & 0b0000_0011) << 6);*/
            }

            return output;
        }
        public static byte[] convert12paddedto16Inputto16bit(byte[] input)
        {
            int inputlengthBytes = input.Length;

            int tmpValue;
            for (long i = 0; i < inputlengthBytes; i += 2)
            {
                tmpValue = ((input[i] | input[i + 1] << 8) << 4) & UInt16.MaxValue;// combine into one 16 bit int and shift 4 bits to the left
                input[i] = (byte)(tmpValue & byte.MaxValue);
                input[i + 1] = (byte)((tmpValue >> 8) & byte.MaxValue);
            }

            return input;
        }

        public static byte[] convert16bitIntermediateTo12paddedto16bit(byte[] input)
        {
            int inputlengthBytes = input.Length;

            int tmpValue;
            for (long i = 0; i < inputlengthBytes; i += 2)
            {
                tmpValue = ((input[i] | input[i + 1] << 8) >> 4) & UInt16.MaxValue;// combine into one 16 bit int and shift 4 bits to the left
                input[i] = (byte)(tmpValue & byte.MaxValue);
                input[i + 1] = (byte)((tmpValue >> 8) & byte.MaxValue);
            }

            return input;
        }
        
        public static byte[] convert16bitIntermediateToDarkIn16bitWithGamma(byte[] input, int targetBitDepth,double gamma)
        {
            int inputlengthBytes = input.Length;

            double tmpValue;
            UInt16 tmpValue2;
            byte[] tmpValue2Bytes;
            for (int i = 0; i < inputlengthBytes; i += 2)
            {
                tmpValue = ((double)BitConverter.ToUInt16(input,i)) / (double)UInt16.MaxValue;
                tmpValue = Math.Pow(tmpValue, gamma) * (Math.Pow(2,targetBitDepth)-1);
                tmpValue2 = (UInt16)Math.Max(0,Math.Min(UInt16.MaxValue,Math.Round(tmpValue)));
                tmpValue2Bytes = BitConverter.GetBytes(tmpValue2);
                input[i] = tmpValue2Bytes[0];
                input[i+1] = tmpValue2Bytes[1];
            }

            return input;
        }

        // No need to supply pixelWidthForDithering if dithering is false.
        public static byte[] convert16bitIntermediateToDarkIn16bitWithLinLogV1(byte[] input, double parameterA, bool dithering=false, int pixelWidthForDithering = 0)
        {
            int inputlengthBytes = input.Length;

            double tmpValue;
            UInt16 outputValue;
            byte[] outputValueBytes;

            if (dithering)
            {

                int height = input.Length / 2 / pixelWidthForDithering;

                double[] srcAsDouble = new double[inputlengthBytes / 2+ pixelWidthForDithering+1]; // Adding the image width and a single pixel as a buffer because the error diffusion accesses the current pixel + width + 1 as a maximum and its easier than to add an if.
                for (int i = 0,realNumbery=0; i < inputlengthBytes; i += 2,realNumbery++)
                {
                    srcAsDouble[realNumbery] = BitConverter.ToUInt16(input, i);
                }

                double quantizationError;
                UInt16 restoredValue;
                int realNumber, index;
                // With Floyd Steinberg dithering
                for (int y=0;y< height;y++)
                {
                    for (int x = 0; x < pixelWidthForDithering; x++)
                    {
                        realNumber = y * pixelWidthForDithering + x;
                        index = realNumber * 2;

                        tmpValue = srcAsDouble[realNumber];
                        outputValue = (UInt16)Math.Min(UInt16.MaxValue, Math.Max(0, Math.Round(Math.Log(parameterA * tmpValue + 1, parameterA + 1))));
                        restoredValue = (UInt16)Math.Min(UInt16.MaxValue, Math.Max(0, Math.Round((Math.Pow(parameterA + 1, outputValue) - 1) / parameterA)));
                        quantizationError = tmpValue - (double)restoredValue;

                        if(x < (pixelWidthForDithering - 1))
                        {
                            srcAsDouble[realNumber + 1] += quantizationError * 7.0 / 16.0;
                        }
                        if(x != 0)
                        {
                            srcAsDouble[realNumber + pixelWidthForDithering - 1] += quantizationError * 3.0 / 16.0;
                        }
                        srcAsDouble[realNumber + pixelWidthForDithering] += quantizationError * 5.0 / 16.0;
                        if (x < (pixelWidthForDithering - 1))
                        {
                            srcAsDouble[realNumber + pixelWidthForDithering + 1] += quantizationError * 1.0 / 16.0;
                        }

                        outputValueBytes = BitConverter.GetBytes(outputValue);
                        input[index] = outputValueBytes[0];
                        input[index + 1] = outputValueBytes[1];
                    }
                }
                /*for (int i = 0,realNumber=0; i < inputlengthBytes; i += 2,realNumber++)
                {
                    
                }*/
            } else
            {
                for (int i = 0; i < inputlengthBytes; i += 2)
                {
                    tmpValue = BitConverter.ToUInt16(input, i);
                    outputValue = (UInt16)Math.Min(UInt16.MaxValue, Math.Max(0, Math.Round(Math.Log(parameterA * tmpValue + 1, parameterA + 1))));
                    outputValueBytes = BitConverter.GetBytes(outputValue);
                    input[i] = outputValueBytes[0];
                    input[i + 1] = outputValueBytes[1];
                }
            }

            

            return input;
        }

        // Same as the main function, but diffusion is applied on same colors in Bayer pattern
        // The green color handling is a deviation from the normal Floyd Steinberg coefficients because its diagonal, so I had to come up with some fantasy coefficients.
        // No need to supply pixelWidthForDithering if dithering is false.
        public static byte[] convert16bitIntermediateToDarkIn16bitWithLinLogV1_bayerPatternAwareDiffusion(byte[] input, double parameterA, byte[,] bayerPattern, bool dithering = false, int pixelWidthForDithering = 0)
        {
            int inputlengthBytes = input.Length;

            int pixelWidthForDitheringX2 = pixelWidthForDithering * 2;

            double tmpValue;
            UInt16 outputValue;
            byte[] outputValueBytes;

            if (dithering)
            {

                int height = input.Length / 2 / pixelWidthForDithering;

                double[] srcAsDouble = new double[inputlengthBytes / 2+ pixelWidthForDithering*2+2]; // Adding the image width and a single pixel as a buffer because the bayer aware error diffusion accesses the current pixel + width*2 + 2 as a maximum and its easier than to add an if.
                for (int i = 0,realNumbery=0; i < inputlengthBytes; i += 2,realNumbery++)
                {
                    srcAsDouble[realNumbery] = BitConverter.ToUInt16(input, i);
                }

                //bool bayerGreenIsGXXG = bayerPattern[0, 0] == 1 && bayerPattern[1, 1] == 1; // This indicates to us the di

                double quantizationError;
                UInt16 restoredValue;
                int realNumber, index;

                int bayerPositionX, bayerPositionY;
                // With Floyd Steinberg dithering
                for (int y=0;y< height;y++)
                {
                    bayerPositionY = y % 2;
                    for (int x = 0; x < pixelWidthForDithering; x++)
                    {
                        bayerPositionX = x % 2;

                        realNumber = y * pixelWidthForDithering + x;
                        index = realNumber * 2;

                        tmpValue = srcAsDouble[realNumber];
                        outputValue = (UInt16)Math.Min(UInt16.MaxValue, Math.Max(0, Math.Round(Math.Log(parameterA * tmpValue + 1, parameterA + 1))));
                        restoredValue = (UInt16)Math.Min(UInt16.MaxValue, Math.Max(0, Math.Round((Math.Pow(parameterA + 1, outputValue) - 1) / parameterA)));
                        quantizationError = tmpValue - (double)restoredValue;

                        // Green is handled differently to other colors, with my own fantasy approach lol
                        if(bayerPattern[bayerPositionX,bayerPositionY] == 1)
                        {
                            if (x < (pixelWidthForDithering - 2))
                            {
                                srcAsDouble[realNumber + 2] += quantizationError * 7.0 / 16.0;
                            }
                            if (x != 0)
                            {
                                srcAsDouble[realNumber + pixelWidthForDithering - 1] += quantizationError * 3.0 / 16.0;
                            }
                            srcAsDouble[realNumber + pixelWidthForDitheringX2] += quantizationError * 5.0 / 16.0;
                            if (x < (pixelWidthForDithering - 2))
                            {
                                srcAsDouble[realNumber + pixelWidthForDithering + 1] += quantizationError * 1.0 / 16.0;
                            }
                        } else
                        {

                            if (x < (pixelWidthForDithering - 2))
                            {
                                srcAsDouble[realNumber + 2] += quantizationError * 7.0 / 16.0;
                            }
                            if (x > 1)
                            {
                                srcAsDouble[realNumber + pixelWidthForDitheringX2 - 2] += quantizationError * 3.0 / 16.0;
                            }
                            srcAsDouble[realNumber + pixelWidthForDitheringX2] += quantizationError * 5.0 / 16.0;
                            if (x < (pixelWidthForDithering - 2))
                            {
                                srcAsDouble[realNumber + pixelWidthForDitheringX2 + 2] += quantizationError * 1.0 / 16.0;
                            }
                        }


                        outputValueBytes = BitConverter.GetBytes(outputValue);
                        input[index] = outputValueBytes[0];
                        input[index + 1] = outputValueBytes[1];
                    }
                }
                /*for (int i = 0,realNumber=0; i < inputlengthBytes; i += 2,realNumber++)
                {
                    
                }*/
            } else
            {
                for (int i = 0; i < inputlengthBytes; i += 2)
                {
                    tmpValue = BitConverter.ToUInt16(input, i);
                    outputValue = (UInt16)Math.Min(UInt16.MaxValue, Math.Max(0, Math.Round(Math.Log(parameterA * tmpValue + 1, parameterA + 1))));
                    outputValueBytes = BitConverter.GetBytes(outputValue);
                    input[i] = outputValueBytes[0];
                    input[i + 1] = outputValueBytes[1];
                }
            }

            

            return input;
        }


        // Guessing...
        public static byte[] convert16BitIntermediateToTiffPacked12BitOutput(byte[] input)
        {
            long inputlength = input.Length * 8;
            long outputLength = inputlength / 16 * 12;
            long inputlengthBytes = inputlength / 8;
            long outputLengthBytes = outputLength / 8;

            byte[] output = new byte[outputLengthBytes];

            
            // For each 3 bytes in input, we write 4 bytes in output
            for (long o = 0, i = 0; i < inputlengthBytes; o += 3, i += 4)
            {
                // known (x=unknown):
                // [0] 0b0000_1000 =  R & G2 = 128, G & B = 0
                // [0] 0b0000_0001 =  R & G2 = 16, G & B = 0
                // [0] 0b1000_0000 =  R & G2 = 2048, G & B = 0
                // [1] 0b1000_0000 =  R & G2 = 8, G & B = 0
                // [1] 0b0000_1000 =  R & G2 = 0, G & B = 2048
                // [2] 0b1000_0000 =  R & G2 = 8, G & B = 128
                // [2] 0b0000_1000 =  R & G2 = 0, G & B = 8
                // This hints towards a simple: AAAA_AAAA AAAA_BBBB BBBB_BBBB. Yet for some reason it ends up not working.
                output[o] = (byte)input[i+1];
                output[o + 1] = (byte)(input[i] & 0b1111_0000 | (input[i + 3] >> 4));
                output[o + 2] = (byte)(((input[i + 3] & 0b0000_1111) << 4) | input[i + 2] >> 4);

                // So for some reason I had to flip the bytes to make it work. This kinda worries me. Very strange!
            }

            return output;
        }
    }
}

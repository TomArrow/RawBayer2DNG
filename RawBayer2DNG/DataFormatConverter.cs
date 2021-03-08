﻿using System;
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
                        srcAsDouble[realNumber + pixelWidthForDithering -1] += quantizationError * 3.0 / 16.0;
                        srcAsDouble[realNumber + pixelWidthForDithering] += quantizationError * 5.0 / 16.0;
                        srcAsDouble[realNumber + pixelWidthForDithering +1] += quantizationError * 1.0 / 16.0;

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

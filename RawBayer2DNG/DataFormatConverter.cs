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

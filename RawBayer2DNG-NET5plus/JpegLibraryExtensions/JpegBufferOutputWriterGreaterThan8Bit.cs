using JpegLibrary;
using RawBayer2DNG;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JpegDecode
{
    public class JpegBufferOutputWriterGreaterThan8Bit : JpegBlockOutputWriter
    {
        private int _width;
        private int _height;
        private int _outputByteDepth;
        private int _shift;
        private int _componentCount;
        private Memory<byte> _output;

        public JpegBufferOutputWriterGreaterThan8Bit(int width, int height, int precision, int componentCount, Memory<byte> output, int outputBitDepth)
        {
            if (output.Length < (width * height * componentCount))
            {
                throw new ArgumentException("Destination buffer is too small.");
            }
            if (precision < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(precision));
            }

            _outputByteDepth = outputBitDepth / 8;
            _width = width;
            _height = height;
            _shift = precision - 8;
            _componentCount = componentCount;
            _output = output;
        }

        int width, height, shift;
        public override void WriteBlock(ref short blockRef, int componentIndex, int x, int y)
        {
            int componentCount = _componentCount;
            width = _width;
            height = _height;
            shift = _shift;

            if (x > width || y > _height)
            {
                return;
            }

            int writeWidth = Math.Min(width - x, 8);
            int writeHeight = Math.Min(height - y, 8);


            ref byte destinationRef = ref MemoryMarshal.GetReference(_output.Span);
            destinationRef = ref Unsafe.Add(ref destinationRef, y * width * componentCount * _outputByteDepth + x * componentCount * _outputByteDepth + componentIndex * _outputByteDepth);

            /*for (int destY = 0; destY < writeHeight; destY++)
            {
                ref byte destinationRowRef = ref Unsafe.Add(ref destinationRef, destY * width * componentCount);
                for (int destX = 0; destX < writeWidth; destX++)
                {
                    Unsafe.Add(ref destinationRowRef, destX * componentCount) = ClampTo8Bit(Unsafe.Add(ref blockRef, destX) >> shift);
                }
                blockRef = ref Unsafe.Add(ref blockRef, 8);
            }*/
            ushort reinterpreted;
            byte msb, lsb;
            for (int destY = 0; destY < writeHeight; destY++)
            {
                ref byte destinationRowRef = ref Unsafe.Add(ref destinationRef, destY * width * componentCount * _outputByteDepth);
                for (int destX = 0; destX < writeWidth; destX++)
                {
                    reinterpreted = Helpers.ReinterpretCast<short, ushort>(Unsafe.Add(ref blockRef, destX));
                    msb = ClampTo8Bit(reinterpreted >> 8);
                    lsb = ClampTo8Bit(reinterpreted & byte.MaxValue);
                    //if (componentIndex == 0) continue;
                    Unsafe.Add(ref destinationRowRef, destX * componentCount * _outputByteDepth) = lsb;
                    Unsafe.Add(ref destinationRowRef, destX * componentCount * _outputByteDepth + 1) = msb;
                }
                blockRef = ref Unsafe.Add(ref blockRef, 8);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ClampTo8Bit(int input)
        {
            return (byte)Helpers.Clamp(input, 0, 255);
        }
    }
}
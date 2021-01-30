using JpegLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JpegDecodingTests
{
    class Program
    {
        static void Main(string[] args)
        {
            //testAdobeJPG();
            //testCRIJPG();
            testAdobeJPGEncode();
        }


        private static void testCRIJPG()
        {
            JpegDecoder jpegLibraryDecoder = new JpegDecoder();

            byte[] rawTileBuffer = File.ReadAllBytes("rawcri.jpg");


            int tileWidth = 256;
            int tileHeight = 240;



            ReadOnlyMemory<byte> rawTileReadOnlyMemory;
            rawTileReadOnlyMemory = new ReadOnlyMemory<byte>(rawTileBuffer);
            jpegLibraryDecoder.SetInput(rawTileReadOnlyMemory);
            //jpegLibraryDecoder.SetFrameHeader()
            jpegLibraryDecoder.Identify(); // fails to identify. missing markers or whatever: Failed to decode JPEG data at offset 91149. No marker found.'

            byte[] tileBuff = new byte[jpegLibraryDecoder.Width * jpegLibraryDecoder.Height * 2];
            jpegLibraryDecoder.SetOutputWriter(new JpegDecode.JpegBufferOutputWriterGreaterThan8Bit(jpegLibraryDecoder.Width, jpegLibraryDecoder.Height, jpegLibraryDecoder.Precision, 1, tileBuff, 16));
            jpegLibraryDecoder.Decode();


            File.WriteAllBytes("cri-sof3-decodetest-"+ jpegLibraryDecoder.Width/2 + "x"+ jpegLibraryDecoder.Height*2+ ".raw", tileBuff);
        }
        private static void testAdobeJPG()
        {
            JpegDecoder jpegLibraryDecoder = new JpegDecoder();

            byte[] rawTileBuffer = File.ReadAllBytes("sof3.jpg");


            int tileWidth = 256;
            int tileHeight = 240;

            byte[] tileBuff = new byte[tileWidth * tileHeight * 2];


            ReadOnlyMemory<byte> rawTileReadOnlyMemory;
            rawTileReadOnlyMemory = new ReadOnlyMemory<byte>(rawTileBuffer);
            jpegLibraryDecoder.SetInput(rawTileReadOnlyMemory);
            //jpegLibraryDecoder.SetFrameHeader()
            jpegLibraryDecoder.Identify(); // fails to identify. missing markers or whatever: Failed to decode JPEG data at offset 91149. No marker found.'
            jpegLibraryDecoder.SetOutputWriter(new JpegDecode.JpegBufferOutputWriterGreaterThan8Bit(tileWidth / 2, tileHeight, jpegLibraryDecoder.Precision, 2, tileBuff, 16));
            jpegLibraryDecoder.Decode();


            File.WriteAllBytes("sof3-decodetest.raw", tileBuff);
        }
        private static void testAdobeJPGEncode()
        {

            int tileWidth = 256;
            int tileHeight = 240;


            byte[] rawReferenceData = File.ReadAllBytes("rawToEncode-DNG.raw");
            UInt16[] rawReferenceDataUInt16 = new UInt16[rawReferenceData.Length / 2];
            for(int i = 0; i < rawReferenceDataUInt16.Length; i++)
            {
                rawReferenceDataUInt16[i] = BitConverter.ToUInt16(rawReferenceData, i * 2);
            }

            dng_stream whatever = new dng_stream();
            DNGLosslessEncoder.EncodeLosslessJPEG(rawReferenceDataUInt16, (uint)tileHeight, (uint)tileWidth/2, 2, 16, tileWidth, 1, whatever);

            File.WriteAllBytes("encodedTest.jpg", whatever.toByteArray());


            // Try decode again
            JpegDecoder jpegLibraryDecoder = new JpegDecoder();

            byte[] rawTileBuffer = File.ReadAllBytes("encodedTest.jpg");



            byte[] tileBuff = new byte[tileWidth * tileHeight * 2];


            ReadOnlyMemory<byte> rawTileReadOnlyMemory;
            rawTileReadOnlyMemory = new ReadOnlyMemory<byte>(rawTileBuffer);
            jpegLibraryDecoder.SetInput(rawTileReadOnlyMemory);
            //jpegLibraryDecoder.SetFrameHeader()
            jpegLibraryDecoder.Identify(); // fails to identify. missing markers or whatever: Failed to decode JPEG data at offset 91149. No marker found.'
            jpegLibraryDecoder.SetOutputWriter(new JpegDecode.JpegBufferOutputWriterGreaterThan8Bit(tileWidth / 2, tileHeight, jpegLibraryDecoder.Precision, 2, tileBuff, 16));
            jpegLibraryDecoder.Decode();


            File.WriteAllBytes("sof3-encodedecodetest.raw", tileBuff);
        }
    }
}

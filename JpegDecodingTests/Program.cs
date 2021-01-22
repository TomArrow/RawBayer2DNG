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
            testCRIJPG();
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
    }
}

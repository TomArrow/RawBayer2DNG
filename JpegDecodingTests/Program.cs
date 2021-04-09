using BitMiracle.LibTiff.Classic;
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

        public const TiffTag TIFFTAG_CFAREPEATPATTERNDIM = (TiffTag)33421;
        public const TiffTag TIFFTAG_CFAPATTERN = (TiffTag)33422;
        public const TiffTag TIFFTAG_SUBIFDS = (TiffTag)330;

        private static Tiff.TiffExtendProc m_parentExtender;

        public static void TagExtender(Tiff tif)
        {
            TiffFieldInfo[] tiffFieldInfo =
            {
                new TiffFieldInfo(TIFFTAG_SUBIFDS, -1, -1, TiffType.LONG, FieldBit.Custom, false, false, "SubIFDs"),
                new TiffFieldInfo(TIFFTAG_CFAREPEATPATTERNDIM, 2, 2, TiffType.SHORT, FieldBit.Custom, false, false, "CFARepeatPatternDim"),
                new TiffFieldInfo(TIFFTAG_CFAPATTERN, 4, 4, TiffType.BYTE, FieldBit.Custom, false, false, "CFAPattern"),
            };

            /* Reference code copied from C++ version of LibTiff (not yet implemented in LibTiff.NET)
             *{ TIFFTAG_CFAREPEATPATTERNDIM, 2, 2, TIFF_SHORT, 0, TIFF_SETGET_C0_UINT16, TIFF_SETGET_UNDEFINED,	FIELD_CUSTOM, 0,	0,	"CFARepeatPatternDim", NULL },
	            { TIFFTAG_CFAPATTERN,	4, 4,	TIFF_BYTE, 0, TIFF_SETGET_C0_UINT8, TIFF_SETGET_UNDEFINED, FIELD_CUSTOM, 0,	0,	"CFAPattern" , NULL},
             */

            tif.MergeFieldInfo(tiffFieldInfo, tiffFieldInfo.Length);

            if (m_parentExtender != null)
                m_parentExtender(tif);
        }

        static void Main(string[] args)
        {

            // Register the custom tag handler
            Tiff.TiffExtendProc extender = TagExtender;
            m_parentExtender = Tiff.SetTagExtender(extender);

            //testAdobeJPG();
            //testCRIJPG();
            //testAdobeJPGEncode();
            testWriteCRIJpegIntoDNG();
        }

        private static void testWriteCRIJpegIntoDNG()
        {
            byte[] jpegData = File.ReadAllBytes("Cintel_00094268.cri.tile.3.jpg");

            char[] bayerSubstitution = { "\x0"[0], "\x1"[0], "\x2"[0] };

            string bayerPatternTag = bayerSubstitution[1].ToString() +
                                            bayerSubstitution[0] + bayerSubstitution[2] +
                                            bayerSubstitution[1];

            int width = 1024;
            int height = 3072;


            string fileName = "Cintel_00094268.cri.tile.3.jpg.dng";

            using (Tiff output = Tiff.Open(fileName, "w"))
            {
                // Basic TIFF functionality
                output.SetField(TiffTag.IMAGEWIDTH, width);
                output.SetField(TiffTag.IMAGELENGTH, height);
                output.SetField(TiffTag.SAMPLESPERPIXEL, 1);


                output.SetField(TiffTag.BITSPERSAMPLE, 16);


                //rawImageData = DataFormatConverter.convert16bitIntermediateTo12paddedto16bit(rawImageData);
                output.SetField(TiffTag.BASELINEEXPOSURE, 4);
                


                output.SetField(TiffTag.ORIENTATION, Orientation.TOPLEFT);
                output.SetField(TiffTag.ROWSPERSTRIP, height);
                output.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
                output.SetField(TiffTag.FILLORDER, FillOrder.MSB2LSB);


                output.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);

                float[] cam_xyz =
                {
                3.2404542f, -1.5371385f , -0.4985314f ,
                    -0.9692660f , 1.8760108f , 0.0415560f,
                    0.0556434f, -0.2040259f , 1.0572252f 
            }; // my sRGB hack
                //float[] cam_xyz =  { 0f, 1f,0f,0f,0f,1f,1f,0f,0f }; // my sRGB hack
                float[] neutral = { 1f , 1f, 1f  }; // my sRGB hack
                int[] bpp = { 8, 8, 8 }; // my sRGB hack
                short[] bayerpatterndimensions = { 2, 2 }; // my sRGB hack
                //float[] neutral = { 0.807133f, 1.0f, 0.913289f };

                //DNG 
                output.SetField(TiffTag.SUBFILETYPE, 0);
                output.SetField(TiffTag.MAKE, "Point Grey");
                output.SetField(TiffTag.MODEL, "Chameleon3");
                output.SetField(TiffTag.SOFTWARE, "FlyCapture2");
                output.SetField(TiffTag.DNGVERSION, "\x1\x4\x0\x0");
                output.SetField(TiffTag.DNGBACKWARDVERSION, "\x1\x4\x0\x0");
                output.SetField(TiffTag.UNIQUECAMERAMODEL, "USB3");
                output.SetField(TiffTag.COLORMATRIX1, 9, cam_xyz);
                output.SetField(TiffTag.ASSHOTNEUTRAL, 3, neutral);
                output.SetField(TiffTag.CALIBRATIONILLUMINANT1, 21);
                output.SetField(TiffTag.PHOTOMETRIC, 32803);
                output.SetField(TiffTag.SAMPLESPERPIXEL, 1);
                //output.SetField(TiffTag.EXIF_CFAPATTERN, 4, "\x1\x0\x2\x1");
                output.SetField(TiffTag.EXIF_CFAPATTERN, 4, bayerPatternTag);
                output.SetField(TIFFTAG_CFAREPEATPATTERNDIM, bayerpatterndimensions);
                //output.SetField(TIFFTAG_CFAPATTERN, "\x1\x0\x2\x1"); //0=Red, 1=Green,   2=Blue,   3=Cyan,   4=Magenta,   5=Yellow,   and   6=White
                output.SetField(TIFFTAG_CFAPATTERN,
                    bayerPatternTag); //0=Red, 1=Green,   2=Blue,   3=Cyan,   4=Magenta,   5=Yellow,   and   6=White

                // Maybe use later if necessary:
                //output.SetField(TiffTag.SAMPLESPERPIXEL, 1);
                //output.SetField(TiffTag.BITSPERSAMPLE, 3, bpp);
                //output.SetField(TiffTag.LINEARIZATIONTABLE, 256, linearizationTable);
                //output.SetField(TiffTag.WHITELEVEL, 1);

                    output.SetField(TiffTag.COMPRESSION, Compression.JPEG);
                    output.WriteRawStrip(0, jpegData, jpegData.Length);
                
            }

            
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
            DNGLosslessEncoder.EncodeLosslessJPEG(rawReferenceDataUInt16, (uint)tileHeight, (uint)tileWidth/2, 2, 16, tileWidth, 2, whatever);

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

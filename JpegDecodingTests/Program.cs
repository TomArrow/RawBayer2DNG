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
            //testWriteCRIJpegIntoDNG();
            //testDecodeCRIJpeg();
            Cintel10BitDecodeTests();
        }

        // This fast variant assumes the pixels come roughly in the correct order within the raw data, so has a smaller search radius
        private static void Cintel10BitDecodeTestsFaster()
        {

            byte[] input = File.ReadAllBytes("Resolution_chart.cri.data.raw");
            byte[] reference = File.ReadAllBytes("reference_from_tif_180rot.raw");

            long inputLength = input.Length * 8;
            //long outputLength = inputLength * 6 / 4;
            long outputLength = inputLength * 8 / 9;
            long inputLengthBytes = inputLength / 8;
            long outputLengthBytes = outputLength;
            byte[] output = new byte[outputLengthBytes];

            int nineAligned = ((reference.Length / 2) / 9 + 1) * 9;

            byte[] reference8bit = new byte[nineAligned];
            byte[] reference4bit = new byte[nineAligned];
            for (long i = 0, o = 0; i < reference.Length; i += 2, o += 1)
            {
                reference8bit[o] = reference[i + 1];
                reference4bit[o] = (byte)(reference[i + 1] & 0b1111_0000);
            }

            byte[] inputBits = new byte[input.Length*8];
            for (int i = 0, bb = 0; bb < inputBits.Length; i += 1, bb += 8)
            {
                for(int bit = 0; bit < 8; bit++)
                {
                    inputBits[bb + bit] = (byte)(0b0000_0001 & (input[i] >> (7 - (bit % 8))));
                }
                /*bit1 = 0b0000_0001 & (input[i + a / 8] >> (7 - (a % 8)));
                bit2 = 0b0000_0001 & (input[i + b / 8] >> (7 - (b % 8)));
                bit3 = 0b0000_0001 & (input[i + c / 8] >> (7 - (c % 8)));
                bit4 = 0b0000_0001 & (input[i + d / 8] >> (7 - (d % 8)));*/
            }
            byte[] inputBitsSmol = new byte[20000];
            Array.Copy(inputBits, 0, inputBitsSmol, 0, inputBitsSmol.Length);

            //File.WriteAllBytes("reference8bits.raw",reference8bit);
            //File.WriteAllBytes("reference4bits.raw",reference4bit);

            // we want to brute force find the correct format.
            // How?
            // We have groups of 9 pixels
            // And we have thousands of groups.
            // We have 16 bytes = 128 bits from which each of the 10 correct output bits can be fed.
            // That means, 128^10 variations? ah shit, is that too much to brute force? Probably...
            // Would be 1180591620717411303424 possible combinations.  1,180,591,620,717,411,303,424
            // That might take quite forever...
            // What if we reduce the desired output precision to 4 bits... then its 268,435,456 combinations. Much more doable.And the reference isn't exactly perfect anyway.
            // We go like this through each offset...
            // 
            // How do we encode the combinations for quick access? each bit requires 7 bits to encode 128 variations. We could just pick a UInt32 and bit shift it around. and ignore the 8th bit for each. basically end up with 4 x 8 bit number
            // Nvm, we can't do this because C# wont let us create a big enough array
            //UInt32[] variations = new UInt32[Int32.MaxValue/2];
            //byte[,,,] variations = new byte[128, 128, 128, 128]; // this works!
            double[,,,] difference = new double[128, 128, 128, 128]; // this works!
            byte[][] bestVariations = new byte[4][];

            int bit1, bit2, bit3,  bit4,combinedValue,referenceValue;
            double averageTotal, averageMultiplier, averageResult;

            FileStream fs = new FileStream("logfile.log",FileMode.Append,FileAccess.Write,FileShare.ReadWrite);
            BinaryWriter bw = new BinaryWriter(fs);

            int a, b, c, d;
            long bitPos = 0;
            int rangeMin, rangeMax;
            for (int offset = 0; offset < 9; offset++) {
                rangeMin = Math.Max(0,offset-20);
                rangeMax = Math.Min(127,offset+20);
                byte[] bestVariationSoFar = new byte[4];
                double bestVariationValueSofar = double.PositiveInfinity;
                for (a = rangeMin; a < rangeMax; a++)
                {
                    //b = a + 1;
                    //for ( b = a+1; b < 126; b++)
                    for ( b = rangeMin; b < rangeMax; b++)
                    {
                        //for (c = b+1; c < 127; c++)
                        for (c = rangeMin; c < rangeMax; c++)
                        //for (c = 0; c < 127; c++)
                        {
                            //d = c + 1;
                            for ( d = rangeMin; d < rangeMax; d++)
                            //for ( d = c+1; d < 128; d++)
                            {
                                averageTotal = 0;
                                averageMultiplier = 0;
                                bitPos = 0;
                                for (int i = 0, r = 0; r < reference8bit.Length; i += 16, r += 9,bitPos+=128)
                                {
                                    /*
                                    bit1 = 0b0000_0001 & (input[i + a / 8] >> (7 - (a % 8)));
                                    bit2 = 0b0000_0001 & (input[i + b / 8] >> (7 - (b % 8)));
                                    bit3 = 0b0000_0001 & (input[i + c / 8] >> (7 - (c % 8)));
                                    bit4 = 0b0000_0001 & (input[i + d / 8] >> (7 - (d % 8)));
                                    combinedValue = (bit1 << 7) | (bit2 << 6) | (bit3 << 5) | (bit4);
                                    */
                                    combinedValue = (inputBits[bitPos + a] << 7) | (inputBits[bitPos + b] << 6) | (inputBits[bitPos + c] << 5) | (inputBits[bitPos + d] << 4);
                                    referenceValue = reference4bit[r+offset];
                                    averageTotal += Math.Abs(combinedValue - referenceValue);
                                    averageMultiplier++;
                                }
                                averageResult = averageTotal / averageMultiplier;
                                //difference[a, b, c, d] = averageResult;
                                
                                if (averageResult < bestVariationValueSofar)
                                {
                                    bestVariationSoFar = new byte[] { (byte)a, (byte)b, (byte)c, (byte)d };
                                    bestVariationValueSofar = averageResult;
                                    Console.WriteLine("offset/pixel " + offset+": " +a + "," + b + "," + c + "," + d + ": " + bestVariationValueSofar);
                                    bw.Write(Encoding.UTF8.GetBytes("offset/pixel " + offset + ": " + a + "," + b + "," + c + "," + d + ": " + bestVariationValueSofar+"\r\n"));
                                    bw.Flush();
                                }
                            }
                            Console.WriteLine("offset/pixel " + offset + ": " + c + " of 128 c.. (b " +b +", a "+a+")" );
                        }
                        Console.WriteLine(b + " of 128 b..");
                    }
                    Console.WriteLine(a+" of 128 a..");
                }/*
                for (a = 0; a < 128; a++)
                {
                    for (b = 0; b < 128; b++)
                    {
                        for (c = 0; c < 128; c++)
                        {
                            for (d = 0; d < 128; d++)
                            {
                                if (difference[a, b, c, d] < bestVariationValueSofar)
                                {
                                    bestVariationSoFar = new byte[] { (byte)a, (byte)b, (byte)c, (byte)d };
                                    bestVariationValueSofar = difference[a, b, c, d];
                                    //Console.WriteLine(a + "," + b + "," + c + "," + d + ": " + bestVariationValueSofar);
                                    Console.WriteLine("offset/pixel " + offset + ": " + a + "," + b + "," + c + "," + d + ": " + bestVariationValueSofar);
                                    bw.Write(Encoding.UTF8.GetBytes("offset/pixel " + offset + ": " + a + "," + b + "," + c + "," + d + ": " + bestVariationValueSofar + "\r\n"));
                                }
                            }
                        }
                        Console.WriteLine(b + " of 128 b (run 2)..");
                    }
                    Console.WriteLine(a + " of 128 a (run 2)..");
                }
                difference = new double[128, 128, 128, 128];*/
            }


            
            
            bw.Dispose();
            fs.Dispose();

            for (long i = 0, o = 0; i < inputLengthBytes; i += 16, o += 18)
            {
                output[o] = (byte)((input[i + 2] & 0b1100_0000)); // not sure?
                output[o + 1] = input[i];
                //output[o + 2] = input[i];
                output[o + 3] = (byte)(((input[i + 1] & 0b0011_1100) << 2) | ((input[i + 2] & 0b1111_0000) >> 4));
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

        }
        
        private static void Cintel10BitDecodeTests()
        {

            byte[] input = File.ReadAllBytes("Resolution_chart.cri.data.raw");
            byte[] reference = File.ReadAllBytes("reference_from_tif_180rot.raw");

            long inputLength = input.Length * 8;
            //long outputLength = inputLength * 6 / 4;
            long outputLength = inputLength * 8 / 9;
            long inputLengthBytes = inputLength / 8;
            long outputLengthBytes = outputLength;
            byte[] output = new byte[outputLengthBytes];

            int nineAligned = ((reference.Length / 2) / 9 + 1) * 9;

            byte[] reference8bit = new byte[nineAligned];
            byte[] reference4bit = new byte[nineAligned];
            for (long i = 0, o = 0; i < reference.Length; i += 2, o += 1)
            {
                reference8bit[o] = reference[i + 1];
                reference4bit[o] = (byte)(reference[i + 1] & 0b1111_0000);
            }

            byte[] inputBits = new byte[input.Length*8];
            for (int i = 0, bb = 0; bb < inputBits.Length; i += 1, bb += 8)
            {
                for(int bit = 0; bit < 8; bit++)
                {
                    inputBits[bb + bit] = (byte)(0b0000_0001 & (input[i] >> (7 - (bit % 8))));
                }
                /*bit1 = 0b0000_0001 & (input[i + a / 8] >> (7 - (a % 8)));
                bit2 = 0b0000_0001 & (input[i + b / 8] >> (7 - (b % 8)));
                bit3 = 0b0000_0001 & (input[i + c / 8] >> (7 - (c % 8)));
                bit4 = 0b0000_0001 & (input[i + d / 8] >> (7 - (d % 8)));*/
            }
            byte[] inputBitsSmol = new byte[20000];
            Array.Copy(inputBits, 0, inputBitsSmol, 0, inputBitsSmol.Length);

            //File.WriteAllBytes("reference8bits.raw",reference8bit);
            //File.WriteAllBytes("reference4bits.raw",reference4bit);

            // we want to brute force find the correct format.
            // How?
            // We have groups of 9 pixels
            // And we have thousands of groups.
            // We have 16 bytes = 128 bits from which each of the 10 correct output bits can be fed.
            // That means, 128^10 variations? ah shit, is that too much to brute force? Probably...
            // Would be 1180591620717411303424 possible combinations.  1,180,591,620,717,411,303,424
            // That might take quite forever...
            // What if we reduce the desired output precision to 4 bits... then its 268,435,456 combinations. Much more doable.And the reference isn't exactly perfect anyway.
            // We go like this through each offset...
            // 
            // How do we encode the combinations for quick access? each bit requires 7 bits to encode 128 variations. We could just pick a UInt32 and bit shift it around. and ignore the 8th bit for each. basically end up with 4 x 8 bit number
            // Nvm, we can't do this because C# wont let us create a big enough array
            //UInt32[] variations = new UInt32[Int32.MaxValue/2];
            //byte[,,,] variations = new byte[128, 128, 128, 128]; // this works!
            double[,,,] difference = new double[128, 128, 128, 128]; // this works!
            byte[][] bestVariations = new byte[4][];

            int bit1, bit2, bit3,  bit4,combinedValue,referenceValue;
            double averageTotal, averageMultiplier, averageResult;

            FileStream fs = new FileStream("logfile.log",FileMode.Append,FileAccess.Write,FileShare.ReadWrite);
            BinaryWriter bw = new BinaryWriter(fs);

            int a, b, c, d;
            long bitPos = 0;
            for (int offset = 0; offset < 9; offset++) {
                byte[] bestVariationSoFar = new byte[4];
                double bestVariationValueSofar = double.PositiveInfinity;
                for (a = 0; a < 128; a++)
                {
                    //b = a + 1;
                    //for ( b = a+1; b < 126; b++)
                    for ( b = 0; b < 128; b++)
                    {
                        //for (c = b+1; c < 127; c++)
                        for (c = 0; c < 128; c++)
                        //for (c = 0; c < 127; c++)
                        {
                            //d = c + 1;
                            for ( d = 0; d < 128; d++)
                            //for ( d = c+1; d < 128; d++)
                            {
                                averageTotal = 0;
                                averageMultiplier = 0;
                                bitPos = 0;
                                for (int i = 0, r = 0; r < reference8bit.Length; i += 16, r += 9,bitPos+=128)
                                {
                                    /*
                                    bit1 = 0b0000_0001 & (input[i + a / 8] >> (7 - (a % 8)));
                                    bit2 = 0b0000_0001 & (input[i + b / 8] >> (7 - (b % 8)));
                                    bit3 = 0b0000_0001 & (input[i + c / 8] >> (7 - (c % 8)));
                                    bit4 = 0b0000_0001 & (input[i + d / 8] >> (7 - (d % 8)));
                                    combinedValue = (bit1 << 7) | (bit2 << 6) | (bit3 << 5) | (bit4);
                                    */
                                    combinedValue = (inputBits[bitPos + a] << 7) | (inputBits[bitPos + b] << 6) | (inputBits[bitPos + c] << 5) | (inputBits[bitPos + d] << 4);
                                    referenceValue = reference4bit[r+offset];
                                    averageTotal += Math.Abs(combinedValue - referenceValue);
                                    averageMultiplier++;
                                }
                                averageResult = averageTotal / averageMultiplier;
                                //difference[a, b, c, d] = averageResult;
                                
                                if (averageResult < bestVariationValueSofar)
                                {
                                    bestVariationSoFar = new byte[] { (byte)a, (byte)b, (byte)c, (byte)d };
                                    bestVariationValueSofar = averageResult;
                                    Console.WriteLine("offset/pixel " + offset+": " +a + "," + b + "," + c + "," + d + ": " + bestVariationValueSofar);
                                    bw.Write(Encoding.UTF8.GetBytes("offset/pixel " + offset + ": " + a + "," + b + "," + c + "," + d + ": " + bestVariationValueSofar+"\r\n"));
                                    bw.Flush();
                                }
                            }
                            Console.WriteLine("offset/pixel " + offset + ": " + c + " of 128 c.. (b " +b +", a "+a+")" );
                        }
                        Console.WriteLine(b + " of 128 b..");
                    }
                    Console.WriteLine(a+" of 128 a..");
                }/*
                for (a = 0; a < 128; a++)
                {
                    for (b = 0; b < 128; b++)
                    {
                        for (c = 0; c < 128; c++)
                        {
                            for (d = 0; d < 128; d++)
                            {
                                if (difference[a, b, c, d] < bestVariationValueSofar)
                                {
                                    bestVariationSoFar = new byte[] { (byte)a, (byte)b, (byte)c, (byte)d };
                                    bestVariationValueSofar = difference[a, b, c, d];
                                    //Console.WriteLine(a + "," + b + "," + c + "," + d + ": " + bestVariationValueSofar);
                                    Console.WriteLine("offset/pixel " + offset + ": " + a + "," + b + "," + c + "," + d + ": " + bestVariationValueSofar);
                                    bw.Write(Encoding.UTF8.GetBytes("offset/pixel " + offset + ": " + a + "," + b + "," + c + "," + d + ": " + bestVariationValueSofar + "\r\n"));
                                }
                            }
                        }
                        Console.WriteLine(b + " of 128 b (run 2)..");
                    }
                    Console.WriteLine(a + " of 128 a (run 2)..");
                }
                difference = new double[128, 128, 128, 128];*/
            }


            
            
            bw.Dispose();
            fs.Dispose();

            for (long i = 0, o = 0; i < inputLengthBytes; i += 16, o += 18)
            {
                output[o] = (byte)((input[i + 2] & 0b1100_0000)); // not sure?
                output[o + 1] = input[i];
                //output[o + 2] = input[i];
                output[o + 3] = (byte)(((input[i + 1] & 0b0011_1100) << 2) | ((input[i + 2] & 0b1111_0000) >> 4));
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

        }

        private static void testDecodeCRIJpeg()
        {

            byte[] jpegData = File.ReadAllBytes("Cintel_00094268.cri.tile.3.jpg");

            dng_stream stream = new dng_stream(jpegData);
            dng_spooler spooler = new dng_spooler();

            DNGLosslessDecoder.DecodeLosslessJPEG(stream, spooler, 0, uint.MaxValue, false, (UInt64)jpegData.Length);

            byte[] output = spooler.toByteArray();
            File.WriteAllBytes("Cintel_00094268.cri.tile.3.jpg-adobeSDKdecodetest.raw",output);
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


            string fileName = "Cintel_00094268.cri.tile.3.jpg-privdat.dng";

            using (Tiff output = Tiff.Open(fileName, "w"))
            {
                // Basic TIFF functionality
                output.SetField(TiffTag.IMAGEWIDTH, width);
                output.SetField(TiffTag.IMAGELENGTH, height);
                output.SetField(TiffTag.SAMPLESPERPIXEL, 1);


                output.SetField(TiffTag.BITSPERSAMPLE, 16);


                //rawImageData = DataFormatConverter.convert16bitIntermediateTo12paddedto16bit(rawImageData);
                output.SetField(TiffTag.BASELINEEXPOSURE, 4);


                string blah = "RawBayer2DNG\0blahblahblah";
                output.SetField(TiffTag.DNGPRIVATEDATA,blah.Length, blah);
                output.SetField(TiffTag.EXIF_MAKERNOTE,blah.Length, blah);
                output.SetField(TiffTag.MAKERNOTESAFETY,1);


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

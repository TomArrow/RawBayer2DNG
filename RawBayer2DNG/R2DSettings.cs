using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PresetManager;
using PresetManager.Attributes;

namespace RawBayer2DNG
{
    class R2DSettings : AppSettings
    {

        // Metadata for dng
        [Category("Metadata")]
        [Control("txtMake")]
        public string metaMake = "";
        [Category("Metadata")]
        [Control("txtModel")]
        public string metaModel = "";
        [Category("Metadata")]
        [Control("txtSoftware")]
        public string metaSoftware = "";
        [Category("Metadata")]
        [Control("txtUniqueCameraModel")]
        public string metaUniqueCameraModel = "";

        // Raw bayer reading settings
        public enum InputFormat
        {
            [Control("formatRadio_rg16")]
            RAW16BIT,
            [Control("formatRadio_rg12p")]
            RAW12P
        }
        [Category("RawInterpret")]
        public InputFormat inputFormat = InputFormat.RAW16BIT;
        [Category("RawInterpret")]
        [Control("colorBayerA")]
        public int colorBayerA = 1;
        [Category("RawInterpret")]
        [Control("colorBayerB")]
        public int colorBayerB = 0;
        [Category("RawInterpret")]
        [Control("colorBayerC")]
        public int colorBayerC = 2;
        [Category("RawInterpret")]
        [Control("colorBayerD")]
        public int colorBayerD = 1;
        [Category("RawInterpret")]
        [Control("rawWidth")]
        public int rawWidth = 2448;
        [Category("RawInterpret")]
        [Control("rawHeight")]
        public int rawHeight = 2048;

        // HDR related settings
        [Category("HDR")]
        [Control("exposureA")]
        public string exposureA = "E";
        [Category("HDR")]
        [Control("exposureB")]
        public string exposureB = "";
        [Category("HDR")]
        [Control("exposureC")]
        public string exposureC = "";
        [Category("HDR")]
        [Control("exposureD")]
        public string exposureD = "";
        [Category("HDR")]
        [Control("exposureE")]
        public string exposureE = "";
        [Category("HDR")]
        [Control("exposureF")]
        public string exposureF = "";
        [Category("HDR")]
        [Control("shotDelay_txt")]
        public int shotDelay = 0;
        [Category("HDR")]
        [Control("clippingPoint_txt")]
        public double clippingPoint = 0.7;
        [Category("HDR")]
        [Control("featherStops_txt")]
        public double featherStops = 0.0;
        [Category("HDR")]
        [Control("analysisPrecision_txt")]
        public double refinement_analysisPrecision = 5.0;

        // Preview related settings
        [Category("Preview")]
        [Control("previewDebayer")]
        public bool previewDebayer = true;
        [Category("Preview")]
        [Control("previewGamma")]
        public bool previewWithSRGBGamma = true;
        [Category("Preview")]
        [Control("drawScope_check")]
        public bool drawScope = false;

        // DNG output settings
        [Category("DNGOutput")]
        [Control("rAmplify")]
        public double redMultiplier = 1;
        [Category("DNGOutput")]
        [Control("gAmplify")]
        public double greenMultiplier = 1;
        [Category("DNGOutput")]
        [Control("bAmplify")]
        public double blueMultiplier = 1;
        [Category("DNGOutput")]
        [Control("compressDNG")]
        public bool compressDNGLegacy = false;
        [Category("DNGOutput")]
        [Control("compressDNGLosslessJPEG")]
        public bool compressDNGLosslessJPEG = true;
        [Category("DNGOutput")]
        [Control("compressDNGLosslessJPEGTiling")]
        public bool losslessJPEGTiling = true;
        [Category("DNGOutput")]
        [Control("txtTileSize")]
        public int losslessJPEGTileSize = 192;
        [Category("DNGOutput")]
        [Control("linLogDithering")]
        public bool linLogDithering = true;
        [Category("DNGOutput")]
        public DNGOutputDataFormat dngOutputDataFormat = DNGOutputDataFormat.BAYER12BITDARKCAPSULEDIN16BIT;
        public enum DNGOutputDataFormat
        {
            //INVALID,
            [Control("output_16bitDarkCapsuled_radio")]
            BAYER12BITDARKCAPSULEDIN16BIT,
            [Control("output_16bitBrightCapsuled_radio")]
            BAYER12BITBRIGHTCAPSULEDIN16BIT,
            [Control("output_12bitPacked_radio")]
            BAYER12BITTIFFPACKED,

            // This is a lossy method useful mostly for HDR which saves data with gamma and saves a linearization table to reverse that again. Not yet implemented, just an idea.
            // Math:
            // In 16 bit, 0 is 0 and the bit value of 1 is 1/(2^16-1), so 1/65535 = 0.00001525902189669642
            // In 12 bit, 0 is 0 and the bit value of 1 is 1/(2^12-1), so 1/4095 = 0.000244200244
            // So we need a gamma value that pushes the 16 bit value of 1 to the 12 bit value of 1.
            // In other words, 0.00001525902189669642^x = 0.000244200244
            // Generalize: a^x = b
            // solution according to wolfram alpha: x = log(b)/log(a)
            // so: double x = Math.Log(1/(Math.Pow(2,12)-1)) / Math.Log(1/(Math.Pow(2,16)-1));
            // or: double realx = 1/( Math.Log(1/(Math.Pow(2,12)-1)) / Math.Log(1/(Math.Pow(2,16)-1)));
            // Result for 16 to 12 bit: 0.749979015407929. Actual gamma thus being 1/0.749979015407929 = 1.3333706403186221
            // Result for 16 to 10 bit: Math.Log(1/(Math.Pow(2,10)-1)) / Math.Log(1/(Math.Pow(2,16)-1)) = 0.62491276165886922 or 1.6002233613303698. So pretty much 1.6

            //BAYER12BITBRIGHTCAPSULEDIN16BITWITHGAMMATO12BIT,
            [Control("output_16bitBrightCapsuledGamma10Bit_radio")]
            BAYER12BITBRIGHTCAPSULEDIN16BITWITHGAMMATO10BIT,

            [Control("output_16bitBrightCapsuledLinLog10Bit_radio")]
            BAYER12BITBRIGHTCAPSULEDIN16BITWITHLINLOGTO10BIT,
            [Control("output_16bitBrightCapsuledLinLog8Bit_radio")]
            BAYER12BITBRIGHTCAPSULEDIN16BITWITHLINLOGTO8BIT,
            [Control("output_16bitBrightCapsuledLinLog7Bit_radio")]
            BAYER12BITBRIGHTCAPSULEDIN16BITWITHLINLOGTO7BIT,

            // SDR
            [Control("output_16bitDarkCapsuledLinLog8Bit_radio")]
            BAYER12BITDARKCAPSULEDIN16BITWITHLINLOGTO8BIT,
            [Control("output_16bitDarkCapsuledLinLog7Bit_radio")]
            BAYER12BITDARKCAPSULEDIN16BITWITHLINLOGTO7BIT,
            [Control("output_16bitDarkCapsuledLinLog6Bit_radio")]
            BAYER12BITDARKCAPSULEDIN16BITWITHLINLOGTO6BIT,
            [Control("output_16bitDarkCapsuledLinLog5Bit_radio")]
            BAYER12BITDARKCAPSULEDIN16BITWITHLINLOGTO5BIT
        };

        // General processing settings
        [Category("Processing")]
        [Control("Threads")]
        public int maxThreads = Environment.ProcessorCount;
        [Category("Processing")]
        [Control("cropLeft_txt")]
        public int cropLeft = 0;
        [Category("Processing")]
        [Control("cropTop_txt")]
        public int cropTop = 0;
        [Category("Processing")]
        [Control("cropRight_txt")]
        public int cropRight = 0;
        [Category("Processing")]
        [Control("cropBottom_txt")]
        public int cropBottom = 0;

        public uint[] getCropAmounts()
        {
            return new uint[] { (uint)cropLeft/2*2, (uint)cropTop / 2 * 2, (uint)cropRight / 2 * 2, (uint)cropBottom / 2 * 2 };
        }

        
        // General output settings
        [Category("Output")]
        [Control("Rename")]
        public string outputSequenceCustomNaming = "";
        [Category("Output")]
        [Control("ReverseOrder")]
        public bool reverseOrder = false;
        [Category("Output")]
        [Control("writeErrorReports")]
        public bool writeErrorReports = true;
        [Category("Output")]
        [Control("writeMetaDataHumanReadable")]
        public bool writeHumanReadableMetaData = false;

        
    }
}

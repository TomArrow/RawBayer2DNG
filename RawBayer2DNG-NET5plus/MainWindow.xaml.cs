using BitMiracle.LibTiff.Classic;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Shapes = System.Windows.Shapes;
//using Forms = System.Windows.Forms;

using System.Drawing;
using Imaging = System.Drawing.Imaging;
using Orientation = BitMiracle.LibTiff.Classic.Orientation;
using System.Runtime.InteropServices;
using System.Threading;
using RawBayer2DNG.ImageSequenceSources;
using System.Text.RegularExpressions;
using System.Numerics;
using System.Globalization;
using PresetManager;

namespace RawBayer2DNG
{



    public enum RAWDATAFORMAT
    {
        INVALID,
        BAYER12BITDARKCAPSULEDIN16BIT, // 12 bit in a 16 bit wrapper, but such that the image ends up dark.
        BAYER12BITBRIGHTCAPSULEDIN16BIT, // 12 bit in a 16 bit wrapper, but such that the image ends up bright

        // 12 bit packed, with the "12p" standard from FLIR cameras. The other standard is "12packed", which is currently not implemented in this tool.
        // It's like this: AAAAAAAA AAAABBBB BBBBBBBB, with the BBBB in the second bit being the first bytes (not the last) of the second sample
        BAYERRG12p,
        BAYERRG12pV2,
        TIFF12BITPACKED, // For reading dngs
        CINTEL10BIT,
        BAYER10p1 // MotionCam
    };

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        public const TiffTag TIFFTAG_CFAREPEATPATTERNDIM = (TiffTag)33421;
        public const TiffTag TIFFTAG_CFAPATTERN = (TiffTag)33422;
        public const TiffTag TIFFTAG_SUBIFDS = (TiffTag)330;

        private static Tiff.TiffExtendProc m_parentExtender;
        private BackgroundWorker worker = new BackgroundWorker();

        string sourceFolder = null;
        string targetFolder = null;
        string[] filesInSourceFolder = null;
        private int currentProgress;
        private string currentStatus;
        private static int _counter = 0;
        private static int _totalFiles = 0;
        private string _newFileName;

        //uint[] cropAmounts = new uint[4];

        R2DSettings r2dSettings = new R2DSettings();
        bool fullSettingsToGUIWriteInProgress = false;



        ImageSequenceSource imageSequenceSource;

        // Declare the event
        public event PropertyChangedEventHandler PropertyChanged;

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

        // Create the OnPropertyChanged method to raise the event
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

        public MainWindow()
        {

            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            InitializeComponent();
            ToolTipService.ShowDurationProperty.OverrideMetadata(
                typeof(DependencyObject), new FrameworkPropertyMetadata(Int32.MaxValue)); // Helps keep the Tooltips open for a longer time if we set their ShowDuration high.

            // Register the custom tag handler
            Tiff.TiffExtendProc extender = TagExtender;
            m_parentExtender = Tiff.SetTagExtender(extender);

            txtMaxThreads.Text = "Threads (" + Environment.ProcessorCount + " CPUs): ";

            r2dSettings.Bind(this);
            r2dSettings.BindConfig("presets");
            r2dSettings.attachPresetManager(presetPanel);
            r2dSettings.ValueUpdatedInGUI += settingsUpdatedInGUI;
            r2dSettings.FullWriteToGUIStarted += (a, b) => { fullSettingsToGUIWriteInProgress = true; };
            r2dSettings.FullWriteToGUIEnded += (a, b) => { fullSettingsToGUIWriteInProgress = false; ReDrawPreview(); };
        }

        private void settingsUpdatedInGUI(object sender, ValueUpdatedEventArgs e)
        {
            switch (e.FieldName)
            {
                case "cropLeft":
                case "cropTop":
                case "cropRight":
                case "cropBottom":
                case "redMultiplier":
                case "greenMultiplier":
                case "blueMultiplier":

                case "previewWithSRGBGamma":
                case "previewDebayer":
                case "drawScope":
                    if (!fullSettingsToGUIWriteInProgress)
                    {

                        ReDrawPreview();
                    }
                    break;
                default:
                    break;
            }
        }

        private void BtnLoadRAW_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Raw bayer files (.raw)|*.raw";

            RAWDATAFORMAT inputFormat = getInputFormat();

            if (ofd.ShowDialog() == true)
            {
                ISSErrorInfo errorInfo = new ISSErrorInfo();
                ISSMetaInfo metaInfo = new ISSMetaInfo();

                string fileNameWithoutExtension = Path.GetDirectoryName(ofd.FileName) + "\\" + Path.GetFileNameWithoutExtension(ofd.FileName);
                string fileName = fileNameWithoutExtension + ".dng";

                byte[,] bayerPattern = getBayerPattern();
                //double[]  RGBamplify =  { rAmplify.Value, gAmplify.Value, bAmplify.Value };
                double[] RGBamplify = { r2dSettings.redMultiplier, r2dSettings.greenMultiplier, r2dSettings.blueMultiplier };

                ProcessRAW(File.ReadAllBytes(ofd.FileName), fileName, bayerPattern, inputFormat, RGBamplify, r2dSettings.getCropAmounts(), metaInfo, errorInfo, Path.GetFileNameWithoutExtension(ofd.FileName));

            }
        }



        private void ProcessRAW(byte[] rawImageData, string targetFilename, byte[,] bayerPattern, RAWDATAFORMAT inputFormat, double[] RGBAmplify, uint[] cropAmounts, ISSMetaInfo metaInfo, ISSErrorInfo errorInfo, string sourceFileNameForTIFFTag = "")
        {

#if DEBUG
            try {
#endif
            char[] bayerSubstitution = { "\x0"[0], "\x1"[0], "\x2"[0] };

            string bayerPatternTag = bayerSubstitution[bayerPattern[0, 0]].ToString() +
                                            bayerSubstitution[bayerPattern[0, 1]] + bayerSubstitution[bayerPattern[1, 0]] +
                                            bayerSubstitution[bayerPattern[1, 1]];

            int width = 2448;
            int height = 2048;

            R2DSettings.DNGOutputDataFormat outputFormat = R2DSettings.DNGOutputDataFormat.BAYER12BITBRIGHTCAPSULEDIN16BIT;

            int tileSize = 192;

            this.Dispatcher.Invoke(() =>
            {
                // backwards compatibility fuckery. trying not to break what already worked.
                if (imageSequenceSource.getSourceType() == ImageSequenceSource.ImageSequenceSourceType.RAW)
                {

                    (imageSequenceSource as RAWSequenceSource).width = r2dSettings.rawWidth;//int.Parse(rawWidth.Text);
                    (imageSequenceSource as RAWSequenceSource).height = r2dSettings.rawHeight;//int.Parse(rawHeight.Text);
                }
                width = imageSequenceSource.getWidth();
                height = imageSequenceSource.getHeight();
                outputFormat = r2dSettings.dngOutputDataFormat;
                /*if(int.TryParse(txtTileSize.Text,out int tileSizeHere))
                {
                    tileSize = tileSizeHere;
                }*/
                tileSize = r2dSettings.losslessJPEGTileSize;
            });



            if (cropAmounts[0] != 0 || cropAmounts[1] != 0 || cropAmounts[2] != 0 || cropAmounts[3] != 0)
            {

                rawImageData = Helpers.cropBuffer16bitMono(rawImageData, (uint)width, (uint)height, cropAmounts);
                width = (int)(width - cropAmounts[0] - cropAmounts[2]);
                height = (int)(height - cropAmounts[1] - cropAmounts[3]);
            }


            string fileName = targetFilename;

            using (Tiff output = Tiff.Open(fileName, "w"))
            {
                // Basic TIFF functionality
                output.SetField(TiffTag.IMAGEWIDTH, width);
                output.SetField(TiffTag.IMAGELENGTH, height);
                output.SetField(TiffTag.SAMPLESPERPIXEL, 1);


                output.SetField(TiffTag.BITSPERSAMPLE, 16);

                //int totalRawDataSize = width * height * 2;
                //
                bool lossyGammaModeEnabled = false;
                double lossyGammaModeGamma = 1;
                int lossyGammaModeOutputBitDepth = 16;

                bool lossyLinLogModeEnabled = false;
                double lossyLinLogModeParameterA = 1;
                int lossyLinLogModeOutputBitDepth = 16;

                // TODO Make lossy modes bake in the RGB sliders.
                if (outputFormat == R2DSettings.DNGOutputDataFormat.BAYER12BITBRIGHTCAPSULEDIN16BIT)
                {
                }
                else if (outputFormat == R2DSettings.DNGOutputDataFormat.BAYER12BITBRIGHTCAPSULEDIN16BITWITHGAMMATO10BIT)
                {
                    lossyGammaModeEnabled = true;
                    lossyGammaModeOutputBitDepth = 10;
                    lossyGammaModeGamma = Math.Log(1 / (Math.Pow(2, 10) - 1)) / Math.Log(1 / (Math.Pow(2, 16) - 1));
                    rawImageData = DataFormatConverter.convert16bitIntermediateToDarkIn16bitWithGamma(rawImageData, 10, lossyGammaModeGamma);
                }
                else if (outputFormat == R2DSettings.DNGOutputDataFormat.BAYER12BITBRIGHTCAPSULEDIN16BITWITHLINLOGTO8BIT)
                {
                    lossyLinLogModeEnabled = true;
                    lossyLinLogModeOutputBitDepth = 8;
                    lossyLinLogModeParameterA = LinLogLutilityClassifiedV1.findAParameterByBitDepths(16, 8);
                    rawImageData = DataFormatConverter.convert16bitIntermediateToDarkIn16bitWithLinLogV1_bayerPatternAwareDiffusion(rawImageData, lossyLinLogModeParameterA, bayerPattern, r2dSettings.linLogDithering, width);
                }
                else if (outputFormat == R2DSettings.DNGOutputDataFormat.BAYER12BITBRIGHTCAPSULEDIN16BITWITHLINLOGTO7BIT)
                {
                    lossyLinLogModeEnabled = true;
                    lossyLinLogModeOutputBitDepth = 7;
                    lossyLinLogModeParameterA = LinLogLutilityClassifiedV1.findAParameterByBitDepths(16, 7);
                    rawImageData = DataFormatConverter.convert16bitIntermediateToDarkIn16bitWithLinLogV1_bayerPatternAwareDiffusion(rawImageData, lossyLinLogModeParameterA, bayerPattern, r2dSettings.linLogDithering, width);
                }
                else if (outputFormat == R2DSettings.DNGOutputDataFormat.BAYER12BITBRIGHTCAPSULEDIN16BITWITHLINLOGTO10BIT)
                {
                    lossyLinLogModeEnabled = true;
                    lossyLinLogModeOutputBitDepth = 10;
                    lossyLinLogModeParameterA = LinLogLutilityClassifiedV1.findAParameterByBitDepths(16, 10);
                    rawImageData = DataFormatConverter.convert16bitIntermediateToDarkIn16bitWithLinLogV1_bayerPatternAwareDiffusion(rawImageData, lossyLinLogModeParameterA, bayerPattern, r2dSettings.linLogDithering, width);
                }
                else if (outputFormat == R2DSettings.DNGOutputDataFormat.BAYER12BITDARKCAPSULEDIN16BITWITHLINLOGTO8BIT)
                {
                    lossyLinLogModeEnabled = true;
                    lossyLinLogModeOutputBitDepth = 8;
                    lossyLinLogModeParameterA = LinLogLutilityClassifiedV1.findAParameterByBitDepths(12, 8);
                    rawImageData = DataFormatConverter.convert16bitIntermediateTo12paddedto16bit(rawImageData);
                    rawImageData = DataFormatConverter.convert16bitIntermediateToDarkIn16bitWithLinLogV1_bayerPatternAwareDiffusion(rawImageData, lossyLinLogModeParameterA, bayerPattern, r2dSettings.linLogDithering, width);
                    output.SetField(TiffTag.BASELINEEXPOSURE, 4);
                }
                else if (outputFormat == R2DSettings.DNGOutputDataFormat.BAYER12BITDARKCAPSULEDIN16BITWITHLINLOGTO7BIT)
                {
                    lossyLinLogModeEnabled = true;
                    lossyLinLogModeOutputBitDepth = 7;
                    lossyLinLogModeParameterA = LinLogLutilityClassifiedV1.findAParameterByBitDepths(12, 7);
                    rawImageData = DataFormatConverter.convert16bitIntermediateTo12paddedto16bit(rawImageData);
                    rawImageData = DataFormatConverter.convert16bitIntermediateToDarkIn16bitWithLinLogV1_bayerPatternAwareDiffusion(rawImageData, lossyLinLogModeParameterA, bayerPattern, r2dSettings.linLogDithering, width);
                    output.SetField(TiffTag.BASELINEEXPOSURE, 4);
                }
                else if (outputFormat == R2DSettings.DNGOutputDataFormat.BAYER12BITDARKCAPSULEDIN16BITWITHLINLOGTO6BIT)
                {
                    lossyLinLogModeEnabled = true;
                    lossyLinLogModeOutputBitDepth = 6;
                    lossyLinLogModeParameterA = LinLogLutilityClassifiedV1.findAParameterByBitDepths(12, 6);
                    rawImageData = DataFormatConverter.convert16bitIntermediateTo12paddedto16bit(rawImageData);
                    rawImageData = DataFormatConverter.convert16bitIntermediateToDarkIn16bitWithLinLogV1_bayerPatternAwareDiffusion(rawImageData, lossyLinLogModeParameterA, bayerPattern, r2dSettings.linLogDithering, width);
                    output.SetField(TiffTag.BASELINEEXPOSURE, 4);
                }
                else if (outputFormat == R2DSettings.DNGOutputDataFormat.BAYER12BITDARKCAPSULEDIN16BITWITHLINLOGTO5BIT)
                {
                    lossyLinLogModeEnabled = true;
                    lossyLinLogModeOutputBitDepth = 5;
                    lossyLinLogModeParameterA = LinLogLutilityClassifiedV1.findAParameterByBitDepths(12, 5);
                    rawImageData = DataFormatConverter.convert16bitIntermediateTo12paddedto16bit(rawImageData);
                    rawImageData = DataFormatConverter.convert16bitIntermediateToDarkIn16bitWithLinLogV1_bayerPatternAwareDiffusion(rawImageData, lossyLinLogModeParameterA, bayerPattern, r2dSettings.linLogDithering, width);
                    output.SetField(TiffTag.BASELINEEXPOSURE, 4);
                }
                else if (outputFormat == R2DSettings.DNGOutputDataFormat.BAYER12BITTIFFPACKED)
                {
                    output.SetField(TiffTag.BITSPERSAMPLE, 12);
                    rawImageData = DataFormatConverter.convert16BitIntermediateToTiffPacked12BitOutput(rawImageData);
                }
                else if (outputFormat == R2DSettings.DNGOutputDataFormat.BAYER12BITDARKCAPSULEDIN16BIT)
                {
                    rawImageData = DataFormatConverter.convert16bitIntermediateTo12paddedto16bit(rawImageData);
                    output.SetField(TiffTag.BASELINEEXPOSURE, 4);
                }


                if (lossyGammaModeEnabled)
                {

                    UInt16 outputMaxValue = (UInt16)(Math.Pow(2, lossyGammaModeOutputBitDepth) - 1);
                    UInt16[] linearizationTable = new UInt16[outputMaxValue + 1];


                    double tmpValue;
                    double invertedGamma = 1 / lossyGammaModeGamma;
                    for (int i = 0; i <= outputMaxValue; i++)
                    {
                        tmpValue = (double)i / (double)outputMaxValue;
                        tmpValue = Math.Pow(tmpValue, invertedGamma) * (double)UInt16.MaxValue;
                        linearizationTable[(Int16)i] = (UInt16)Math.Max(0, Math.Min(UInt16.MaxValue, Math.Round(tmpValue)));
                    }

                    output.SetField(TiffTag.LINEARIZATIONTABLE, linearizationTable.Length, linearizationTable);
                }
                if (lossyLinLogModeEnabled)
                {

                    UInt16 outputMaxValue = (UInt16)(Math.Pow(2, lossyLinLogModeOutputBitDepth) - 1);
                    UInt16[] linearizationTable = new UInt16[outputMaxValue + 1];

                    for (int i = 0; i <= outputMaxValue; i++)
                    {

                        linearizationTable[(Int16)i] = (UInt16)Math.Max(0, Math.Min(UInt16.MaxValue, Math.Round(LinLogLutilityClassifiedV1.LogToLin(i, lossyLinLogModeParameterA))));
                    }

                    output.SetField(TiffTag.LINEARIZATIONTABLE, linearizationTable.Length, linearizationTable);
                }



                // DNG Private data. Includes original files metadata for example, and error info if any errors occurred. 
                metaInfo.mergeErrors(errorInfo);
                const string DNGPRIVDATA_START = "RAWBAYER2DNG\0";
                byte[] dngPrivData = metaInfo.getMergedBinary(Encoding.UTF8.GetBytes(DNGPRIVDATA_START));
                output.SetField(TiffTag.DNGPRIVATEDATA, dngPrivData.Length, dngPrivData);

                //
                if (r2dSettings.writeErrorReports)
                {
                    if (errorInfo.errors.Count > 0)
                    {
                        string errorFileName = Helpers.findUnoccupiedFileName(fileName + ".errors", ".csv", ".");
                        string errorsCSV = errorInfo.getHumanReadableErrorsCSV();
                        File.WriteAllText(errorFileName, errorsCSV);
                    }
                }



                output.SetField(TiffTag.ORIENTATION, Orientation.TOPLEFT);
                output.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
                output.SetField(TiffTag.FILLORDER, FillOrder.MSB2LSB);
                //output.SetField(TiffTag.COMPRESSION, Compression.LZW); //LZW doesn't work with DNG apparently

                if (r2dSettings.compressDNGLegacy && outputFormat != R2DSettings.DNGOutputDataFormat.BAYER12BITTIFFPACKED && !r2dSettings.compressDNGLosslessJPEG)
                { // Sadly combining the ADOBE_DEFLATE compression with 12 bit packing breaks the resulting file.
                    output.SetField(TiffTag.COMPRESSION, Compression.ADOBE_DEFLATE);
                }
                else
                {
                    output.SetField(TiffTag.COMPRESSION, Compression.NONE);
                }


                output.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);

                float[] cam_xyz =
                {
                3.2404542f / (float)RGBAmplify[0], -1.5371385f / (float)RGBAmplify[0], -0.4985314f / (float)RGBAmplify[0],
                    -0.9692660f / (float)RGBAmplify[1], 1.8760108f / (float)RGBAmplify[1], 0.0415560f / (float)RGBAmplify[1],
                    0.0556434f / (float)RGBAmplify[2], -0.2040259f / (float)RGBAmplify[2], 1.0572252f / (float)RGBAmplify[2]
            }; // my sRGB hack
                //float[] cam_xyz =  { 0f, 1f,0f,0f,0f,1f,1f,0f,0f }; // my sRGB hack
                float[] neutral = { 1f / (float)RGBAmplify[0], 1f / (float)RGBAmplify[1], 1f / (float)RGBAmplify[2] }; // my sRGB hack
                int[] bpp = { 8, 8, 8 }; // my sRGB hack
                short[] bayerpatterndimensions = { 2, 2 }; // my sRGB hack
                //float[] neutral = { 0.807133f, 1.0f, 0.913289f };

                //DNG 
                output.SetField(TiffTag.SUBFILETYPE, 0);
                output.SetField(TiffTag.MAKE, r2dSettings.metaMake);
                output.SetField(TiffTag.MODEL, r2dSettings.metaModel);
                output.SetField(TiffTag.SOFTWARE, r2dSettings.metaSoftware.Trim() == "" ? "RawBayer2DNG" : r2dSettings.metaSoftware + "(+RawBayer2DNG)");
                output.SetField(TiffTag.DNGVERSION, "\x1\x4\x0\x0");
                output.SetField(TiffTag.DNGBACKWARDVERSION, "\x1\x4\x0\x0");
                output.SetField(TiffTag.UNIQUECAMERAMODEL, r2dSettings.metaUniqueCameraModel);
                output.SetField(TiffTag.COLORMATRIX1, 9, cam_xyz);
                output.SetField(TiffTag.ASSHOTNEUTRAL, 3, neutral);
                output.SetField(TiffTag.CALIBRATIONILLUMINANT1, 21);
                output.SetField(TiffTag.ORIGINALRAWFILENAME, sourceFileNameForTIFFTag);
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

                if (outputFormat != R2DSettings.DNGOutputDataFormat.BAYER12BITTIFFPACKED && r2dSettings.compressDNGLosslessJPEG)
                {
                    if (r2dSettings.losslessJPEGTiling)
                    {

                        UInt16[] rawImageDataUInt16 = new UInt16[rawImageData.Length / 2];
                        for (int i = 0; i < rawImageDataUInt16.Length; i++)
                        {
                            rawImageDataUInt16[i] = BitConverter.ToUInt16(rawImageData, i * 2);
                        }

                        int tilesHCount = (int)Math.Ceiling((double)width / (double)tileSize);
                        int tilesVCount = (int)Math.Ceiling((double)height / (double)tileSize);
                        UInt16[] tileData = new UInt16[tileSize * tileSize];

                        byte[,][] encodedTile = new byte[tilesHCount, tilesVCount][];

                        dng_stream whatever;

                        for (int x = 0; x < tilesHCount; x++)
                        {
                            for (int y = 0; y < tilesVCount; y++)
                            {
                                tileData = new UInt16[tileSize * tileSize];

                                for (int row = 0; row < (Math.Min(height, ((y + 1) * tileSize)) - (y * tileSize)); row++)
                                {
                                    Array.Copy(rawImageDataUInt16, (y * tileSize + row) * width + x * tileSize, tileData, row * tileSize, Math.Min(width, ((x + 1) * tileSize)) - x * tileSize);
                                }

                                // Tile is prepared now. Let's encode it.
                                whatever = new dng_stream();
                                DNGLosslessEncoder.EncodeLosslessJPEG(tileData, (uint)tileSize, (uint)tileSize / 2, 2, 16, tileSize, 2, whatever);

                                encodedTile[x, y] = whatever.toByteArray();
                                whatever = null;


                                //encodedFileSizeHere += encodedTile.Length;

                                //File.WriteAllBytes("encodedTest.jpg", whatever.toByteArray());

                            }
                        }

                        output.SetField(TiffTag.COMPRESSION, Compression.JPEG);
                        output.SetField(TiffTag.TILELENGTH, tileSize);
                        output.SetField(TiffTag.TILEWIDTH, tileSize);

                        for (int x = 0; x < tilesHCount; x++)
                        {
                            for (int y = 0; y < tilesVCount; y++)
                            {
                                int tileNubmer = output.ComputeTile(x * tileSize, y * tileSize, 0, 0);
                                output.WriteRawTile(tileNubmer, encodedTile[x, y], encodedTile[x, y].Length);
                            }
                        }
                        encodedTile = null;
                        rawImageDataUInt16 = null;
                        /*

                        dng_stream compressedJpegData = new dng_stream();
                        DNGLosslessEncoder.EncodeLosslessJPEG(rawImageDataUInt16, (uint)height, (uint)width / 2, 2, 16, width, 2, compressedJpegData);
                        byte[] compressedJpegDataByteArray = compressedJpegData.toByteArray();

                        output.SetField(TiffTag.COMPRESSION, Compression.JPEG);
                        output.WriteRawStrip(0, compressedJpegDataByteArray, compressedJpegDataByteArray.Length);*/

                    }
                    else
                    {
                        output.SetField(TiffTag.ROWSPERSTRIP, height);
                        UInt16[] rawImageDataUInt16 = new UInt16[rawImageData.Length / 2];
                        for (int i = 0; i < rawImageDataUInt16.Length; i++)
                        {
                            rawImageDataUInt16[i] = BitConverter.ToUInt16(rawImageData, i * 2);
                        }

                        dng_stream compressedJpegData = new dng_stream();
                        DNGLosslessEncoder.EncodeLosslessJPEG(rawImageDataUInt16, (uint)height, (uint)width / 2, 2, 16, width, 2, compressedJpegData);
                        rawImageDataUInt16 = null;
                        byte[] compressedJpegDataByteArray = compressedJpegData.toByteArray();

                        output.SetField(TiffTag.COMPRESSION, Compression.JPEG);
                        output.WriteRawStrip(0, compressedJpegDataByteArray, compressedJpegDataByteArray.Length);
                        compressedJpegDataByteArray = null;
                    }
                }
                else
                {

                    output.SetField(TiffTag.ROWSPERSTRIP, height);
                    output.WriteEncodedStrip(0, rawImageData, rawImageData.Length);
                }
            }
#if DEBUG
        } catch (Exception e)
            {
                MessageBox.Show($"Error processing file {targetFilename}: "+e.Message);
            }
#endif

        }

        private int getSetCount()
        {
            ShotSettings shotSettings = getShotSettings();
            return (int)Math.Floor(((double)imageSequenceSource.getImageCount() - (double)shotSettings.delay) / (double)shotSettings.shots.Length);

        }

        private void loadedSequenceGUIUpdate(string sourceName = "")
        {



            txtSrcFolder.Text = sourceName;
            txtStatus.Text = "Source set to " + sourceName;

            int setCount = getSetCount();

            currentImagNumber.Text = "1";
            totalImageCount.Text = setCount.ToString();
            slide_currentFile.Maximum = setCount;
            slide_currentFile.Minimum = 1;
            slide_currentFile.Value = 1;
            btnProcessFolder.IsEnabled = true;


        }

        private void BtnLoadRAWFolder_Click(object sender, RoutedEventArgs e)
        {
            // reset progress counters
            CurrentProgress = 0;
            _counter = 0;
            var fbd = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            //  save path as a setting - We typically capture to the same folder every time.
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.InputFolder) && Directory.Exists(Properties.Settings.Default.InputFolder))
            {
                fbd.SelectedPath = Properties.Settings.Default.InputFolder;
            }

            bool? result = fbd.ShowDialog();

            if (result == true && !string.IsNullOrWhiteSpace(fbd.SelectedPath) && Directory.Exists(fbd.SelectedPath))
            {
                sourceFolder = fbd.SelectedPath;

                if (targetFolder == null)
                {
                    targetFolder = sourceFolder;
                    txtTargetFolder.Text = targetFolder;
                }
                filesInSourceFolder = Directory.GetFiles(fbd.SelectedPath, "*.raw");
                Array.Sort(filesInSourceFolder, new AlphanumComparatorFast());


                int width = 2448;
                int height = 2048;
                width = r2dSettings.rawWidth;//int.Parse(rawWidth.Text);
                height = r2dSettings.rawHeight;//int.Parse(rawHeight.Text);


                // Option to reverse file order when running film in reverse!
                // Todo find a more universal way to do this
                /*if (r2dSettings.reverseOrder)
                {
                    Array.Reverse(filesInSourceFolder);
                    filesAreReversed = true;
                }*/ // Is now implemented directly in the processing, so works with all kinds of sources.

                imageSequenceSource = new RAWSequenceSource(getInputFormat(), width, height, getBayerPattern(), filesInSourceFolder);

                loadedSequenceGUIUpdate("[RAW Folder] " + sourceFolder);

                Properties.Settings.Default.InputFolder = sourceFolder;
                Properties.Settings.Default.Save();

            }
        }

        private void Slide_currentFile_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ReDrawPreview();
        }

        private byte[,] getBayerPattern()
        {
            return this.Dispatcher.Invoke(() =>
            {
                //0=Red, 1=Green,   2=Blue
                byte bayerColorA = (byte)r2dSettings.colorBayerA;
                byte bayerColorB = (byte)r2dSettings.colorBayerB;
                byte bayerColorC = (byte)r2dSettings.colorBayerC;
                byte bayerColorD = (byte)r2dSettings.colorBayerD;
                byte[,] bayerPattern = { { bayerColorA, bayerColorB }, { bayerColorC, bayerColorD } };
                return bayerPattern;
            });
        }


        private RAWDATAFORMAT getInputFormat()
        {
            return this.Dispatcher.Invoke(() =>
            {
                RAWDATAFORMAT inputFormat = RAWDATAFORMAT.BAYER12BITBRIGHTCAPSULEDIN16BIT;

                if (r2dSettings.inputFormat == R2DSettings.InputFormat.RAW16BIT)
                {
                    inputFormat = RAWDATAFORMAT.BAYER12BITBRIGHTCAPSULEDIN16BIT;
                }
                else if (r2dSettings.inputFormat == R2DSettings.InputFormat.RAW12P)
                {
                    inputFormat = RAWDATAFORMAT.BAYERRG12p;
                }
                else if (r2dSettings.inputFormat == R2DSettings.InputFormat.RAW12PV2)
                {
                    inputFormat = RAWDATAFORMAT.BAYERRG12pV2;
                }
                else if (r2dSettings.inputFormat == R2DSettings.InputFormat.RAW10P1)
                {
                    inputFormat = RAWDATAFORMAT.BAYER10p1;
                }
                return inputFormat;
            });
        }

        // returns buffer with HDR merge already performed.
        // Expects 16 bit linear input.
        private byte[] HDRMerge(byte[][] buffers, ShotSettings shotSettings)
        {

            // Fast skip for non HDR
            // Shouldn't really be necessary because it should be caught outside during normal raw processing. This might speed up the preview though.
            if (buffers.Length == 1 && shotSettings.shots.Length == 1)
            {
                return buffers[0];
            }

            float clippingPoint = shotSettings.clippingPoint;
            float featherMultiplier = shotSettings.featherMultiplier;

            float featherBottomIntensity = clippingPoint;
            float featherRange = 0;
            if (featherMultiplier != 1)
            {
                featherBottomIntensity *= featherMultiplier;
                featherRange = clippingPoint - featherBottomIntensity;
            }

            byte[] outputBuffer = new byte[buffers[0].Length];

            int singleBufferLength = buffers[0].Length;

            //double maxValue = 0;

            //double Uint16MaxValueDouble = (double)UInt16.MaxValue;
            float Uint16MaxValueFloat = (float)UInt16.MaxValue;


            // Do one color after another
            //for (var colorIndex = 0; colorIndex < 3; colorIndex++)


            Vector2 Uint16Divider = new Vector2();
            float thisMultiplierMultiplier;
            int thisIndex;
            ShotSettingBayer thisShotSetting;
            float effectiveMultiplier;
            float currentOutputValue;
            float currentInputValue;
            bool isClipping;
            UInt16 finalValue;
            float inputIntensity;
            float tmpValue;
            byte[] sixteenbitbytes;


            thisIndex = 0;
            thisMultiplierMultiplier = 1;
            for (var shotSettingIndex = 0; shotSettingIndex < shotSettings.shots.Length; shotSettingIndex++)
            {
                thisShotSetting = shotSettings.shots[shotSettingIndex];

                // first image of each set just has its buffer copied for speed reasons
                if (thisIndex == 0)
                {
                    outputBuffer = buffers[thisShotSetting.orderIndex];
                    // The darkest image's multiplier should technically be 1 by default. But if it isn't, we use this to normalize the following images.
                    // For example, if the darkest image multiplier is 2, we record the "multiplier multiplier" as 0.5, as we aren't actually multiplying this image data by 2
                    // and as a result we need to reduce the image multiplier of following images by multiplying it with 0.5.
                    thisMultiplierMultiplier = 1 / thisShotSetting.exposureMultiplier;
                }

                // Do actual HDR merging
                else
                {
                    effectiveMultiplier = thisMultiplierMultiplier * thisShotSetting.exposureMultiplier;

                    if (featherMultiplier < 1 && featherRange != 0)
                    {
                        for (var i = 0; i < singleBufferLength; i += 2) // 16 bit steps
                        {
                            Uint16Divider.X = (float)BitConverter.ToUInt16(outputBuffer, i);
                            Uint16Divider.Y = (float)BitConverter.ToUInt16(buffers[thisShotSetting.orderIndex], i);


                            /*currentOutputValue = (double)BitConverter.ToUInt16(outputBuffers[colorIndex], i) / Uint16MaxValueDouble;
                            currentInputValue = (double)BitConverter.ToUInt16(buffers[thisShotSetting.orderIndex], i) / Uint16MaxValueDouble;*/
                            Uint16Divider = Vector2.Divide(Uint16Divider, Uint16MaxValueFloat);
                            currentOutputValue = Uint16Divider.X;
                            currentInputValue = Uint16Divider.Y;

                            //if(currentInputValue > maxValue) { maxValue = currentInputValue; }
                            isClipping = currentInputValue > clippingPoint;
                            if (!isClipping)
                            {
                                finalValue = 0;
                                if (currentInputValue > featherBottomIntensity)
                                {
                                    inputIntensity = (featherRange - (clippingPoint - currentInputValue)) / featherRange;
                                    currentInputValue /= effectiveMultiplier;
                                    tmpValue = inputIntensity * currentInputValue + (1 - inputIntensity) * currentOutputValue;
                                    finalValue = (UInt16)Math.Round(tmpValue * Uint16MaxValueFloat);
                                }
                                else
                                {
                                    currentInputValue /= effectiveMultiplier;
                                    finalValue = (UInt16)Math.Round(currentInputValue * Uint16MaxValueFloat);
                                }

                                sixteenbitbytes = BitConverter.GetBytes(finalValue);
                                outputBuffer[i] = sixteenbitbytes[0];
                                outputBuffer[i + 1] = sixteenbitbytes[1];
                            }
                        }
                    }
                    else
                    {

                        for (var i = 0; i < singleBufferLength; i += 2) // 16 bit steps
                        {
                            // Comments:
                            // You might want to use Buffer.BlockCopy to convert the array from raw bytes to unsigned shorts
                            // https://markheath.net/post/how-to-convert-byte-to-short-or-float
                            // Or: Span<ushort> a = MemoryMarshal.Cast<byte, ushort>(data)
                            // https://markheath.net/post/span-t-audio
                            Uint16Divider.X = (float)BitConverter.ToUInt16(outputBuffer, i);
                            Uint16Divider.Y = (float)BitConverter.ToUInt16(buffers[thisShotSetting.orderIndex], i);


                            /*currentOutputValue = (double)BitConverter.ToUInt16(outputBuffers[colorIndex], i) / Uint16MaxValueDouble;
                            currentInputValue = (double)BitConverter.ToUInt16(buffers[thisShotSetting.orderIndex], i) / Uint16MaxValueDouble;*/
                            Uint16Divider = Vector2.Divide(Uint16Divider, Uint16MaxValueFloat);
                            currentOutputValue = Uint16Divider.X;
                            currentInputValue = Uint16Divider.Y;

                            //if (currentInputValue > maxValue) { maxValue = currentInputValue; }
                            isClipping = currentInputValue > clippingPoint;
                            if (!isClipping)
                            {
                                finalValue = 0;
                                currentInputValue /= effectiveMultiplier;
                                finalValue = (UInt16)Math.Round(currentInputValue * Uint16MaxValueFloat);

                                sixteenbitbytes = BitConverter.GetBytes(finalValue);
                                outputBuffer[i] = sixteenbitbytes[0];
                                outputBuffer[i + 1] = sixteenbitbytes[1];
                            }
                        }
                    }

                }
                thisIndex++;

            }




            //MessageBox.Show(maxValue.ToString());

            return outputBuffer;
        }

        private void ReDrawPreview()
        {
            if (imageSequenceSource == null)
            {
                return; // Nothing to do here
            }

            // Do this to not break functionality with the old algorithm and allow changes on the fly
            // Might change/remove in the future.
            if (imageSequenceSource.getSourceType() == ImageSequenceSource.ImageSequenceSourceType.RAW)
            {

                int width = r2dSettings.rawWidth;//int.Parse(rawWidth.Text);
                int height = r2dSettings.rawHeight;//int.Parse(rawHeight.Text);
                ((RAWSequenceSource)imageSequenceSource).width = width;
                ((RAWSequenceSource)imageSequenceSource).height = height;
                RAWDATAFORMAT inputFormat = getInputFormat();

                ((RAWSequenceSource)imageSequenceSource).rawDataFormat = inputFormat;
            }

            bool doPreviewDebayer = r2dSettings.previewDebayer;
            bool doPreviewGamma = r2dSettings.previewWithSRGBGamma;
            bool doDrawPreviewScope = r2dSettings.drawScope;

            ShotSettings shotSettings = getShotSettings();

            int sliderNumber = (int)slide_currentFile.Value;
            //int index = ;

            int firstIndex = (sliderNumber - 1) * shotSettings.shots.Length + shotSettings.delay;

            bool anythingMissing = false;
            string whatsMissing = "";

            int[] indiziForMerge = new int[shotSettings.shots.Length];
            byte[][] buffersForMerge = new byte[shotSettings.shots.Length][];

            // Check if all necessary shots exist and add the indizi to an array.
            int thatIndex;
            for (int i = 0; i < shotSettings.shots.Length; i++)
            {
                thatIndex = firstIndex + i;
                if (!imageSequenceSource.imageExists(i))
                {
                    anythingMissing = true;
                    whatsMissing = imageSequenceSource.getImageName(i);
                    break;
                }
                else
                {
                    indiziForMerge[i] = thatIndex;
                    buffersForMerge[i] = imageSequenceSource.getRawImageData(thatIndex);
                    // Convert to 16 bit if necessary
                    if (imageSequenceSource.getRawDataFormat() == RAWDATAFORMAT.BAYERRG12p)
                    {
                        buffersForMerge[i] = DataFormatConverter.convert12pInputto16bit(buffersForMerge[i]);
                    }
                    if (imageSequenceSource.getRawDataFormat() == RAWDATAFORMAT.BAYERRG12pV2)
                    {
                        buffersForMerge[i] = DataFormatConverter.convert12pV2Inputto16bit(buffersForMerge[i]);
                    }/*
                    if (imageSequenceSource.getRawDataFormat() == RAWDATAFORMAT.CINTEL10BIT)
                    {
                        buffersForMerge[i] = DataFormatConverter.tryConvertCintel10Inputto16bit(buffersForMerge[i]);
                    }*/
                    if (imageSequenceSource.getRawDataFormat() == RAWDATAFORMAT.BAYER10p1)
                    {
                        buffersForMerge[i] = DataFormatConverter.convert10p1Inputto16bit(buffersForMerge[i]);
                    }
                    if (imageSequenceSource.getRawDataFormat() == RAWDATAFORMAT.BAYER12BITDARKCAPSULEDIN16BIT)
                    {
                        buffersForMerge[i] = DataFormatConverter.convert12paddedto16Inputto16bit(buffersForMerge[i]);
                    }
                }
            }

            //string selectedRawFile = filesInSourceFolder[index];
            if (anythingMissing)
            {
                MessageBox.Show("weirdo error, apparently image " + whatsMissing + " (no longer?) exists");
                return;
            }
            else
            {


                int subsample = 4;

                int width = imageSequenceSource.getWidth();
                int height = imageSequenceSource.getHeight();


                byte[] buff = HDRMerge(buffersForMerge, shotSettings);

                uint[] cropAmounts = r2dSettings.getCropAmounts();

                if (cropAmounts[0] != 0 || cropAmounts[1] != 0 || cropAmounts[2] != 0 || cropAmounts[3] != 0)
                {

                    buff = Helpers.cropBuffer16bitMono(buff, (uint)width, (uint)height, cropAmounts);
                    width = (int)(width - cropAmounts[0] - cropAmounts[2]);
                    height = (int)(height - cropAmounts[1] - cropAmounts[3]);
                }


                int newWidth = (int)Math.Ceiling((double)width / subsample);
                int newHeight = (int)Math.Ceiling((double)height / subsample);



                int byteDepth = 2; // This is for the source
                int byteWidth = newWidth * 3; // This is for the preview. 3 means RGB
                int newStride = Helpers.getStride(byteWidth);
                //byte[] newbytes = Helpers.PadLines(buff, height, width, newStride,2);

                byte[] newbytes;

                byte[,] bayerPattern = imageSequenceSource.getBayerPattern();


                double[] RGBamplify = { r2dSettings.redMultiplier, r2dSettings.greenMultiplier, r2dSettings.blueMultiplier };
                if (doPreviewDebayer)
                {
                    newbytes = Helpers.DrawBayerPreview(buff, newHeight, newWidth, height, width, newStride, byteDepth, subsample, doPreviewGamma, bayerPattern, RGBamplify);
                }
                else
                {

                    newbytes = Helpers.DrawPreview(buff, newHeight, newWidth, height, width, newStride, byteDepth, RGBamplify, subsample, doPreviewGamma);
                }

                Bitmap scopeImage = new Bitmap(1, 1);
                // Draw scope
                if (doDrawPreviewScope)
                {
                    int scopeWidth = (int)scopeDockPanel.ActualWidth;
                    int scopeHeight = (int)scopeDockPanel.ActualHeight;
                    int newStrideScope = Helpers.getStride(scopeWidth * 3);
                    byte[] scopeBytes;
                    scopeBytes = Helpers.DrawScope(buff, scopeHeight, scopeWidth, height, width, newStrideScope, byteDepth, subsample, doPreviewGamma, bayerPattern, RGBamplify);
                    scopeImage = new Bitmap(scopeWidth, scopeHeight, Imaging.PixelFormat.Format24bppRgb);
                    Imaging.BitmapData scopePixelData = scopeImage.LockBits(new Rectangle(0, 0, scopeWidth, scopeHeight), Imaging.ImageLockMode.WriteOnly, Imaging.PixelFormat.Format24bppRgb);
                    System.Runtime.InteropServices.Marshal.Copy(scopeBytes, 0, scopePixelData.Scan0, scopeBytes.Count());
                    scopeImage.UnlockBits(scopePixelData);
                }

                // Draw magnifier rectangle
                // Position values are in percent from 0 to 1
                // Explanation: value 0 means left edge at left image border. value 1 means right edge at right image border.
                int magnifierSrcSizeX = 20;
                int magnifierSrcSizeY = 20;
                int magnifierPreviewSizeX = (int)Math.Floor((double)magnifierSrcSizeX / subsample);
                int magnifierPreviewSizeY = (int)Math.Floor((double)magnifierSrcSizeY / subsample);
                double magnifierPositionX = magnifierHorizontalPosition.Value;
                double magnifierPositionY = magnifierVerticalPosition.Value;
                Rectangle positionSrc = new Rectangle(
                    Helpers.MinMultipleOfTwo((int)Math.Floor(magnifierPositionX * (width - magnifierSrcSizeX))),  // Using multiple of two function here to not mess up bayer pattern evaluation
                    Helpers.MinMultipleOfTwo((int)Math.Floor(magnifierPositionY * (height - magnifierSrcSizeY))),
                    magnifierSrcSizeX, magnifierSrcSizeY);
                Rectangle positionPreview = new Rectangle(
                    (int)Math.Floor((double)positionSrc.X / subsample),
                    (int)Math.Floor((double)positionSrc.Y / subsample), magnifierPreviewSizeX, magnifierPreviewSizeY
                    );
                newbytes = Helpers.drawRectangle(newbytes, newWidth, newHeight, positionPreview);




                // Put preview into WPF image tag
                Bitmap manipulatedImage = new Bitmap(newWidth, newHeight, Imaging.PixelFormat.Format24bppRgb);
                Imaging.BitmapData pixelData = manipulatedImage.LockBits(new Rectangle(0, 0, newWidth, newHeight), Imaging.ImageLockMode.WriteOnly, Imaging.PixelFormat.Format24bppRgb);

                //Bitmap im = new Bitmap(width, height, newStride, Imaging.PixelFormat.Format16bppGrayScale,  Marshal.UnsafeAddrOfPinnedArrayElement(newbytes, 0));

                System.Runtime.InteropServices.Marshal.Copy(newbytes, 0, pixelData.Scan0, newbytes.Count());
                //im.GetPixel(1, 1);
                //im.GetPixel(2447, 2047);
                //pixelData.
                manipulatedImage.UnlockBits(pixelData);

                // Calculate Magnifier
                Bitmap magnifierArea = new Bitmap(positionSrc.Width, positionSrc.Height, Imaging.PixelFormat.Format24bppRgb);
                pixelData = magnifierArea.LockBits(new Rectangle(0, 0, positionSrc.Width, positionSrc.Height), Imaging.ImageLockMode.WriteOnly, Imaging.PixelFormat.Format24bppRgb);
                newbytes = Helpers.DrawMagnifier(buff, positionSrc, width, doPreviewGamma, byteDepth, RGBamplify, bayerPattern);

                System.Runtime.InteropServices.Marshal.Copy(newbytes, 0, pixelData.Scan0, newbytes.Count());
                magnifierArea.UnlockBits(pixelData);

                magnifierArea = Helpers.ResizeBitmapNN(magnifierArea, 200, 200);

                // Do the displaying
                if (doDrawPreviewScope)
                {
                    mainPreviewScope.Source = Helpers.BitmapToImageSource(scopeImage);
                }
                mainPreview.Source = Helpers.BitmapToImageSource(manipulatedImage);
                Magnifier.Source = Helpers.BitmapToImageSource(magnifierArea);
            }
        }


        private void BtnLoadTargetFolder_Click(object sender, RoutedEventArgs e)
        {
            var fbd = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();

            // save path as a setting - We typically capture & output to the same folder every time.
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.OutputFolder) && Directory.Exists(Properties.Settings.Default.OutputFolder))
            {
                fbd.SelectedPath = Properties.Settings.Default.OutputFolder;
            }

            bool? result = fbd.ShowDialog();

            if (result == true && !string.IsNullOrWhiteSpace(fbd.SelectedPath) && Directory.Exists(fbd.SelectedPath))
            {
                targetFolder = fbd.SelectedPath;
                txtTargetFolder.Text = targetFolder;
                txtStatus.Text = "Target folder set to " + targetFolder;
                Properties.Settings.Default.OutputFolder = targetFolder;
                Properties.Settings.Default.Save();
            }
        }

        private void BtnProcessFolder_Click(object sender, RoutedEventArgs e)
        {
            // reset progress counters
            currentProgress = 0;
            _counter = 0;
            _newFileName = r2dSettings.outputSequenceCustomNaming;
            //_newFileName = Rename.Text;
            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = true;
            worker.DoWork += worker_DoWork;
            worker.ProgressChanged += worker_ProgressChanged;
            worker.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    MessageBox.Show("There was an error! " + b.Error.ToString());
                }
            };
            worker.RunWorkerAsync();
        }

        private void btnSaveSettings_Click(object sender, RoutedEventArgs e)
        {

            Properties.Settings.Default.Save();
        }

        public int CurrentProgress
        {
            get { return currentProgress; }
            private set
            {
                if (currentProgress != value)
                {
                    currentProgress = value;
                    OnPropertyChanged("CurrentProgress");
                }
            }
        }

        public string CurrentStatus
        {
            get { return currentStatus; }
            private set
            {
                if (currentStatus != value)
                {
                    currentStatus = value;
                    OnPropertyChanged("CurrentStatus");
                }
            }
        }

        private struct SetInfo
        {
            public int[] shotIndizi;
            public string outputName;
            public string originalFilename;
        }


        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            // Fallback to not break functionality for now
            if (imageSequenceSource.getSourceType() == ImageSequenceSource.ImageSequenceSourceType.RAW)
            {

                ((RAWSequenceSource)imageSequenceSource).bayerPattern = getBayerPattern();
                ((RAWSequenceSource)imageSequenceSource).rawDataFormat = getInputFormat();
            }


            byte[,] bayerPattern = imageSequenceSource.getBayerPattern();
            RAWDATAFORMAT inputFormat = imageSequenceSource.getRawDataFormat();

            _totalFiles = imageSequenceSource.getImageCount();





            ShotSettings shotSettings = new ShotSettings();
            int setCount = 0;

            double[] RGBamplify = { 1, 1, 1 };

            this.Dispatcher.Invoke(() =>
            {
                //RGBamplify = new double[]{ rAmplify.Value, gAmplify.Value, bAmplify.Value };
                RGBamplify = new double[] { r2dSettings.redMultiplier, r2dSettings.greenMultiplier, r2dSettings.blueMultiplier };
                shotSettings = getShotSettings();
                setCount = getSetCount();
            });

            // create lookup table: <inputfile, outputfile> 
            //Dictionary<int, SetInfo> dic = new Dictionary<int, SetInfo>();
            SetInfo[] dic = new SetInfo[setCount];


            uint[] cropAmountsAtBegin = r2dSettings.getCropAmounts();//(uint[])cropAmounts.Clone();


            int firstIndex;
            bool anythingMissing = false;
            string whatsMissing = "";
            int[] indiziForMerge;
            bool reverse = r2dSettings.reverseOrder;

            int startIndex = 0, endIndex = setCount - 1, increment = 1;
            if (reverse)
            {
                startIndex = setCount - 1;
                endIndex = 0;
                increment = -1;
            }

            int index = 0;
            string[] originalFilenames = new string[shotSettings.shots.Length];
            for (int i = startIndex; increment > 0 ? i <= endIndex : i >= endIndex; i += increment)
            //for (int i=0;i<setCount;i++)
            {

                firstIndex = i * shotSettings.shots.Length + shotSettings.delay;
                string fileNameWithoutExtension =
                    targetFolder + "\\" + Path.GetFileNameWithoutExtension(imageSequenceSource.getImageName(firstIndex));
                string outputFile = fileNameWithoutExtension + ".dng";

                // Check if anything's missing
                indiziForMerge = new int[shotSettings.shots.Length];
                anythingMissing = false;
                whatsMissing = "";
                int thatIndex;
                for (int a = 0; a < shotSettings.shots.Length; a++)
                {
                    thatIndex = firstIndex + a;
                    if (!imageSequenceSource.imageExists(thatIndex))
                    {
                        anythingMissing = true;
                        whatsMissing = imageSequenceSource.getImageName(thatIndex);
                        break;
                    }
                    else
                    {
                        indiziForMerge[a] = thatIndex;
                        originalFilenames[a] = Path.GetFileName(imageSequenceSource.getImageName(thatIndex));
                    }
                }

                if (anythingMissing) continue; //skip this one, files are missing


                if (!String.IsNullOrWhiteSpace(_newFileName))
                {
                    string serializer = index.ToString().PadLeft(6, '0');
                    outputFile = targetFolder + "\\" + _newFileName + "_" + serializer + ".dng";
                }
                //dic.Add(index, new SetInfo() { shotIndizi = indiziForMerge, outputName= outputFile }) ;
                dic[index] = new SetInfo() { shotIndizi = indiziForMerge, outputName = outputFile, originalFilename = String.Join(",", originalFilenames) };
                index++;
            }

            // Split into separate sequences if desired
            // We do it here separately from the for above to make it easier to read and not convolute too many concepts into one block
            if (r2dSettings.splitOutputSequence && r2dSettings.splitOutputSequenceCount > 1)
            {
                int[] indizi = new int[r2dSettings.splitOutputSequenceCount];

                // Create output sequence folders if they do not yet exist.
                for (int i = 0; i < r2dSettings.splitOutputSequenceCount; i++)
                {
                    string outputFolder = targetFolder + "\\" + (i + 1).ToString();
                    Directory.CreateDirectory(outputFolder);
                }

                for (int i = 0; i < dic.Length; i++)
                {
                    if (!String.IsNullOrWhiteSpace(_newFileName)) // In this case, enumerate all entries
                    {
                        string serializer = (indizi[i % r2dSettings.splitOutputSequenceCount]++).ToString().PadLeft(6, '0');
                        dic[i].outputName = targetFolder + "\\" + ((i % r2dSettings.splitOutputSequenceCount) + 1).ToString() + "\\" + _newFileName + "_" + serializer + ".dng";
                    }
                    else
                    {
                        dic[i].outputName = targetFolder + "\\" + ((i % r2dSettings.splitOutputSequenceCount) + 1).ToString() + "\\" + Path.GetFileName(dic[i].outputName);
                    }
                }
            }


            var countLock = new object();
            CurrentProgress = 0;

            int threads = r2dSettings.maxThreads;//Properties.Settings.Default.MaxThreads;

            if (threads == 0)
                threads = Environment.ProcessorCount > 1 ? Environment.ProcessorCount / 2 : Environment.ProcessorCount;

            Parallel.ForEach(dic,
                new ParallelOptions { MaxDegreeOfParallelism = threads }, (currentImage, loopState) =>
                // foreach (string srcFileName in filesInSourceFolder)
                {
                    if (worker.CancellationPending == true)
                    {
                        e.Cancel = true;
                        return;
                    }


                    ISSMetaInfo metaInfo = new ISSMetaInfo();
                    ISSErrorInfo errorInfo = new ISSErrorInfo();

                    // check to see if output file already exists
                    if (File.Exists(currentImage.outputName))
                    {
                        // Error: File already exists. No overwriting. Move on.
                        //continue;
                        return;
                    }

                    if (shotSettings.shots.Length == 1) // SDR (single image)
                    {
                        byte[] tmpBuff = imageSequenceSource.getRawImageData(currentImage.shotIndizi[0], ref metaInfo, ref errorInfo);
                        if (inputFormat == RAWDATAFORMAT.BAYERRG12p)
                        {
                            tmpBuff = DataFormatConverter.convert12pInputto16bit(tmpBuff);
                        }
                        if (inputFormat == RAWDATAFORMAT.BAYERRG12pV2)
                        {
                            tmpBuff = DataFormatConverter.convert12pV2Inputto16bit(tmpBuff);
                        }/*
                        if (inputFormat == RAWDATAFORMAT.CINTEL10BIT)
                        {
                            tmpBuff = DataFormatConverter.tryConvertCintel10Inputto16bit(tmpBuff);
                        }*/
                        //byte[] tmpBuff = imageSequenceSource.getRawImageData(currentImage.shotIndizi[0]);

                        if (inputFormat == RAWDATAFORMAT.BAYER10p1)
                        {
                            tmpBuff = DataFormatConverter.convert10p1Inputto16bit(tmpBuff);
                        }
                        if (imageSequenceSource.getRawDataFormat() == RAWDATAFORMAT.BAYER12BITDARKCAPSULEDIN16BIT)
                        {
                            tmpBuff = DataFormatConverter.convert12paddedto16Inputto16bit(tmpBuff);
                        }

                        ProcessRAW(tmpBuff, currentImage.outputName, bayerPattern, inputFormat, RGBamplify, cropAmountsAtBegin, metaInfo, errorInfo, currentImage.originalFilename);

                    }
                    else // HDR
                    {
                        byte[][] buffersForHDR = new byte[3][];
                        int c = 0;
                        foreach (int thisThereThatIndex in currentImage.shotIndizi)
                        {

                            buffersForHDR[c] = imageSequenceSource.getRawImageData(thisThereThatIndex, ref metaInfo, ref errorInfo);
                            if (inputFormat == RAWDATAFORMAT.BAYERRG12p)
                            {
                                buffersForHDR[c] = DataFormatConverter.convert12pInputto16bit(buffersForHDR[c]);
                            }
                            if (inputFormat == RAWDATAFORMAT.BAYERRG12pV2)
                            {
                                buffersForHDR[c] = DataFormatConverter.convert12pV2Inputto16bit(buffersForHDR[c]);
                            }/*
                            if (inputFormat == RAWDATAFORMAT.CINTEL10BIT)
                            {
                                buffersForHDR[c] = DataFormatConverter.tryConvertCintel10Inputto16bit(buffersForHDR[c]);
                            }*/
                            if (inputFormat == RAWDATAFORMAT.BAYER10p1)
                            {
                                buffersForHDR[c] = DataFormatConverter.convert10p1Inputto16bit(buffersForHDR[c]);
                            }
                            if (imageSequenceSource.getRawDataFormat() == RAWDATAFORMAT.BAYER12BITDARKCAPSULEDIN16BIT)
                            {
                                buffersForHDR[c] = DataFormatConverter.convert12paddedto16Inputto16bit(buffersForHDR[c]);
                            }
                            c++;
                        }

                        // For debugging
                        //File.WriteAllText("debug.txt","Clipping point: "+shotSettings.clippingPoint+", feather "+shotSettings.featherMultiplier);

                        ProcessRAW(HDRMerge(buffersForHDR, shotSettings), currentImage.outputName, bayerPattern, inputFormat, RGBamplify, cropAmountsAtBegin, metaInfo, errorInfo, currentImage.originalFilename);
                    }


                    _counter++;
                    var percentage = (double)_counter / _totalFiles * 100.0;
                    lock (countLock) { worker?.ReportProgress((int)percentage); }

                });

            worker?.ReportProgress(100);
        }

        private void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            // pbStatus.Value = e.ProgressPercentage;
            CurrentProgress = e.ProgressPercentage;
            txtStatus.Text = $"Processed {_counter} out of {_totalFiles}";

            if (currentProgress == 100) txtStatus.Text = "Processing complete.";

            //this.Dispatcher.BeginInvoke(new Action(() => { pbStatus.Value = e.ProgressPercentage; }));
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            worker.CancelAsync();
        }

        private void MagnifierPosition_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ReDrawPreview();
        }

        private void FormatRadio_Checked(object sender, RoutedEventArgs e)
        {
            ReDrawPreview();
        }
        private void btnLoadStreamPixSeq_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Streampix .seq files (.seq)|*.seq";

            bool isUsable = true;

            if (ofd.ShowDialog() == true)
            {

                imageSequenceSource = new StreampixSequenceSource(ofd.FileName,r2dSettings.streampixFlip12in16DarkLight);
                streampix_fileInfo_txt.Text = "Header version: " + (imageSequenceSource as StreampixSequenceSource).version.ToString()
                    + "\nDescription: " + (imageSequenceSource as StreampixSequenceSource).description
                    + "\nImage count: " + imageSequenceSource.getImageCount()
                    + "\nWrapper bit depth: " + (imageSequenceSource as StreampixSequenceSource).bitDepth
                    + "\nData bit depth: " + (imageSequenceSource as StreampixSequenceSource).bitDepthReal
                    + "\nData format: " + (imageSequenceSource as StreampixSequenceSource).imageFormat
                    + "\nBayer pattern: " + bayerPatternToString((imageSequenceSource as StreampixSequenceSource).bayerPattern)
                    + "\nImage width: " + (imageSequenceSource as StreampixSequenceSource).width
                    + "\nImage height: " + (imageSequenceSource as StreampixSequenceSource).height;

                if ((imageSequenceSource as StreampixSequenceSource).compression != 0)
                {
                    MessageBox.Show("Only uncompressed Streampix sequences are supported.");
                    isUsable = false;
                }
                if ((imageSequenceSource as StreampixSequenceSource).imageFormat != StreampixSequenceSource.ImageFormat.MONO_BAYER_MSB && (imageSequenceSource as StreampixSequenceSource).imageFormat != StreampixSequenceSource.ImageFormat.MONO_BAYER && (imageSequenceSource as StreampixSequenceSource).imageFormat != StreampixSequenceSource.ImageFormat.MONO_BAYER_PPACKED)
                {
                    MessageBox.Show("Only raw Bayer Streampix sequences are supported.");
                    isUsable = false;
                }

                if (imageSequenceSource.getRawDataFormat() == RAWDATAFORMAT.INVALID)
                {
                    MessageBox.Show("Only 16 bit Streampix sequences with 16 or 12 bits real data bit depth and a special packed 12 bits format are supported.");
                    isUsable = false;
                }

                if (!isUsable)
                {

                    imageSequenceSource = null;
                }
                else
                {


                    // reset progress counters
                    CurrentProgress = 0;
                    _counter = 0;

                    if (targetFolder == null)
                    {
                        targetFolder = Path.GetDirectoryName(ofd.FileName);
                        txtTargetFolder.Text = targetFolder;
                    }

                    loadedSequenceGUIUpdate("[Streampix] " + ofd.FileName);

                }


            }
        }

        string[] bayerPatternColorsAsString = { "R", "G", "B" };
        private string bayerPatternToString(byte[,] aBayerPattern)
        {
            return bayerPatternColorsAsString[aBayerPattern[0, 0]]
                + bayerPatternColorsAsString[aBayerPattern[0, 1]]
                + bayerPatternColorsAsString[aBayerPattern[1, 0]]
                + bayerPatternColorsAsString[aBayerPattern[1, 1]];
        }

        private struct ShotSettingBayer
        {
            public int orderIndex;
            public float exposureMultiplier;
            override public string ToString()
            {
                return "[ShotSettingBayer order " + orderIndex + ", " + exposureMultiplier.ToString() + "]";
            }
        }

        private struct ShotSettings
        {
            public int delay;
            public float clippingPoint;
            public float featherMultiplier;
            public ShotSettingBayer[] shots;
            override public string ToString()
            {
                return "[ShotSettings delay " + delay + ", clippingPoint " + clippingPoint + ", featherMultiplier " + featherMultiplier + " shots" + shots.ToString() + "]";
            }
        }

        static Regex shotSettingTextRegexp = new Regex(@"(E|X)(?:(\+|\-|\*|\/)([\d\.\,]+))?", RegexOptions.IgnoreCase);

        private ShotSettings getShotSettings()
        {
            int delayTmp = 0;
            try
            {
                //delayTmp = int.Parse(shotDelay_txt.Text);
                delayTmp = r2dSettings.shotDelay;
            }
            catch (Exception e)
            {
                MessageBox.Show("Invalid delay number? " + e.Message);
            }

            double featherStopsTmp = r2dSettings.featherStops;

            float clippingPointTmp = (float)r2dSettings.clippingPoint;

            float featherMultiplier = (float)Math.Pow(2, -featherStopsTmp);

            return new ShotSettings() { delay = delayTmp, clippingPoint = clippingPointTmp, featherMultiplier = featherMultiplier, shots = getShots() };

        }
        private ShotSettingBayer[] getShots()
        {

            string[] shotTexts = { r2dSettings.exposureA, r2dSettings.exposureB, r2dSettings.exposureC, r2dSettings.exposureD, r2dSettings.exposureE, r2dSettings.exposureF };
            List<ShotSettingBayer> shotSettings = new List<ShotSettingBayer>();

            int index = 0;

            for (int i = 0; i < 6; i++)
            {

                if (shotTexts[i].Trim() == "")
                {
                    // nothing
                }
                else
                {
                    ShotSettingBayer shotSettingTemp = new ShotSettingBayer();
                    shotSettingTemp.exposureMultiplier = 1;
                    shotSettingTemp.orderIndex = index;

                    MatchCollection matches = shotSettingTextRegexp.Matches(shotTexts[i]);

                    // Try cach just in case the regexp doesnt give proper results for some reason.
                    try
                    {
                        string color = matches[0].Groups[1].Value.ToUpper();
                        string operater = matches[0].Groups[2].Value;
                        string number = matches[0].Groups[3].Value;

                        float numberParsed = 1;
                        bool numberParsingSuccessful = float.TryParse(number.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out numberParsed);

                        bool isEmpty = false;

                        switch (color)
                        {
                            case "E":
                                // a real exposure
                                break;
                            case "X":
                            default:
                                isEmpty = true;
                                break;
                        }

                        // If the number wasn't parsed properly, may as well stop here.
                        if (!isEmpty && numberParsingSuccessful)
                        {
                            switch (operater)
                            {
                                case "+":
                                    shotSettingTemp.exposureMultiplier = (float)Math.Pow(2, numberParsed);
                                    break;
                                case "*":
                                    shotSettingTemp.exposureMultiplier = numberParsed;
                                    break;

                                case "-":
                                case "/":
                                default:
                                    // Not implemented. Always declare the overexposed shots!
                                    shotSettingTemp.exposureMultiplier = 1;
                                    break;
                            }
                        }

                        if (!isEmpty)
                        {
                            shotSettings.Add(shotSettingTemp);
                            index++;
                        }
                        else
                        {

                            // nothing
                        }
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("Some weird error parsing the shot settings.");
                    }
                }


            }

            // Order by  exposure
            // Exposure order: Starting with smallest exposure multiplier.
            shotSettings.Sort(OrderComparisonTwoShotSettings);

            return shotSettings.ToArray();
        }

        static int OrderComparisonTwoShotSettings(ShotSettingBayer shotSetting1, ShotSettingBayer shotSetting2)
        {

            return shotSetting1.exposureMultiplier.CompareTo(shotSetting2.exposureMultiplier);
        }


        private void exposure_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void btnLoadDNGFolder_Click(object sender, RoutedEventArgs e)
        {
            // reset progress counters
            CurrentProgress = 0;
            _counter = 0;
            var fbd = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            //  save path as a setting - We typically capture to the same folder every time.
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.InputFolder) && Directory.Exists(Properties.Settings.Default.InputFolder))
            {
                fbd.SelectedPath = Properties.Settings.Default.InputFolder;
            }

            bool? result = fbd.ShowDialog();

            if (result == true && !string.IsNullOrWhiteSpace(fbd.SelectedPath) && Directory.Exists(fbd.SelectedPath))
            {
                sourceFolder = fbd.SelectedPath;

                if (targetFolder == null)
                {
                    targetFolder = sourceFolder;
                    txtTargetFolder.Text = targetFolder;
                }
                filesInSourceFolder = Directory.GetFiles(fbd.SelectedPath, "*.dng");
                Array.Sort(filesInSourceFolder, new AlphanumComparatorFast());



                imageSequenceSource = new DNGSequenceSource(filesInSourceFolder);


                loadedSequenceGUIUpdate("[DNG Folder] " + sourceFolder);


            }
        }

        private void btnLoadCRIFolder_Click(object sender, RoutedEventArgs e)
        {
            // reset progress counters
            CurrentProgress = 0;
            _counter = 0;
            var fbd = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            //  save path as a setting - We typically capture to the same folder every time.
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.InputFolder) && Directory.Exists(Properties.Settings.Default.InputFolder))
            {
                fbd.SelectedPath = Properties.Settings.Default.InputFolder;
            }

            bool? result = fbd.ShowDialog();

            if (result == true && !string.IsNullOrWhiteSpace(fbd.SelectedPath) && Directory.Exists(fbd.SelectedPath))
            {
                sourceFolder = fbd.SelectedPath;

                if (targetFolder == null)
                {
                    targetFolder = sourceFolder;
                    txtTargetFolder.Text = targetFolder;
                }
                filesInSourceFolder = Directory.GetFiles(fbd.SelectedPath, "*.cri");
                Array.Sort(filesInSourceFolder, new AlphanumComparatorFast());



                imageSequenceSource = new CRISequenceSource(filesInSourceFolder);


                btnCRIStabExport.IsEnabled = true;

                loadedSequenceGUIUpdate("[CRI Folder] " + sourceFolder);


            }
        }
        private void analyzeHDR_btn_Click(object sender, RoutedEventArgs e)
        {
            if (imageSequenceSource == null)
            {
                return; // Nothing to do here
            }

            double analysisPrecisionLimitMultiplier = r2dSettings.refinement_analysisPrecision;
            //double analysisPrecisionLimitMultiplier = 16;
            //double.TryParse(analysisPrecision_txt.Text, out analysisPrecisionLimitMultiplier);
            analysisPrecisionLimitMultiplier = Math.Pow(2, analysisPrecisionLimitMultiplier);

            // Do this to not break functionality with the old algorithm and allow changes on the fly
            // Might change/remove in the future.
            if (imageSequenceSource.getSourceType() == ImageSequenceSource.ImageSequenceSourceType.RAW)
            {

                //int width = int.Parse(rawWidth.Text);
                //int height = int.Parse(rawHeight.Text);
                int width = r2dSettings.rawWidth;
                int height = r2dSettings.rawHeight;
                ((RAWSequenceSource)imageSequenceSource).width = width;
                ((RAWSequenceSource)imageSequenceSource).height = height;
                RAWDATAFORMAT inputFormat = getInputFormat();

                ((RAWSequenceSource)imageSequenceSource).rawDataFormat = inputFormat;
            }

            //bool doPreviewDebayer = (bool)previewDebayer.IsChecked;
            bool doPreviewDebayer = r2dSettings.previewDebayer;
            //bool doPreviewGamma = (bool)previewGamma.IsChecked;
            bool doPreviewGamma = r2dSettings.previewWithSRGBGamma;

            ShotSettings shotSettings = getShotSettings();

            int sliderNumber = (int)slide_currentFile.Value;
            //int index = ;

            int firstIndex = (sliderNumber - 1) * shotSettings.shots.Length + shotSettings.delay;

            bool anythingMissing = false;
            string whatsMissing = "";

            int[] indiziForMerge = new int[shotSettings.shots.Length];
            byte[][] buffersForMerge = new byte[shotSettings.shots.Length][];

            // Check if all necessary shots exist and add the indizi to an array.
            int thatIndex;
            for (int i = 0; i < shotSettings.shots.Length; i++)
            {
                thatIndex = firstIndex + i;
                if (!imageSequenceSource.imageExists(i))
                {
                    anythingMissing = true;
                    whatsMissing = imageSequenceSource.getImageName(i);
                    break;
                }
                else
                {
                    indiziForMerge[i] = thatIndex;
                    buffersForMerge[i] = imageSequenceSource.getRawImageData(thatIndex);
                    // Convert to 16 bit if necessary
                    if (imageSequenceSource.getRawDataFormat() == RAWDATAFORMAT.BAYERRG12p)
                    {
                        buffersForMerge[i] = DataFormatConverter.convert12pInputto16bit(buffersForMerge[i]);
                    }
                    if (imageSequenceSource.getRawDataFormat() == RAWDATAFORMAT.BAYERRG12pV2)
                    {
                        buffersForMerge[i] = DataFormatConverter.convert12pV2Inputto16bit(buffersForMerge[i]);
                    }/*
                    if (imageSequenceSource.getRawDataFormat() == RAWDATAFORMAT.CINTEL10BIT)
                    {
                        buffersForMerge[i] = DataFormatConverter.tryConvertCintel10Inputto16bit(buffersForMerge[i]);
                    }*/
                    if (imageSequenceSource.getRawDataFormat() == RAWDATAFORMAT.BAYER10p1)
                    {
                        buffersForMerge[i] = DataFormatConverter.convert10p1Inputto16bit(buffersForMerge[i]);
                    }
                    if (imageSequenceSource.getRawDataFormat() == RAWDATAFORMAT.BAYER12BITDARKCAPSULEDIN16BIT)
                    {
                        buffersForMerge[i] = DataFormatConverter.convert12paddedto16Inputto16bit(buffersForMerge[i]);
                    }
                }
            }

            //string selectedRawFile = filesInSourceFolder[index];
            if (anythingMissing)
            {
                MessageBox.Show("weirdo error, apparently image " + whatsMissing + " (no longer?) exists");
                return;
            }
            else
            {
                ShotSettings correctExposureSettings = HDRAnalyzeRefine(buffersForMerge, shotSettings, analysisPrecisionLimitMultiplier);
                double[] multipliersOrdered = new double[correctExposureSettings.shots.Length];

                foreach (ShotSettingBayer shotSetting in correctExposureSettings.shots)
                {
                    multipliersOrdered[shotSetting.orderIndex] = Math.Round(shotSetting.exposureMultiplier, 2);
                }
                string output = "Based on the current frame set (" + sliderNumber + ") the following exposure settings were found to be the best fit: " + Environment.NewLine;
                int index = 0;
                foreach (double multiplier in multipliersOrdered)
                {
                    output += Environment.NewLine;
                    output += "E*" + multiplier;
                }
                output += Environment.NewLine + Environment.NewLine + "Would you like to apply these settings and overwrite your own?";
                if (MessageBox.Show(output, "", MessageBoxButton.YesNoCancel) == MessageBoxResult.Yes)
                {
                    for (int i = 0; i < multipliersOrdered.Length; i++)
                    {
                        string theString = "E*" + multipliersOrdered[i];
                        switch (i)
                        {
                            case 0:
                                r2dSettings.exposureA = theString;
                                break;
                            case 1:
                                r2dSettings.exposureB = theString;
                                break;
                            case 2:
                                r2dSettings.exposureC = theString;
                                break;
                            case 3:
                                r2dSettings.exposureD = theString;
                                break;
                            case 4:
                                r2dSettings.exposureE = theString;
                                break;
                            case 5:
                                r2dSettings.exposureF = theString;
                                break;
                            default:
                                throw new Exception("wtf");
                                break;
                        }
                    }
                    r2dSettings.sendToGUI();
                }
            }
        }


        struct AverageData
        {
            public double value;
            public double divisor;
        }

        private ShotSettings HDRAnalyzeRefine(byte[][] buffers, ShotSettings shotSettings, double analysisPrecisionLimitMultiplier)
        {

            ShotSettings refinedShotSettings = new ShotSettings();
            refinedShotSettings.delay = shotSettings.delay;
            refinedShotSettings.clippingPoint = shotSettings.clippingPoint;
            refinedShotSettings.featherMultiplier = shotSettings.featherMultiplier;
            refinedShotSettings.shots = new ShotSettingBayer[shotSettings.shots.Length];


            double maxValueToAnalyze = 1 / analysisPrecisionLimitMultiplier;  // This is about avoiding noise or low values distorting the calculated value

            // Fast skip for non HDR
            // Shouldn't really be necessary because it should be caught outside during normal raw processing. This might speed up the preview though.
            if (buffers.Length == 1 && shotSettings.shots.Length == 1)
            {
                return shotSettings;
            }

            float clippingPoint = shotSettings.clippingPoint;
            float featherMultiplier = shotSettings.featherMultiplier;

            float featherBottomIntensity = clippingPoint;
            float featherRange = 0;
            if (featherMultiplier != 1)
            {
                featherBottomIntensity *= featherMultiplier;
                featherRange = clippingPoint - featherBottomIntensity;
            }

            byte[] outputBuffer = new byte[buffers[0].Length];

            int singleBufferLength = buffers[0].Length;

            //double maxValue = 0;

            //double Uint16MaxValueDouble = (double)UInt16.MaxValue;
            float Uint16MaxValueFloat = (float)UInt16.MaxValue;


            // Do one color after another
            //for (var colorIndex = 0; colorIndex < 3; colorIndex++)


            Vector2 Uint16Divider = new Vector2();
            float thisMultiplierMultiplier;
            int thisIndex;
            ShotSettingBayer thisShotSetting;
            float effectiveMultiplier;
            float currentOutputValue;
            float currentInputValue;
            float lastInputValue;
            bool isClipping;
            UInt16 finalValue;
            float inputIntensity;
            float tmpValue;
            byte[] sixteenbitbytes;


            thisIndex = 0;
            thisMultiplierMultiplier = 1;
            for (var shotSettingIndex = 0; shotSettingIndex < shotSettings.shots.Length; shotSettingIndex++)
            {
                refinedShotSettings.shots[shotSettingIndex].orderIndex = shotSettings.shots[shotSettingIndex].orderIndex;
                thisShotSetting = shotSettings.shots[shotSettingIndex];

                // first image of each set just has its buffer copied for speed reasons
                if (thisIndex == 0)
                {
                    //outputBuffer = buffers[thisShotSetting.orderIndex];
                    // The darkest image's multiplier should technically be 1 by default. But if it isn't, we use this to normalize the following images.
                    // For example, if the darkest image multiplier is 2, we record the "multiplier multiplier" as 0.5, as we aren't actually multiplying this image data by 2
                    // and as a result we need to reduce the image multiplier of following images by multiplying it with 0.5.
                    thisMultiplierMultiplier = 1 / thisShotSetting.exposureMultiplier;
                    refinedShotSettings.shots[shotSettingIndex].exposureMultiplier = thisMultiplierMultiplier;
                }

                // Do actual HDR merging
                else
                {


                    AverageData averageShotMultiplier = new AverageData();

                    for (var i = 0; i < singleBufferLength; i += 2) // 16 bit steps
                    {
                        // Comments:
                        // You might want to use Buffer.BlockCopy to convert the array from raw bytes to unsigned shorts
                        // https://markheath.net/post/how-to-convert-byte-to-short-or-float
                        // Or: Span<ushort> a = MemoryMarshal.Cast<byte, ushort>(data)
                        // https://markheath.net/post/span-t-audio
                        Uint16Divider.X = (float)BitConverter.ToUInt16(buffers[shotSettings.shots[shotSettingIndex - 1].orderIndex], i);
                        Uint16Divider.Y = (float)BitConverter.ToUInt16(buffers[thisShotSetting.orderIndex], i);


                        /*currentOutputValue = (double)BitConverter.ToUInt16(outputBuffers[colorIndex], i) / Uint16MaxValueDouble;
                        currentInputValue = (double)BitConverter.ToUInt16(buffers[thisShotSetting.orderIndex], i) / Uint16MaxValueDouble;*/
                        Uint16Divider = Vector2.Divide(Uint16Divider, Uint16MaxValueFloat);
                        lastInputValue = Uint16Divider.X;
                        currentInputValue = Uint16Divider.Y;



                        //if (currentInputValue > maxValue) { maxValue = currentInputValue; }
                        isClipping = currentInputValue > clippingPoint;
                        if (!isClipping && lastInputValue != 0 && currentInputValue != 0 && lastInputValue > maxValueToAnalyze && currentInputValue > maxValueToAnalyze) // zero will lead to division by zero -> bad.
                        {

                            averageShotMultiplier.value += currentInputValue / lastInputValue;
                            averageShotMultiplier.divisor += 1;
                            //finalValue = 0;
                            //currentInputValue /= effectiveMultiplier;
                            //finalValue = (UInt16)Math.Round(currentInputValue * Uint16MaxValueFloat);

                            //sixteenbitbytes = BitConverter.GetBytes(finalValue);
                            //outputBuffer[i] = sixteenbitbytes[0];
                            //outputBuffer[i + 1] = sixteenbitbytes[1];
                        }
                    }

                    refinedShotSettings.shots[shotSettingIndex].exposureMultiplier = thisMultiplierMultiplier * (float)(averageShotMultiplier.value / averageShotMultiplier.divisor);


                    thisMultiplierMultiplier = thisMultiplierMultiplier * refinedShotSettings.shots[shotSettingIndex].exposureMultiplier;
                }
                thisIndex++;

            }




            //MessageBox.Show(maxValue.ToString());

            return refinedShotSettings;
        }



        private float[][] getCRIStabilizationData()
        {
            float[][] retVal = new float[1][];
            if (imageSequenceSource is CRISequenceSource)
            {


                ulong count = (ulong)imageSequenceSource.getImageCount();
                retVal = new float[count][];

                for (ulong i = 0; i < count; i++)
                {

                    retVal[i] = (imageSequenceSource as CRISequenceSource).getStabilizationInfo((int)i);
                }

            }
            else
            {
                // Shouldn't do this!
            }
            return retVal;
        }

        private void btnCRIStabExport_Click(object sender, RoutedEventArgs e)
        {
            if (imageSequenceSource is CRISequenceSource)
            {

                float[][] stabData = getCRIStabilizationData();

                // CSV
                StringBuilder csv = new StringBuilder();
                csv.AppendLine("frame,H,V");

                // AE
                StringBuilder ae = new StringBuilder();
                ae.AppendLine("Adobe After Effects 8.0 Keyframe Data");
                ae.AppendLine();
                ae.AppendLine("\tUnits Per Second\t24");
                ae.AppendLine("\tSource Width\t" + imageSequenceSource.getWidth());
                ae.AppendLine("\tSource Height\t" + imageSequenceSource.getHeight());
                ae.AppendLine("\tSource Pixel Aspect Ratio\t1");
                ae.AppendLine("\tComp Pixel Aspect Ratio\t1");
                ae.AppendLine();
                ae.AppendLine("Effects	Point Control\tPoint");
                ae.AppendLine("\tFrame\tX pixels\tY pixels\t");

                string x, y;

                for (int i = 0; i < stabData.Length; i++)
                {
                    x = Helpers.floatToString(stabData[i][0]);
                    y = Helpers.floatToString(stabData[i][1]);
                    // CSV
                    csv.AppendLine(i + "," + x + "," + y);

                    // AE
                    ae.AppendLine("\t" + i + "\t" + x + "\t" + y + "\t");
                }


                // End files
                ae.AppendLine();
                ae.AppendLine();
                ae.AppendLine("End of Keyframe Data");

                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "Save stabilization data in CSV Format?";
                sfd.FileName = "Stabilization CSV.csv";
                if (sfd.ShowDialog() == true)
                {
                    File.WriteAllText(sfd.FileName, csv.ToString());
                }

                sfd = new SaveFileDialog();
                sfd.Title = "Save stabilization data in After Effects Keyframe Format?";
                sfd.FileName = "Stabilization AE.txt";
                if (sfd.ShowDialog() == true)
                {
                    File.WriteAllText(sfd.FileName, ae.ToString());
                }
            }
            else
            {

                MessageBox.Show("Error. Cannot export stabilization data from non-CRI sources.");
            }
        }

    }
}
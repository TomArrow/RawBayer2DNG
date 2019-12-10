using BitMiracle.LibTiff.Classic;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
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
using Forms = System.Windows.Forms;

using System.Drawing;
using Imaging = System.Drawing.Imaging;
using Orientation = BitMiracle.LibTiff.Classic.Orientation;
using System.Runtime.InteropServices;

namespace RawBayer2DNG
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        
        private const TiffTag TIFFTAG_CFAREPEATPATTERNDIM = (TiffTag)33421;
        private const TiffTag TIFFTAG_CFAPATTERN = (TiffTag)33422;

        private static Tiff.TiffExtendProc m_parentExtender;


        string sourceFolder = null;
        string targetFolder = null;
        string[] filesInSourceFolder = null;


        public static void TagExtender(Tiff tif)
        {
            TiffFieldInfo[] tiffFieldInfo =
            {
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

        public MainWindow()
        {
            InitializeComponent();
            // Register the custom tag handler
            Tiff.TiffExtendProc extender = TagExtender;
            m_parentExtender = Tiff.SetTagExtender(extender);
        }

        private void BtnLoadRAW_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Raw bayer files (.raw)|*.raw";
            if(ofd.ShowDialog() == true)
            {

                string fileNameWithoutExtension = Path.GetDirectoryName(ofd.FileName) + "\\" + Path.GetFileNameWithoutExtension(ofd.FileName);
                string fileName = fileNameWithoutExtension + ".dng";

                byte[,] bayerPattern = getBayerPattern();

                ProcessRAW(ofd.FileName, fileName, bayerPattern);
            }
        }

        private void ProcessRAW( string srcFilename,string targetFilename, byte[,] bayerPattern)
        {
            byte[] buff = File.ReadAllBytes(srcFilename);


            char[] bayerSubstitution = {"\x0"[0], "\x1"[0], "\x2"[0] };

            string bayerPatternTag = bayerSubstitution[bayerPattern[0, 0]].ToString() + bayerSubstitution[bayerPattern[0, 1]] +bayerSubstitution[bayerPattern[1, 0]]+bayerSubstitution[bayerPattern[1, 1]];

            int width = int.Parse(rawWidth.Text);
            int height = int.Parse(rawHeight.Text);
            
            string fileName = targetFilename;

            using (Tiff output = Tiff.Open(fileName, "w"))
            {
                // Basic TIFF functionality
                output.SetField(TiffTag.IMAGEWIDTH, width);
                output.SetField(TiffTag.IMAGELENGTH, height);
                output.SetField(TiffTag.SAMPLESPERPIXEL, 1);
                output.SetField(TiffTag.BITSPERSAMPLE, 16);
                output.SetField(TiffTag.ORIENTATION, Orientation.TOPLEFT);
                output.SetField(TiffTag.ROWSPERSTRIP, height);
                output.SetField(TiffTag.COMPRESSION, Compression.ADOBE_DEFLATE);
                output.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
                output.SetField(TiffTag.FILLORDER, FillOrder.MSB2LSB);
                //output.SetField(TiffTag.COMPRESSION, Compression.LZW); //LZW doesn't work with DNG apparently
                //output.SetField(TiffTag.COMPRESSION, Compression.NONE);
                output.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);

                float[] cam_xyz = { 3.2404542f, -1.5371385f, -0.4985314f, -0.9692660f, 1.8760108f, 0.0415560f, 0.0556434f, -0.2040259f, 1.0572252f }; // my sRGB hack
                                                                                                                                                      //float[] cam_xyz =  { 0f, 1f,0f,0f,0f,1f,1f,0f,0f }; // my sRGB hack
                float[] neutral = { 1f, 1f, 1f }; // my sRGB hack
                int[] bpp = { 8, 8, 8 }; // my sRGB hack
                short[] bayerpatterndimensions = { 2, 2 }; // my sRGB hack
                short[] linearizationTable = new short[256];
                //float[] neutral = { 0.807133f, 1.0f, 0.913289f };

                //DNG 
                output.SetField(TiffTag.SUBFILETYPE, 0);
                output.SetField(TiffTag.MAKE, "blah");
                output.SetField(TiffTag.MODEL, "blah");
                output.SetField(TiffTag.SOFTWARE, "Blah");
                output.SetField(TiffTag.DNGVERSION, "\x1\x4\x0\x0");
                output.SetField(TiffTag.DNGBACKWARDVERSION, "\x1\x4\x0\x0");
                output.SetField(TiffTag.UNIQUECAMERAMODEL, "blah");
                output.SetField(TiffTag.COLORMATRIX1, 9, cam_xyz);
                output.SetField(TiffTag.ASSHOTNEUTRAL, 3, neutral);
                output.SetField(TiffTag.CALIBRATIONILLUMINANT1, 21);
                output.SetField(TiffTag.ORIGINALRAWFILENAME, srcFilename);
                output.SetField(TiffTag.PHOTOMETRIC, 32803);
                output.SetField(TiffTag.SAMPLESPERPIXEL, 1);
                //output.SetField(TiffTag.EXIF_CFAPATTERN, 4, "\x1\x0\x2\x1");
                output.SetField(TiffTag.EXIF_CFAPATTERN, 4, bayerPatternTag);
                output.SetField(TIFFTAG_CFAREPEATPATTERNDIM, bayerpatterndimensions);
                //output.SetField(TIFFTAG_CFAPATTERN, "\x1\x0\x2\x1"); //0=Red, 1=Green,   2=Blue,   3=Cyan,   4=Magenta,   5=Yellow,   and   6=White
                output.SetField(TIFFTAG_CFAPATTERN, bayerPatternTag); //0=Red, 1=Green,   2=Blue,   3=Cyan,   4=Magenta,   5=Yellow,   and   6=White

                // Maybe use later if necessary:
                //output.SetField(TiffTag.SAMPLESPERPIXEL, 1);
                //output.SetField(TiffTag.BITSPERSAMPLE, 3, bpp);
                //output.SetField(TiffTag.LINEARIZATIONTABLE, 256, linearizationTable);
                //output.SetField(TiffTag.WHITELEVEL, 1);

                output.WriteEncodedStrip(0, buff, width * height * 2);
            }
        } 



        private void BtnLoadRAWFolder_Click(object sender, RoutedEventArgs e)
        {

            var fbd = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            bool? result = fbd.ShowDialog();

            if (result == true && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
            {
                sourceFolder = fbd.SelectedPath;
                txtSrcFolder.Text = sourceFolder;
                if(targetFolder == null)
                {
                    targetFolder = sourceFolder;
                    txtTargetFolder.Text = targetFolder;
                }
                filesInSourceFolder = Directory.GetFiles(fbd.SelectedPath,"*.raw");
                currentImagNumber.Text = "1";
                totalImageCount.Text = filesInSourceFolder.Count().ToString();
                slide_currentFile.Maximum = filesInSourceFolder.Count();
                slide_currentFile.Minimum = 1;
                slide_currentFile.Value = 1;
                btnProcessFolder.IsEnabled = true;
            }
        }

        private void Slide_currentFile_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

            ReDraw();
        }

        private byte[,] getBayerPattern()
        {

            byte bayerColorA = (byte)int.Parse(colorBayerA.Text);
            byte bayerColorB = (byte)int.Parse(colorBayerB.Text);
            byte bayerColorC = (byte)int.Parse(colorBayerC.Text);
            byte bayerColorD = (byte)int.Parse(colorBayerD.Text);
            byte[,] bayerPattern = { { bayerColorA, bayerColorB }, { bayerColorC, bayerColorD } };
            return bayerPattern;
        }

        private void ReDraw()
        {
            if(sourceFolder == null || filesInSourceFolder == null)
            {
                return; // Nothing to do here
            }

            int width = int.Parse(rawWidth.Text);
            int height = int.Parse(rawHeight.Text);

            bool doPreviewDebayer = (bool)previewDebayer.IsChecked;
            bool doPreviewGamma = (bool)previewGamma.IsChecked;

            int sliderNumber = (int)slide_currentFile.Value;
            int index = sliderNumber - 1;
            string selectedRawFile = filesInSourceFolder[index];
            if (!File.Exists(selectedRawFile))
            {
                MessageBox.Show("weirdo error, apparently file " + selectedRawFile + " (no longer?) exists");
                return;
            }
            else
            {
                int subsample = 2;

                int newWidth = (int)Math.Ceiling((double)width / subsample);
                int newHeight = (int)Math.Ceiling((double)height / subsample);

                byte[] buff = File.ReadAllBytes(selectedRawFile);
                int byteDepth = 2; // This is for the source
                int byteWidth = newWidth * 3; // This is for the preview. 3 means RGB
                int newStride = Helpers.getStride(byteWidth);
                //byte[] newbytes = Helpers.PadLines(buff, height, width, newStride,2);

                byte[] newbytes;

                if (doPreviewDebayer) {
                    byte[,] bayerPattern = getBayerPattern();
                    newbytes = Helpers.DrawBayerPreview(buff, newHeight, newWidth, height, width, newStride, byteDepth, subsample,doPreviewGamma,bayerPattern);
                } else
                {

                    newbytes = Helpers.DrawPreview(buff, newHeight, newWidth, height, width, newStride, byteDepth, subsample, doPreviewGamma);
                }

                Bitmap manipulatedImage = new Bitmap(newWidth, newHeight, Imaging.PixelFormat.Format24bppRgb);
                Imaging.BitmapData pixelData = manipulatedImage.LockBits(new Rectangle(0, 0, newWidth, newHeight), Imaging.ImageLockMode.WriteOnly, Imaging.PixelFormat.Format24bppRgb);

                //Bitmap im = new Bitmap(width, height, newStride, Imaging.PixelFormat.Format16bppGrayScale,  Marshal.UnsafeAddrOfPinnedArrayElement(newbytes, 0));

                System.Runtime.InteropServices.Marshal.Copy(newbytes, 0, pixelData.Scan0, newbytes.Count());
                //im.GetPixel(1, 1);
                //im.GetPixel(2447, 2047);
                //pixelData.
                manipulatedImage.UnlockBits(pixelData);
                mainPreview.Source = Helpers.BitmapToImageSource(manipulatedImage);
            }
        }

        private void PreviewGamma_Click(object sender, RoutedEventArgs e)
        {

            ReDraw();
        }

        private void PreviewDebayer_Click(object sender, RoutedEventArgs e)
        {

            ReDraw();
        }

        private void ColorBayer_TextChanged(object sender, TextChangedEventArgs e)
        {
            ReDraw();
        }

        private void BtnLoadTargetFolder_Click(object sender, RoutedEventArgs e)
        {
            var fbd = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            bool? result = fbd.ShowDialog();

            if (result == true && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
            {
                targetFolder = fbd.SelectedPath;
                txtTargetFolder.Text = targetFolder;
                txtStatus.Text = "Target fodler set to " + targetFolder;
            }
        }

        private void BtnProcessFolder_Click(object sender, RoutedEventArgs e)
        {
            byte[,] bayerPattern = getBayerPattern();
            foreach (string srcFileName in filesInSourceFolder)
            {
                string fileNameWithoutExtension = targetFolder + "\\" + Path.GetFileNameWithoutExtension(srcFileName);
                string fileName = fileNameWithoutExtension + ".dng";

                if (File.Exists(fileName))
                {
                    // Error: File already exists. No overwriting. Move on.
                    continue;
                } else
                {

                    ProcessRAW(srcFileName, fileName, bayerPattern);
                }

            }
        }
    }
}

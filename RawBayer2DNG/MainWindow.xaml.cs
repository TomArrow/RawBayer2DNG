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

using System.Drawing;
using Orientation = BitMiracle.LibTiff.Classic.Orientation;

namespace RawBayer2DNG
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {



        /*private const TiffTag TIFFTAG_ASCIITAG = (TiffTag)666;
        private const TiffTag TIFFTAG_LONGTAG = (TiffTag)667;
        private const TiffTag TIFFTAG_SHORTTAG = (TiffTag)668;
        private const TiffTag TIFFTAG_RATIONALTAG = (TiffTag)669;
        private const TiffTag TIFFTAG_FLOATTAG = (TiffTag)670;
        private const TiffTag TIFFTAG_DOUBLETAG = (TiffTag)671;
        private const TiffTag TIFFTAG_BYTE = (TiffTag)672;*/
        private const TiffTag TIFFTAG_CFAREPEATPATTERNDIM = (TiffTag)33421;
        private const TiffTag TIFFTAG_CFAPATTERN = (TiffTag)33422;


        private static Tiff.TiffExtendProc m_parentExtender;

        public static void TagExtender(Tiff tif)
        {
            TiffFieldInfo[] tiffFieldInfo =
            {
                //new TiffFieldInfo(TIFFTAG_ASCIITAG, -1, -1, TiffType.ASCII, FieldBit.Custom, true, false, "MyTag"),
                new TiffFieldInfo(TIFFTAG_CFAREPEATPATTERNDIM, 2, 2, TiffType.SHORT, FieldBit.Custom, false, false, "CFARepeatPatternDim"),
                new TiffFieldInfo(TIFFTAG_CFAPATTERN, 4, 4, TiffType.BYTE, FieldBit.Custom, false, false, "CFAPattern"),
                /*new TiffFieldInfo(TIFFTAG_SHORTTAG, 2, 2, TiffType.SHORT, FieldBit.Custom, false, true, "ShortTag"),
                new TiffFieldInfo(TIFFTAG_LONGTAG, 2, 2, TiffType.LONG, FieldBit.Custom, false, true, "LongTag"),
                new TiffFieldInfo(TIFFTAG_RATIONALTAG, 2, 2, TiffType.RATIONAL, FieldBit.Custom, false, true, "RationalTag"),
                new TiffFieldInfo(TIFFTAG_FLOATTAG, 2, 2, TiffType.FLOAT, FieldBit.Custom, false, true, "FloatTag"),
                new TiffFieldInfo(TIFFTAG_DOUBLETAG, 2, 2, TiffType.DOUBLE, FieldBit.Custom, false, true, "DoubleTag"),
                new TiffFieldInfo(TIFFTAG_BYTE, 2, 2, TiffType.BYTE, FieldBit.Custom, false, true, "ByteTag"),*/
            };

            /*
             *
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
                byte[] buff = File.ReadAllBytes(ofd.FileName);

                int width = int.Parse(rawWidth.Text);
                int height = int.Parse(rawHeight.Text);

                string fileNameWithoutExtension = Path.GetDirectoryName(ofd.FileName)+"\\"+ Path.GetFileNameWithoutExtension(ofd.FileName);
                string fileName = fileNameWithoutExtension+".tif";

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
                    //output.SetField(TiffTag.COMPRESSION, Compression.LZW);
                    //output.SetField(TiffTag.COMPRESSION, Compression.NONE);
                    output.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);

                    float[] cam_xyz =  { 3.2404542f, -1.5371385f, -0.4985314f, -0.9692660f, 1.8760108f, 0.0415560f, 0.0556434f, -0.2040259f, 1.0572252f }; // my sRGB hack
                    //float[] cam_xyz =  { 0f, 1f,0f,0f,0f,1f,1f,0f,0f }; // my sRGB hack
                    float[] neutral =  { 1f,1f,1f}; // my sRGB hack
                    int[] bpp =  { 8,8,8}; // my sRGB hack
                    short[] bayerpatterndimensions =  { 2,2}; // my sRGB hack
                    short[] linearizationTable = new short[256];
                    //float[] neutral = { 0.807133f, 1.0f, 0.913289f };

                    //DNG 
                    output.SetField(TiffTag.SUBFILETYPE, 0);
                    output.SetField(TiffTag.MAKE, "blah");
                    output.SetField(TiffTag.MODEL, "blah");
                    //output.SetField(TiffTag.SAMPLESPERPIXEL, 1);
                    output.SetField(TiffTag.SOFTWARE, "Blah");
                    //output.SetField(TiffTag.DNGVERSION, "\001\001\0\0");
                    output.SetField(TiffTag.DNGVERSION, "\x1\x4\x0\x0");
                    //output.SetField(TiffTag.DNGBACKWARDVERSION, "\001\0\0\0");
                    output.SetField(TiffTag.DNGBACKWARDVERSION, "\x1\x4\x0\x0");
                    output.SetField(TiffTag.UNIQUECAMERAMODEL, "blah");
                    output.SetField(TiffTag.COLORMATRIX1, 9, cam_xyz);
                    output.SetField(TiffTag.ASSHOTNEUTRAL, 3, neutral);
                    output.SetField(TiffTag.CALIBRATIONILLUMINANT1, 21);
                    output.SetField(TiffTag.ORIGINALRAWFILENAME, ofd.FileName);
                    //output.SetField(TiffTag.BITSPERSAMPLE, 3, bpp);
                    output.SetField(TiffTag.PHOTOMETRIC, 32803);
                    output.SetField(TiffTag.SAMPLESPERPIXEL, 1);
                    //output.SetField(TiffTag.EXIF_CFAPATTERN, 4, "\001\002\000\001");
                    output.SetField(TiffTag.EXIF_CFAPATTERN, 4, "\x1\x0\x2\x1");
                    //output.SetField(TiffTag.CFAPLANECOLOR, );
                    output.SetField(TIFFTAG_CFAREPEATPATTERNDIM, bayerpatterndimensions);
                    output.SetField(TIFFTAG_CFAPATTERN, "\x1\x0\x2\x1"); //0=Red, 1=Green,   2=Blue,   3=Cyan,   4=Magenta,   5=Yellow,   and   6=White
                    //output.SetField(TiffTag.CFA, 4, "\001\002\000\001");
                    //output.SetField(TiffTag.LINEARIZATIONTABLE, 256, linearizationTable);
                    //output.SetField(TiffTag.WHITELEVEL, 1);

                    output.WriteEncodedStrip(0, buff, width * height * 2);
                }
            }
        }
    }
}

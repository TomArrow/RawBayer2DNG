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
using Forms = System.Windows.Forms;

using System.Drawing;
using Imaging = System.Drawing.Imaging;
using Orientation = BitMiracle.LibTiff.Classic.Orientation;
using System.Runtime.InteropServices;
using System.Threading;

namespace RawBayer2DNG
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window,  INotifyPropertyChanged
    {
        
        private const TiffTag TIFFTAG_CFAREPEATPATTERNDIM = (TiffTag)33421;
        private const TiffTag TIFFTAG_CFAPATTERN = (TiffTag)33422;

        private static Tiff.TiffExtendProc m_parentExtender;
        private BackgroundWorker worker = new BackgroundWorker();

        string sourceFolder = null;
        string targetFolder = null;
        string[] filesInSourceFolder = null;
        private int currentProgress;
        private string currentStatus;
        private static int _counter = 0;
        private static int _totalFiles = 0;
        private bool _compressDng = false;
        private string _newFileName;

        // Declare the event
        public event PropertyChangedEventHandler PropertyChanged;

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
            InitializeComponent();
            // Register the custom tag handler
            Tiff.TiffExtendProc extender = TagExtender;
            m_parentExtender = Tiff.SetTagExtender(extender);

            // load saved settings
            rawWidth.Text = Properties.Settings.Default.Width.ToString();
            rawHeight.Text = Properties.Settings.Default.Height.ToString();
            txtMaxThreads.Text = "Threads (Max " + Environment.ProcessorCount + "): ";
            colorBayerA.Text = Properties.Settings.Default.colorBayerA.ToString();
            colorBayerB.Text = Properties.Settings.Default.colorBayerB.ToString();
            colorBayerC.Text = Properties.Settings.Default.colorBayerC.ToString();
            colorBayerD.Text = Properties.Settings.Default.colorBayerD.ToString();
            Threads.Text = Properties.Settings.Default.MaxThreads.ToString();
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

            char[] bayerSubstitution = {"\x0"[0], "\x1"[0], "\x2"[0]};

            string bayerPatternTag = bayerSubstitution[bayerPattern[0, 0]].ToString() +
                                         bayerSubstitution[bayerPattern[0, 1]] + bayerSubstitution[bayerPattern[1, 0]] +
                                         bayerSubstitution[bayerPattern[1, 1]];

            int width = 2448;
            int height = 2048;

            this.Dispatcher.Invoke(() =>
            {
                width = int.Parse(rawWidth.Text);
                height = int.Parse(rawHeight.Text);
            });

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
                output.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
                output.SetField(TiffTag.FILLORDER, FillOrder.MSB2LSB);
                //output.SetField(TiffTag.COMPRESSION, Compression.LZW); //LZW doesn't work with DNG apparently

                if (_compressDng)
                    output.SetField(TiffTag.COMPRESSION, Compression.ADOBE_DEFLATE);
                else
                    output.SetField(TiffTag.COMPRESSION, Compression.NONE);

                output.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);

                float[] cam_xyz =
                {
                    3.2404542f, -1.5371385f, -0.4985314f, -0.9692660f, 1.8760108f, 0.0415560f, 0.0556434f,
                    -0.2040259f, 1.0572252f
                }; // my sRGB hack
                //float[] cam_xyz =  { 0f, 1f,0f,0f,0f,1f,1f,0f,0f }; // my sRGB hack
                float[] neutral = {1f, 1f, 1f}; // my sRGB hack
                int[] bpp = {8, 8, 8}; // my sRGB hack
                short[] bayerpatterndimensions = {2, 2}; // my sRGB hack
                short[] linearizationTable = new short[256];
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
                output.SetField(TiffTag.ORIGINALRAWFILENAME, srcFilename);
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

                output.WriteEncodedStrip(0, buff, width * height * 2);
            }
        } 

        private void BtnLoadRAWFolder_Click(object sender, RoutedEventArgs e)
        {
            // reset progress counters
            CurrentProgress = 0;
            _counter = 0;
            var fbd = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            bool? result = fbd.ShowDialog();

            if (result == true && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
            {
                sourceFolder = fbd.SelectedPath;
                txtSrcFolder.Text = sourceFolder;
                txtStatus.Text = "Source folder set to " + sourceFolder;
                if (targetFolder == null)
                {
                    targetFolder = sourceFolder;
                    txtTargetFolder.Text = targetFolder;
                }
                filesInSourceFolder = Directory.GetFiles(fbd.SelectedPath,"*.raw");
                Array.Sort(filesInSourceFolder, new AlphanumComparatorFast());
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
            ReDrawPreview();
        }

        private byte[,] getBayerPattern()
        {
            return this.Dispatcher.Invoke(() =>
            {
                //0=Red, 1=Green,   2=Blue
                byte bayerColorA = (byte)int.Parse(colorBayerA.Text);
                byte bayerColorB = (byte)int.Parse(colorBayerB.Text);
                byte bayerColorC = (byte)int.Parse(colorBayerC.Text);
                byte bayerColorD = (byte)int.Parse(colorBayerD.Text);
                byte[,] bayerPattern = { { bayerColorA, bayerColorB }, { bayerColorC, bayerColorD } };
                return bayerPattern;
            });
        }

        private void ReDrawPreview()
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
                int subsample = 4;

                int newWidth = (int)Math.Ceiling((double)width / subsample);
                int newHeight = (int)Math.Ceiling((double)height / subsample);

                byte[] buff = File.ReadAllBytes(selectedRawFile);
                int byteDepth = 2; // This is for the source
                int byteWidth = newWidth * 3; // This is for the preview. 3 means RGB
                int newStride = Helpers.getStride(byteWidth);
                //byte[] newbytes = Helpers.PadLines(buff, height, width, newStride,2);

                byte[] newbytes;

                byte[,] bayerPattern = getBayerPattern();
                if (doPreviewDebayer) {
                    newbytes = Helpers.DrawBayerPreview(buff, newHeight, newWidth, height, width, newStride, byteDepth, subsample,doPreviewGamma,bayerPattern);
                } else
                {

                    newbytes = Helpers.DrawPreview(buff, newHeight, newWidth, height, width, newStride, byteDepth, subsample, doPreviewGamma);
                }

                // Draw magnifier rectangle
                // Position values are in percent from 0 to 1
                // Explanation: value 0 means left edge at left image border. value 1 means right edge at right image border.
                int magnifierSrcSizeX = 20;
                int magnifierSrcSizeY = 20;
                int magnifierPreviewSizeX = (int)Math.Floor((double)magnifierSrcSizeX/subsample);
                int magnifierPreviewSizeY = (int)Math.Floor((double)magnifierSrcSizeY/ subsample);
                double magnifierPositionX = magnifierHorizontalPosition.Value;
                double magnifierPositionY = magnifierVerticalPosition.Value;
                Rectangle positionSrc = new Rectangle(
                    Helpers.MinMultipleOfTwo((int)Math.Floor(magnifierPositionX * (width-magnifierSrcSizeX))),  // Using multiple of two function here to not mess up bayer pattern evaluation
                    Helpers.MinMultipleOfTwo((int)Math.Floor(magnifierPositionY * (height - magnifierSrcSizeY))),
                    magnifierSrcSizeX,magnifierSrcSizeY);
                Rectangle positionPreview = new Rectangle(
                    (int)Math.Floor((double)positionSrc.X/subsample),
                    (int)Math.Floor((double)positionSrc.Y / subsample),magnifierPreviewSizeX,magnifierPreviewSizeY
                    );
                newbytes = Helpers.drawRectangle(newbytes, newWidth, newHeight, positionPreview);

                double[] RGBamplify = { rAmplify.Value, gAmplify.Value, bAmplify.Value };



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
                newbytes = Helpers.DrawMagnifier(buff,positionSrc,width,doPreviewGamma,byteDepth, RGBamplify, bayerPattern);

                System.Runtime.InteropServices.Marshal.Copy(newbytes, 0, pixelData.Scan0, newbytes.Count());
                magnifierArea.UnlockBits(pixelData);

                magnifierArea = Helpers.ResizeBitmapNN(magnifierArea, 200, 200);

                // Do the displaying
                mainPreview.Source = Helpers.BitmapToImageSource(manipulatedImage);
                Magnifier.Source = Helpers.BitmapToImageSource(magnifierArea);
            }
        }

        private void PreviewGamma_Click(object sender, RoutedEventArgs e)
        {

            ReDrawPreview();
        }

        private void PreviewDebayer_Click(object sender, RoutedEventArgs e)
        {

            ReDrawPreview();
        }

        private void CompressDNG_Checked(object sender, RoutedEventArgs e)
        {
            HandleCompression(sender as CheckBox);
        }

        private void CompressDNG_Unchecked(object sender, RoutedEventArgs e)
        {
            HandleCompression(sender as CheckBox);
        }

        void HandleCompression(CheckBox checkBox)
        {
            // Use IsChecked.
            _compressDng = checkBox.IsChecked.Value;
        }

        private void ColorBayer_TextChanged(object sender, TextChangedEventArgs e)
        {
            if(! string.IsNullOrWhiteSpace(((System.Windows.Controls.TextBox)sender).Text))
                ReDrawPreview();
        }

        private void BtnLoadTargetFolder_Click(object sender, RoutedEventArgs e)
        {
            var fbd = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            bool? result = fbd.ShowDialog();

            if (result == true && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
            {
                targetFolder = fbd.SelectedPath;
                txtTargetFolder.Text = targetFolder;
                txtStatus.Text = "Target folder set to " + targetFolder;
            }
        }

        private void BtnProcessFolder_Click(object sender, RoutedEventArgs e)
        {
            // reset progress counters
            currentProgress = 0;
            _counter = 0;
            _newFileName = Rename.Text;
            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = true;
            worker.DoWork += worker_DoWork;
            worker.ProgressChanged += worker_ProgressChanged;
            worker.RunWorkerAsync();
        }

        private void btnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Width = int.Parse(rawWidth.Text);
            Properties.Settings.Default.Height = int.Parse(rawHeight.Text);

            if (int.Parse(Threads.Text) > Environment.ProcessorCount) Threads.Text = Environment.ProcessorCount.ToString();
            Properties.Settings.Default.MaxThreads = int.Parse(Threads.Text);

            Properties.Settings.Default.colorBayerA = int.Parse(colorBayerA.Text);
            Properties.Settings.Default.colorBayerB = int.Parse(colorBayerB.Text);
            Properties.Settings.Default.colorBayerC = int.Parse(colorBayerC.Text);
            Properties.Settings.Default.colorBayerD = int.Parse(colorBayerD.Text);

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

        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            byte[,] bayerPattern = getBayerPattern();

            _totalFiles = filesInSourceFolder.Length;

            // create lookup table: <inputfile, outputfile> 
            var dic = new Dictionary<string, string>();

            int index = 0;
            foreach (var inputfile in filesInSourceFolder)
            {
                string fileNameWithoutExtension =
                    targetFolder + "\\" + Path.GetFileNameWithoutExtension(inputfile);
                string outputFile = fileNameWithoutExtension + ".dng";

                if (!String.IsNullOrWhiteSpace(_newFileName))
                {
                    string serializer = index.ToString().PadLeft(6, '0');
                    outputFile = targetFolder + "\\" + _newFileName + "_" + serializer + ".dng";
                }
                dic.Add(inputfile, outputFile);
                index++;
            }

            var countLock = new object();
            CurrentProgress = 0;

            int threads = Properties.Settings.Default.MaxThreads;

            if(threads == 0)
                threads = Environment.ProcessorCount > 1 ? Environment.ProcessorCount / 2 : Environment.ProcessorCount;

            Parallel.ForEach(dic,
                new ParallelOptions { MaxDegreeOfParallelism = threads }, (currentFile, loopState) =>
                    // foreach (string srcFileName in filesInSourceFolder)
                {
                    if (worker.CancellationPending == true)
                    {
                        e.Cancel = true;
                        return;
                    }

                    _counter++;
                    var percentage = (double)_counter / _totalFiles * 100.0;
                    lock (countLock) { worker?.ReportProgress((int)percentage); }

                    // check to see if output file already exists
                    if (File.Exists(currentFile.Value))
                    {
                        // Error: File already exists. No overwriting. Move on.
                        //continue;
                        return;
                    }

                    ProcessRAW(currentFile.Key, currentFile.Value, bayerPattern);
                });

            worker?.ReportProgress(100);
        }

        private void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            // pbStatus.Value = e.ProgressPercentage;
            CurrentProgress = e.ProgressPercentage;
            txtStatus.Text = $"Processed {_counter} out of {_totalFiles}";
           
            if(currentProgress == 100) txtStatus.Text = "Processing complete.";

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

        private void Amplify_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ReDrawPreview();
        }
    }
}

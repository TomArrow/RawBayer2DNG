using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawBayer2DNG.ImageSequenceSources
{
    class StreampixSequenceSource : ImageSequenceSource
    {
        public UInt32 width;
        public UInt32 height;
        public UInt32 bitDepth;
        public UInt32 bitDepthReal;
        public UInt32 singleImageByteSize;
        public UInt32 singleImageRealByteSize; // taking into account that the actual images are aligned on 8192 byte boundaries
        public ImageFormat imageFormat;
        public Compression compression;
        public byte[,] bayerPattern;
        public string path;
        public UInt32 version;
        public string description;
        public UInt32 imageCount;
        public string seqFileBasenameNoDots;

        public enum ImageFormat
        {
            UNKNOWN = 0, 
            MONO = 100, 
            MONO_BAYER = 101, // We need this
            BGR = 200, 
            PLANAR = 300,
            RGB = 400, 
            BGRx = 500,
            YUV422 = 600, 
            YUV422_20 = 610,
            UVY422 = 700, 
            UVY411 = 800, 
            UVY444 = 900,
            BGR555_PACKED = 905, 
            BGR565_PACKED = 906,
            MONO_MSB = 112, 
            MONO_BAYER_MSB = 113, 
            MONO_MSB_SWAP = 114, 
            MONO_BAYER_MSB_SWAP = 115,
            BGR10_PPACKED = 123, 
            BGR10_PPACKED_PHOENIX = 124, 
            RGB10_PPACKED_PHOENIX = 125, 
            MONO_PPACKED = 131, 
            MONO_BAYER_PPACKED = 132, // Or this
            MONO_PPACKED_8448 = 133, 
            MONO_BAYER_PPACKED_8448 = 134,
            GVSP_BGR10V1_PACKED = 135, 
            GVSP_BGR10V2_PACKED = 136, 
            BASLER_VENDOR_SPECIFIC = 1000,
            EURESYS_JPEG = 1001,
            ISG_JPEG = 1002
        };

        public enum Compression
        {
            NONE = 0, // We need this
            JPEG,
            RLE,
            HUFFMAN,
            LZ,
            RLE_FAST,
            HUFFMAN_FAST,
            LZ_FAST,
            H264,
            WAVELET
        };

        public RAWDATAFORMAT rawDataFormat;

        ImageSequenceSourceType sourceType = ImageSequenceSourceType.RAW;

        public StreampixSequenceSource(string sequencePath)
        {
            path = sequencePath;
            using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read),Encoding.Unicode))
            {
                reader.BaseStream.Seek(28,SeekOrigin.Begin);
                version = reader.ReadUInt32();
                reader.BaseStream.Seek(36, SeekOrigin.Begin);
                description = new string(reader.ReadChars(52)); // The length seems to vary. Officially length is supposedly 512 bytes but that seems to be nonsense. But who knows.
                reader.BaseStream.Seek(548, SeekOrigin.Begin);
                width = reader.ReadUInt32();
                height = reader.ReadUInt32();
                bitDepth = reader.ReadUInt32();
                bitDepthReal = reader.ReadUInt32();
                singleImageByteSize = reader.ReadUInt32();
                imageFormat = (ImageFormat)reader.ReadUInt32();
                reader.BaseStream.Seek(572, SeekOrigin.Begin);
                imageCount = reader.ReadUInt32();
                reader.BaseStream.Seek(580, SeekOrigin.Begin);
                singleImageRealByteSize = reader.ReadUInt32();
                reader.BaseStream.Seek(608, SeekOrigin.Begin);

                // Bayer pattern parsing
                // Should correspond to these in this order: GBRG, GRBG, BGGR a RGGB.Moje kamera pouziva RGGB co je v nastaveni 3 patern ale jeho kamera a jeho kamera Basler pouziva GBRG cos je u tebe cislo 0.
                UInt32 bayerPatternAsUINT = reader.ReadUInt32();
                // 0= red, 1=green,2=blue
                switch (bayerPatternAsUINT)
                {
                    // GBRG
                    case 0:
                        bayerPattern = new byte[2,2]{ {1,2 },{0,1 } };
                        break;
                    // GRBG
                    case 1:
                        bayerPattern = new byte[2,2]{ {1,0},{2,1 } };
                        break;
                    // BGGR
                    case 2:
                        bayerPattern = new byte[2,2]{ {2,1 },{1,0 } };
                        break;
                    // RGGB
                    case 3:
                    default:
                        bayerPattern = new byte[2,2]{ {0,1 },{1,2 } };
                        break;
                }

                reader.BaseStream.Seek(620, SeekOrigin.Begin);
                compression = (Compression)reader.ReadUInt32();

                reader.BaseStream.Seek(660, SeekOrigin.Begin);
                UInt32 imageBytesAlignment = reader.ReadUInt32(); // Not quite sure what this one does tbh


                if (bitDepthReal == bitDepth && bitDepth == 16)
                {
                    rawDataFormat = RAWDATAFORMAT.BAYER12BITBRIGHTCAPSULEDIN16BIT;
                } else if (bitDepthReal == 12 && bitDepth == 16)
                {
                    rawDataFormat = RAWDATAFORMAT.BAYER12BITDARKCAPSULEDIN16BIT;
                } else if (bitDepthReal == 12 && bitDepth == 12 && imageFormat == ImageFormat.MONO_BAYER_PPACKED) {
                    rawDataFormat = RAWDATAFORMAT.BAYERRG12p;

                } else
                {
                    rawDataFormat = RAWDATAFORMAT.INVALID;
                }

                seqFileBasenameNoDots = Path.GetFileNameWithoutExtension(path).Replace(".","_");
            }
        }

        override public RAWDATAFORMAT getRawDataFormat()
        {
            return rawDataFormat;
        }

        override public int getWidth()
        {
            return (int)width;
        }
        override public int getHeight()
        {
            return (int)height;
        }
        override public byte[,] getBayerPattern()
        {
            return bayerPattern;
        }
        override public byte[] getRawImageData(int index, out ISSMetaInfo metaInfo, out ISSErrorInfo errorInfo)
        {
            metaInfo = new ISSMetaInfo();
            errorInfo = new ISSErrorInfo();
            using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open,FileAccess.Read,FileShare.Read), Encoding.Unicode))
            {
                reader.BaseStream.Seek(8192+ index*singleImageRealByteSize,SeekOrigin.Begin);
                return reader.ReadBytes((int)singleImageByteSize);
            }

        }
        public override bool imageExists(int index)
        {
            if (index < imageCount)
            {
                return true; //TODO Check for end of file due to corrupted transfers and such
            }
            else
            {
                return false;
            }
        }
        override public string getImageName(int index)
        {
            if (imageExists(index))
            {
                return seqFileBasenameNoDots+"_"+index.ToString();
            }
            else
            {
                return "undefined file [index " + index + "]";
            }
        }
        override public int getImageCount()
        {
            return (int)imageCount;
        }
    }
}

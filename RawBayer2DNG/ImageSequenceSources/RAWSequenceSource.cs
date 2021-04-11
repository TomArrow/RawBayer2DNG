using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawBayer2DNG.ImageSequenceSources
{
    class RAWSequenceSource : ImageSequenceSource
    {

        public int width;
        public int height;
        public byte[,] bayerPattern;
        public string[] paths;
        public RAWDATAFORMAT rawDataFormat;

        ImageSequenceSourceType sourceType = ImageSequenceSourceType.RAW;

        public RAWSequenceSource(RAWDATAFORMAT theRawDataFormat,int theWidth, int theHeight, byte[,] theBayerPattern, string[] thePaths)
        {
            rawDataFormat = theRawDataFormat;
            width = theWidth;
            height = theHeight;
            bayerPattern = theBayerPattern;
            paths = thePaths;
        }

        override public RAWDATAFORMAT getRawDataFormat()
        {
            return rawDataFormat;
        }

        override public int getWidth()
        {
            return width;
        }
        override public int getHeight()
        {
            return height;
        }
        override public byte[,] getBayerPattern()
        {
            return bayerPattern;
        }
        override public byte[] getRawImageData(int index, out ISSMetaInfo metaInfo, out ISSErrorInfo errorInfo)
        {
            metaInfo = new ISSMetaInfo();
            errorInfo = new ISSErrorInfo();
            return File.ReadAllBytes(paths[index]);
        }
        public override bool imageExists(int index)
        {
            if(index < paths.Length)
            {
                return File.Exists(paths[index]);
            } else
            {
                return false;
            }
        }
        override public string getImageName(int index)
        {
            if (imageExists(index))
            {
                return paths[index];
            } else
            {
                return "undefined file [index " + index + "]";
            }
        }
        override public int getImageCount()
        {
            return paths.Length;
        }
    }
}

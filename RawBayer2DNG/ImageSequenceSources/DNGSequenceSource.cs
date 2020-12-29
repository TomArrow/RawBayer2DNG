using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawBayer2DNG.ImageSequenceSources
{
    class DNGSequenceSource : ImageSequenceSource
    {

        public int width;
        public int height;
        public byte[] bayerPattern;
        public string[] paths;

        ImageSequenceSourceType sourceType = ImageSequenceSourceType.DNG;

        public DNGSequenceSource(int theWidth, int theHeight, byte[] theBayerPattern, string[] thePaths)
        {
            width = theWidth;
            height = theHeight;
            bayerPattern = theBayerPattern;
            paths = thePaths;
        }

        override public int getWidth()
        {
            return width;
        }
        override public int getHeight()
        {
            return height;
        }
        override public byte[] getBayerPattern()
        {
            return bayerPattern;
        }
        override public byte[] getRawImageData(int index)
        {
            return File.ReadAllBytes(paths[index]);
        }
        override public int getImageCount()
        {
            return paths.Length;
        }
    }
}

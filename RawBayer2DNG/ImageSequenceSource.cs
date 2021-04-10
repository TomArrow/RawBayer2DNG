using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawBayer2DNG
{
    abstract class ImageSequenceSource
    {


        public enum ImageSequenceSourceType { INVALID,RAW , STREAMPIX_SEQ,DNG };

        private ImageSequenceSourceType sourceType = ImageSequenceSourceType.INVALID;

        abstract public RAWDATAFORMAT getRawDataFormat();
        abstract public int getWidth();
        abstract public int getHeight();
        abstract public byte[,] getBayerPattern();
        abstract public byte[] getRawImageData(int index);
        abstract public bool imageExists(int index);
        abstract public int getImageCount();
        abstract public string getImageName(int index);
        public ImageSequenceSourceType getSourceType()
        {
            return sourceType;
        }

    }
}

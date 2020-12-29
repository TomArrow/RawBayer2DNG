using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawBayer2DNG
{
    abstract class ImageSequenceSource
    {

        public enum ImageSequenceSourceType { INVALID, DNG, STREAMPIX_SEQ };

        private ImageSequenceSourceType sourceType = ImageSequenceSourceType.INVALID; 

        abstract public int getWidth();
        abstract public int getHeight();
        abstract public byte[] getBayerPattern();
        abstract public byte[] getRawImageData(int index);
        abstract public int getImageCount();
        public ImageSequenceSourceType getSourceType()
        {
            return sourceType;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawBayer2DNG
{

    // This is for passing metadata worthy of preservation out of getRawImageData()
    class ISSMetaInfo
    {
        public List<byte[]> additionalFrameMetaBinary = new List<byte[]>();
        public List<string> additionalFrameMetaReadable = new List<string>();
        public List<string> additionalFrameMetaOriginalFilenames = new List<string>();

        public byte[] metaBinary = new byte[0];
        public string metaReadable = "";
        public string metaOriginalFilename = "";

        // For example for HDR merging there may be multiple source frames for a single output image. So we can combine multiple metadata chunks to preserve them all.
        public void mergeAdditionalMetaInfo(ISSMetaInfo additionalMetaInfo)
        {
            additionalFrameMetaBinary.Add(additionalMetaInfo.metaBinary);
            additionalFrameMetaBinary.AddRange(additionalMetaInfo.additionalFrameMetaBinary); // This technically shouldn't happen but we cover for it anyway.
            additionalFrameMetaReadable.Add(additionalMetaInfo.metaReadable);
            additionalFrameMetaReadable.AddRange(additionalMetaInfo.additionalFrameMetaReadable); // This technically shouldn't happen but we cover for it anyway.
            additionalFrameMetaOriginalFilenames.Add(additionalMetaInfo.metaOriginalFilename);
            additionalFrameMetaOriginalFilenames.AddRange(additionalMetaInfo.additionalFrameMetaOriginalFilenames); // This technically shouldn't happen but we cover for it anyway.
        }

        public byte[] getMergedMetaBinary()
        {
            List<byte> retVal = new List<byte>();

            retVal.AddRange(BitConverter.GetBytes((UInt32)(additionalFrameMetaBinary.Count+1))); // First we encode the total count of metadata chunks as a UInt32

            // Now for each metadata chunk
            // Main chunk
            retVal.AddRange(encodeChunk(metaOriginalFilename,metaBinary));
            // Additional chunks
            for(int i = 0; i < additionalFrameMetaBinary.Count; i++)
            {
                retVal.AddRange(encodeChunk(additionalFrameMetaOriginalFilenames[i], additionalFrameMetaBinary[i]));
            }

            return retVal.ToArray();
        }

        private List<byte> encodeChunk(string originalFilename, byte[] binaryMetadata)
        {
            List<byte> retVal = new List<byte>();

            byte[] originalFileNameBytes = Encoding.UTF8.GetBytes(originalFilename);
            retVal.AddRange(BitConverter.GetBytes((UInt32)(originalFileNameBytes.Length))); // Encode the length of the original filename as a UInt32
            retVal.AddRange(originalFileNameBytes); // Add the bytes of the original filename

            retVal.AddRange(BitConverter.GetBytes((UInt32)(binaryMetadata.Length))); // Encode the length of the metadata as a UInt32
            retVal.AddRange(binaryMetadata); // Add the bytes of binary metadata

            return retVal;
        }

    }

    class ISSMeta { 
    
        public enum MetaFormat
        {
            UNDEFINED = 0,
            CRI_METADATA = 1
        }

        public MetaFormat metaFormat = MetaFormat.UNDEFINED;
        public byte[] binaryData = new byte[0];
        public string metaHumanReadable = "";
        public string originalFilename = "";

        public ISSMeta(MetaFormat metaFormatA, byte[] binaryDataA, string metaHumanReadableA, string originalFilenameA)
        {
            metaFormat = metaFormatA;
            binaryData = binaryDataA;
            metaHumanReadable = metaHumanReadableA;
            originalFilename = originalFilenameA;
        }

    }

    // This is for passing error information/feedback worthy of preservation out of getRawImageData()
    // Not yet finished.
    class ISSErrorInfo
    {

        List<ISSError> errors = new List<ISSError>();
        public void addError(ISSError error)
        {
            errors.Add(error);
        }

        public void mergeMoreErrors(ISSErrorInfo errorInfo)
        {
            errors.AddRange(errorInfo.errors);
        }
    }

    class ISSError
    {
        public enum ErrorCode
        {
            UNDEFINED=0,
            ORIGINAL_FILE_CORRUPTED = 1,
            ORIGINAL_FILE_PARTIALLY_CORRUPTED_RESCUE = 2,
        }

        public ErrorCode errorCode = ErrorCode.UNDEFINED;
        public string originalFilename = "";
        public string description = "";
        public byte[] binaryData = new byte[0];

        public ISSError(ErrorCode errorCodeA, string originalFileNameA, string descriptionA, byte[] binaryDataA)
        {
            errorCode = errorCodeA;
            originalFilename = originalFileNameA;
            description = descriptionA;
            binaryData = binaryDataA;
        }
    }

    abstract class ImageSequenceSource
    {


        public enum ImageSequenceSourceType { INVALID,RAW , STREAMPIX_SEQ,DNG };

        private ImageSequenceSourceType sourceType = ImageSequenceSourceType.INVALID;

        abstract public RAWDATAFORMAT getRawDataFormat();
        abstract public int getWidth();
        abstract public int getHeight();
        abstract public byte[,] getBayerPattern();
        abstract public byte[] getRawImageData(int index,out ISSMetaInfo metaInfo,out ISSErrorInfo errorInfo);
        public byte[] getRawImageData(int index) // Alternative when metadata or error info return is not required and should be discardedo. Just for comfort.
        {
            ISSMetaInfo metaInfo;
            ISSErrorInfo errorInfo;
            return getRawImageData(index,out metaInfo, out errorInfo);
        }
        public byte[] getRawImageData(int index, out ISSMetaInfo metaInfo) // Alternative when only metadata is desired, but no error infoo. Just for comfort.
        {
            ISSErrorInfo errorInfo;
            return getRawImageData(index,out metaInfo, out errorInfo);
        }
        public byte[] getRawImageData(int index, out ISSErrorInfo errorInfo) // Alternative when only error info is desired, but no meta info. Just for comfort.
        {
            ISSMetaInfo metaInfo;
            return getRawImageData(index,out metaInfo, out errorInfo);
        }
        abstract public bool imageExists(int index);
        abstract public int getImageCount();
        abstract public string getImageName(int index);
        public ImageSequenceSourceType getSourceType()
        {
            return sourceType;
        }

    }
}

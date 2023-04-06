using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawBayer2DNG
{

    // This is for passing metadata worthy of preservation out of getRawImageData()
    class ISSMetaInfo
    {
        public List<ISSMeta> metaChunks = new List<ISSMeta>();

        public void addMeta(ISSMeta metaChunk)
        {
            metaChunks.Add(metaChunk);
        }

        // For example for HDR merging there may be multiple source frames for a single output image. So we can combine multiple metadata chunks to preserve them all.
        public void mergeAdditionalMetaInfo(ISSMetaInfo additionalMetaInfo)
        {
            metaChunks.AddRange(additionalMetaInfo.metaChunks);
        }

        public void mergeErrors(ISSErrorInfo errorInfo)
        {
            metaChunks.Add(new ISSMeta(ISSMeta.MetaFormat.ERROR_INFO,errorInfo.getMergedBinary(),"","")); // No need to replicate filename and human readable here, that's already within the merged binary of the errorInfo
        }

        public byte[] getMergedBinary(byte[] prepend = null)
        {
            List<byte> retVal = new List<byte>();

            if(prepend != null)
            {
                retVal.AddRange(prepend);
            }

            retVal.AddRange(BitConverter.GetBytes((UInt32)metaChunks.Count)); // First we encode the total count of metadata chunks as a UInt32

            // Now for each metadata chunk
            // Main chunk
            // Additional chunks
            for(int i = 0; i < metaChunks.Count; i++)
            {
                retVal.AddRange(encodeChunk(metaChunks[i]));
            }

            return retVal.ToArray();
        }

        // Each individual chunk in binary consists of 7 elements:
        // MetaFormat as UInt32
        // Length of binary data as UInt32
        // Binary data
        // Length of human readable metadata as UInt32
        // Human readable metadata as bytes (from UTF8 getbytes)
        // Length of original filename as UInt32
        // Original filename as bytes (from UTF8 getbytes)
        private List<byte> encodeChunk(ISSMeta metaChunk)
        {

            ISSMeta.MetaFormat metaFormat = metaChunk.metaFormat;
            string originalFilename = metaChunk.originalFilename;
            byte[] binaryMetadata = metaChunk.binaryData;
            string humanReadable = metaChunk.metaHumanReadable;

            List<byte> retVal = new List<byte>();

            byte[] originalFileNameBytes = Encoding.UTF8.GetBytes(originalFilename);
            byte[] humanReadableBytes = Encoding.UTF8.GetBytes(humanReadable);

            retVal.AddRange(BitConverter.GetBytes((UInt32)metaFormat)); // First the metadata format, so we know what exactly we're encoding here, in case we ever need to decode it again.

            retVal.AddRange(BitConverter.GetBytes((UInt32)(binaryMetadata.Length))); // Encode the length of the metadata as a UInt32
            retVal.AddRange(binaryMetadata); // Add the bytes of binary metadata

            retVal.AddRange(BitConverter.GetBytes((UInt32)(humanReadableBytes.Length))); // Encode the length of the original filename as a UInt32
            retVal.AddRange(humanReadableBytes); // Add the bytes of the original filename

            retVal.AddRange(BitConverter.GetBytes((UInt32)(originalFileNameBytes.Length))); // Encode the length of the original filename as a UInt32
            retVal.AddRange(originalFileNameBytes); // Add the bytes of the original filename


            return retVal;
        }

    }

    class ISSMeta { 
    
        public enum MetaFormat
        {
            UNDEFINED = 0,
            CRI_METADATA = 10,
            ERROR_INFO = 200,
        }

        public MetaFormat metaFormat = MetaFormat.UNDEFINED;
        public byte[] binaryData = new byte[0];
        public string metaHumanReadable = "";
        public string originalFilename = "";

        public ISSMeta(MetaFormat metaFormatA, byte[] binaryDataA, string metaHumanReadableA, string originalFilenameA,bool keepFullFilepath = false)
        {
            metaFormat = metaFormatA;
            binaryData = binaryDataA;
            metaHumanReadable = metaHumanReadableA;
            if (keepFullFilepath)
            {

                originalFilename = originalFilenameA;
            }
            else
            {

                originalFilename = Path.GetFileName(originalFilenameA);
            }
        }

    }

    // This is for passing error information/feedback worthy of preservation out of getRawImageData()
    // Not yet finished.
    class ISSErrorInfo
    {

        public List<ISSError> errors = new List<ISSError>();

        public void addError(ISSError error)
        {
            errors.Add(error);
        }

        public void mergeMoreErrors(ISSErrorInfo errorInfo)
        {
            errors.AddRange(errorInfo.errors);
        }

        public string getHumanReadableErrorsCSV()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("ErrorCode,Severity,\"Error Description\",\"Original Filename\",\"Binary Data\"");
            foreach(ISSError error in errors)
            {

                sb.Append(error.errorCode);
                sb.Append(",");
                sb.Append(error.errorSeverity);
                sb.Append(",");
                sb.Append("\"");
                sb.Append(error.description.Replace("\"", "\"\"")); // Turn quotes into double quotes to escpae them for CSV
                sb.Append("\"");
                sb.Append(",");
                sb.Append("\"");
                sb.Append(error.originalFilename.Replace("\"", "\"\"")); // Turn quotes into double quotes to escpae them for CSV
                sb.Append("\"");
                sb.Append(",");
                sb.Append(Helpers.byteArrayToHexString(error.binaryData));
                sb.AppendLine();
            }

            return sb.ToString();
        }


        public byte[] getMergedBinary()
        {
            List<byte> retVal = new List<byte>();

            retVal.AddRange(BitConverter.GetBytes((UInt32)errors.Count)); // First we encode the total count of metadata chunks as a UInt32

            // Now for each metadata chunk
            // Main chunk
            // Additional chunks
            for (int i = 0; i < errors.Count; i++)
            {
                retVal.AddRange(encodeError(errors[i]));
            }

            return retVal.ToArray();
        }

        // Each individual chunk in binary consists of 8 elements:
        // Errorcode as UInt32
        // Error severity as UInt32
        // Length of binary data as UInt32
        // Binary data
        // Length of human readable error as UInt32
        // Human readable error  as bytes (from UTF8 getbytes)
        // Length of original filename as UInt32
        // Original filename as bytes (from UTF8 getbytes)
        private List<byte> encodeError(ISSError anError)
        {
            ISSError.ErrorCode errorCode = anError.errorCode;
            ISSError.ErrorSeverity errorSeverity = anError.errorSeverity;
            string originalFilename = anError.originalFilename;
            byte[] binaryMetadata = anError.binaryData;
            string humanReadable = anError.description;

            List<byte> retVal = new List<byte>();

            byte[] originalFileNameBytes = Encoding.UTF8.GetBytes(originalFilename);
            byte[] humanReadableBytes = Encoding.UTF8.GetBytes(humanReadable);

            retVal.AddRange(BitConverter.GetBytes((UInt32)errorCode)); // First the error type, so we know what exactly we're encoding here, in case we ever need to decode it again.
            retVal.AddRange(BitConverter.GetBytes((UInt32)errorSeverity)); // 

            retVal.AddRange(BitConverter.GetBytes((UInt32)(binaryMetadata.Length))); // Encode the length of the metadata as a UInt32
            retVal.AddRange(binaryMetadata); // Add the bytes of binary metadata

            retVal.AddRange(BitConverter.GetBytes((UInt32)(humanReadableBytes.Length))); // Encode the length of the original filename as a UInt32
            retVal.AddRange(humanReadableBytes); // Add the bytes of the original filename

            retVal.AddRange(BitConverter.GetBytes((UInt32)(originalFileNameBytes.Length))); // Encode the length of the original filename as a UInt32
            retVal.AddRange(originalFileNameBytes); // Add the bytes of the original filename


            return retVal;
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
        public enum ErrorSeverity
        {
            UNDEFINED = 0,
            NOTE = 1,
            WARNING = 2,
            SEVERE = 3,
            CRITICAL = 4,
        }

        public ErrorCode errorCode = ErrorCode.UNDEFINED;
        public ErrorSeverity errorSeverity = ErrorSeverity.UNDEFINED;
        public string originalFilename = "";
        public string description = "";
        public byte[] binaryData = new byte[0];

        public ISSError(ErrorCode errorCodeA, ErrorSeverity errorSeverityA, string originalFileNameA, string descriptionA, byte[] binaryDataA,bool keepFullFilepath = false)
        {
            errorCode = errorCodeA;
            errorSeverity = errorSeverityA;

            if (keepFullFilepath)
            {

                originalFilename = originalFileNameA;
            } else
            {

                originalFilename = Path.GetFileName(originalFileNameA);
            }
            description = descriptionA;
            binaryData = binaryDataA;
        }
    }

    interface ImageSequenceSourceCountable
    {
        abstract public int getImageCount();
    }

    abstract class ImageSequenceSource
    {


        public enum ImageSequenceSourceType { INVALID,RAW , STREAMPIX_SEQ,DNG,CRI };

        private ImageSequenceSourceType sourceType = ImageSequenceSourceType.INVALID;

        abstract public RAWDATAFORMAT getRawDataFormat();
        abstract public int getWidth();
        abstract public int getHeight();
        abstract public byte[,] getBayerPattern();
        abstract public byte[] getRawImageData(int index,ref ISSMetaInfo metaInfo,ref ISSErrorInfo errorInfo);
        public byte[] getRawImageData(int index) // Alternative when metadata or error info return is not required and should be discardedo. Just for comfort.
        {
            ISSMetaInfo metaInfo = new ISSMetaInfo();
            ISSErrorInfo errorInfo = new ISSErrorInfo();
            return getRawImageData(index,ref metaInfo, ref errorInfo);
        }
        public byte[] getRawImageData(int index, ref ISSMetaInfo metaInfo) // Alternative when only metadata is desired, but no error infoo. Just for comfort.
        {
            ISSErrorInfo errorInfo = new ISSErrorInfo();
            return getRawImageData(index,ref metaInfo, ref errorInfo);
        }
        public byte[] getRawImageData(int index, ref ISSErrorInfo errorInfo) // Alternative when only error info is desired, but no meta info. Just for comfort.
        {
            ISSMetaInfo metaInfo = new ISSMetaInfo();
            return getRawImageData(index,ref metaInfo, ref errorInfo);
        }
        abstract public bool imageExists(int index);
        abstract public string getImageName(int index);
        public ImageSequenceSourceType getSourceType()
        {
            return sourceType;
        }

    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using int8 = System.SByte;
using uint8 = System.Byte;
using uint16 = System.UInt16;
using int16 = System.Int16;
using int32 = System.Int32;
using uint32 = System.UInt32;
using System.Runtime.CompilerServices;

namespace JpegDecodingTests
{


    /*
	class dng_stream : dng_uncopyable
	{
	
	
		public int32 kSmallBufferSize = 8 * 1024;
	public int32 kBigBufferSize = 64 * 1024;

	public int32 kDefaultBufferSize = kSmallBufferSize;


	
		private bool fSwapBytes;

		private bool fHaveLength;

		private uint64 fLength;

		private const uint64 fOffsetInOriginalFile;

		private uint64 fPosition;

		private AutoPtr<dng_memory_block> fMemBlock;

		private uint8* fBuffer;

		private uint32 fBufferSize;

		private uint64 fBufferStart;
		private uint64 fBufferEnd;
		private uint64 fBufferLimit;

		private bool fBufferDirty;

		private dng_abort_sniffer* fSniffer;

		protected dng_stream(dng_abort_sniffer* sniffer = NULL,
					uint32 bufferSize = kDefaultBufferSize,
					uint64 offsetInOriginalFile = kDNGStreamInvalidOffset);

		protected virtual uint64 DoGetLength();

		protected virtual void DoRead(void* data,
						 uint32 count,
						 uint64 offset);

		protected virtual void DoSetLength(uint64 length);

		protected virtual void DoWrite(const void* data,
						  uint32 count,
						  uint64 offset);

	
		/// Construct a stream with initial data.
		/// \param data Pointer to initial contents of stream.
		/// \param count Number of bytes data is valid for.
		/// \param offsetInOriginalFile If data came from a file originally,
		/// offset can be saved here for later use.

		public dng_stream(const void* data,
					uint32 count,
					uint64 offsetInOriginalFile = kDNGStreamInvalidOffset);

	public virtual ~dng_stream();

		/// Getter for whether stream is swapping byte order on input/output.
		/// \retval If true, data will be swapped on input/output.

		public bool SwapBytes() const
			{
			return fSwapBytes;
			}

	/// Setter for whether stream is swapping byte order on input/output.
	/// \param swapBytes If true, stream will swap byte order on input or
	/// output for future reads/writes.

	public void SetSwapBytes(bool swapBytes)
{
	fSwapBytes = swapBytes;
}

	/// Getter for whether data in stream is big endian.
	/// \retval If true, data in stream is big endian.

	public bool BigEndian() const;

	/// Setter for whether data in stream is big endian.
	/// \param bigEndian If true, data in stream is big endian.

	public void SetBigEndian(bool bigEndian = true);

	/// Getter for whether data in stream is big endian.
	/// \retval If true, data in stream is big endian.

	public bool LittleEndian() const
			{
			return !BigEndian ();
			}

/// Setter for whether data in stream is big endian.
/// \param littleEndian If true, data in stream is big endian.

public void SetLittleEndian(bool littleEndian = true)
{
	SetBigEndian(!littleEndian);
}

/// Returns the size of the buffer used by the stream.

public uint32 BufferSize() const
			{
			return fBufferSize;
			}

        /// Change the buffer size on the stream, if possible.
                
        public void SetBufferSize(dng_memory_allocator &allocator,
							uint32 newBufferSize);

/// Getter for length of data in stream.
/// \retval Length of readable data in stream.

public uint64 Length()
{

	if (!fHaveLength)
	{

		fLength = DoGetLength();

		fHaveLength = true;

	}

	return fLength;

}

/// Getter for current offset in stream.
/// \retval current offset from start of stream.

public uint64 Position() const
			{
			return fPosition;
			}
			
		/// Getter for current position in original file, taking into account
		/// OffsetInOriginalFile stream data was taken from.
		/// \retval kInvalidOffset if no offset in original file is set, sum 
		/// of offset in original file and current position otherwise.

		public uint64 PositionInOriginalFile() const;

/// Getter for offset in original file.
/// \retval kInvalidOffset if no offset in original file is set,
/// offset in original file otherwise.

public uint64 OffsetInOriginalFile() const;

/// Return pointer to stream contents if the stream is entirely 
/// available as a single memory block, NULL otherwise.

public const void* Data () const;

/// Return the entire stream as a single memory block.
/// This works for all streams, but requires copying the data to a new buffer.
/// \param allocator Allocator used to allocate memory.

public dng_memory_block* AsMemoryBlock(dng_memory_allocator &allocator);

/// Seek to a new position in stream for reading.

public void SetReadPosition(uint64 offset);

/// Skip forward in stream.
/// \param delta Number of bytes to skip forward.

public void Skip(uint64 delta)
{
	SetReadPosition(Position() + delta);
}

/// Quick check to see if data range in completely buffered.

public bool DataInBuffer(uint32 count,
				   uint64 offset)
{
	return (offset >= fBufferStart &&
			offset + count <= fBufferEnd);
}

/// Get data from stream. Exception is thrown and no data is read if 
/// insufficient data available in stream.
/// \param data Buffer to put data into. Must be valid for count bytes.
/// \param count Bytes of data to read.
/// \exception dng_exception with fErrorCode equal to dng_error_end_of_file 
/// if not enough data in stream.

public void Get(void* data, uint32 count, uint32 maxOverRead = 0);

/// Seek to a new position in stream for writing.

public void SetWritePosition(uint64 offset);

/// Force any stored data in stream to be written to underlying storage.

public void Flush();

/// Set length of available data.
/// \param length Number of bytes of avialble data in stream.

public void SetLength(uint64 length);

/// Write data to stream.
/// \param data Buffer of data to write to stream.
/// \param count Bytes of in data.

public void Put(const void* data, uint32 count);

/// Get an unsigned 8-bit integer from stream and advance read position.
/// \retval One unsigned 8-bit integer.
/// \exception dng_exception with fErrorCode equal to dng_error_end_of_file 
/// if not enough data in stream.

public  uint8 Get_uint8()
{

	// Fast check to see if in buffer

	if (fPosition >= fBufferStart && fPosition < fBufferEnd)
	{

		return fBuffer[fPosition++ - fBufferStart];

	}

	// Not in buffer, let main routine do the work.

	uint8 x;

	Get(&x, 1);

	return x;

}

/// Put an unsigned 8-bit integer to stream and advance write position.
/// \param x One unsigned 8-bit integer.

public  void Put_uint8(uint8 x)
{

	if (fBufferDirty &&
		fPosition >= fBufferStart &&
		fPosition <= fBufferEnd &&
		fPosition < fBufferLimit)
	{

		fBuffer[fPosition - fBufferStart] = x;

		fPosition++;

		if (fBufferEnd < fPosition)
			fBufferEnd = fPosition;

		fLength = Max_uint64(Length(), fPosition);

	}

	else
	{

		Put(&x, 1);

	}

}

/// Get an unsigned 16-bit integer from stream and advance read position. 
/// Byte swap if byte swapping is turned on.
/// \retval One unsigned 16-bit integer.
/// \exception dng_exception with fErrorCode equal to dng_error_end_of_file
/// if not enough data in stream.

public uint16 Get_uint16();

/// Put an unsigned 16-bit integer to stream and advance write position.
/// Byte swap if byte swapping is turned on.
/// \param x One unsigned 16-bit integer.

public void Put_uint16(uint16 x);

/// Get an unsigned 32-bit integer from stream and advance read position. 
/// Byte swap if byte swapping is turned on.
/// \retval One unsigned 32-bit integer.
/// \exception dng_exception with fErrorCode equal to dng_error_end_of_file
/// if not enough data in stream.

public uint32 Get_uint32();

#if !qDNGBigEndian
inline // ep, enable compiler inlining
uint32 Get_uint32_LE ()
			{

	uint32 x;

	Get(&x, 4, 3); // Allow 3-byte overread (undefined data returned but not used)

	// No check for fSwapBytes

	return x;

}
#endif

/// Put an unsigned 32-bit integer to stream and advance write position. 
/// Byte swap if byte swapping is turned on.
/// \param x One unsigned 32-bit integer.

void Put_uint32(uint32 x);

/// Get an unsigned 64-bit integer from stream and advance read position. 
/// Byte swap if byte swapping is turned on.
/// \retval One unsigned 64-bit integer.
/// \exception dng_exception with fErrorCode equal to dng_error_end_of_file
/// if not enough data in stream.

uint64 Get_uint64();

/// Put an unsigned 64-bit integer to stream and advance write position. 
/// Byte swap if byte swapping is turned on.
/// \param x One unsigned 64-bit integer.

void Put_uint64(uint64 x);

/// Get one 8-bit integer from stream and advance read position.
/// \retval One 8-bit integer.
/// \exception dng_exception with fErrorCode equal to dng_error_end_of_file
/// if not enough data in stream.

int8 Get_int8()
{
	return (int8)Get_uint8();
}

/// Put one 8-bit integer to stream and advance write position.
/// \param x One  8-bit integer.

void Put_int8(int8 x)
{
	Put_uint8((uint8)x);
}

/// Get one 16-bit integer from stream and advance read position. 
/// Byte swap if byte swapping is turned on.
/// \retval One 16-bit integer.
/// \exception dng_exception with fErrorCode equal to dng_error_end_of_file 
/// if not enough data in stream.

int16 Get_int16()
{
	return (int16)Get_uint16();
}

/// Put one 16-bit integer to stream and advance write position.
/// Byte swap if byte swapping is turned on.
/// \param x One 16-bit integer.

void Put_int16(int16 x)
{
	Put_uint16((uint16)x);
}

/// Get one 32-bit integer from stream and advance read position. 
/// Byte swap if byte swapping is turned on.
/// \retval One 32-bit integer.
/// \exception dng_exception with fErrorCode equal to dng_error_end_of_file
/// if not enough data in stream.

int32 Get_int32()
{
	return (int32)Get_uint32();
}

/// Put one 32-bit integer to stream and advance write position.
/// Byte swap if byte swapping is turned on.
/// \param x One 32-bit integer.

void Put_int32(int32 x)
{
	Put_uint32((uint32)x);
}

/// Get one 64-bit integer from stream and advance read position. 
/// Byte swap if byte swapping is turned on.
/// \retval One 64-bit integer.
/// \exception dng_exception with fErrorCode equal to dng_error_end_of_file
/// if not enough data in stream.

int64 Get_int64()
{
	return (int64)Get_uint64();
}

/// Put one 64-bit integer to stream and advance write position.
/// Byte swap if byte swapping is turned on.
/// \param x One 64-bit integer.

void Put_int64(int64 x)
{
	Put_uint64((uint64)x);
}

/// Get one 32-bit IEEE floating-point number from stream and advance 
/// read position. Byte swap if byte swapping is turned on.
/// \retval One 32-bit IEEE floating-point number.
/// \exception dng_exception with fErrorCode equal to dng_error_end_of_file 
/// if not enough data in stream.

real32 Get_real32();

/// Put one 32-bit IEEE floating-point number to stream and advance write
/// position. Byte swap if byte swapping is turned on.
/// \param x One 32-bit IEEE floating-point number.

void Put_real32(real32 x);

/// Get one 64-bit IEEE floating-point number from stream and advance
/// read position. Byte swap if byte swapping is turned on.
/// \retval One 64-bit IEEE floating-point number .
/// \exception dng_exception with fErrorCode equal to dng_error_end_of_file
/// if not enough data in stream.

real64 Get_real64();

/// Put one 64-bit IEEE floating-point number to stream and advance write
/// position. Byte swap if byte swapping is turned on.
/// \param x One64-bit IEEE floating-point number.

void Put_real64(real64 x);

/// Get an 8-bit character string from stream and advance read position.
/// Routine always reads until a NUL character (8-bits of zero) is read.
/// (That is, only maxLength bytes will be returned in buffer, but the
/// stream is always advanced until a NUL is read or EOF is reached.)
/// \param data Buffer in which string is returned.
/// \param maxLength Maximum number of bytes to place in buffer.
/// \exception dng_exception with fErrorCode equal to dng_error_end_of_file
/// if stream runs out before NUL is seen.

void Get_CString(char* data,
				  uint32 maxLength);

/// Get a 16-bit character string from stream and advance read position.
/// 16-bit characters are truncated to 8-bits.
/// Routine always reads until a NUL character (16-bits of zero) is read.
/// (That is, only maxLength bytes will be returned in buffer, but the 
/// stream is always advanced until a NUL is read or EOF is reached.)
/// \param data Buffer to place string in.
/// \param maxLength Maximum number of bytes to place in buffer.
/// \exception dng_exception with fErrorCode equal to dng_error_end_of_file
/// if stream runs out before NUL is seen.

void Get_UString(char* data,
				  uint32 maxLength);

/// Writes the specified number of zero bytes to stream.
/// \param count Number of zero bytes to write.

void PutZeros(uint64 count);

/// Writes zeros to align the stream position to a multiple of 2.

void PadAlign2();

/// Writes zeros to align the stream position to a multiple of 4.

void PadAlign4();

/// Get a value of size indicated by tag type from stream and advance
/// read position. Byte swap if byte swapping is turned on and tag type
/// is larger than a byte. Value is returned as an unsigned 32-bit integer. 
/// \param tagType Tag type of data stored in stream.
/// \retval One unsigned 32-bit integer.
/// \exception dng_exception with fErrorCode equal to dng_error_end_of_file
/// if not enough data in stream.

uint32 TagValue_uint32(uint32 tagType);

/// Get a value of size indicated by tag type from stream and advance read
/// position. Byte swap if byte swapping is turned on and tag type is larger
/// than a byte. Value is returned as a 32-bit integer. 
/// \param tagType Tag type of data stored in stream.
/// \retval One 32-bit integer.
/// \exception dng_exception with fErrorCode equal to dng_error_end_of_file 
/// if not enough data in stream.

int32 TagValue_int32(uint32 tagType);

/// Get a value of size indicated by tag type from stream and advance read 
/// position. Byte swap if byte swapping is turned on and tag type is larger
/// than a byte. Value is returned as a dng_urational. 
/// \param tagType Tag type of data stored in stream.
/// \retval One dng_urational.
/// \exception dng_exception with fErrorCode equal to dng_error_end_of_file
/// if not enough data in stream.

dng_urational TagValue_urational(uint32 tagType);

/// Get a value of size indicated by tag type from stream and advance read
/// position. Byte swap if byte swapping is turned on and tag type is larger
/// than a byte. Value is returned as a dng_srational. 
/// \param tagType Tag type of data stored in stream.
/// \retval One dng_srational.
/// \exception dng_exception with fErrorCode equal to dng_error_end_of_file
/// if not enough data in stream.

dng_srational TagValue_srational(uint32 tagType);

/// Get a value of size indicated by tag type from stream and advance read
/// position. Byte swap if byte swapping is turned on and tag type is larger
/// than a byte. Value is returned as a 64-bit IEEE floating-point number. 
/// \param tagType Tag type of data stored in stream.
/// \retval One 64-bit IEEE floating-point number.
/// \exception dng_exception with fErrorCode equal to dng_error_end_of_file
/// if not enough data in stream.

real64 TagValue_real64(uint32 tagType);

/// Getter for sniffer associated with stream.
/// \retval The sniffer for this stream.

dng_abort_sniffer* Sniffer() const
			{
			return fSniffer;
			}
			
		/// Putter for sniffer associated with stream.
		/// \param sniffer The new sniffer to use (or NULL for none).
		
		void SetSniffer(dng_abort_sniffer* sniffer)
{
	fSniffer = sniffer;
}

/// Copy a specified number of bytes to a target stream.
/// \param dstStream The target stream.
/// \param count The number of bytes to copy.

virtual void CopyToStream(dng_stream &dstStream,
						   uint64 count);

/// Makes the target stream a copy of this stream.
/// \param dstStream The target stream.

void DuplicateStream(dng_stream &dstStream);
		
	};


class dng_uncopyable
	{

		protected:

		dng_uncopyable()
		{
		}

		~dng_uncopyable()
		{
		}

		private:
		
		dng_uncopyable(const dng_uncopyable &);

		dng_uncopyable & operator= (const dng_uncopyable &);

	};

*/
    enum JpegMarker
    {

        M_TEM = 0x01,

        M_SOF0 = 0xc0,
        M_SOF1 = 0xc1,
        M_SOF2 = 0xc2,
        M_SOF3 = 0xc3,
        M_DHT = 0xc4,
        M_SOF5 = 0xc5,
        M_SOF6 = 0xc6,
        M_SOF7 = 0xc7,
        M_JPG = 0xc8,
        M_SOF9 = 0xc9,
        M_SOF10 = 0xca,
        M_SOF11 = 0xcb,
        M_DAC = 0xcc,
        M_SOF13 = 0xcd,
        M_SOF14 = 0xce,
        M_SOF15 = 0xcf,

        M_RST0 = 0xd0,
        M_RST1 = 0xd1,
        M_RST2 = 0xd2,
        M_RST3 = 0xd3,
        M_RST4 = 0xd4,
        M_RST5 = 0xd5,
        M_RST6 = 0xd6,
        M_RST7 = 0xd7,

        M_SOI = 0xd8,
        M_EOI = 0xd9,
        M_SOS = 0xda,
        M_DQT = 0xdb,
        M_DNL = 0xdc,
        M_DRI = 0xdd,
        M_DHP = 0xde,
        M_EXP = 0xdf,

        M_APP0 = 0xe0,
        M_APP1 = 0xe1,
        M_APP2 = 0xe2,
        M_APP3 = 0xe3,
        M_APP4 = 0xe4,
        M_APP5 = 0xe5,
        M_APP6 = 0xe6,
        M_APP7 = 0xe7,
        M_APP8 = 0xe8,
        M_APP9 = 0xe9,
        M_APP10 = 0xea,
        M_APP11 = 0xeb,
        M_APP12 = 0xec,
        M_APP13 = 0xed,
        M_APP14 = 0xee,
        M_APP15 = 0xef,

        M_JPG0 = 0xf0,
        M_JPG1 = 0xf1,
        M_JPG2 = 0xf2,
        M_JPG3 = 0xf3,
        M_JPG4 = 0xf4,
        M_JPG5 = 0xf5,
        M_JPG6 = 0xf6,
        M_JPG7 = 0xf7,
        M_JPG8 = 0xf8,
        M_JPG9 = 0xf9,
        M_JPG10 = 0xfa,
        M_JPG11 = 0xfb,
        M_JPG12 = 0xfc,
        M_JPG13 = 0xfd,
        M_COM = 0xfe,

        M_ERROR = 0x100

    };

    class HuffmanTable
    {

        /*
		 * These two fields directly represent the contents of a JPEG DHT
		 * marker
		 */
        public uint8[] bits = new uint8[17];
        public uint8[] huffval = new uint8[256];

        /*
		 * The remaining fields are computed from the above to allow more
		 * efficient coding and decoding.  These fields should be considered
		 * private to the Huffman compression & decompression modules.
		 */

        public uint16[] mincode = new uint16[17];
        public int32[] maxcode = new int32[18];
        public int16[] valptr = new int16[17];
        public int32[] numbits = new int32[256];
        public int32[] value = new int32[256];

        public uint16[] ehufco = new uint16[256];
        public int8[] ehufsi = new int8[256];

    };

    /*
 * The following structure stores basic information about one component.
 */

    struct JpegComponentInfo
    {

        /*
		 * These values are fixed over the whole image.
		 * They are read from the SOF marker.
		 */
        public int16 componentId;      /* identifier for this component (0..255) */
        public int16 componentIndex;   /* its index in SOF or cPtr->compInfo[]   */

        /*
		 * Downsampling is not normally used in lossless JPEG, although
		 * it is permitted by the JPEG standard (DIS). We set all sampling 
		 * factors to 1 in this program.
		 */
        public int16 hSampFactor;      /* horizontal sampling factor */
        public int16 vSampFactor;      /* vertical sampling factor   */

        /*
		 * Huffman table selector (0..3). The value may vary
		 * between scans. It is read from the SOS marker.
		 */
        public int16 dcTblNo;

    };

    /*
	 * One of the following structures is used to pass around the
	 * decompression information.
	 */

    class DecompressInfo
    {

        /*
		 * Image width, height, and image data precision (bits/sample)
		 * These fields are set by ReadFileHeader or ReadScanHeader
		 */
        public int32 imageWidth;
        public int32 imageHeight;
        public int32 dataPrecision;

        /*
		 * compInfo[i] describes component that appears i'th in SOF
		 * numComponents is the # of color components in JPEG image.
		 */
        public JpegComponentInfo compInfo;
        public int16 numComponents;

        /*
		 * *curCompInfo[i] describes component that appears i'th in SOS.
		 * compsInScan is the # of color components in current scan.
		 */
        public JpegComponentInfo[] curCompInfo = new JpegComponentInfo[4];
        public int16 compsInScan;

        /*
		 * MCUmembership[i] indexes the i'th component of MCU into the
		 * curCompInfo array.
		 */
        public int16[] MCUmembership = new int16[10];

        /*
		 * ptrs to Huffman coding tables, or NULL if not defined
		 */
        public HuffmanTable[] dcHuffTblPtrs = new HuffmanTable[4];

        /* 
		 * prediction selection value (PSV) and point transform parameter (Pt)
		 */
        public int32 Ss;
        public int32 Pt;

        /*
		 * In lossless JPEG, restart interval shall be an integer
		 * multiple of the number of MCU in a MCU row.
		 */
        public int32 restartInterval;/* MCUs per restart interval, 0 = no restart */
        public int32 restartInRows; /*if > 0, MCU rows per restart interval; 0 = no restart*/

        /*
		 * these fields are private data for the entropy decoder
		 */
        public int32 restartRowsToGo;  /* MCUs rows left in this restart interval */
        public int16 nextRestartNum;   /* # of next RSTn marker (0..7) */

    };

    /*****************************************************************************/

    class DNGLosslessEncoder
    {



        /*****************************************************************************/

        // Computes the derived fields in the Huffman table structure.

        static void FixHuffTbl(ref HuffmanTable htbl)
        {

            int32 l;
            int32 i;

            uint32[] bitMask =
                {
            0xffffffff, 0x7fffffff, 0x3fffffff, 0x1fffffff,
            0x0fffffff, 0x07ffffff, 0x03ffffff, 0x01ffffff,
            0x00ffffff, 0x007fffff, 0x003fffff, 0x001fffff,
            0x000fffff, 0x0007ffff, 0x0003ffff, 0x0001ffff,
            0x0000ffff, 0x00007fff, 0x00003fff, 0x00001fff,
            0x00000fff, 0x000007ff, 0x000003ff, 0x000001ff,
            0x000000ff, 0x0000007f, 0x0000003f, 0x0000001f,
            0x0000000f, 0x00000007, 0x00000003, 0x00000001
            };

            // Figure C.1: make table of Huffman code length for each symbol
            // Note that this is in code-length order.

            int8[] huffsize = new int8[257];

            int32 p = 0;

            for (l = 1; l <= 16; l++)
            {

                for (i = 1; i <= (int32)htbl.bits[l]; i++)
                    huffsize[p++] = (int8)l;

            }

            huffsize[p] = 0;

            int32 lastp = p;

            // Figure C.2: generate the codes themselves
            // Note that this is in code-length order.

            uint16[] huffcode = new uint16[257];

            uint16 code = 0;

            int32 si = huffsize[0];

            p = 0;

            while (huffsize[p] != 0)
            {

                while (((int32)huffsize[p]) == si)
                {
                    huffcode[p++] = code;
                    code++;
                }

                code <<= 1;

                si++;

            }

            // Figure C.3: generate encoding tables
            // These are code and size indexed by symbol value
            // Set any codeless symbols to have code length 0; this allows
            // EmitBits to detect any attempt to emit such symbols.

            //memset(htbl->ehufsi, 0, sizeof(htbl->ehufsi));
            htbl.ehufsi = new int8[htbl.ehufsi.Length];

            for (p = 0; p < lastp; p++)
            {

                htbl.ehufco[htbl.huffval[p]] = huffcode[p];
                htbl.ehufsi[htbl.huffval[p]] = huffsize[p];

            }

            // Figure F.15: generate decoding tables

            p = 0;

            for (l = 1; l <= 16; l++)
            {

                if (htbl.bits[l] != 0)
                {

                    htbl.valptr[l] = (int16)p;
                    htbl.mincode[l] = huffcode[p];

                    p += htbl.bits[l];

                    htbl.maxcode[l] = huffcode[p - 1];

                }

                else
                {
                    htbl.maxcode[l] = -1;
                }

            }

            // We put in this value to ensure HuffDecode terminates.

            htbl.maxcode[17] = (int)0xFFFFFL;

            // Build the numbits, value lookup tables.
            // These table allow us to gather 8 bits from the bits stream,
            // and immediately lookup the size and value of the huffman codes.
            // If size is zero, it means that more than 8 bits are in the huffman
            // code (this happens about 3-4% of the time).

            //memset(htbl->numbits, 0, sizeof(htbl->numbits));
            htbl.numbits = new int32[htbl.numbits.Length];

            for (p = 0; p < lastp; p++)
            {

                int32 size = huffsize[p];

                if (size <= 8)
                {

                    int32 value = htbl.huffval[p];

                    code = huffcode[p];

                    int32 ll = code << (8 - size);

                    int32 ul = (size < 8 ? (Int32)((uint32)ll | bitMask[24 + size])
                                         : ll);

                    if (ul >= (int32)htbl.numbits.Length ||
                     ul >= (Int32)htbl.value.Length)
                    {
                        //ThrowBadFormat(); // TODO see if I need to add this back in somehow
                    }

                    for (i = ll; i <= ul; i++)
                    {
                        htbl.numbits[i] = size;
                        htbl.value[i] = value;
                    }

                }





                /*****************************************************************************/



                // An MCU (minimum coding unit) is an array of samples.

                typedef uint16 ComponentType;       // the type of image components

                typedef ComponentType *MCU;         // MCU - array of samples

                /*****************************************************************************/

                /*
	class dng_lossless_decoder : private dng_uncopyable
	{


		private:

			dng_stream* fStream;        // Input data.

	dng_spooler* fSpooler;      // Output data.

	bool fBug16;                // Decode data with the "16-bit" bug.

	dng_memory_data huffmanBuffer[4];

	dng_memory_data compInfoBuffer;

	DecompressInfo info;

	dng_memory_data mcuBuffer1;
	dng_memory_data mcuBuffer2;
	dng_memory_data mcuBuffer3;
	dng_memory_data mcuBuffer4;

	MCU* mcuROW1;
	MCU* mcuROW2;

	uint64 getBuffer;           // current bit-extraction buffer
	int32 bitsLeft;             // # of unused bits in it

	#if qSupportHasselblad_3FR
			bool fHasselblad3FR;
	#endif

	public:

			dng_lossless_decoder(dng_stream * stream,
								  dng_spooler * spooler,
								  bool bug16);

	void StartRead(uint32 &imageWidth,
					uint32 &imageHeight,
					uint32 &imageChannels);

	void FinishRead();

	#if qSupportHasselblad_3FR

			bool IsHasselblad3FR ()
				{
				return fHasselblad3FR;
				}

	#endif

	private:

			uint8 GetJpegChar()
	{
		return fStream->Get_uint8();
	}

	void UnGetJpegChar()
	{
		fStream->SetReadPosition(fStream->Position() - 1);
	}

	uint16 Get2bytes();

	void SkipVariable();

	void GetDht();

	void GetDri();

	void GetApp0();

	void GetSof(int32 code);

	void GetSos();

	void GetSoi();

	int32 NextMarker();

	JpegMarker ProcessTables();

	void ReadFileHeader();

	int32 ReadScanHeader();

	void DecoderStructInit();

	void HuffDecoderInit();

	void ProcessRestart();

	int32 QuickPredict(int32 col,
						int32 curComp,
						MCU* curRowBuf,
						MCU* prevRowBuf);

	void FillBitBuffer(int32 nbits);

	int32 show_bits8();

	void flush_bits(int32 nbits);

	int32 get_bits(int32 nbits);

	int32 get_bit();

	int32 HuffDecode(HuffmanTable* htbl);

	void HuffExtend(int32 &x, int32 s);

	void PmPutRow(MCU* buf,
				   int32 numComp,
				   int32 numCol,
				   int32 row);

	void DecodeFirstRow(MCU* curRowBuf);

	void DecodeImage();

		};

	/*****************************************************************************/
                /*
	dng_lossless_decoder::dng_lossless_decoder(dng_stream * stream,
												dng_spooler * spooler,
												bool bug16)

		:	fStream(stream)
		,	fSpooler(spooler)
		,	fBug16(bug16)

		,	compInfoBuffer()
		,	info()
		,	mcuBuffer1()
		,	mcuBuffer2()
		,	mcuBuffer3()
		,	mcuBuffer4()
		,	mcuROW1(NULL)
		,	mcuROW2(NULL)
		,	getBuffer(0)
		,	bitsLeft(0)

		#if qSupportHasselblad_3FR
		,	fHasselblad3FR (false)
	#endif

	{

		memset(&info, 0, sizeof(info));

	}

	/*****************************************************************************/

                /*uint16 dng_lossless_decoder::Get2bytes()
				{

					uint16 a = GetJpegChar();

					return (uint16)((a << 8) + GetJpegChar());

				}

				/*****************************************************************************/

                /*
				 *--------------------------------------------------------------
				 *
				 * SkipVariable --
				 *
				 *	Skip over an unknown or uninteresting variable-length marker
				 *
				 * Results:
				 *	None.
				 *
				 * Side effects:
				 *	Bitstream is parsed over marker.
				 *
				 *
				 *--------------------------------------------------------------
				 */
                /*
				void dng_lossless_decoder::SkipVariable()
				{

					uint32 length = Get2bytes() - 2;

					fStream->Skip(length);

				}

				/*****************************************************************************/

                /*
				 *--------------------------------------------------------------
				 *
				 * GetDht --
				 *
				 *	Process a DHT marker
				 *
				 * Results:
				 *	None
				 *
				 * Side effects:
				 *	A huffman table is read.
				 *	Exits on error.
				 *
				 *--------------------------------------------------------------
				 */
                /*
				void dng_lossless_decoder::GetDht()
				{

					int32 length = Get2bytes() - 2;

					while (length > 0)
					{

						int32 index = GetJpegChar();

						if (index < 0 || index >= 4)
						{
							ThrowBadFormat();
						}

						HuffmanTable * &htblptr = info.dcHuffTblPtrs[index];

						if (htblptr == NULL)
						{

							huffmanBuffer[index].Allocate(sizeof(HuffmanTable));

							htblptr = (HuffmanTable*)huffmanBuffer[index].Buffer();

						}

						htblptr->bits[0] = 0;

						int32 count = 0;

						for (int32 i = 1; i <= 16; i++)
						{

							htblptr->bits[i] = GetJpegChar();

							count += htblptr->bits[i];

						}

						if (count > 256)
						{
							ThrowBadFormat();
						}

						for (int32 j = 0; j < count; j++)
						{

							htblptr->huffval[j] = GetJpegChar();

						}

						length -= 1 + 16 + count;

					}

				}

				/*****************************************************************************/

                /*
				 *--------------------------------------------------------------
				 *
				 * GetDri --
				 *
				 *	Process a DRI marker
				 *
				 * Results:
				 *	None
				 *
				 * Side effects:
				 *	Exits on error.
				 *	Bitstream is parsed.
				 *
				 *--------------------------------------------------------------
				 *//*

				void dng_lossless_decoder::GetDri()
				{

					if (Get2bytes() != 4)
					{
						ThrowBadFormat();
					}

					info.restartInterval = Get2bytes();

				}

				/*****************************************************************************/

                /*
				 *--------------------------------------------------------------
				 *
				 * GetApp0 --
				 *
				 *	Process an APP0 marker.
				 *
				 * Results:
				 *	None
				 *
				 * Side effects:
				 *	Bitstream is parsed
				 *
				 *--------------------------------------------------------------
				 */
                /*
				void dng_lossless_decoder::GetApp0()
				{

					SkipVariable();

				}

				/*****************************************************************************/

                /*
				 *--------------------------------------------------------------
				 *
				 * GetSof --
				 *
				 *	Process a SOFn marker
				 *
				 * Results:
				 *	None.
				 *
				 * Side effects:
				 *	Bitstream is parsed
				 *	Exits on error
				 *	info structure is filled in
				 *
				 *--------------------------------------------------------------
				 */
                /*
				void dng_lossless_decoder::GetSof(int32 /*code*//*)
				{

					int32 length = Get2bytes();

					info.dataPrecision = GetJpegChar();
					info.imageHeight = Get2bytes();
					info.imageWidth = Get2bytes();
					info.numComponents = GetJpegChar();

					// We don't support files in which the image height is initially
					// specified as 0 and is later redefined by DNL.  As long as we
					// have to check that, might as well have a general sanity check.

					if ((info.imageHeight <= 0) ||
						(info.imageWidth <= 0) ||
						(info.numComponents <= 0))
					{
						ThrowBadFormat();
					}

					// Lossless JPEG specifies data precision to be from 2 to 16 bits/sample.

					const int32 MinPrecisionBits = 2;
					const int32 MaxPrecisionBits = 16;

					if ((info.dataPrecision < MinPrecisionBits) ||
						(info.dataPrecision > MaxPrecisionBits))
					{
						ThrowBadFormat();
					}

					// Check length of tag.

					if (length != (info.numComponents * 3 + 8))
					{
						ThrowBadFormat();
					}

					// Allocate per component info.

					// We can cast info.numComponents to a uint32 because the check above
					// guarantees that it cannot be negative.

					compInfoBuffer.Allocate(static_cast<uint32>(info.numComponents),
											 sizeof(JpegComponentInfo));

					info.compInfo = (JpegComponentInfo*)compInfoBuffer.Buffer();

					// Read in the per compent info.

					for (int32 ci = 0; ci < info.numComponents; ci++)
					{

						JpegComponentInfo* compptr = &info.compInfo[ci];

						compptr->componentIndex = (int16)ci;

						compptr->componentId = GetJpegChar();

						int32 c = GetJpegChar();

						compptr->hSampFactor = (int16)((c >> 4) & 15);
						compptr->vSampFactor = (int16)((c) & 15);

						(void)GetJpegChar();   /* skip Tq */

            }

        }

        /*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * GetSos --
		 *
		 *	Process a SOS marker
		 *
		 * Results:
		 *	None.
		 *
		 * Side effects:
		 *	Bitstream is parsed.
		 *	Exits on error.
		 *
		 *--------------------------------------------------------------
		 */
        /*
		void dng_lossless_decoder::GetSos()
		{

			int32 length = Get2bytes();

			// Get the number of image components.

			int32 n = GetJpegChar();
			info.compsInScan = (int16)n;

			// Check length.

			length -= 3;

			if (length != (n * 2 + 3) || n < 1 || n > 4)
			{
				ThrowBadFormat();
			}

			// Find index and huffman table for each component.

			for (int32 i = 0; i < n; i++)
			{

				int32 cc = GetJpegChar();
				int32 c = GetJpegChar();

				int32 ci;

				for (ci = 0; ci < info.numComponents; ci++)
				{

					if (cc == info.compInfo[ci].componentId)
					{
						break;
					}

				}

				if (ci >= info.numComponents)
				{
					ThrowBadFormat();
				}

				JpegComponentInfo* compptr = &info.compInfo[ci];

				info.curCompInfo[i] = compptr;

				compptr->dcTblNo = (int16)((c >> 4) & 15);

			}

			// Get the PSV, skip Se, and get the point transform parameter.

			info.Ss = GetJpegChar();

			(void)GetJpegChar();

			info.Pt = GetJpegChar() & 0x0F;

		}

		/*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * GetSoi --
		 *
		 *	Process an SOI marker
		 *
		 * Results:
		 *	None.
		 *
		 * Side effects:
		 *	Bitstream is parsed.
		 *	Exits on error.
		 *
		 *--------------------------------------------------------------
		 */
        /*
		void dng_lossless_decoder::GetSoi()
		{

			// Reset all parameters that are defined to be reset by SOI

			info.restartInterval = 0;

		}

		/*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * NextMarker --
		 *
		 *      Find the next JPEG marker Note that the output might not
		 *	be a valid marker code but it will never be 0 or FF
		 *
		 * Results:
		 *	The marker found.
		 *
		 * Side effects:
		 *	Bitstream is parsed.
		 *
		 *--------------------------------------------------------------
		 */
        /*
		int32 dng_lossless_decoder::NextMarker()
		{

			int32 c;

			do
			{

				// skip any non-FF bytes

				do
				{
					c = GetJpegChar();
				}
				while (c != 0xFF);

				// skip any duplicate FFs, since extra FFs are legal

				do
				{
					c = GetJpegChar();
				}
				while (c == 0xFF);

			}
			while (c == 0);     // repeat if it was a stuffed FF/00

			return c;

		}

		/*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * ProcessTables --
		 *
		 *	Scan and process JPEG markers that can appear in any order
		 *	Return when an SOI, EOI, SOFn, or SOS is found
		 *
		 * Results:
		 *	The marker found.
		 *
		 * Side effects:
		 *	Bitstream is parsed.
		 *
		 *--------------------------------------------------------------
		 */
        /*
		JpegMarker dng_lossless_decoder::ProcessTables()
		{

			while (true)
			{

				int32 c = NextMarker();

				switch (c)
				{

					case M_SOF0:
					case M_SOF1:
					case M_SOF2:
					case M_SOF3:
					case M_SOF5:
					case M_SOF6:
					case M_SOF7:
					case M_JPG:
					case M_SOF9:
					case M_SOF10:
					case M_SOF11:
					case M_SOF13:
					case M_SOF14:
					case M_SOF15:
					case M_SOI:
					case M_EOI:
					case M_SOS:
						return (JpegMarker)c;

					case M_DHT:
						GetDht();
						break;

					case M_DQT:
						break;

					case M_DRI:
						GetDri();
						break;

					case M_APP0:
						GetApp0();
						break;

					case M_RST0:    // these are all parameterless
					case M_RST1:
					case M_RST2:
					case M_RST3:
					case M_RST4:
					case M_RST5:
					case M_RST6:
					case M_RST7:
					case M_TEM:
						break;

					default:        // must be DNL, DHP, EXP, APPn, JPGn, COM, or RESn
						SkipVariable();
						break;

				}

			}

			return M_ERROR;
		}

		/*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * ReadFileHeader --
		 *
		 *	Initialize and read the stream header (everything through
		 *	the SOF marker).
		 *
		 * Results:
		 *	None
		 *
		 * Side effects:
		 *	Exit on error.
		 *
		 *--------------------------------------------------------------
		 */
        /*
		void dng_lossless_decoder::ReadFileHeader()
		{

			// Demand an SOI marker at the start of the stream --- otherwise it's
			// probably not a JPEG stream at all.

			int32 c = GetJpegChar();
			int32 c2 = GetJpegChar();

			if ((c != 0xFF) || (c2 != M_SOI))
			{
				ThrowBadFormat();
			}

			// OK, process SOI

			GetSoi();

			// Process markers until SOF

			c = ProcessTables();

			switch (c)
			{

				case M_SOF0:
				case M_SOF1:
				case M_SOF3:
					GetSof(c);
					break;

				default:
					ThrowBadFormat();
					break;

			}

		}

		/*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * ReadScanHeader --
		 *
		 *	Read the start of a scan (everything through the SOS marker).
		 *
		 * Results:
		 *	1 if find SOS, 0 if find EOI
		 *
		 * Side effects:
		 *	Bitstream is parsed, may exit on errors.
		 *
		 *--------------------------------------------------------------
		 */
        /*
		int32 dng_lossless_decoder::ReadScanHeader()
		{

			// Process markers until SOS or EOI

			int32 c = ProcessTables();

			switch (c)
			{

				case M_SOS:
					GetSos();
					return 1;

				case M_EOI:
					return 0;

				default:
					ThrowBadFormat();
					break;

			}

			return 0;

		}

		/*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * DecoderStructInit --
		 *
		 *	Initalize the rest of the fields in the decompression
		 *	structure.
		 *
		 * Results:
		 *	None.
		 *
		 * Side effects:
		 *	None.
		 *
		 *--------------------------------------------------------------
		 */
        /*
		void dng_lossless_decoder::DecoderStructInit()
		{

			int32 ci;

		#if qSupportCanon_sRAW

			bool canon_sRAW = (info.numComponents == 3) &&
							  (info.compInfo [0].hSampFactor == 2) &&
							  (info.compInfo [1].hSampFactor == 1) &&
							  (info.compInfo [2].hSampFactor == 1) &&
							  (info.compInfo [0].vSampFactor == 1) &&
							  (info.compInfo [1].vSampFactor == 1) &&
							  (info.compInfo [2].vSampFactor == 1) &&
							  (info.dataPrecision == 15) &&
							  (info.Ss == 1) &&
							  ((info.imageWidth & 1) == 0);

			bool canon_sRAW2 = (info.numComponents == 3) &&
							   (info.compInfo [0].hSampFactor == 2) &&
							   (info.compInfo [1].hSampFactor == 1) &&
							   (info.compInfo [2].hSampFactor == 1) &&
							   (info.compInfo [0].vSampFactor == 2) &&
							   (info.compInfo [1].vSampFactor == 1) &&
							   (info.compInfo [2].vSampFactor == 1) &&
							   (info.dataPrecision == 15) &&
							   (info.Ss == 1) &&
							   ((info.imageWidth  & 1) == 0) &&
							   ((info.imageHeight & 1) == 0);

			if (!canon_sRAW && !canon_sRAW2)

		#endif

			{

				// Check sampling factor validity.

				for (ci = 0; ci < info.numComponents; ci++)
				{

					JpegComponentInfo* compPtr = &info.compInfo[ci];

					if (compPtr->hSampFactor != 1 ||
						compPtr->vSampFactor != 1)
					{
						ThrowBadFormat();
					}

				}

			}

			// Prepare array describing MCU composition.

			if (info.compsInScan < 0 ||
				info.compsInScan > 4)
			{
				ThrowBadFormat();
			}

			for (ci = 0; ci < info.compsInScan; ci++)
			{
				info.MCUmembership[ci] = (int16)ci;
			}

			// Initialize mucROW1 and mcuROW2 which buffer two rows of
			// pixels for predictor calculation.

			// This multiplication cannot overflow because info.compsInScan is
			// guaranteed to be between 0 and 4 inclusive (see checks above).

			int32 mcuSize = info.compsInScan * (uint32)sizeof(ComponentType);

			mcuBuffer1.Allocate(info.imageWidth, sizeof(MCU));
			mcuBuffer2.Allocate(info.imageWidth, sizeof(MCU));

			mcuROW1 = (MCU*)mcuBuffer1.Buffer();
			mcuROW2 = (MCU*)mcuBuffer2.Buffer();

			mcuBuffer3.Allocate(info.imageWidth, mcuSize);
			mcuBuffer4.Allocate(info.imageWidth, mcuSize);

			mcuROW1[0] = (ComponentType*)mcuBuffer3.Buffer();
			mcuROW2[0] = (ComponentType*)mcuBuffer4.Buffer();

			for (int32 j = 1; j < info.imageWidth; j++)
			{

				mcuROW1[j] = mcuROW1[j - 1] + info.compsInScan;
				mcuROW2[j] = mcuROW2[j - 1] + info.compsInScan;

			}

		}

		/*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * HuffDecoderInit --
		 *
		 *	Initialize for a Huffman-compressed scan.
		 *	This is invoked after reading the SOS marker.
		 *
		 * Results:
		 *	None
		 *
		 * Side effects:
		 *	None.
		 *
		 *--------------------------------------------------------------
		 */
        /*
		void dng_lossless_decoder::HuffDecoderInit()
		{

			// Initialize bit parser state

			getBuffer = 0;
			bitsLeft = 0;

			// Prepare Huffman tables.

			for (int16 ci = 0; ci < info.compsInScan; ci++)
			{

				JpegComponentInfo* compptr = info.curCompInfo[ci];

				// Make sure requested tables are present

				if (compptr->dcTblNo < 0 || compptr->dcTblNo > 3)
				{
					ThrowBadFormat();
				}

				if (info.dcHuffTblPtrs[compptr->dcTblNo] == NULL)
				{
					ThrowBadFormat();
				}

				// Compute derived values for Huffman tables.
				// We may do this more than once for same table, but it's not a
				// big deal

				FixHuffTbl(info.dcHuffTblPtrs[compptr->dcTblNo]);

			}

			// Initialize restart stuff

			info.restartInRows = info.restartInterval / info.imageWidth;
			info.restartRowsToGo = info.restartInRows;
			info.nextRestartNum = 0;

		}

		/*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * ProcessRestart --
		 *
		 *	Check for a restart marker & resynchronize decoder.
		 *
		 * Results:
		 *	None.
		 *
		 * Side effects:
		 *	BitStream is parsed, bit buffer is reset, etc.
		 *
		 *--------------------------------------------------------------
		 */
        /*
		void dng_lossless_decoder::ProcessRestart()
		{

			// Throw away and unused odd bits in the bit buffer.

			fStream->SetReadPosition(fStream->Position() - bitsLeft / 8);

			bitsLeft = 0;
			getBuffer = 0;

			// Scan for next JPEG marker

			int32 c;

			do
			{

				// skip any non-FF bytes

				do
				{
					c = GetJpegChar();
				}
				while (c != 0xFF);

				// skip any duplicate FFs

				do
				{
					c = GetJpegChar();
				}
				while (c == 0xFF);

			}
			while (c == 0);     // repeat if it was a stuffed FF/00

			// Verify correct restart code.

			if (c != (M_RST0 + info.nextRestartNum))
			{
				ThrowBadFormat();
			}

			// Update restart state.

			info.restartRowsToGo = info.restartInRows;
			info.nextRestartNum = (info.nextRestartNum + 1) & 7;

		}

		/*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * QuickPredict --
		 *
		 *      Calculate the predictor for sample curRowBuf[col][curComp].
		 *	It does not handle the special cases at image edges, such 
		 *      as first row and first column of a scan. We put the special 
		 *	case checkings outside so that the computations in main
		 *	loop can be simpler. This has enhenced the performance
		 *	significantly.
		 *
		 * Results:
		 *      predictor is passed out.
		 *
		 * Side effects:
		 *      None.
		 *
		 *--------------------------------------------------------------
		 */
        /*
		inline int32 dng_lossless_decoder::QuickPredict (int32 col,
														  int32 curComp,
														  MCU *curRowBuf,
														  MCU *prevRowBuf)
			{

			int32 diag = prevRowBuf[col - 1][curComp];
			int32 upper = prevRowBuf[col][curComp];
			int32 left = curRowBuf[col - 1][curComp];

			switch (info.Ss)
			{

				case 0:
					return 0;

				case 1:
					return left;

				case 2:
					return upper;

				case 3:
					return diag;

				case 4:
					return left + upper - diag;

				case 5:
					return left + ((upper - diag) >> 1);

				case 6:
					return upper + ((left - diag) >> 1);

				case 7:
					return (left + upper) >> 1;

				default:
					{
						ThrowBadFormat();
						return 0;
					}

			}

		}

		/*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * FillBitBuffer --
		 *
		 *	Load up the bit buffer with at least nbits
		 *	Process any stuffed bytes at this time.
		 *
		 * Results:
		 *	None
		 *
		 * Side effects:
		 *	The bitwise global variables are updated.
		 *
		 *--------------------------------------------------------------
		 */
        /*
		inline void dng_lossless_decoder::FillBitBuffer(int32 nbits)
		{

			const int32 kMinGetBits = sizeof(uint32) * 8 - 7;

		#if qSupportHasselblad_3FR

			if (fHasselblad3FR)
				{

				while (bitsLeft < kMinGetBits)
					{

					int32 c0 = 0;
					int32 c1 = 0;
					int32 c2 = 0;
					int32 c3 = 0;

					try
						{
						c0 = GetJpegChar ();
						c1 = GetJpegChar ();
						c2 = GetJpegChar ();
						c3 = GetJpegChar ();
						}

					catch (dng_exception &except)
						{

						// If we got any exception other than EOF, rethrow.

						if (except.ErrorCode () != dng_error_end_of_file)
							{
							throw except;
							}

						// Some Hasselblad files now use the JPEG end of image marker.
						// If we DIDN'T hit that, rethrow.
						// This sequence also sometimes occurs in the image data, so
						// we can't simply check for it and exit - we need to wait until
						// we throw the EOF and then look to see if we had it.

						// Look for the marker in c1 and c2 as well.
						// (if we get it in c2 and c3, we won't throw.)

						if (!((c0 == 0xFF && c1 == 0xD9) ||
							  (c1 == 0xFF && c2 == 0xD9)))
							{
							throw except;
							}

						// Swallow the case where we hit EOF with the JPEG EOI marker.

						}

					getBuffer = (getBuffer << 8) | c3;
					getBuffer = (getBuffer << 8) | c2;
					getBuffer = (getBuffer << 8) | c1;
					getBuffer = (getBuffer << 8) | c0;

					bitsLeft += 32;

					}

				return;

				}

		#endif

			while (bitsLeft < kMinGetBits)
			{

				int32 c = GetJpegChar();

				// If it's 0xFF, check and discard stuffed zero byte

				if (c == 0xFF)
				{

					int32 c2 = GetJpegChar();

					if (c2 != 0)
					{

						// Oops, it's actually a marker indicating end of
						// compressed data.  Better put it back for use later.

						UnGetJpegChar();
						UnGetJpegChar();

						// There should be enough bits still left in the data
						// segment; if so, just break out of the while loop.

						if (bitsLeft >= nbits)
							break;

						// Uh-oh.  Corrupted data: stuff zeroes into the data
						// stream, since this sometimes occurs when we are on the
						// last show_bits8 during decoding of the Huffman
						// segment.

						c = 0;

					}

				}

				getBuffer = (getBuffer << 8) | c;

				bitsLeft += 8;

			}

		}

		/*****************************************************************************/
        /*
		inline int32 dng_lossless_decoder::show_bits8 ()
			{

			if (bitsLeft < 8)
				FillBitBuffer(8);

			return (int32)((getBuffer >> (bitsLeft - 8)) & 0xff);

		}

		/*****************************************************************************/
        /*
		inline void dng_lossless_decoder::flush_bits(int32 nbits)
		{

			bitsLeft -= nbits;

		}

		/*****************************************************************************/
        /*
		inline int32 dng_lossless_decoder::get_bits (int32 nbits)
			{

			if (nbits > 16)
			{
				ThrowBadFormat();
			}

			if (bitsLeft < nbits)
				FillBitBuffer(nbits);

			return (int32)((getBuffer >> (bitsLeft -= nbits)) & (0x0FFFF >> (16 - nbits)));

		}

		/*****************************************************************************/
        /*
		inline int32 dng_lossless_decoder::get_bit ()
			{

			if (!bitsLeft)
				FillBitBuffer(1);

			return (int32)((getBuffer >> (--bitsLeft)) & 1);

		}

		/*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * HuffDecode --
		 *
		 *	Taken from Figure F.16: extract next coded symbol from
		 *	input stream.  This should becode a macro.
		 *
		 * Results:
		 *	Next coded symbol
		 *
		 * Side effects:
		 *	Bitstream is parsed.
		 *
		 *--------------------------------------------------------------
		 */
        /*
		inline int32 dng_lossless_decoder::HuffDecode (HuffmanTable *htbl)
			{

			// If the huffman code is less than 8 bits, we can use the fast
			// table lookup to get its value.  It's more than 8 bits about
			// 3-4% of the time.

			int32 code = show_bits8();

			if (htbl->numbits[code])
			{

				flush_bits(htbl->numbits[code]);

				return htbl->value[code];

			}

			else
			{

				flush_bits(8);

				int32 l = 8;

				while (code > htbl->maxcode[l])
				{
					code = (code << 1) | get_bit();
					l++;
				}

				// With garbage input we may reach the sentinel value l = 17.

				if (l > 16)
				{
					return 0;       // fake a zero as the safest result
				}
				else
				{
					return htbl->huffval[htbl->valptr[l] +
										  ((int32)(code - htbl->mincode[l]))];
				}

			}

		}

		/*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * HuffExtend --
		 *
		 *	Code and table for Figure F.12: extend sign bit
		 *
		 * Results:
		 *	The extended value.
		 *
		 * Side effects:
		 *	None.
		 *
		 *--------------------------------------------------------------
		 */
        //DNG_ATTRIB_NO_SANITIZE("undefined")
/*
inline void dng_lossless_decoder::HuffExtend(int32 &x, int32 s)
{

	if (x < (0x08000 >> (16 - s)))
	{
		x += (-1 << s) + 1;
	}

}

/*****************************************************************************/

        // Called from DecodeImage () to write one row.

        /*
		void dng_lossless_decoder::PmPutRow(MCU* buf,
											 int32 numComp,
											 int32 numCol,
											 int32 /* row */
        /*)
		{

			uint16* sPtr = &buf[0][0];

			uint32 pixels = numCol * numComp;

			fSpooler->Spool(sPtr, pixels * (uint32)sizeof(uint16));

		}

		/*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * DecodeFirstRow --
		 *
		 *	Decode the first raster line of samples at the start of 
		 *      the scan and at the beginning of each restart interval.
		 *	This includes modifying the component value so the real
		 *      value, not the difference is returned.
		 *
		 * Results:
		 *	None.
		 *
		 * Side effects:
		 *	Bitstream is parsed.
		 *
		 *--------------------------------------------------------------
		 */

        /*
		void dng_lossless_decoder::DecodeFirstRow(MCU* curRowBuf)
		{

			int32 compsInScan = info.compsInScan;

			// Process the first column in the row.

			for (int32 curComp = 0; curComp < compsInScan; curComp++)
			{

				int32 ci = info.MCUmembership[curComp];

				JpegComponentInfo* compptr = info.curCompInfo[ci];

				HuffmanTable* dctbl = info.dcHuffTblPtrs[compptr->dcTblNo];

				// Section F.2.2.1: decode the difference

				int32 d = 0;

				int32 s = HuffDecode(dctbl);

				if (s)
				{

					if (s == 16 && !fBug16)
					{
						d = -32768;
					}

					else
					{
						d = get_bits(s);
						HuffExtend(d, s);
					}

				}

				// Add the predictor to the difference.

				int32 Pr = info.dataPrecision;
				int32 Pt = info.Pt;

				curRowBuf[0][curComp] = (ComponentType)(d + (1 << (Pr - Pt - 1)));

			}

			// Process the rest of the row.

			int32 numCOL = info.imageWidth;

			for (int32 col = 1; col < numCOL; col++)
			{

				for (int32 curComp = 0; curComp < compsInScan; curComp++)
				{

					int32 ci = info.MCUmembership[curComp];

					JpegComponentInfo* compptr = info.curCompInfo[ci];

					HuffmanTable* dctbl = info.dcHuffTblPtrs[compptr->dcTblNo];

					// Section F.2.2.1: decode the difference

					int32 d = 0;

					int32 s = HuffDecode(dctbl);

					if (s)
					{

						if (s == 16 && !fBug16)
						{
							d = -32768;
						}

						else
						{
							d = get_bits(s);
							HuffExtend(d, s);
						}

					}

					// Add the predictor to the difference.

					curRowBuf[col][curComp] = (ComponentType)(d + curRowBuf[col - 1][curComp]);

				}

			}

			// Update the restart counter

			if (info.restartInRows)
			{
				info.restartRowsToGo--;
			}

		}

		/*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * DecodeImage --
		 *
		 *      Decode the input stream. This includes modifying
		 *      the component value so the real value, not the
		 *      difference is returned.
		 *
		 * Results:
		 *      None.
		 *
		 * Side effects:
		 *      Bitstream is parsed.
		 *
		 *--------------------------------------------------------------
		 */

        /*
		void dng_lossless_decoder::DecodeImage()
		{

		#define swap(type,a,b) {type c; c=(a); (a)=(b); (b)=c;}

			int32 numCOL = info.imageWidth;
			int32 numROW = info.imageHeight;
			int32 compsInScan = info.compsInScan;

			// Precompute the decoding table for each table.

			HuffmanTable* ht[4];

			memset(ht, 0, sizeof(ht));

			for (int32 curComp = 0; curComp < compsInScan; curComp++)
			{

				int32 ci = info.MCUmembership[curComp];

				JpegComponentInfo* compptr = info.curCompInfo[ci];

				ht[curComp] = info.dcHuffTblPtrs[compptr->dcTblNo];

			}

			MCU* prevRowBuf = mcuROW1;
			MCU* curRowBuf = mcuROW2;

		#if qSupportCanon_sRAW

			// Canon sRAW support

			if (info.compInfo [0].hSampFactor == 2 &&
				info.compInfo [0].vSampFactor == 1)
				{

				for (int32 row = 0; row < numROW; row++)
					{

					// Initialize predictors.

					int32 p0;
					int32 p1;
					int32 p2;

					if (row == 0)
						{
						p0 = 1 << 14;
						p1 = 1 << 14;
						p2 = 1 << 14;
						}

					else
						{
						p0 = prevRowBuf [0] [0];
						p1 = prevRowBuf [0] [1];
						p2 = prevRowBuf [0] [2];
						}

					for (int32 col = 0; col < numCOL; col += 2)
						{

						// Read first luminance component.

							{

							int32 d = 0;

							int32 s = HuffDecode (ht [0]);

							if (s)
								{

								if (s == 16)
									{
									d = -32768;
									}

								else
									{
									d = get_bits (s);
									HuffExtend (d, s);
									}

								}

							p0 += d;

							curRowBuf [col] [0] = (ComponentType) p0;

							}

						// Read second luminance component.

							{

							int32 d = 0;

							int32 s = HuffDecode (ht [0]);

							if (s)
								{

								if (s == 16)
									{
									d = -32768;
									}

								else
									{
									d = get_bits (s);
									HuffExtend (d, s);
									}

								}

							p0 += d;

							curRowBuf [col + 1] [0] = (ComponentType) p0;

							}

						// Read first chroma component.

							{

							int32 d = 0;

							int32 s = HuffDecode (ht [1]);

							if (s)
								{

								if (s == 16)
									{
									d = -32768;
									}

								else
									{
									d = get_bits (s);
									HuffExtend (d, s);
									}

								}

							p1 += d;

							curRowBuf [col    ] [1] = (ComponentType) p1;
							curRowBuf [col + 1] [1] = (ComponentType) p1;

							}

						// Read second chroma component.

							{

							int32 d = 0;

							int32 s = HuffDecode (ht [2]);

							if (s)
								{

								if (s == 16)
									{
									d = -32768;
									}

								else
									{
									d = get_bits (s);
									HuffExtend (d, s);
									}

								}

							p2 += d;

							curRowBuf [col    ] [2] = (ComponentType) p2;
							curRowBuf [col + 1] [2] = (ComponentType) p2;

							}

						}

					PmPutRow (curRowBuf, compsInScan, numCOL, row);

					swap (MCU *, prevRowBuf, curRowBuf);

					}

				return;

				}

			if (info.compInfo [0].hSampFactor == 2 &&
				info.compInfo [0].vSampFactor == 2)
				{

				for (int32 row = 0; row < numROW; row += 2)
					{

					// Initialize predictors.

					int32 p0;
					int32 p1;
					int32 p2;

					if (row == 0)
						{
						p0 = 1 << 14;
						p1 = 1 << 14;
						p2 = 1 << 14;
						}

					else
						{
						p0 = prevRowBuf [0] [0];
						p1 = prevRowBuf [0] [1];
						p2 = prevRowBuf [0] [2];
						}

					for (int32 col = 0; col < numCOL; col += 2)
						{

						// Read first luminance component.

							{

							int32 d = 0;

							int32 s = HuffDecode (ht [0]);

							if (s)
								{

								if (s == 16)
									{
									d = -32768;
									}

								else
									{
									d = get_bits (s);
									HuffExtend (d, s);
									}

								}

							p0 += d;

							prevRowBuf [col] [0] = (ComponentType) p0;

							}

						// Read second luminance component.

							{

							int32 d = 0;

							int32 s = HuffDecode (ht [0]);

							if (s)
								{

								if (s == 16)
									{
									d = -32768;
									}

								else
									{
									d = get_bits (s);
									HuffExtend (d, s);
									}

								}

							p0 += d;

							prevRowBuf [col + 1] [0] = (ComponentType) p0;

							}

						// Read third luminance component.

							{

							int32 d = 0;

							int32 s = HuffDecode (ht [0]);

							if (s)
								{

								if (s == 16)
									{
									d = -32768;
									}

								else
									{
									d = get_bits (s);
									HuffExtend (d, s);
									}

								}

							p0 += d;

							curRowBuf [col] [0] = (ComponentType) p0;

							}

						// Read fourth luminance component.

							{

							int32 d = 0;

							int32 s = HuffDecode (ht [0]);

							if (s)
								{

								if (s == 16)
									{
									d = -32768;
									}

								else
									{
									d = get_bits (s);
									HuffExtend (d, s);
									}

								}

							p0 += d;

							curRowBuf [col + 1] [0] = (ComponentType) p0;

							}

						// Read first chroma component.

							{

							int32 d = 0;

							int32 s = HuffDecode (ht [1]);

							if (s)
								{

								if (s == 16)
									{
									d = -32768;
									}

								else
									{
									d = get_bits (s);
									HuffExtend (d, s);
									}

								}

							p1 += d;

							prevRowBuf [col    ] [1] = (ComponentType) p1;
							prevRowBuf [col + 1] [1] = (ComponentType) p1;

							curRowBuf [col    ] [1] = (ComponentType) p1;
							curRowBuf [col + 1] [1] = (ComponentType) p1;

							}

						// Read second chroma component.

							{

							int32 d = 0;

							int32 s = HuffDecode (ht [2]);

							if (s)
								{

								if (s == 16)
									{
									d = -32768;
									}

								else
									{
									d = get_bits (s);
									HuffExtend (d, s);
									}

								}

							p2 += d;

							prevRowBuf [col    ] [2] = (ComponentType) p2;
							prevRowBuf [col + 1] [2] = (ComponentType) p2;

							curRowBuf [col    ] [2] = (ComponentType) p2;
							curRowBuf [col + 1] [2] = (ComponentType) p2;

							}

						}

					PmPutRow (prevRowBuf, compsInScan, numCOL, row);
					PmPutRow (curRowBuf, compsInScan, numCOL, row);

					}

				return;

				}

		#endif

		#if qSupportHasselblad_3FR

			if (info.Ss == 8 && (numCOL & 1) == 0)
				{

				fHasselblad3FR = true;

				for (int32 row = 0; row < numROW; row++)
					{

					int32 p0 = 32768;
					int32 p1 = 32768;

					for (int32 col = 0; col < numCOL; col += 2)
						{

						int32 s0 = HuffDecode (ht [0]);
						int32 s1 = HuffDecode (ht [0]);

						if (s0)
							{
							int32 d = get_bits (s0);
							if (s0 == 16)
								{
								d = -32768;
								}
							else
								{
								HuffExtend (d, s0);
								}
							p0 += d;
							}

						if (s1)
							{
							int32 d = get_bits (s1);
							if (s1 == 16)
								{
								d = -32768;
								}
							else
								{
								HuffExtend (d, s1);
								}
							p1 += d;
							}

						curRowBuf [col    ] [0] = (ComponentType) p0;
						curRowBuf [col + 1] [0] = (ComponentType) p1;

						}

					PmPutRow (curRowBuf, compsInScan, numCOL, row);

					}

				return;

				}

		#endif

			// Decode the first row of image. Output the row and
			// turn this row into a previous row for later predictor
			// calculation.

			DecodeFirstRow(mcuROW1);

			PmPutRow(mcuROW1, compsInScan, numCOL, 0);

			// Process each row.

			for (int32 row = 1; row < numROW; row++)
			{

				// Account for restart interval, process restart marker if needed.

				if (info.restartInRows)
				{

					if (info.restartRowsToGo == 0)
					{

						ProcessRestart();

						// Reset predictors at restart.

						DecodeFirstRow(curRowBuf);

						PmPutRow(curRowBuf, compsInScan, numCOL, row);

						swap(MCU *, prevRowBuf, curRowBuf);

						continue;

					}

					info.restartRowsToGo--;

				}

				// The upper neighbors are predictors for the first column.

				for (int32 curComp = 0; curComp < compsInScan; curComp++)
				{

					// Section F.2.2.1: decode the difference

					int32 d = 0;

					int32 s = HuffDecode(ht[curComp]);

					if (s)
					{

						if (s == 16 && !fBug16)
						{
							d = -32768;
						}

						else
						{
							d = get_bits(s);
							HuffExtend(d, s);
						}

					}

					// First column of row above is predictor for first column.

					curRowBuf[0][curComp] = (ComponentType)(d + prevRowBuf[0][curComp]);

				}

				// For the rest of the column on this row, predictor
				// calculations are based on PSV. 

				if (compsInScan == 2 && info.Ss == 1 && numCOL > 1)
				{

					// This is the combination used by both the Canon and Kodak raw formats. 
					// Unrolling the general case logic results in a significant speed increase.

					uint16* dPtr = &curRowBuf[1][0];

					int32 prev0 = dPtr[-2];
					int32 prev1 = dPtr[-1];

					for (int32 col = 1; col < numCOL; col++)
					{

						int32 s = HuffDecode(ht[0]);

						if (s)
						{

							int32 d;

							if (s == 16 && !fBug16)
							{
								d = -32768;
							}

							else
							{
								d = get_bits(s);
								HuffExtend(d, s);
							}

							prev0 += d;

						}

						s = HuffDecode(ht[1]);

						if (s)
						{

							int32 d;

							if (s == 16 && !fBug16)
							{
								d = -32768;
							}

							else
							{
								d = get_bits(s);
								HuffExtend(d, s);
							}

							prev1 += d;

						}

						dPtr[0] = (uint16)prev0;
						dPtr[1] = (uint16)prev1;

						dPtr += 2;

					}

				}

				else
				{

					for (int32 col = 1; col < numCOL; col++)
					{

						for (int32 curComp = 0; curComp < compsInScan; curComp++)
						{

							// Section F.2.2.1: decode the difference

							int32 d = 0;

							int32 s = HuffDecode(ht[curComp]);

							if (s)
							{

								if (s == 16 && !fBug16)
								{
									d = -32768;
								}

								else
								{
									d = get_bits(s);
									HuffExtend(d, s);
								}

							}

							// Predict the pixel value.

							int32 predictor = QuickPredict(col,
															curComp,
															curRowBuf,
															prevRowBuf);

							// Save the difference.

							curRowBuf[col][curComp] = (ComponentType)(d + predictor);

						}

					}

				}

				PmPutRow(curRowBuf, compsInScan, numCOL, row);

				swap(MCU *, prevRowBuf, curRowBuf);

			}

		#undef swap

		}

		/*****************************************************************************/

        /*
		void dng_lossless_decoder::StartRead(uint32 &imageWidth,
											  uint32 &imageHeight,
											  uint32 &imageChannels)
		{

			ReadFileHeader();
			ReadScanHeader();
			DecoderStructInit();
			HuffDecoderInit();

			imageWidth = info.imageWidth;
			imageHeight = info.imageHeight;
			imageChannels = info.compsInScan;

		}

		/*****************************************************************************/

        /*
		void dng_lossless_decoder::FinishRead()
		{

			DecodeImage();

		}

		/*****************************************************************************/

        /*
		void DecodeLosslessJPEG(dng_stream &stream,
								 dng_spooler &spooler,
								 uint32 minDecodedSize,
								 uint32 maxDecodedSize,
								 bool bug16,
								 uint64 endOfData)
		{

			dng_lossless_decoder decoder(&stream,
										  &spooler,
										  bug16);

		uint32 imageWidth;
		uint32 imageHeight;
		uint32 imageChannels;

		decoder.StartRead(imageWidth,
						   imageHeight,
						   imageChannels);

		uint32 decodedSize = imageWidth *
							 imageHeight *
							 imageChannels *
							 (uint32)sizeof(uint16);

		if (decodedSize < minDecodedSize ||
			decodedSize > maxDecodedSize)
		{
			ThrowBadFormat();
		}

		decoder.FinishRead();

		uint64 streamPos = stream.Position();

		if (streamPos > endOfData)
		{

			bool throwBadFormat = true;

			// Per Hasselblad's request:
			// If we have a Hassy file with exactly four extra bytes,
			// let it through; the file is likely still valid.

		#if qSupportHasselblad_3FR

				if (decoder.IsHasselblad3FR () &&
					streamPos - endOfData == 4)
					{
					throwBadFormat = false;
					}

		#endif

			if (throwBadFormat)
			{
				ThrowBadFormat();
			}
		}

			}

		/*****************************************************************************/


            private void DNG_ASSERT(bool expression, string errorMessage)
        {
            if (!expression) throw new Exception(errorMessage);
            //#define DNG_ASSERT(x,y) { if (!(x)) dng_show_message (y); }
        }

        private uint16[] fSrcData;

        private uint32 fSrcRows;
        private uint32 fSrcCols;
        private uint32 fSrcChannels;
        private uint32 fSrcBitDepth;

        private int32 fSrcRowStep;
        private int32 fSrcColStep;

        private dng_stream fStream;

		private HuffmanTable[] huffTable = new HuffmanTable[4];

        //private uint32 freqCount[4] [257];
        uint32[][] freqCount = new uint32[4][] {
            new uint32[257],
            new uint32[257],
            new uint32[257],
            new uint32[257]
        };


        // Current bit-accumulation buffer

        private int32 huffPutBuffer;
        private int32 huffPutBits;

        // Lookup table for number of bits in an 8 bit value.

        private int[] numBitsTable = new int[256];
        /*
            public:

                dng_lossless_encoder(const uint16* srcData,
                                      uint32 srcRows,
                                      uint32 srcCols,
                                       uint32 srcChannels,
                                      uint32 srcBitDepth,
                                       int32 srcRowStep,
                                      int32 srcColStep,
                                       dng_stream &stream);

                void Encode();

            private:

                void EmitByte(uint8 value);

            void EmitBits(int code, int size);

            void FlushBits();

            void CountOneDiff(int diff, uint32* countTable);

            void EncodeOneDiff(int diff, HuffmanTable* dctbl);

            void FreqCountSet();

            void HuffEncode();

            void GenHuffCoding(HuffmanTable* htbl, uint32* freq);

            void HuffOptimize();

            void EmitMarker(JpegMarker mark);

            void Emit2bytes(int value);

            void EmitDht(int index);

            void EmitSof(JpegMarker code);

            void EmitSos();

            void WriteFileHeader();

            void WriteScanHeader();

            void WriteFileTrailer();

        };

        /*****************************************************************************/

        public DNGLosslessEncoder(uint16[] srcData,
                                                   uint32 srcRows,
                                                   uint32 srcCols,
                                                   uint32 srcChannels,
                                                   uint32 srcBitDepth,
                                                   int32 srcRowStep,
                                                   int32 srcColStep,
                                                   dng_stream stream)




        {
            fSrcData = srcData;
            fSrcRows = srcRows;
            fSrcCols = srcCols;
            fSrcChannels = srcChannels;
            fSrcBitDepth = srcBitDepth;
            fSrcRowStep = srcRowStep;
            fSrcColStep = srcColStep;
            fStream = stream;

            huffPutBuffer = 0;
            huffPutBits = 0;


            // Initialize number of bits lookup table.

            numBitsTable[0] = 0;

            for (int i = 1; i < 256; i++)
            {

                int temp = i;
                int nbits = 1;

                while ((temp >>= 1) != 0)
                {
                    nbits++;
                }

                numBitsTable[i] = nbits;

            }

        }

        /*****************************************************************************/
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EmitByte(uint8 value)
        {

            fStream.Put_uint8(value);

        }

        /*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * EmitBits --
		 *
		 *	Code for outputting bits to the file
		 *
		 *	Only the right 24 bits of huffPutBuffer are used; the valid
		 *	bits are left-justified in this part.  At most 16 bits can be
		 *	passed to EmitBits in one call, and we never retain more than 7
		 *	bits in huffPutBuffer between calls, so 24 bits are
		 *	sufficient.
		 *
		 * Results:
		 *	None.
		 *
		 * Side effects:
		 *	huffPutBuffer and huffPutBits are updated.
		 *
		 *--------------------------------------------------------------
		 */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EmitBits(int code, int size)
        {

            DNG_ASSERT(size != 0, "Bad Huffman table entry");

            int putBits = size;
            int putBuffer = code;

            putBits += huffPutBits;

            putBuffer <<= 24 - putBits;
            putBuffer |= huffPutBuffer;

            while (putBits >= 8)
            {

                uint8 c = (uint8)(putBuffer >> 16);

                // Output whole bytes we've accumulated with byte stuffing

                EmitByte(c);

                if (c == 0xFF)
                {
                    EmitByte(0);
                }

                putBuffer <<= 8;
                putBits -= 8;

            }

            huffPutBuffer = putBuffer;
            huffPutBits = putBits;

        }

        /*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * FlushBits --
		 *
		 *	Flush any remaining bits in the bit buffer. Used before emitting
		 *	a marker.
		 *
		 * Results:
		 *	None.
		 *
		 * Side effects:
		 *	huffPutBuffer and huffPutBits are reset
		 *
		 *--------------------------------------------------------------
		 */

        public void FlushBits()
        {

            // The first call forces output of any partial bytes.

            EmitBits(0x007F, 7);

            // We can then zero the buffer.

            huffPutBuffer = 0;
            huffPutBits = 0;

        }

        /*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * CountOneDiff --
		 *
		 *      Count the difference value in countTable.
		 *
		 * Results:
		 *      diff is counted in countTable.
		 *
		 * Side effects:
		 *      None. 
		 *
		 *--------------------------------------------------------------
		 */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CountOneDiff(int diff, uint32[] countTable)
        {

            // Encode the DC coefficient difference per section F.1.2.1

            int temp = diff;

            if (temp < 0)
            {

                temp = -temp;

            }

            // Find the number of bits needed for the magnitude of the coefficient

            int nbits = temp >= 256 ? numBitsTable[temp >> 8] + 8
                                    : numBitsTable[temp & 0xFF];

            // Update count for this bit length

            countTable[nbits]++;

        }

        /*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * EncodeOneDiff --
		 *
		 *	Encode a single difference value.
		 *
		 * Results:
		 *	None.
		 *
		 * Side effects:
		 *	None.
		 *
		 *--------------------------------------------------------------
		 */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EncodeOneDiff(int diff, ref HuffmanTable dctbl)
        {

            // Encode the DC coefficient difference per section F.1.2.1

            int temp = diff;
            int temp2 = diff;

            if (temp < 0)
            {

                temp = -temp;

                // For a negative input, want temp2 = bitwise complement of
                // abs (input).  This code assumes we are on a two's complement
                // machine.

                temp2--;

            }

            // Find the number of bits needed for the magnitude of the coefficient

            int nbits = temp >= 256 ? numBitsTable[temp >> 8] + 8
                                    : numBitsTable[temp & 0xFF];

            // Emit the Huffman-coded symbol for the number of bits

            EmitBits(dctbl.ehufco[nbits],
                      dctbl.ehufsi[nbits]);

            // Emit that number of bits of the value, if positive,
            // or the complement of its magnitude, if negative.

            // If the number of bits is 16, there is only one possible difference
            // value (-32786), so the lossless JPEG spec says not to output anything
            // in that case.  So we only need to output the diference value if
            // the number of bits is between 1 and 15.

            if ((nbits & 15) != 0)
            {

                EmitBits(temp2 & (0x0FFFF >> (16 - nbits)),
                          nbits);

            }

        }

        /*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * FreqCountSet --
		 *
		 *      Count the times each category symbol occurs in this image.
		 *
		 * Results:
		 *	None.
		 *
		 * Side effects:
		 *	The freqCount has counted all category 
		 *	symbols appeared in the image.        
		 *
		 *--------------------------------------------------------------
		 */

        public void FreqCountSet()
        {

            uint32[][] freqCount = new uint32[4][] {
                new uint32[257],
                new uint32[257],
                new uint32[257],
                new uint32[257]
            };
            //freqCount = new uint32[4, 257];

            //memset(freqCount, 0, sizeof(freqCount));

            DNG_ASSERT((int32)fSrcRows >= 0, "dng_lossless_encoder::FreqCountSet: fSrcRpws too large.");

            int offset;

            for (int32 row = 0; row < (int32)fSrcRows; row++)
            {

                //const uint16* sPtr = fSrcData + row * fSrcRowStep;
                offset = row * fSrcRowStep;

                // Initialize predictors for this row.

                int32[] predictor = { 0, 0, 0, 0 };

                for (int32 channel = 0; channel < (int32)fSrcChannels; channel++)
                {

                    if (row == 0)
                        predictor[channel] = 1 << ((int)fSrcBitDepth - 1);

                    else
                        predictor[channel] = fSrcData[offset + (channel - fSrcRowStep)];// sPtr[channel - fSrcRowStep];

                }

                // Unroll most common case of two channels

                if (fSrcChannels == 2)
                {

                    int32 pred0 = predictor[0];
                    int32 pred1 = predictor[1];

                    uint32 srcCols = fSrcCols;
                    int32 srcColStep = fSrcColStep;

                    for (uint32 col = 0; col < srcCols; col++)
                    {

                        int32 pixel0 = fSrcData[offset + 0];//sPtr[0];
                        int32 pixel1 = fSrcData[offset + 1];//sPtr[1];

                        int16 diff0 = (int16)(pixel0 - pred0);
                        int16 diff1 = (int16)(pixel1 - pred1);

                        CountOneDiff(diff0, freqCount[0]);
                        CountOneDiff(diff1, freqCount[1]);

                        pred0 = pixel0;
                        pred1 = pixel1;

                        offset += srcColStep;//sPtr += srcColStep;

                    }

                }

                // General case.

                else
                {

                    for (uint32 col = 0; col < fSrcCols; col++)
                    {

                        for (uint32 channel = 0; channel < fSrcChannels; channel++)
                        {

                            int32 pixel = fSrcData[offset + channel];//sPtr[channel];

                            int16 diff = (int16)(pixel - predictor[channel]);

                            CountOneDiff(diff, freqCount[channel]);

                            predictor[channel] = pixel;

                        }

                        offset += fSrcColStep;//sPtr += fSrcColStep;

                    }

                }

            }

        }

        /*****************************************************************************/


        /*
		 *--------------------------------------------------------------
		 *
		 * HuffEncode --
		 *
		 *      Encode and output Huffman-compressed image data.
		 *
		 * Results:
		 *      None.
		 *
		 * Side effects:
		 *      None.
		 *
		 *--------------------------------------------------------------
		 */
        private void HuffEncode()
        {

            DNG_ASSERT((int32)fSrcRows >= 0, "dng_lossless_encoder::HuffEncode: fSrcRows too large.");

            int offset;

            for (int32 row = 0; row < (int32)fSrcRows; row++)
            {

                //const uint16* sPtr = fSrcData + row * fSrcRowStep;
                offset = row * fSrcRowStep;

                // Initialize predictors for this row.

                int32[] predictor = { 0, 0, 0, 0 };

                for (int32 channel = 0; channel < (int32)fSrcChannels; channel++)
                {

                    if (row == 0)
                        predictor[channel] = 1 << ((int)fSrcBitDepth - 1);

                    else
                        predictor[channel] = fSrcData[offset + (channel - fSrcRowStep)];// sPtr[channel - fSrcRowStep];

                }

                // Unroll most common case of two channels

                if (fSrcChannels == 2)
                {

                    int32 pred0 = predictor[0];
                    int32 pred1 = predictor[1];

                    uint32 srcCols = fSrcCols;
                    int32 srcColStep = fSrcColStep;

                    for (uint32 col = 0; col < srcCols; col++)
                    {

                        int32 pixel0 = fSrcData[offset + 0];//sPtr[0];
                        int32 pixel1 = fSrcData[offset + 1];// sPtr[1];

                        int16 diff0 = (int16)(pixel0 - pred0);
                        int16 diff1 = (int16)(pixel1 - pred1);

                        EncodeOneDiff(diff0, ref huffTable[0]);
                        EncodeOneDiff(diff1, ref huffTable[1]);

                        pred0 = pixel0;
                        pred1 = pixel1;

                        offset += srcColStep;//sPtr += srcColStep;

                    }

                }

                // General case.

                else
                {

                    for (uint32 col = 0; col < fSrcCols; col++)
                    {

                        for (uint32 channel = 0; channel < fSrcChannels; channel++)
                        {

                            int32 pixel = fSrcData[offset + channel];// sPtr[channel];

                            int16 diff = (int16)(pixel - predictor[channel]);

                            EncodeOneDiff(diff, ref huffTable[channel]);

                            predictor[channel] = pixel;

                        }

                        offset += fSrcColStep;//sPtr += fSrcColStep;

                    }

                }

            }

            FlushBits();

        }

        /*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * GenHuffCoding --
		 *
		 * 	Generate the optimal coding for the given counts. 
		 *	This algorithm is explained in section K.2 of the
		 *	JPEG standard. 
		 *
		 * Results:
		 *      htbl->bits and htbl->huffval are constructed.
		 *
		 * Side effects:
		 *      None.
		 *
		 *--------------------------------------------------------------
		 */

        private void GenHuffCoding(ref HuffmanTable htbl, uint32[] freq)
        {

            int i;
            int j;

            const int MAX_CLEN = 32;        // assumed maximum initial code length

            uint8[] bits = new uint8[MAX_CLEN + 1];   // bits [k] = # of symbols with code length k
            short[] codesize = new short[257];            // codesize [k] = code length of symbol k
            short[] others = new short[257];          // next symbol in current branch of tree

            //memset(bits, 0, sizeof(bits));
            //memset(codesize, 0, sizeof(codesize));

            for (i = 0; i < 257; i++)
                others[i] = -1;         // init links to empty

            // Including the pseudo-symbol 256 in the Huffman procedure guarantees
            // that no real symbol is given code-value of all ones, because 256
            // will be placed in the largest codeword category.

            freq[256] = 1;                  // make sure there is a nonzero count

            // Huffman's basic algorithm to assign optimal code lengths to symbols

            while (true)
            {

                // Find the smallest nonzero frequency, set c1 = its symbol.
                // In case of ties, take the larger symbol number.

                int c1 = -1;

                uint32 v = 0xFFFFFFFF;

                for (i = 0; i <= 256; i++)
                {

                    if ((freq[i]) != 0 && freq[i] <= v)
                    {
                        v = freq[i];
                        c1 = i;
                    }

                }

                // Find the next smallest nonzero frequency, set c2 = its symbol.
                // In case of ties, take the larger symbol number.

                int c2 = -1;

                v = 0xFFFFFFFF;

                for (i = 0; i <= 256; i++)
                {

                    if ((freq[i]) != 0 && freq[i] <= v && i != c1)
                    {
                        v = freq[i];
                        c2 = i;
                    }

                }

                // Done if we've merged everything into one frequency.

                if (c2 < 0)
                    break;

                // Else merge the two counts/trees.

                freq[c1] += freq[c2];
                freq[c2] = 0;

                // Increment the codesize of everything in c1's tree branch.

                codesize[c1]++;

                while (others[c1] >= 0)
                {
                    c1 = others[c1];
                    codesize[c1]++;
                }

                // chain c2 onto c1's tree branch 

                others[c1] = (short)c2;

                // Increment the codesize of everything in c2's tree branch.

                codesize[c2]++;

                while (others[c2] >= 0)
                {
                    c2 = others[c2];
                    codesize[c2]++;
                }

            }

            // Now count the number of symbols of each code length.

            for (i = 0; i <= 256; i++)
            {

                if (codesize[i] != 0)
                {

                    // The JPEG standard seems to think that this can't happen,
                    // but I'm paranoid...

                    if (codesize[i] > MAX_CLEN)
                    {

                        throw new Exception("Huffman code size table overflow");

                    }

                    bits[codesize[i]]++;

                }

            }

            // JPEG doesn't allow symbols with code lengths over 16 bits, so if the pure
            // Huffman procedure assigned any such lengths, we must adjust the coding.
            // Here is what the JPEG spec says about how this next bit works:
            // Since symbols are paired for the longest Huffman code, the symbols are
            // removed from this length category two at a time.  The prefix for the pair
            // (which is one bit shorter) is allocated to one of the pair; then,
            // skipping the BITS entry for that prefix length, a code word from the next
            // shortest nonzero BITS entry is converted into a prefix for two code words
            // one bit longer.

            for (i = MAX_CLEN; i > 16; i--)
            {

                while (bits[i] > 0)
                {

                    // Kludge: I have never been able to test this logic, and there
                    // are comments on the web that this encoder has bugs with 16-bit
                    // data, so just throw an error if we get here and revert to a
                    // default table.	 - tknoll 12/1/03.

                    //DNG_REPORT("Info: Optimal huffman table bigger than 16 bits");
                    //ThrowProgramError();
                    throw new Exception("Info: Optimal huffman table bigger than 16 bits");

                    // Original logic:

                    j = i - 2;      // find length of new prefix to be used

                    while (bits[j] == 0)
                        j--;

                    bits[i] -= 2;       // remove two symbols
                    bits[i - 1]++;      // one goes in this length
                    bits[j + 1] += 2;       // two new symbols in this length
                    bits[j]--;      // symbol of this length is now a prefix

                }

            }

            // Remove the count for the pseudo-symbol 256 from
            // the largest codelength.

            while (bits[i] == 0)        // find largest codelength still in use
                i--;

            bits[i]--;

            // Return final symbol counts (only for lengths 0..16).

            //memcpy(htbl->bits, bits, sizeof(htbl->bits));
            htbl.bits = new uint8[17];

            // Return a list of the symbols sorted by code length. 
            // It's not real clear to me why we don't need to consider the codelength
            // changes made above, but the JPEG spec seems to think this works.

            int p = 0;

            for (i = 1; i <= MAX_CLEN; i++)
            {

                for (j = 0; j <= 255; j++)
                {

                    if (codesize[j] == i)
                    {
                        htbl.huffval[p] = (uint8)j;
                        p++;
                    }

                }

            }

        }


        /*****************************************************************************/

        /*
		 *--------------------------------------------------------------
		 *
		 * HuffOptimize --
		 *
		 *	Find the best coding parameters for a Huffman-coded scan.
		 *	When called, the scan data has already been converted to
		 *	a sequence of MCU groups of source image samples, which
		 *	are stored in a "big" array, mcuTable.
		 *
		 *	It counts the times each category symbol occurs. Based on
		 *	this counting, optimal Huffman tables are built. Then it
		 *	uses this optimal Huffman table and counting table to find
		 *	the best PSV. 
		 *
		 * Results:
		 *	Optimal Huffman tables are retured in cPtr->dcHuffTblPtrs[tbl].
		 *	Best PSV is retured in cPtr->Ss.
		 *
		 * Side effects:
		 *	None.
		 *
		 *--------------------------------------------------------------
		 */

        public void HuffOptimize()
        {

            // Collect the frequency counts.

            FreqCountSet();

            // Generate Huffman encoding tables.

            for (uint32 channel = 0; channel < fSrcChannels; channel++)
            {

                try
                {

                    GenHuffCoding(ref huffTable[channel], freqCount[channel]);

                }

                catch (Exception e)
                {

                    DNG_REPORT("Info: Reverting to default huffman table");

                    for (uint32 j = 0; j <= 256; j++)
                    {

                        freqCount[channel][j] = (uint32)(j <= 16 ? 1 : 0);

                    }

                    GenHuffCoding(ref huffTable[channel], freqCount[channel]);

                }

                FixHuffTbl(ref huffTable[channel]);

            }

        }

        /*****************************************************************************/

        /*
         *--------------------------------------------------------------
         *
         * EmitMarker --
         *
         *	Emit a marker code into the output stream.
         *
         * Results:
         *	None.
         *
         * Side effects:
         *	None.
         *
         *--------------------------------------------------------------
         */

        public void EmitMarker(JpegMarker mark)
        {

            EmitByte(0xFF);
            EmitByte((uint8)mark);

        }

        /*****************************************************************************/

        /*
         *--------------------------------------------------------------
         *
         * Emit2bytes --
         *
         *	Emit a 2-byte integer; these are always MSB first in JPEG
         *	files
         *
         * Results:
         *	None.
         *
         * Side effects:
         *	None.
         *
         *--------------------------------------------------------------
         */

        public void Emit2bytes(int value)
        {

            EmitByte((uint8)((value >> 8) & 0xFF));
            EmitByte((uint8)(value & 0xFF));

        }

        /*****************************************************************************/

        /*
         *--------------------------------------------------------------
         *
         * EmitDht --
         *
         *	Emit a DHT marker, follwed by the huffman data.
         *
         * Results:
         *	None
         *
         * Side effects:
         *	None
         *
         *--------------------------------------------------------------
         */

        public void EmitDht(int index)
        {

            int i;

            HuffmanTable htbl = huffTable[index];

            EmitMarker(JpegMarker.M_DHT);

            int length = 0;

            for (i = 1; i <= 16; i++)
                length += htbl.bits[i];

            Emit2bytes(length + 2 + 1 + 16);

            EmitByte((uint8)index);

            for (i = 1; i <= 16; i++)
                EmitByte(htbl.bits[i]);

            for (i = 0; i < length; i++)
                EmitByte(htbl.huffval[i]);

        }

        /*****************************************************************************/

        /*
         *--------------------------------------------------------------
         *
         * EmitSof --
         *
         *	Emit a SOF marker plus data.
         *
         * Results:
         *	None.
         *
         * Side effects:
         *	None.
         *
         *--------------------------------------------------------------
         */

        public void EmitSof(JpegMarker code)
        {

            EmitMarker(code);

            Emit2bytes((int)(3 * fSrcChannels + 2 + 5 + 1));   // length

            EmitByte((uint8)fSrcBitDepth);

            Emit2bytes((int)fSrcRows);
            Emit2bytes((int)fSrcCols);

            EmitByte((uint8)fSrcChannels);

            for (uint32 i = 0; i < fSrcChannels; i++)
            {

                EmitByte((uint8)i);

                EmitByte((uint8)((1 << 4) + 1));        // Not subsampled.

                EmitByte(0);                    // Tq shall be 0 for lossless.

            }

        }

        /*****************************************************************************/

        /*
         *--------------------------------------------------------------
         *
         * EmitSos --
         *
         *	Emit a SOS marker plus data.
         *
         * Results:
         *	None.
         *
         * Side effects:
         *	None.
         *
         *--------------------------------------------------------------
         */

        public void EmitSos()
        {

            EmitMarker(JpegMarker.M_SOS);

            Emit2bytes((int)(2 * fSrcChannels + 2 + 1 + 3));   // length

            EmitByte((uint8)fSrcChannels);          // Ns

            for (uint32 i = 0; i < fSrcChannels; i++)
            {

                // Cs,Td,Ta

                EmitByte((uint8)i);
                EmitByte((uint8)(i << 4));

            }

            EmitByte(1);        // PSV - hardcoded - tknoll
            EmitByte(0);        // Spectral selection end  - Se
            EmitByte(0);        // The point transform parameter 

        }

        /*****************************************************************************/

        /*
         *--------------------------------------------------------------
         *
         * WriteFileHeader --
         *
         *	Write the file header.
         *
         * Results:
         *	None.
         *
         * Side effects:
         *	None.
         *
         *--------------------------------------------------------------
         */

        public void WriteFileHeader()
        {

            EmitMarker(JpegMarker.M_SOI);      // first the SOI

            EmitSof(JpegMarker.M_SOF3);

        }

        /*****************************************************************************/

        /*
         *--------------------------------------------------------------
         *
         * WriteScanHeader --
         *
         *	Write the start of a scan (everything through the SOS marker).
         *
         * Results:
         *	None.
         *
         * Side effects:
         *	None.
         *
         *--------------------------------------------------------------
         */

        public void WriteScanHeader()
        {

            // Emit Huffman tables.

            for (uint32 i = 0; i < fSrcChannels; i++)
            {

                EmitDht(i);

            }

            EmitSos();

        }

        /*****************************************************************************/

        /*
         *--------------------------------------------------------------
         *
         * WriteFileTrailer --
         *
         *	Write the End of image marker at the end of a JPEG file.
         *
         * Results:
         *	None.
         *
         * Side effects:
         *	None.
         *
         *--------------------------------------------------------------
         */

        public void WriteFileTrailer()
        {

            EmitMarker(JpegMarker.M_EOI);

        }

        /*****************************************************************************/

        public void Encode()
        {

            DNG_ASSERT(fSrcChannels <= 4, "Too many components in scan");

            // Count the times each difference category occurs. 
            // Construct the optimal Huffman table.

            HuffOptimize();

            // Write the frame and scan headers.

            WriteFileHeader();

            WriteScanHeader();

            // Encode the image.

            HuffEncode();

            // Clean up everything.

            WriteFileTrailer();

        }

        /*****************************************************************************/

        static public void EncodeLosslessJPEG(uint16[] srcData,
                                 uint32 srcRows,
                                 uint32 srcCols,
                                 uint32 srcChannels,
                                 uint32 srcBitDepth,
                                 int32 srcRowStep,
                                 int32 srcColStep,
                                 dng_stream stream)
        {

            DNGLosslessEncoder encoder = new DNGLosslessEncoder(srcData,
                                          srcRows,
                                          srcCols,
                                          srcChannels,
                                          srcBitDepth,
                                          srcRowStep,
                                          srcColStep,
                                          stream);

            encoder.Encode();

        }

        /*****************************************************************************/

    }

	public class dng_stream
    {
		List<byte> data;

		public void Put_uint8(byte toAdd)
        {
			data.Add(toAdd);
        }

		public byte[] toByteArray()
        {
			return data.ToArray();
        }
	}
}

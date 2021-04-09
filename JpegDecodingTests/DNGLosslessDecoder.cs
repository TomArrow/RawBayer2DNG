// This file was adapted from dng_lossless_jpeg.cpp from the Adobe DNG SDK:

/*****************************************************************************/
// Copyright 2006-2019 Adobe Systems Incorporated
// All Rights Reserved.
//
// NOTICE:  Adobe permits you to use, modify, and distribute this file in
// accordance with the terms of the Adobe license agreement accompanying it.
/*****************************************************************************/

// Lossless JPEG code adapted from:

/* Copyright (C) 1991, 1992, Thomas G. Lane.
 * Part of the Independent JPEG Group's software.
 * See the file Copyright for more details.
 *
 * Copyright (c) 1993 Brian C. Smith, The Regents of the University
 * of California
 * All rights reserved.
 * 
 * Copyright (c) 1994 Kongji Huang and Brian C. Smith.
 * Cornell University
 * All rights reserved.
 * 
 * Permission to use, copy, modify, and distribute this software and its
 * documentation for any purpose, without fee, and without written agreement is
 * hereby granted, provided that the above copyright notice and the following
 * two paragraphs appear in all copies of this software.
 * 
 * IN NO EVENT SHALL CORNELL UNIVERSITY BE LIABLE TO ANY PARTY FOR
 * DIRECT, INDIRECT, SPECIAL, INCIDENTAL, OR CONSEQUENTIAL DAMAGES ARISING OUT
 * OF THE USE OF THIS SOFTWARE AND ITS DOCUMENTATION, EVEN IF CORNELL
 * UNIVERSITY HAS BEEN ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 * 
 * CORNELL UNIVERSITY SPECIFICALLY DISCLAIMS ANY WARRANTIES,
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY
 * AND FITNESS FOR A PARTICULAR PURPOSE.  THE SOFTWARE PROVIDED HEREUNDER IS
 * ON AN "AS IS" BASIS, AND CORNELL UNIVERSITY HAS NO OBLIGATION TO
 * PROVIDE MAINTENANCE, SUPPORT, UPDATES, ENHANCEMENTS, OR MODIFICATIONS.
 */

#define qSupportCanon_sRAW
#define qSupportHasselblad_3FR

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
using uint64 = System.UInt64;
using System.Runtime.CompilerServices;


/*****************************************************************************/

/*
 * The following structure stores basic information about one component.
 */

/*****************************************************************************/

// An MCU (minimum coding unit) is an array of samples.

using ComponentType = System.UInt16;
//using MCU = System.UInt16; //no idea how to do pointer. this is suppoed to be an array tho.
//typedef ComponentType *MCU;         // MCU - array of samples

/*****************************************************************************/

namespace JpegDecodingTests
{

	public class dng_memory_data
    {
		byte[] myBuffer;

		public void Allocate(uint32 size)
        {
			Clear(); 
			
			//printf("Calling malloc from %s\n", __FUNCTION__);
			myBuffer = new uint8[size];
		}
		public void Clear()
        {
			myBuffer = new uint8[1];
		}
		
		public byte[] Buffer()
        {
			return myBuffer;
        }
	}

	public class dng_spooler
	{
		List<byte> data = new List<byte>();

		/*public void Put_uint8(byte toAdd)
		{
			data.Add(toAdd);
		}
		*/
		public byte[] toByteArray()
		{
			return data.ToArray();
		}

		// Add bytes, basically.
		public void Spool(byte[] dataToAdd)
        {
			data.AddRange(dataToAdd); 
        }
	}

	class DNGLosslessDecoder
    {
        public enum JpegMarker
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

        public class HuffmanTable
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

			public static uint getSize()
            {
				return sizeof(uint8) * 17 + //bits
					sizeof(uint8) * 256 + // huffval
					sizeof(uint16) * 17 + // mincode
					sizeof(int32) * 18 + // maxcode
					sizeof(int16) * 17 + // valptr
					sizeof(int32) * 256 + // numbits
					sizeof(int32) * 256 + // value
					sizeof(uint16) * 256 + // ehufco
					sizeof(int8) * 256; // ehufsi

            }

        };

        /*
     * The following structure stores basic information about one component.
     */

        public class JpegComponentInfo
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

        public class DecompressInfo
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
            public JpegComponentInfo[] compInfo;
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

			}
		}


		/*private void ThrowBadFormat()
		{
			throw new Exception("bad format");
		}*/



		dng_stream fStream;        // Input data.

		dng_spooler fSpooler;      // Output data.

		bool fBug16;                // Decode data with the "16-bit" bug.

		//dng_memory_data[] huffmanBuffer = new dng_memory_data[4];
		HuffmanTable[] huffmanBuffer = new HuffmanTable[4];

		//dng_memory_data compInfoBuffer;
		JpegComponentInfo[] compInfoBuffer;

		DecompressInfo info;


		//dng_memory_data mcuBuffer1;
		//dng_memory_data mcuBuffer2;
		//dng_memory_data mcuBuffer3;
		//dng_memory_data mcuBuffer4;
		ComponentType[,] mcuBuffer1;
		ComponentType[,] mcuBuffer2;
		ComponentType[] mcuBuffer3;
		ComponentType[] mcuBuffer4;

		// MCU *mcuROW1;
		// MCU *mcuROW2;
		ComponentType[,] mcuROW1;
		ComponentType[,] mcuROW2;

		uint64 getBuffer;           // current bit-extraction buffer
		int32 bitsLeft;             // # of unused bits in it

#if qSupportHasselblad_3FR
		bool fHasselblad3FR;
#endif
		/*
		public:
	
				dng_lossless_decoder(dng_stream * stream,
									  dng_spooler * spooler,
									  bool bug16);

		void StartRead(uint32 &imageWidth,
						uint32 &imageHeight,
						uint32 &imageChannels);

		void FinishRead();
		*/
		#if qSupportHasselblad_3FR
	
		public bool IsHasselblad3FR ()
		{
			return fHasselblad3FR;
		}
		
		#endif
		/*
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
			*/
		/*****************************************************************************/

		public uint8 GetJpegChar()
		{
			return fStream.Get_uint8();
		}

		public void UnGetJpegChar()
		{
			fStream.SetReadPosition(fStream.Position() - 1);
		}

		public DNGLosslessDecoder(dng_stream  stream,
											dng_spooler  spooler,
											bool bug16)/*
									
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
			*/
		{

			fStream = stream;
			fSpooler = spooler;
			fBug16 = bug16;
			compInfoBuffer = new JpegComponentInfo[1]; //new dng_memory_data(); // This will be overwritten later anyway wheren ormally it would do an allocate
			info = new DecompressInfo();
			//mcuBuffer1 = new dng_memory_data();
			//mcuBuffer2 = new dng_memory_data();
			//mcuBuffer3 = new dng_memory_data();
			//mcuBuffer4 = new dng_memory_data();
			//mcuROW1 = 
			//	mcuROW2=
			getBuffer = 0;
			bitsLeft = 0;
			//memset(&info, 0, sizeof(info)); // not necessary because C# automatically nulls

		}

/*****************************************************************************/

		public uint16 Get2bytes()
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

		void SkipVariable()
		{

			uint32 length = (uint32)Get2bytes() - 2;

			fStream.Skip(length);

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

		public void GetDht()
		{

			int32 length = Get2bytes() - 2;

			while (length > 0)
			{

				int32 index = GetJpegChar();

				if (index < 0 || index >= 4)
				{
					//ThrowBadFormat();
					throw new Exception("bad format");
				}

				//HuffmanTable * &htblptr = info.dcHuffTblPtrs[index];
				HuffmanTable htblptr = info.dcHuffTblPtrs[index];

				if (htblptr == null)
				{

					//huffmanBuffer[index].Allocate(sizeof(HuffmanTable));
					huffmanBuffer[index] = new HuffmanTable(); //.Allocate(HuffmanTable.getSize());

					//htblptr = (HuffmanTable*)huffmanBuffer[index].Buffer();
					htblptr = huffmanBuffer[index];//(HuffmanTable*)huffmanBuffer[index].Buffer();

				}

				htblptr.bits[0] = 0;

				int32 count = 0;

				for (int32 i = 1; i <= 16; i++)
				{

					htblptr.bits[i] = GetJpegChar();

					count += htblptr.bits[i];

				}

				if (count > 256)
				{
					throw new Exception("bad format");
				}

				for (int32 j = 0; j < count; j++)
				{

					htblptr.huffval[j] = GetJpegChar();

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
		 */

		public void GetDri()
		{

			if (Get2bytes() != 4)
			{

				throw new Exception("bad format");
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

		public void GetApp0()
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

		public void GetSof(int32 code) // the "code" part was commented out in the original for whatever reason. But the code doesnt do shit with "code" anyway, so eh.
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
				throw new Exception("bad format");
			}

			// Lossless JPEG specifies data precision to be from 2 to 16 bits/sample.

			const int32 MinPrecisionBits = 2;
			const int32 MaxPrecisionBits = 16;

			if ((info.dataPrecision < MinPrecisionBits) ||
				(info.dataPrecision > MaxPrecisionBits))
			{
				throw new Exception("bad format");
			}

			// Check length of tag.

			if (length != (info.numComponents * 3 + 8))
			{
				throw new Exception("bad format");
			}

			// Allocate per component info.

			// We can cast info.numComponents to a uint32 because the check above
			// guarantees that it cannot be negative.

			//compInfoBuffer.Allocate((uint32)info.numComponents,	sizeof(JpegComponentInfo));
			compInfoBuffer = new JpegComponentInfo[info.numComponents];

			//info.compInfo = (JpegComponentInfo*)compInfoBuffer.Buffer();
			info.compInfo = compInfoBuffer;

			// Read in the per compent info.

			for (int32 ci = 0; ci < info.numComponents; ci++)
			{

				JpegComponentInfo compptr = info.compInfo[ci];

				compptr.componentIndex = (int16)ci;

				compptr.componentId = GetJpegChar();

				int32 c = GetJpegChar();

				compptr.hSampFactor = (int16)((c >> 4) & 15);
				compptr.vSampFactor = (int16)((c) & 15);

				/*(void)*/GetJpegChar();   /* skip Tq */

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

		public void GetSos()
		{

			int32 length = Get2bytes();

			// Get the number of image components.

			int32 n = GetJpegChar();
			info.compsInScan = (int16)n;

			// Check length.

			length -= 3;

			if (length != (n * 2 + 3) || n < 1 || n > 4)
			{
						throw new Exception("bad format");
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
							throw new Exception("bad format");
						}

				JpegComponentInfo compptr = info.compInfo[ci];

				info.curCompInfo[i] = compptr;

				compptr.dcTblNo = (int16)((c >> 4) & 15);

			}

			// Get the PSV, skip Se, and get the point transform parameter.

			info.Ss = GetJpegChar();

			/*(void)*/GetJpegChar();

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

		public void GetSoi()
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

		public int32 NextMarker()
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

		public JpegMarker ProcessTables()
		{

			while (true)
			{

				int32 c = NextMarker();

				switch ((JpegMarker)c)
				{

					case JpegMarker.M_SOF0:
					case JpegMarker.M_SOF1:
					case JpegMarker.M_SOF2:
					case JpegMarker.M_SOF3:
					case JpegMarker.M_SOF5:
					case JpegMarker.M_SOF6:
					case JpegMarker.M_SOF7:
					case JpegMarker.M_JPG:
					case JpegMarker.M_SOF9:
					case JpegMarker.M_SOF10:
					case JpegMarker.M_SOF11:
					case JpegMarker.M_SOF13:
					case JpegMarker.M_SOF14:
					case JpegMarker.M_SOF15:
					case JpegMarker.M_SOI:
					case JpegMarker.M_EOI:
					case JpegMarker.M_SOS:
						return (JpegMarker)c;

					case JpegMarker.M_DHT:
						GetDht();
						break;

					case JpegMarker.M_DQT:
						break;

					case JpegMarker.M_DRI:
						GetDri();
						break;

					case JpegMarker.M_APP0:
						GetApp0();
						break;

					case JpegMarker.M_RST0:    // these are all parameterless
					case JpegMarker.M_RST1:
					case JpegMarker.M_RST2:
					case JpegMarker.M_RST3:
					case JpegMarker.M_RST4:
					case JpegMarker.M_RST5:
					case JpegMarker.M_RST6:
					case JpegMarker.M_RST7:
					case JpegMarker.M_TEM:
						break;

					default:        // must be DNL, DHP, EXP, APPn, JPGn, COM, or RESn
						SkipVariable();
						break;

				}

			}

			return JpegMarker.M_ERROR;
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

		public void ReadFileHeader()
		{

			// Demand an SOI marker at the start of the stream --- otherwise it's
			// probably not a JPEG stream at all.

			int32 c = GetJpegChar();
			int32 c2 = GetJpegChar();

			if ((c != 0xFF) || ((JpegMarker)c2 != JpegMarker.M_SOI))
			{
				throw new Exception("bad format");
			}

			// OK, process SOI

			GetSoi();

			// Process markers until SOF

			c = (int)ProcessTables();

			switch ((JpegMarker)c)
			{

				case JpegMarker.M_SOF0:
				case JpegMarker.M_SOF1:
				case JpegMarker.M_SOF3:
					GetSof(c);
					break;

				default:
							throw new Exception("bad format");
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

		public int32 ReadScanHeader()
		{

			// Process markers until SOS or EOI

			int32 c = (int)ProcessTables();

			switch ((JpegMarker)c)
			{

				case JpegMarker.M_SOS:
					GetSos();
					return 1;

				case JpegMarker.M_EOI:
					return 0;

				default:
					throw new Exception("bad format");
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

		public void DecoderStructInit()
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

					JpegComponentInfo compPtr = info.compInfo[ci];

					if (compPtr.hSampFactor != 1 ||
						compPtr.vSampFactor != 1)
					{
								throw new Exception("bad format");
							}

				}

			}

			// Prepare array describing MCU composition.

			if (info.compsInScan < 0 ||
				info.compsInScan > 4)
			{
						throw new Exception("bad format");
					}

			for (ci = 0; ci < info.compsInScan; ci++)
			{
				info.MCUmembership[ci] = (int16)ci;
			}

			// Initialize mucROW1 and mcuROW2 which buffer two rows of
			// pixels for predictor calculation.

			// This multiplication cannot overflow because info.compsInScan is
			// guaranteed to be between 0 and 4 inclusive (see checks above).

			int32 mcuSize = (int32)(info.compsInScan * (uint32)sizeof(ComponentType));

			/*mcuBuffer1.Allocate(info.imageWidth, sizeof(MCU));
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

			*/


			//mcuBuffer1.Allocate(info.imageWidth, sizeof(MCU));
			//mcuBuffer2.Allocate(info.imageWidth, sizeof(MCU));

			mcuBuffer1 = new UInt16[info.imageWidth, info.compsInScan];
			mcuBuffer2 = new UInt16[info.imageWidth, info.compsInScan];

			//mcuROW1 = (MCU*)mcuBuffer1.Buffer();
			//mcuROW2 = (MCU*)mcuBuffer2.Buffer();

			mcuROW1 = mcuBuffer1;
			mcuROW2 = mcuBuffer2;

			//mcuBuffer3.Allocate(info.imageWidth, mcuSize);
			//mcuBuffer4.Allocate(info.imageWidth, mcuSize);

			//mcuBuffer3 = new UInt16[info.imageWidth* mcuSize/ sizeof(ComponentType)];
			//mcuBuffer4 = new UInt16[info.imageWidth*mcuSize/ sizeof(ComponentType)];

			//mcuROW1[0] = (ComponentType*)mcuBuffer3.Buffer();
			//mcuROW2[0] = (ComponentType*)mcuBuffer4.Buffer();

			//mcuROW1[0] = mcuBuffer3;
			//mcuROW2[0] = mcuBuffer4;

			/*for (int32 j = 1; j < info.imageWidth; j++)
			{

				mcuROW1[j] = new ComponentType[info.compsInScan];//mcuROW1[j - 1] + info.compsInScan;
				mcuROW2[j] = new ComponentType[info.compsInScan];//mcuROW2[j - 1] + info.compsInScan;

			}*/

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

		public void HuffDecoderInit()
		{

			// Initialize bit parser state

			getBuffer = 0;
			bitsLeft = 0;

			// Prepare Huffman tables.

			for (int16 ci = 0; ci < info.compsInScan; ci++)
			{

				JpegComponentInfo compptr = info.curCompInfo[ci];

				// Make sure requested tables are present

				if (compptr.dcTblNo < 0 || compptr.dcTblNo > 3)
				{
					throw new Exception("bad format");
				}

				if (info.dcHuffTblPtrs[compptr.dcTblNo] == null)
				{
					throw new Exception("bad format");
				}

				// Compute derived values for Huffman tables.
				// We may do this more than once for same table, but it's not a
				// big deal

				FixHuffTbl(ref info.dcHuffTblPtrs[compptr.dcTblNo]);

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

		public void ProcessRestart()
		{

			// Throw away and unused odd bits in the bit buffer.

			fStream.SetReadPosition((UInt64)((int)fStream.Position() - bitsLeft / 8));

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

			if ((JpegMarker)c != (JpegMarker.M_RST0 + info.nextRestartNum))
			{
						throw new Exception("bad format");
					}

			// Update restart state.

			info.restartRowsToGo = info.restartInRows;
			info.nextRestartNum = (int16)((info.nextRestartNum + 1) & 7);

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
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		//public int32 QuickPredict (int32 col,int32 curComp,MCU *curRowBuf, MCU *prevRowBuf)
		public int32 QuickPredict (int32 col,int32 curComp,ref ComponentType[,] curRowBuf, ref ComponentType[,] prevRowBuf)
			{

			int32 diag = prevRowBuf[col - 1,curComp];
			int32 upper = prevRowBuf[col,curComp];
			int32 left = curRowBuf[col - 1,curComp];

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
						throw new Exception("bad format");
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
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void FillBitBuffer(int32 nbits)
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
				
					catch (Exception except)
						{
				
						// If we got any exception other than EOF, rethrow.
				
						if (except.Message != "dng_error_end_of_file")
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
			
					getBuffer = (getBuffer << 8) | (uint64)c3;
					getBuffer = (getBuffer << 8) | (uint64)c2;
					getBuffer = (getBuffer << 8) | (uint64)c1;
					getBuffer = (getBuffer << 8) | (uint64)c0;
			
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

				getBuffer = (getBuffer << 8) | (uint64)c;

				bitsLeft += 8;

			}

		}

		/*****************************************************************************/
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int32 show_bits8()
		{

			if (bitsLeft < 8)
				FillBitBuffer(8);

			return (int32)((getBuffer >> (bitsLeft - 8)) & 0xff);

		}

		/*****************************************************************************/
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void flush_bits(int32 nbits)
		{

			bitsLeft -= nbits;

		}

		/*****************************************************************************/
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int32 get_bits(int32 nbits)
		{

			if (nbits > 16)
			{
				throw new Exception("bad format");
			}

			if (bitsLeft < nbits)
				FillBitBuffer(nbits);

			return (int32)((getBuffer >> (bitsLeft -= nbits)) & (uint64)(0x0FFFF >> (16 - nbits)));

		}

		/*****************************************************************************/
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int32 get_bit ()
			{

			//if (!bitsLeft)
			if (!(bitsLeft != 0))
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
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int32 HuffDecode (ref HuffmanTable htbl)
			{

			// If the huffman code is less than 8 bits, we can use the fast
			// table lookup to get its value.  It's more than 8 bits about
			// 3-4% of the time.

			int32 code = show_bits8();

			//if (htbl.numbits[code])
			if (htbl.numbits[code] != 0)
			{

				flush_bits(htbl.numbits[code]);

				return htbl.value[code];

			}

			else
			{

				flush_bits(8);

				int32 l = 8;

				while (code > htbl.maxcode[l])
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
					return htbl.huffval[htbl.valptr[l] +
										  ((int32)(code - htbl.mincode[l]))];
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
		// No idea what this does ^, let's ignore it
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		void HuffExtend(ref int32 x, int32 s)
		{

			if (x < (0x08000 >> (16 - s)))
			{
				x += (-1 << s) + 1;
			}

		}

		/*****************************************************************************/

		// My test: byte[][] abc = new byte[2][] { new byte[]{ 1, 2 }, new byte[]{ 3, 4 } }; byte[] bcd = new byte[4]; Buffer.BlockCopy(abc, 0, bcd, 0, 4); Console.WriteLine(bcd[0].ToString() + bcd[1].ToString()+ bcd[2].ToString()+ bcd[3]);
		// My test: byte[,] abc = new byte[2,2] {{ 1, 2 },{ 3, 4 } }; byte[] bcd = new byte[4]; Buffer.BlockCopy(abc, 0, bcd, 0, 4); Console.WriteLine(bcd[0].ToString() + bcd[1].ToString()+ bcd[2].ToString()+ bcd[3]);

		// Called from DecodeImage () to write one row.

		//public void PmPutRow(MCU* buf,
		public void PmPutRow(ref ComponentType[,] buf,
											 int32 numComp,
											 int32 numCol,
											 int32  row ) // row was commented out
		{


			uint32 pixels = (uint)(numCol * numComp);

			int byteCount = (int)pixels * sizeof(uint16);

			byte[] dataToSend = new uint8[byteCount];
			//uint16* sPtr = &buf[0][0];

			Buffer.BlockCopy(buf, 0, dataToSend, 0, byteCount);

			//fSpooler.Spool(sPtr, pixels * (uint32)sizeof(uint16));
			fSpooler.Spool(dataToSend);

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

		//public void DecodeFirstRow(MCU* curRowBuf)
		public void DecodeFirstRow(ref ComponentType[,] curRowBuf)
		{

			int32 compsInScan = info.compsInScan;

			// Process the first column in the row.

			for (int32 curComp = 0; curComp < compsInScan; curComp++)
			{

				int32 ci = info.MCUmembership[curComp];

				JpegComponentInfo compptr = info.curCompInfo[ci];

				HuffmanTable dctbl = info.dcHuffTblPtrs[compptr.dcTblNo];

				// Section F.2.2.1: decode the difference

				int32 d = 0;

				int32 s = HuffDecode(ref dctbl);

				if (s != 0)
				{

					if (s == 16 && !fBug16)
					{
						d = -32768;
					}

					else
					{
						d = get_bits(s);
						HuffExtend(ref d, s);
					}

				}

				// Add the predictor to the difference.

				int32 Pr = info.dataPrecision;
				int32 Pt = info.Pt;

				curRowBuf[0,curComp] = (ComponentType)(d + (1 << (Pr - Pt - 1)));

			}

			// Process the rest of the row.

			int32 numCOL = info.imageWidth;

			for (int32 col = 1; col < numCOL; col++)
			{

				for (int32 curComp = 0; curComp < compsInScan; curComp++)
				{

					int32 ci = info.MCUmembership[curComp];

					JpegComponentInfo compptr = info.curCompInfo[ci];

					HuffmanTable dctbl = info.dcHuffTblPtrs[compptr.dcTblNo];

					// Section F.2.2.1: decode the difference

					int32 d = 0;

					int32 s = HuffDecode(ref dctbl);

					if (s != 0)
					{

						if (s == 16 && !fBug16)
						{
							d = -32768;
						}

						else
						{
							d = get_bits(s);
							HuffExtend(ref d, s);
						}

					}

					// Add the predictor to the difference.

					curRowBuf[col,curComp] = (ComponentType)(d + curRowBuf[col - 1,curComp]);

				}

			}

			// Update the restart counter

			if (info.restartInRows != 0)
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

		public void DecodeImage()
		{

		#define swap(type,a,b) {type c; c=(a); (a)=(b); (b)=c;}

			int32 numCOL = info.imageWidth;
			int32 numROW = info.imageHeight;
			int32 compsInScan = info.compsInScan;

			// Precompute the decoding table for each table.

			HuffmanTable[] ht = new HuffmanTable[4];

			//memset(ht, 0, sizeof(ht));

			for (int32 curComp = 0; curComp < compsInScan; curComp++)
			{

				int32 ci = info.MCUmembership[curComp];

				JpegComponentInfo compptr = info.curCompInfo[ci];

				ht[curComp] = info.dcHuffTblPtrs[compptr.dcTblNo];

			}

			ComponentType[,] prevRowBuf = mcuROW1; // find out if this is by reference
			ComponentType[,] curRowBuf = mcuROW2;
			//MCU* prevRowBuf = mcuROW1;
			//MCU* curRowBuf = mcuROW2;

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
						p0 = prevRowBuf [0,0];
						p1 = prevRowBuf [0,1];
						p2 = prevRowBuf [0,2];
						}
			
					for (int32 col = 0; col < numCOL; col += 2)
						{
				
						// Read first luminance component.
				
							{
				
							int32 d = 0;
				
							int32 s = HuffDecode (ref ht [0]);
					
							if (s != 0)
								{

								if (s == 16)
									{
									d = -32768;
									}
						
								else
									{
									d = get_bits (s);
									HuffExtend (ref d, s);
									}

								}
						
							p0 += d;
					
							curRowBuf [col,0] = (ComponentType) p0;
				
							}
				
						// Read second luminance component.
				
							{
				
							int32 d = 0;
				
							int32 s = HuffDecode (ref ht [0]);
					
							if (s != 0)
								{

								if (s == 16)
									{
									d = -32768;
									}
						
								else
									{
									d = get_bits (s);
									HuffExtend (ref d, s);
									}

								}
						
							p0 += d;
					
							curRowBuf [col + 1,0] = (ComponentType) p0;
				
							}
				
						// Read first chroma component.
				
							{
				
							int32 d = 0;
				
							int32 s = HuffDecode (ref ht [1]);
					
							if (s != 0)
								{

								if (s == 16)
									{
									d = -32768;
									}
						
								else
									{
									d = get_bits (s);
									HuffExtend (ref d, s);
									}

								}
						
							p1 += d;
					
							curRowBuf [col    ,1] = (ComponentType) p1;
							curRowBuf [col + 1,1] = (ComponentType) p1;
				
							}
				
						// Read second chroma component.
				
							{
				
							int32 d = 0;
				
							int32 s = HuffDecode (ref ht [2]);
					
							if (s != 0)
								{

								if (s == 16)
									{
									d = -32768;
									}
						
								else
									{
									d = get_bits (s);
									HuffExtend (ref d, s);
									}

								}
						
							p2 += d;
					
							curRowBuf [col    ,2] = (ComponentType) p2;
							curRowBuf [col + 1,2] = (ComponentType) p2;
				
							}
								
						}
			
					PmPutRow (ref curRowBuf, compsInScan, numCOL, row);

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
						p0 = prevRowBuf [0,0];
						p1 = prevRowBuf [0,1];
						p2 = prevRowBuf [0,2];
						}
			
					for (int32 col = 0; col < numCOL; col += 2)
						{
				
						// Read first luminance component.
				
							{
				
							int32 d = 0;
				
							int32 s = HuffDecode (ref ht [0]);
					
							if (s != 0)
								{

								if (s == 16)
									{
									d = -32768;
									}
						
								else
									{
									d = get_bits (s);
									HuffExtend (ref d, s);
									}

								}
						
							p0 += d;
					
							prevRowBuf [col,0] = (ComponentType) p0;
				
							}
				
						// Read second luminance component.
				
							{
				
							int32 d = 0;
				
							int32 s = HuffDecode (ref ht [0]);
					
							if (s != 0)
								{

								if (s == 16)
									{
									d = -32768;
									}
						
								else
									{
									d = get_bits (s);
									HuffExtend (ref d, s);
									}

								}
						
							p0 += d;
					
							prevRowBuf [col + 1,0] = (ComponentType) p0;
				
							}
				
						// Read third luminance component.
				
							{
				
							int32 d = 0;
				
							int32 s = HuffDecode (ref ht [0]);
					
							if (s != 0)
								{

								if (s == 16)
									{
									d = -32768;
									}
						
								else
									{
									d = get_bits (s);
									HuffExtend (ref d, s);
									}

								}
						
							p0 += d;
					
							curRowBuf [col,0] = (ComponentType) p0;
				
							}
				
						// Read fourth luminance component.
				
							{
				
							int32 d = 0;
				
							int32 s = HuffDecode (ref ht [0]);
					
							if (s != 0)
								{

								if (s == 16)
									{
									d = -32768;
									}
						
								else
									{
									d = get_bits (s);
									HuffExtend (ref d, s);
									}

								}
						
							p0 += d;
					
							curRowBuf [col + 1,0] = (ComponentType) p0;
				
							}
				
						// Read first chroma component.
				
							{
				
							int32 d = 0;
				
							int32 s = HuffDecode (ref ht [1]);
					
							if (s != 0)
								{

								if (s == 16)
									{
									d = -32768;
									}
						
								else
									{
									d = get_bits (s);
									HuffExtend (ref d, s);
									}

								}
						
							p1 += d;
					
							prevRowBuf [col    ,1] = (ComponentType) p1;
							prevRowBuf [col + 1,1] = (ComponentType) p1;

							curRowBuf [col    ,1] = (ComponentType) p1;
							curRowBuf [col + 1,1] = (ComponentType) p1;
				
							}
				
						// Read second chroma component.
				
							{
				
							int32 d = 0;
				
							int32 s = HuffDecode (ref ht [2]);
					
							if (s != 0)
								{

								if (s == 16)
									{
									d = -32768;
									}
						
								else
									{
									d = get_bits (s);
									HuffExtend (ref d, s);
									}

								}
						
							p2 += d;
					
							prevRowBuf [col    ,2] = (ComponentType) p2;
							prevRowBuf [col + 1,2] = (ComponentType) p2;
				
							curRowBuf [col    ,2] = (ComponentType) p2;
							curRowBuf [col + 1,2] = (ComponentType) p2;
				
							}
								
						}
			
					PmPutRow (ref prevRowBuf, compsInScan, numCOL, row);
					PmPutRow (ref curRowBuf, compsInScan, numCOL, row);

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
				
						int32 s0 = HuffDecode (ref ht [0]);
						int32 s1 = HuffDecode (ref ht [0]);
				
						if (s0 != 0)
							{
							int32 d = get_bits (s0);
							if (s0 == 16)
								{
								d = -32768;
								}
							else
								{
								HuffExtend (ref d, s0);
								}
							p0 += d;
							}

						if (s1 != 0)
							{
							int32 d = get_bits (s1);
							if (s1 == 16)
								{
								d = -32768;
								}
							else
								{
								HuffExtend (ref d, s1);
								}
							p1 += d;
							}

						curRowBuf [col    ,0] = (ComponentType) p0;
						curRowBuf [col + 1,0] = (ComponentType) p1;
				
						}
			
					PmPutRow (ref curRowBuf, compsInScan, numCOL, row);

					}

				return;
		
				}
	
		#endif

			// Decode the first row of image. Output the row and
			// turn this row into a previous row for later predictor
			// calculation.

			DecodeFirstRow(ref mcuROW1);

			PmPutRow(ref mcuROW1, compsInScan, numCOL, 0);

			// Process each row.

			for (int32 row = 1; row < numROW; row++)
			{

				// Account for restart interval, process restart marker if needed.

				if (info.restartInRows != 0)
				{

					if (info.restartRowsToGo == 0)
					{

						ProcessRestart();

						// Reset predictors at restart.

						DecodeFirstRow(ref curRowBuf);

						PmPutRow(ref curRowBuf, compsInScan, numCOL, row);

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

					int32 s = HuffDecode(ref ht[curComp]);

					if (s != 0)
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

					curRowBuf[0,curComp] = (ComponentType)(d + prevRowBuf[0,curComp]);

				}

				// For the rest of the column on this row, predictor
				// calculations are based on PSV. 

				if (compsInScan == 2 && info.Ss == 1 && numCOL > 1)
				{

					// This is the combination used by both the Canon and Kodak raw formats. 
					// Unrolling the general case logic results in a significant speed increase.

					uint16* dPtr = &curRowBuf[1,0];

					int32 prev0 = dPtr[-2];
					int32 prev1 = dPtr[-1];

					for (int32 col = 1; col < numCOL; col++)
					{

						int32 s = HuffDecode(ref ht[0]);

						if (s != 0)
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

						s = HuffDecode(ref ht[1]);

						if (s != 0)
						{

							int32 d;

							if (s == 16 && !fBug16)
							{
								d = -32768;
							}

							else
							{
								d = get_bits(s);
								HuffExtend(ref d, s);
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

							int32 s = HuffDecode(ref ht[curComp]);

							if (s != 0)
							{

								if (s == 16 && !fBug16)
								{
									d = -32768;
								}

								else
								{
									d = get_bits(s);
									HuffExtend(ref d, s);
								}

							}

							// Predict the pixel value.

							int32 predictor = QuickPredict(col,
															curComp,
															ref curRowBuf,
															ref prevRowBuf);

							// Save the difference.

							curRowBuf[col,curComp] = (ComponentType)(d + predictor);

						}

					}

				}

				PmPutRow(ref curRowBuf, compsInScan, numCOL, row);

				swap(MCU *, prevRowBuf, curRowBuf);

			}

		#undef swap

		}

		/*****************************************************************************/

		public void StartRead(ref uint32 imageWidth,
											  ref uint32 imageHeight,
											  ref uint32 imageChannels)
		{

			ReadFileHeader();
			ReadScanHeader();
			DecoderStructInit();
			HuffDecoderInit();

			imageWidth = (uint)info.imageWidth;
			imageHeight = (uint)info.imageHeight;
			imageChannels = (uint)info.compsInScan;

		}

		/*****************************************************************************/

		public void FinishRead()
		{

			DecodeImage();

		}

		/*****************************************************************************/

		public void DecodeLosslessJPEG(dng_stream stream,
								 dng_spooler spooler,
								 uint32 minDecodedSize,
								 uint32 maxDecodedSize,
								 bool bug16,
								 uint64 endOfData)
		{

			DNGLosslessDecoder decoder = new DNGLosslessDecoder(stream,
								  spooler,
								  bug16);

			uint32 imageWidth = 0;
			uint32 imageHeight = 0;
			uint32 imageChannels = 0;

			decoder.StartRead(ref imageWidth,
							   ref imageHeight,
							   ref imageChannels);

			uint32 decodedSize = imageWidth *
								 imageHeight *
								 imageChannels *
								 (uint32)sizeof(uint16);

			if (decodedSize < minDecodedSize ||
				decodedSize > maxDecodedSize)
			{
				throw new Exception("bad format");
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

				if (decoder.IsHasselblad3FR() &&
					streamPos - endOfData == 4)
				{
					throwBadFormat = false;
				}

#endif

				if (throwBadFormat)
				{
					throw new Exception("bad format");
				}
			}

		}

	}
}

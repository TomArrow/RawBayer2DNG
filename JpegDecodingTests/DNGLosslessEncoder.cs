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

    };

    /*
 * The following structure stores basic information about one component.
 */

    public struct JpegComponentInfo
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

    /*****************************************************************************/

    public class DNGLosslessEncoder
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

            }
        }



        /*****************************************************************************/



        // An MCU (minimum coding unit) is an array of samples.

        //typedef uint16 ComponentType;       // the type of image components

        //typedef ComponentType *MCU;         // MCU - array of samples

        /*****************************************************************************/



        private void DNG_ASSERT(bool expression, string errorMessage)
        {
            if (!expression) throw new Exception(errorMessage);
            //#define DNG_ASSERT(x,y) { if (!(x)) dng_show_message (y); }
        }
        private void DNG_REPORT(string message)
        {
            Console.WriteLine(message);
        }

        private uint16[] fSrcData;

        private uint32 fSrcRows;
        private uint32 fSrcCols;
        private uint32 fSrcChannels;
        private uint32 fSrcBitDepth;

        private int32 fSrcRowStep;
        private int32 fSrcColStep;

        private dng_stream fStream;

        private HuffmanTable[] huffTable = new HuffmanTable[4] {
        new HuffmanTable(),
        new HuffmanTable(),
        new HuffmanTable(),
        new HuffmanTable()};

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

            /*uint32[][]*/ freqCount = new uint32[4][] {
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
            Array.Copy(bits, htbl.bits, htbl.bits.Length);

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

                EmitDht((int)i);

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
        List<byte> data = new List<byte>();

        public void Put_uint8(byte toAdd)
        {
            data.Add(toAdd);
        }

        public byte[] toByteArray()
        {
            return data.ToArray();
        }

        // for the decoder:

        // Create stream with data already in it.
        public dng_stream()
        {

        }
        public dng_stream(byte[] dataIn)
        {
            data.AddRange(dataIn);
        }

        private UInt64 position =0;

        public uint8 Get_uint8()
        {
            if ((int)position >= data.Count) {
                throw new Exception("dng_error_end_of_file");
            }
            return data[(int)position++];
        }

        public UInt64 Position()
        {
            return position;
        }

        public void SetReadPosition(UInt64 newPosition)
        {
            position = newPosition;
        }

        public void Skip(UInt64 length)
        {
            // Not sure about this one!
            position += length;
        }
    }
}

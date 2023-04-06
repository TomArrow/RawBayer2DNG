﻿using BitMiracle.LibTiff.Classic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JpegLibrary;

namespace RawBayer2DNG.ImageSequenceSources
{
    class CRISequenceSource : ImageSequenceSource, ImageSequenceSourceCountable
    {


        // The following license is for the content copied (and slightly modified) from the Blackmagic Cintel SDK and for all references to it in this file. There will be a comment indicating the start and end of the Cintel SDK content.
        // The references to it in the code are obvious and self-explanatory.
        /* -LICENSE-START-
         ** Copyright (c) 2018 Blackmagic Design
         **
         ** Permission is hereby granted, free of charge, to any person or organization
         ** obtaining a copy of the software and accompanying documentation covered by
         ** this license (the "Software") to use, reproduce, display, distribute,
         ** execute, and transmit the Software, and to prepare derivative works of the
         ** Software, and to permit third-parties to whom the Software is furnished to
         ** do so, all subject to the following:
         **
         ** The copyright notices in the Software and this entire statement, including
         ** the above license grant, this restriction and the following disclaimer,
         ** must be included in all copies of the Software, in whole or in part, and
         ** all derivative works of the Software, unless such copies or derivative
         ** works are solely in the form of machine-executable object code generated by
         ** a source language processor.
         **
         ** THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
         ** IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
         ** FITNESS FOR A PARTICULAR PURPOSE, TITLE AND NON-INFRINGEMENT. IN NO EVENT
         ** SHALL THE COPYRIGHT HOLDERS OR ANYONE DISTRIBUTING THE SOFTWARE BE LIABLE
         ** FOR ANY DAMAGES OR OTHER LIABILITY, WHETHER IN CONTRACT, TORT OR OTHERWISE,
         ** ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
         ** DEALINGS IN THE SOFTWARE.
         ** -LICENSE-END-
         */
        // Start of Blackmagic Cintel SDK content
        enum Key : UInt32
        {
            Header = 1,
            FrameInfo = 100,        // [ width, height, ColorModel, Endianness ]
            CodecType = 101,
            CodecName = 102,        // used since Resolve 14.3 (Cintel 2.1) for compressed RAW files
            FrameData = 103,        // pixel data (can be uncompressed or JPEG compressed)
            Negative = 104,     // obsolete, replaced by FilmType
            FlipHorizontal = 105,
            FlipVertical = 106,
            FilmFrameRate = 107,
            FilmType = 108,
            LinearMask = 109,       // 3 x 3
            TimeCode = 110,
            FilmGauge = 111,
            LogMask = 112,      // 3 x 3
            OffsetDetectedH = 113,      // integer stabilization in horizontal direction
            OffsetDetectedV = 114,      // integer stabilization in vertical direction
            ExtendedRange = 115,
            Keykode = 116,
            StabilizerEnabledH = 117,
            StabilizerEnabledV = 118,
            TileSizes = 119,        // 1 x 4, used since Resolve 14.3 (Cintel 2.1) for compressed RAW files
            Gains = 120,        // 1 x 3
            Lifts = 121,        // 1 x 3
            HDRGains = 122,     // 1 x 3
            OffsetToApplyH = 123,      // floating point sub-pixel stabilization in horizontal direction
            OffsetToApplyV = 124,      // floating point sub-pixel stabilization in vertical direction
            SkewToApply = 125,      // floating point skew
            Filler = 1000,      // used by applications that require frame data to be aligned on a particular boundary
        };
        enum ColorModel
        {
            COLOR_MODEL_BAYER_GRGR_CINTEL_10 = 45,

            COLOR_MODEL_BAYER_BGGR_CINTEL_12 = 76,
            COLOR_MODEL_BAYER_GBGB_CINTEL_12 = 77,
            COLOR_MODEL_BAYER_RGRG_CINTEL_12 = 78,
            COLOR_MODEL_BAYER_GRGR_CINTEL_12 = 79,

            COLOR_MODEL_BAYER_BGGR_CINTEL_16 = 88,
            COLOR_MODEL_BAYER_GBGB_CINTEL_16 = 89,
            COLOR_MODEL_BAYER_RGRG_CINTEL_16 = 90,
            COLOR_MODEL_BAYER_GRGR_CINTEL_16 = 91,
        };
        enum ContainerCodecType
        {
            CODEC_TYPE_NONE = 0,
            CODEC_TYPE_VIDEO
        };
        enum FilmType
        {
            Positive = 0,
            Negative = 1,
            InterPositive = 2,
            InterNegative = 3
        };

        enum FilmGauge
        {
            Gauge16mm = 0,
            Gauge35mm2Perf = 1,
            Gauge35mm3Perf = 2,
            Gauge35mm4Perf = 3
        };
        enum Endianness
        {
            ENDIAN_BIG = 0,
            ENDIAN_LITTLE
        };
        // End of Blackmagic Cintel SDK content

        public int width;
        public int height;
        public byte[,] bayerPattern;
        public string[] paths;
        public RAWDATAFORMAT rawDataFormat;

        private bool mustDo10bitUnpack = false;

        ImageSequenceSourceType sourceType = ImageSequenceSourceType.CRI;

        public CRISequenceSource(string[] thePaths)
        {
            byte[] tmpValue;
            paths = thePaths;
            Dictionary<UInt32, byte[]> tagData = readCRITagData(paths[0]);

            bool isInvalid = false;
            if (tagData.ContainsKey((UInt32)Key.FrameInfo))
            {
                tmpValue = tagData[(UInt32)Key.FrameInfo];
                width = (int)BitConverter.ToUInt32(tmpValue, 0);
                height = (int)BitConverter.ToUInt32(tmpValue, 4);

                // Bayer pattern and bit depth.
                UInt32 colorModel = BitConverter.ToUInt32(tmpValue, 8);
                switch(colorModel)
                {

                    case (UInt32)ColorModel.COLOR_MODEL_BAYER_GRGR_CINTEL_10:
                        bayerPattern = new byte[2, 2] { { 1, 0 }, { 2, 1 } };
                        //throw new Exception("10 bit modes not supported (yet?)");
                        //rawDataFormat = RAWDATAFORMAT.BAYERRG12p;
                        mustDo10bitUnpack = true;
                        rawDataFormat = RAWDATAFORMAT.BAYER12BITBRIGHTCAPSULEDIN16BIT;
                        break;

                    // 12 bit modes
                    // NOTE: Untested so far!
                    case (UInt32)ColorModel.COLOR_MODEL_BAYER_BGGR_CINTEL_12:
                        bayerPattern = new byte[2, 2] { { 2, 1 }, { 1, 0 } };
                        rawDataFormat = RAWDATAFORMAT.BAYERRG12p;
                        break;
                    case (UInt32)ColorModel.COLOR_MODEL_BAYER_GBGB_CINTEL_12:
                        bayerPattern = new byte[2, 2] { { 1, 2 }, { 0, 1 } };
                        rawDataFormat = RAWDATAFORMAT.BAYERRG12p;
                        break;
                    case (UInt32)ColorModel.COLOR_MODEL_BAYER_RGRG_CINTEL_12:
                        bayerPattern = new byte[2,2] { {0, 1},{1 ,2} };
                        rawDataFormat = RAWDATAFORMAT.BAYERRG12p;
                        break;
                    case (UInt32)ColorModel.COLOR_MODEL_BAYER_GRGR_CINTEL_12:
                        bayerPattern = new byte[2, 2] { { 1, 0 }, { 2, 1 } };
                        rawDataFormat = RAWDATAFORMAT.BAYERRG12p;
                        break;

                    // 16 bit modes
                    case (UInt32)ColorModel.COLOR_MODEL_BAYER_BGGR_CINTEL_16:
                        bayerPattern = new byte[2, 2] { { 2, 1 }, { 1, 0 } };
                        rawDataFormat = RAWDATAFORMAT.BAYER12BITDARKCAPSULEDIN16BIT;
                        break;
                    case (UInt32)ColorModel.COLOR_MODEL_BAYER_GBGB_CINTEL_16:
                        bayerPattern = new byte[2, 2] { { 1, 2 }, { 0, 1 } };
                        rawDataFormat = RAWDATAFORMAT.BAYER12BITDARKCAPSULEDIN16BIT;
                        break;
                    case (UInt32)ColorModel.COLOR_MODEL_BAYER_RGRG_CINTEL_16:
                        bayerPattern = new byte[2,2] { {0, 1},{1 ,2} };
                        rawDataFormat = RAWDATAFORMAT.BAYER12BITDARKCAPSULEDIN16BIT;
                        break;
                    case (UInt32)ColorModel.COLOR_MODEL_BAYER_GRGR_CINTEL_16:
                        bayerPattern = new byte[2, 2] { { 1, 0 }, { 2, 1 } };
                        rawDataFormat = RAWDATAFORMAT.BAYER12BITDARKCAPSULEDIN16BIT;
                        break;

                    default:
                        break;
                }


            } else
            {
                isInvalid = true;
            }

            /*
            if (tagData.ContainsKey((UInt32)Key.CodecType))
            {
                tmpValue = tagData[(UInt32)Key.CodecType];
                UInt32 codecType = BitConverter.ToUInt32(tmpValue, 0);
                if(codecType != (UInt32)ContainerCodecType.CODEC_TYPE_NONE)
                {
                    throw new Exception("Only uncompressed CRI files supported"); // Actually, this will not detect compression reliably! So currently this whole code doesn't work yet.
                }
            }
            else
            {
                isInvalid = true;
            }*/

            if (isInvalid)
            {
                throw new Exception("No width/height tag data found in CRI file");
            }
        }


        private Dictionary<UInt32, byte[]> readCRITagData(string path)
        {
            Dictionary<UInt32, byte[]> tagData = new Dictionary<UInt32, byte[]>();
            UInt32 currentTag;
            UInt32 currentTagLength;
            byte[] currentTagData;
            using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                /*while (reader.BaseStream.Position < (reader.BaseStream.Length-1))
                {

                    currentTag = reader.ReadUInt32();
                    currentTagLength = reader.ReadUInt32();
                    currentTagData = reader.ReadBytes((int)currentTagLength);
                    if(currentTag == 0)
                    {
                        continue;
                    }
                    tagData.Add(currentTag, currentTagData);
                }*/
                while (reader.BaseStream.Position < (reader.BaseStream.Length - 3))
                {

                    currentTag = reader.ReadUInt32();
                    if (currentTag == 0)
                    {
                        continue;
                    }
                    currentTagLength = reader.ReadUInt32();
                    currentTagData = reader.ReadBytes((int)currentTagLength);
                    tagData.Add(currentTag, currentTagData);
                }
            }
            return tagData;
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

        byte[] compressedFileCache;
        int compressedFileCacheIndex = -1;


        // The following 10 bit unpack function (unpack_10bit) is adapted from ffmpeg:
        /*
         * CRI image decoder
         *
         * Copyright (c) 2020 Paul B Mahol
         *
         * This file is part of FFmpeg.
         *
         * FFmpeg is free software; you can redistribute it and/or
         * modify it under the terms of the GNU Lesser General Public
         * License as published by the Free Software Foundation; either
         * version 2.1 of the License, or (at your option) any later version.
         *
         * FFmpeg is distributed in the hope that it will be useful,
         * but WITHOUT ANY WARRANTY; without even the implied warranty of
         * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
         * Lesser General Public License for more details.
         *
         * You should have received a copy of the GNU Lesser General Public
         * License along with FFmpeg; if not, write to the Free Software
         * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA
         */
        //private static void unpack_10bit(GetByteContext* gb, uint16_t* dst, int shift,int w, int h, ptrdiff_t stride)
        public static byte[] unpack_10bit(byte[] input,int w, int h, int shift=4/*, int stride*/)
        {
            UInt16[] dst = new ushort[w*h];
            byte[] output = new byte[dst.Length*2];
            int stride = w;

            int count = w * h;
            int pos = 0;

            int readByteOffset = 0;
            int dstOffset = 0;

            while (count > 0)
            {
                UInt32 a0, a1, a2, a3;
                //if (bytestream2_get_bytes_left(gb) < 4)
                if (readByteOffset >= (input.Length-16))
                    break;
                a0 = BitConverter.ToUInt32(input,readByteOffset); readByteOffset += 4;//bytestream2_get_le32(gb); 
                a1 = BitConverter.ToUInt32(input, readByteOffset); readByteOffset += 4;//bytestream2_get_le32(gb);
                a2 = BitConverter.ToUInt32(input, readByteOffset); readByteOffset += 4;//bytestream2_get_le32(gb);
                a3 = BitConverter.ToUInt32(input, readByteOffset); readByteOffset += 4;//bytestream2_get_le32(gb);
                dst[pos + dstOffset] = (UInt16)((((a0 >> 1) & 0xE00) | (a0 & 0x1FF)) << shift);
                pos++;
                if (pos >= w)
                {
                    if (count == 1)
                        break;
                    dstOffset += stride;
                    pos = 0;
                }
                dst[pos + dstOffset] = (UInt16)((((a0 >> 13) & 0x3F) | ((a0 >> 14) & 0xFC0)) << shift);
                pos++;
                if (pos >= w)
                {
                    if (count == 2)
                        break;
                    dstOffset += stride;
                    pos = 0;
                }
                dst[pos + dstOffset] = (UInt16)((((a0 >> 26) & 7) | ((a1 & 0x1FF) << 3)) << shift);
                pos++;
                if (pos >= w)
                {
                    if (count == 3)
                        break;
                    dstOffset += stride;
                    pos = 0;
                }
                dst[pos + dstOffset] = (UInt16)((((a1 >> 10) & 0x1FF) | ((a1 >> 11) & 0xE00)) << shift);
                pos++;
                if (pos >= w)
                {
                    if (count == 4)
                        break;
                    dstOffset += stride;
                    pos = 0;
                }
                dst[pos + dstOffset] = (UInt16)((((a1 >> 23) & 0x3F) | ((a2 & 0x3F) << 6)) << shift);
                pos++;
                if (pos >= w)
                {
                    if (count == 5)
                        break;
                    dstOffset += stride;
                    pos = 0;
                }
                dst[pos + dstOffset] = (UInt16)((((a2 >> 7) & 0xFF8) | ((a2 >> 6) & 7)) << shift);
                pos++;
                if (pos >= w)
                {
                    if (count == 6)
                        break;
                    dstOffset += stride;
                    pos = 0;
                }
                dst[pos + dstOffset] = (UInt16)((((a3 & 7) << 9) | ((a2 >> 20) & 0x1FF)) << shift);
                pos++;
                if (pos >= w)
                {
                    if (count == 7)
                        break;
                    dstOffset += stride;
                    pos = 0;
                }
                dst[pos + dstOffset] = (UInt16)((((a3 >> 4) & 0xFC0) | ((a3 >> 3) & 0x3F)) << shift);
                pos++;
                if (pos >= w)
                {
                    if (count == 8)
                        break;
                    dstOffset += stride;
                    pos = 0;
                }
                dst[pos+ dstOffset] = (UInt16)((((a3 >> 16) & 7) | ((a3 >> 17) & 0xFF8)) << shift);
                pos++;
                if (pos >= w)
                {
                    if (count == 9)
                        break;
                    dstOffset += stride;
                    pos = 0;
                }

                count -= 9;
            }
            Buffer.BlockCopy(dst, 0, output, 0, output.Length);
            return output;
        }

        // fast(er?) access to stabilization data
        public float[] getStabilizationInfo(int index)
        {
            float[] stabilizationInfo = new float[2] { 0f, 0f }; //H,V
            string path = paths[index];
            UInt32 currentTag;
            UInt32 currentTagLength;
            byte[] currentTagData;
            bool Hfound = false;
            bool Vfound = false;
            using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                while ((!Hfound || !Vfound)  && reader.BaseStream.Position < (reader.BaseStream.Length - 3))
                {

                    currentTag = reader.ReadUInt32();
                    if (currentTag == 0)
                    {
                        continue;
                    }
                    currentTagLength = reader.ReadUInt32();
                    if((Key)currentTag == Key.OffsetToApplyH)
                    {

                        currentTagData = reader.ReadBytes((int)currentTagLength);
                        stabilizationInfo[0] = BitConverter.ToSingle(currentTagData,0);
                        Hfound = true;

                    } else if ((Key)currentTag == Key.OffsetToApplyV)
                    {

                        currentTagData = reader.ReadBytes((int)currentTagLength);
                        stabilizationInfo[1] = BitConverter.ToSingle(currentTagData, 0);
                        Vfound = true;
                    }
                    else
                    {

                        reader.BaseStream.Seek(currentTagLength, SeekOrigin.Current); // Skip it!
                    }
                }
            }
            return stabilizationInfo;
        }

        private byte[] CRITagDataBackToBinary(ref Dictionary<UInt32, byte[]> criTagData, UInt32[] tagsToExclude = null)
        {
            List<byte> retVal  = new List<byte>();

            if(tagsToExclude == null)
            {
                tagsToExclude = new UInt32[0];
            }

            foreach(KeyValuePair<UInt32,byte[]> kvPair in criTagData)
            {
                // Ignore exluded keys
                bool excludeThis = false;
                foreach(UInt32 excludedTag in tagsToExclude)
                {
                    if(excludedTag == kvPair.Key)
                    {
                        excludeThis = true;
                    }
                }
                if (excludeThis)
                {
                    continue;
                }

                retVal.AddRange(BitConverter.GetBytes((UInt32)kvPair.Key));
                retVal.AddRange(BitConverter.GetBytes((UInt32)kvPair.Value.Length));
                retVal.AddRange(kvPair.Value);
            }

            return retVal.ToArray();
        }

        private string getHumanReadableTagData(ref Dictionary<UInt32, byte[]> tags)
        {

            string genericSeparator = ",";

            StringBuilder sb = new StringBuilder();
            //sb.AppendLine("tagname/number,interpreted,hex");
            sb.AppendLine("tagname/number,interpreted");
            foreach (KeyValuePair<UInt32, byte[]> tag in tags)
            {
                if ((Key)tag.Key != Key.FrameData && (Key)tag.Key != Key.Filler)
                {


                    sb.Append((Key)tag.Key);
                    sb.Append(",");
                    sb.Append('"');

                    string stuff;

                    switch ((Key)tag.Key)
                    {
                        case Key.FrameInfo:
                            int width = (int)BitConverter.ToUInt32(tag.Value, 0);
                            int height = (int)BitConverter.ToUInt32(tag.Value, 4);
                            ColorModel colorModel = (ColorModel)BitConverter.ToUInt32(tag.Value, 8);
                            Endianness endianness = (Endianness)BitConverter.ToUInt32(tag.Value, 12);
                            sb.Append(width + "x" + height + genericSeparator + colorModel + genericSeparator + endianness);
                            break;

                        // Bool
                        case Key.ExtendedRange: // Unverified
                        case Key.StabilizerEnabledH:
                        case Key.StabilizerEnabledV:
                        case Key.FlipHorizontal:
                        case Key.FlipVertical:
                        case Key.Negative: // Unverified
                            stuff = Helpers.byteArrayToString<bool>(tag.Value, genericSeparator);
                            sb.Append(stuff);
                            break;
                        // UInt64
                        case Key.Keykode: // Unverified
                        case Key.TileSizes:
                            stuff = Helpers.byteArrayToString<UInt64>(tag.Value, genericSeparator);
                            sb.Append(stuff);
                            break;
                        // UInt16
                        case Key.OffsetDetectedH: // Unverified
                        case Key.OffsetDetectedV:// Unverified
                            stuff = Helpers.byteArrayToString<UInt16>(tag.Value, genericSeparator);
                            sb.Append(stuff);
                            break;

                        // String
                        case Key.Header:
                        case Key.CodecName:
                        case Key.TimeCode:
                            stuff = System.Text.Encoding.Default.GetString(tag.Value);
                            sb.Append(stuff);
                            break;

                        // Specialized
                        case Key.CodecType:
                            stuff = ((ContainerCodecType)BitConverter.ToUInt32(tag.Value,0)).ToString();
                            sb.Append(stuff);
                            break;
                        case Key.FilmType:
                            stuff = ((FilmType)tag.Value[0]).ToString();
                            sb.Append(stuff);
                            break;
                        case Key.FilmGauge:
                            stuff = ((FilmGauge)tag.Value[0]).ToString();
                            sb.Append(stuff);
                            break;

                        // floats
                        case Key.FilmFrameRate:
                        case Key.OffsetToApplyH:
                        case Key.OffsetToApplyV:
                        case Key.SkewToApply:
                        case Key.LinearMask:
                        case Key.LogMask:
                        case Key.Gains:
                        case Key.Lifts:
                        case Key.HDRGains:
                            //string stuff = Helpers.arrayToString<float>(Helpers.byteArrayTo<float>(tag.Value), genericSeparator);
                            stuff = Helpers.byteArrayToString<float>(tag.Value, genericSeparator);
                            sb.Append(stuff);
                            break;
                        default:
                            sb.Append("[no interpretation implemented]");
                            break;
                    }
                    sb.Append('"');
                    /*sb.Append(",");
                    
                    foreach (byte b in tag.Value)
                    {

                        sb.AppendFormat("{0:x2}", b);
                    }*/
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }
            

        override public byte[] getRawImageData(int index, ref ISSMetaInfo metaInfo, ref ISSErrorInfo errorInfo)
        {



            Dictionary<UInt32, byte[]> tagData = readCRITagData(paths[index]);

            metaInfo.addMeta(new ISSMeta(ISSMeta.MetaFormat.CRI_METADATA,
                CRITagDataBackToBinary(ref tagData, new UInt32[] { (UInt32)Key.FrameData, (UInt32)Key.Filler }),
                getHumanReadableTagData(ref tagData),
                getImageName(index)));

            if (tagData.ContainsKey((UInt32)Key.FrameData))
            {

                // Detect compression
                // Only horizontal tiles are supported so far. Assuming there is no vertical tiling.
                if (tagData.ContainsKey((UInt32)Key.TileSizes))
                {
                    byte[] decodedOutputBuffer = new byte[width*height*2];

                    byte[] tileSizeData = tagData[(UInt32)Key.TileSizes];
                    int tileCount = tileSizeData.Length / 8; // The tilesizes are saved as Uint64s I think, so dividing by 8 should give the right number.

                    UInt64 totalSizeFromTileSizes = 0;
                    UInt64[] tileSizes = new UInt64[tileCount];
                    for(int i = 0; i < tileCount; i++)
                    {
                        tileSizes[i] = BitConverter.ToUInt64(tileSizeData,i*8);
                        totalSizeFromTileSizes += tileSizes[i];
                    }

                    byte[] compressedData = tagData[(UInt32)Key.FrameData];


                    //JpegDecoder jpegLibraryDecoder = new JpegDecoder();
                    //ReadOnlyMemory<byte> rawTileReadOnlyMemory;

                    byte[] tmpBuffer;
                    UInt64 alreadyRead = 0;
                    UInt32 horizOffset = 0;
                    foreach(UInt64 tileSize in tileSizes)
                    {
                        tmpBuffer = new byte[tileSize];

                        // Catch damaged CRI files:
                        // 
                        bool isDamaged = false;
                        if(alreadyRead + tileSize <= (ulong)compressedData.Length) { 
                            Array.Copy(compressedData, (int)alreadyRead, tmpBuffer,0,(int)tileSize);
                        } else if (alreadyRead>((ulong)compressedData.Length-1)) // See if we can get anything at all out of this...
                        {
                            errorInfo.addError(new ISSError(ISSError.ErrorCode.ORIGINAL_FILE_PARTIALLY_CORRUPTED_RESCUE,ISSError.ErrorSeverity.SEVERE,getImageName(index),"Source CRI file corrupted! Image info for tile starting at "+alreadyRead+" completely missing. Ignoring, but output image will be obviously missing that part.",new byte[0]));
                            // completely broken. No use messing with this file anymore. 
                            break;
                        } else
                        { // If there's a little bit of stuff, try rescuing what we can.
                            errorInfo.addError(new ISSError(ISSError.ErrorCode.ORIGINAL_FILE_PARTIALLY_CORRUPTED_RESCUE, ISSError.ErrorSeverity.SEVERE, getImageName(index), "Source CRI file corrupted! Image info for tile starting at " + alreadyRead + " partially missing. Attempting to rescue what's left.", new byte[0]));

                            ulong amountToCopy = (ulong)compressedData.Length - alreadyRead;
                            isDamaged = true;
                            Array.Copy(compressedData, (int)alreadyRead, tmpBuffer, 0, (int)amountToCopy);
                        }

                        alreadyRead += tileSize;

                        //rawTileReadOnlyMemory = new ReadOnlyMemory<byte>(tmpBuffer);
                        //jpegLibraryDecoder.SetInput(rawTileReadOnlyMemory);
                        //jpegLibraryDecoder.SetFrameHeader()
                        //jpegLibraryDecoder.Identify(); // fails to identify. missing markers or whatever: Failed to decode JPEG data at offset 91149. No marker found.'


                        dng_stream stream = new dng_stream(tmpBuffer);
                        dng_spooler spooler = new dng_spooler();

                        uint tileWidth = 0, tileHeight = 0;

                        if (isDamaged)
                        {
                            // Be careful if damaged. 
                            try
                            {

                                DNGLosslessDecoder.DecodeLosslessJPEGProper(stream, spooler, ref tileWidth, ref tileHeight, false);
                            }
                            catch (Exception e)
                            {
                                errorInfo.addError(new ISSError(ISSError.ErrorCode.ORIGINAL_FILE_PARTIALLY_CORRUPTED_RESCUE, ISSError.ErrorSeverity.SEVERE, getImageName(index), "Source CRI file corrupted! A rescue of remaining data was attempted but possibly failed with error: "+e.Message, new byte[0]));

                            }
                        } else
                        {

                            DNGLosslessDecoder.DecodeLosslessJPEGProper(stream, spooler, ref tileWidth, ref tileHeight, false);
                        }

                         

                        uint tileActualWidth = tileWidth / 2;
                        uint tileActualHeight = tileHeight * 2;
                        //byte[] tileBuff = new byte[jpegLibraryDecoder.Width * jpegLibraryDecoder.Height * 2];
                        byte[] tileBuff = spooler.toByteArray();

                        if (isDamaged)
                        {
                            tileBuff = new byte[tileActualWidth * tileActualHeight * 2];
                            byte[] tileBuffTmp = spooler.toByteArray();
                            Array.Copy(tileBuffTmp,0,tileBuff,0,tileBuffTmp.Length);
                        } else
                        {
                            tileBuff = spooler.toByteArray();
                        }

                        int actualX;
                        for (int y = 0; y < tileActualHeight; y++)
                        {
                            for (int x = 0; x < tileActualWidth; x++)
                            {
                                actualX = (Int32)horizOffset + x;
                                decodedOutputBuffer[y * width * 2 + actualX * 2] = tileBuff[y*tileActualWidth*2+(x ) *2];
                                decodedOutputBuffer[y * width * 2 + actualX * 2+1] = tileBuff[y*tileActualWidth*2+ (x) * 2+1];

                            }
                        }

                        horizOffset += (uint)tileActualWidth;
                    }
                    //File.WriteAllBytes("decoded raw cri"+width + " "+ height + ".raw", decodedOutputBuffer);
                    return decodedOutputBuffer;
                } else
                {

                    // Presuming uncompressed
                    if (mustDo10bitUnpack)
                    {
                        return unpack_10bit(tagData[(UInt32)Key.FrameData],width,height);
                    } else { 
                        return tagData[(UInt32)Key.FrameData];
                    }
                }

                //File.WriteAllBytes("rawcri.jpg", tagData[(UInt32)Key.FrameData]);
                
            }
            else
            {
                throw new Exception("CRI file contains no image data apparently.");
            }
        }
        /* old version:
        override public byte[] getRawImageData(int index)
        {

            Dictionary<UInt32, byte[]> tagData = readCRITagData(paths[0]);

            if (tagData.ContainsKey((UInt32)Key.FrameData))
            {

                // Detect compression
                // Only horizontal tiles are supported so far. Assuming there is no vertical tiling.
                if (tagData.ContainsKey((UInt32)Key.TileSizes))
                {
                    byte[] decodedOutputBuffer = new byte[width*height*2];

                    byte[] tileSizeData = tagData[(UInt32)Key.TileSizes];
                    int tileCount = tileSizeData.Length / 8; // The tilesizes are saved as Uint64s I think, so dividing by 8 should give the right number.

                    UInt64 totalSizeFromTileSizes = 0;
                    UInt64[] tileSizes = new UInt64[tileCount];
                    for(int i = 0; i < tileCount; i++)
                    {
                        tileSizes[i] = BitConverter.ToUInt64(tileSizeData,i*8);
                        totalSizeFromTileSizes += tileSizes[i];
                    }

                    byte[] compressedData = tagData[(UInt32)Key.FrameData];


                    JpegDecoder jpegLibraryDecoder = new JpegDecoder();
                    ReadOnlyMemory<byte> rawTileReadOnlyMemory;

                    byte[] tmpBuffer;
                    UInt64 alreadyRead = 0;
                    UInt32 horizOffset = 0;
                    foreach(UInt64 tileSize in tileSizes)
                    {
                        tmpBuffer = new byte[tileSize];

                        Array.Copy(compressedData, (int)alreadyRead, tmpBuffer,0,(int)tileSize);
                        alreadyRead += tileSize;

                        rawTileReadOnlyMemory = new ReadOnlyMemory<byte>(tmpBuffer);
                        jpegLibraryDecoder.SetInput(rawTileReadOnlyMemory);
                        //jpegLibraryDecoder.SetFrameHeader()
                        jpegLibraryDecoder.Identify(); // fails to identify. missing markers or whatever: Failed to decode JPEG data at offset 91149. No marker found.'

                        int tileActualWidth = jpegLibraryDecoder.Width/2;
                        int tileActualHeight = jpegLibraryDecoder.Height*2;
                        byte[] tileBuff = new byte[jpegLibraryDecoder.Width * jpegLibraryDecoder.Height * 2];
                        jpegLibraryDecoder.SetOutputWriter(new JpegDecode.JpegBufferOutputWriterGreaterThan8BitCRI(jpegLibraryDecoder.Width, jpegLibraryDecoder.Height, jpegLibraryDecoder.Precision-1, 1, tileBuff, 16));
                        jpegLibraryDecoder.Decode();

                        int actualX;
                        for (int y = 0; y < tileActualHeight; y++)
                        {
                            for (int x = 0; x < tileActualWidth; x++)
                            {
                                actualX = (Int32)horizOffset + x;
                                decodedOutputBuffer[y * width * 2 + actualX * 2] = tileBuff[y*tileActualWidth*2+(x ) *2];
                                decodedOutputBuffer[y * width * 2 + actualX * 2+1] = tileBuff[y*tileActualWidth*2+ (x) * 2+1];

                            }
                        }

                        horizOffset += (uint)tileActualWidth;
                    }
                    File.WriteAllBytes("decoded raw cri"+width + " "+ height + ".raw", decodedOutputBuffer);
                    return decodedOutputBuffer;
                } else
                {

                    // Presuming uncompressed
                    return tagData[(UInt32)Key.FrameData];
                }

                //File.WriteAllBytes("rawcri.jpg", tagData[(UInt32)Key.FrameData]);
                
            }
            else
            {
                throw new Exception("CRI file contains no image data apparently.");
            }
        }
        */
        public override bool imageExists(int index)
        {
            if (index < paths.Length)
            {
                return File.Exists(paths[index]);
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
                return paths[index];
            }
            else
            {
                return "undefined file [index " + index + "]";
            }
        }
        public int getImageCount()
        {
            return paths.Length;
        }
    }
}

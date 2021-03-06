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
    class CRISequenceSource : ImageSequenceSource
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
        // End of Blackmagic Cintel SDK content

        public int width;
        public int height;
        public byte[,] bayerPattern;
        public string[] paths;
        public RAWDATAFORMAT rawDataFormat;

        ImageSequenceSourceType sourceType = ImageSequenceSourceType.DNG;

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
                        throw new Exception("10 bit modes not supported (yet?)");
                        //rawDataFormat = RAWDATAFORMAT.BAYERRG12p;
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
                        rawDataFormat = RAWDATAFORMAT.BAYER12BITBRIGHTCAPSULEDIN16BIT;
                        break;
                    case (UInt32)ColorModel.COLOR_MODEL_BAYER_GBGB_CINTEL_16:
                        bayerPattern = new byte[2, 2] { { 1, 2 }, { 0, 1 } };
                        rawDataFormat = RAWDATAFORMAT.BAYER12BITBRIGHTCAPSULEDIN16BIT;
                        break;
                    case (UInt32)ColorModel.COLOR_MODEL_BAYER_RGRG_CINTEL_16:
                        bayerPattern = new byte[2,2] { {0, 1},{1 ,2} };
                        rawDataFormat = RAWDATAFORMAT.BAYER12BITBRIGHTCAPSULEDIN16BIT;
                        break;
                    case (UInt32)ColorModel.COLOR_MODEL_BAYER_GRGR_CINTEL_16:
                        bayerPattern = new byte[2, 2] { { 1, 0 }, { 2, 1 } };
                        rawDataFormat = RAWDATAFORMAT.BAYER12BITBRIGHTCAPSULEDIN16BIT;
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
            using (BinaryReader reader = new BinaryReader(File.Open(paths[0], FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                while (reader.BaseStream.Position < (reader.BaseStream.Length-1))
                {

                    currentTag = reader.ReadUInt32();
                    currentTagLength = reader.ReadUInt32();
                    currentTagData = reader.ReadBytes((int)currentTagLength);
                    if(currentTag == 0)
                    {
                        continue;
                    }
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
        override public int getImageCount()
        {
            return paths.Length;
        }
    }
}

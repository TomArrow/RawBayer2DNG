using BitMiracle.LibTiff.Classic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JpegLibrary;

namespace RawBayer2DNG.ImageSequenceSources
{
    class DNGSequenceSource : ImageSequenceSource
    {

        public const TiffTag TIFFTAG_CFAREPEATPATTERNDIM = (TiffTag)33421;
        public const TiffTag TIFFTAG_CFAPATTERN = (TiffTag)33422;
        public const TiffTag TIFFTAG_SUBIFDS = (TiffTag)330;

        public int width;
        public int height;
        public byte[,] bayerPattern;
        public string[] paths;
        public RAWDATAFORMAT rawDataFormat;

        ImageSequenceSourceType sourceType = ImageSequenceSourceType.DNG;

        public DNGSequenceSource(string[] thePaths)
        {
            /*
            char[] bayerSubstitution = { "\x0"[0], "\x1"[0], "\x2"[0] };

            string bayerPatternTag = bayerSubstitution[bayerPattern[0, 0]].ToString() +
                                            bayerSubstitution[bayerPattern[0, 1]] + bayerSubstitution[bayerPattern[1, 0]] +
                                            bayerSubstitution[bayerPattern[1, 1]];
            */
            paths = thePaths;
            using (Tiff input = Tiff.Open(thePaths[0], "r"))
            {


                // Try to make Adobe DNG work
                bool subIFDTagExists = input.GetField(TIFFTAG_SUBIFDS) != null;
                if (subIFDTagExists)
                {

                    UInt64 offsetOfSubIFD = (UInt64)input.GetField(TIFFTAG_SUBIFDS)[1].TolongArray()[0];
                    input.SetSubDirectory((long)offsetOfSubIFD);
                }


                width = input.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
                height = input.GetField(TiffTag.IMAGELENGTH)[0].ToInt();

                // For debugging
                int[] tagList = new int[input.GetTagListCount()];
                for(int i = 0; i < tagList.Length; i++)
                {
                    tagList[i] = input.GetTagListEntry(i);
                }



                byte[] bayerTiffBytes = input.GetField(TIFFTAG_CFAPATTERN)[0].ToByteArray();
                // We can probably get away with not doing any substitution because we follow the same logic for the bayer pattern values, with 0=red, 1=green, 2=blue
                bayerPattern = new byte[2, 2] { {bayerTiffBytes[0], 
                                                bayerTiffBytes[1] },{
                                                bayerTiffBytes[2],
                                                bayerTiffBytes[3] } };
                int bitDepth = input.GetField(TiffTag.BITSPERSAMPLE)[0].ToInt();
                float baselineExposure = input.GetField(TiffTag.BASELINEEXPOSURE) == null ? 0 : input.GetField(TiffTag.BASELINEEXPOSURE)[0].ToFloat();

                if (bitDepth == 12)
                {
                    rawDataFormat = RAWDATAFORMAT.TIFF12BITPACKED;
                } else if(bitDepth == 16 && (int)Math.Round(baselineExposure) == 4)
                {
                    rawDataFormat = RAWDATAFORMAT.BAYER12BITDARKCAPSULEDIN16BIT;
                } else if(bitDepth == 16 && (int)Math.Round(baselineExposure) == 0)
                {
                    rawDataFormat = RAWDATAFORMAT.BAYER12BITBRIGHTCAPSULEDIN16BIT;
                }

            }
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
            using (Tiff input = Tiff.Open(paths[index], "r"))
            {
                // Try to make Adobe DNG work
                bool subIFDTagExists = input.GetField(TIFFTAG_SUBIFDS) != null;
                if (subIFDTagExists)
                {

                    UInt64 offsetOfSubIFD = (UInt64)input.GetField(TIFFTAG_SUBIFDS)[1].TolongArray()[0];
                    input.SetSubDirectory((long)offsetOfSubIFD);
                }

                if (input.IsTiled())
                {

                    if(compressedFileCacheIndex == index)
                    {
                        return compressedFileCache;
                    }

                    int tileWidth = input.GetField(TiffTag.TILEWIDTH)[0].ToInt();
                    int tileHeight = input.GetField(TiffTag.TILELENGTH)[0].ToInt();
                    byte[] buffer = new byte[width*height*2];
                    long tileSize = input.TileSize();
                    byte[] tileBuff = new byte[tileSize ];

                    JpegDecoder jpegLibraryDecoder = new JpegDecoder();

                    long rawTileSize;
                    byte[] rawTileBuffer;
                    int tileIndex;
                    ReadOnlyMemory<byte> rawTileReadOnlyMemory;

                    int row, col, x, y;

                    for (row = 0; row < height; row += tileHeight)
                    {
                        for (col = 0; col < width; col += tileWidth)
                        {


                            // Read the tile
                            if (input.ReadTile(tileBuff, 0, col, row, 0, 0) < 0)
                            {
                                // This means the normal tile reading failed, so we try something else.
                                tileIndex = input.ComputeTile(col, row, 0, 0);
                                rawTileSize = input.RawTileSize(tileIndex);
                                rawTileBuffer = new byte[rawTileSize];
                                input.ReadRawTile(tileIndex, rawTileBuffer, 0, (int)rawTileSize);
                                rawTileReadOnlyMemory = new ReadOnlyMemory<byte>(rawTileBuffer);
                                jpegLibraryDecoder.SetInput(rawTileReadOnlyMemory);
                                //jpegLibraryDecoder.SetFrameHeader()
                                jpegLibraryDecoder.Identify(); // fails to identify. missing markers or whatever: Failed to decode JPEG data at offset 91149. No marker found.'
                                jpegLibraryDecoder.SetOutputWriter(new JpegDecode.JpegBufferOutputWriterGreaterThan8Bit(tileWidth, tileHeight, jpegLibraryDecoder.Precision, 1, tileBuff));
                                jpegLibraryDecoder.Decode();
                                //throw new Exception("Error reading data");
                            }

                            int indexTileStuff = 0;

                            // Iterate the rows in the tile
                            for (y = row; y < row + tileHeight; y++)
                            {
                                if (y >= height) break;
                                for (x = col;x  < col + tileWidth; x++)
                                {
                                    if (x >= width) continue;

                                    buffer[y * width * 2 + x * 2] = tileBuff[(y-row) * tileWidth * 2 + (x-col) * 2];
                                    buffer[y * width * 2 + x * 2+1] = tileBuff[(y-row) * tileWidth * 2 + (x-col) * 2 +1];
                                }
                            }
                            
                            /*for (var i = 0; i < tileHeight && i + row < height; i++)
                            {
                                var length = tileWidth;

                                // Index of the first position in the row
                                var position = (row + i) * tileWidth + col;

                                // Check we are not outside the image
                                if (col + length > width)
                                {
                                    length = width - col;
                                }
                                for (var p = 0; p < length; p++)
                                {
                                    buffer[position + p] = buffer[indexTileStuff * 2 + p * 2];
                                }


                                index += tileWidth;

                            }*/
                        }
                    }

                    compressedFileCache = buffer;
                    compressedFileCacheIndex = index;

                    return buffer;
                } else
                {
                    byte[] buffer = new byte[input.StripSize() * input.NumberOfStrips()];
                    int bufferoffset = 0;
                    int stripsize = input.StripSize();
                    int stripcount = input.NumberOfStrips();
                    for (int i = 0; i < stripcount; i++)
                    {
                        int read = input.ReadEncodedStrip(i, buffer, bufferoffset, stripsize); // This throws an error with Adobe-created DNG files
                        if (read == -1)
                        {
                            throw new Exception("Error on decoding strip " + i + " of " + input.FileName());
                        }

                        bufferoffset += read;
                    }
                    return buffer;
                }
                
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

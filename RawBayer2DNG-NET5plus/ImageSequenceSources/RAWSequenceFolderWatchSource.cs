using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static RawBayer2DNG.ImageSequenceSource;

namespace RawBayer2DNG.ImageSequenceSources
{
    internal class RAWSequenceFolderWatchSource : ImageSequenceSource
    {
        public int width;
        public int height;
        public byte[,] bayerPattern;
        //public string[] paths;
        public string folderPath;
        public RAWDATAFORMAT rawDataFormat;
        bool endReached = false;

        ImageSequenceSourceType sourceType = ImageSequenceSourceType.RAW;

        List<string> rawImagePaths = new List<string>();
        HashSet<string> alreadyUsedImages = new HashSet<string>();
        Int64 highestIndex = -1;
        AutoResetEvent are = new AutoResetEvent(true);
        Int64 waitingThreads = 0;

        FileSystemWatcher fsw = null;

        public RAWSequenceFolderWatchSource(RAWDATAFORMAT theRawDataFormat, int theWidth, int theHeight, byte[,] theBayerPattern, string theFolderPath)
        {
            rawDataFormat = theRawDataFormat;
            width = theWidth;
            height = theHeight;
            bayerPattern = theBayerPattern;
            //paths = thePaths;
            folderPath = theFolderPath;

            fsw = new FileSystemWatcher();

            fsw.Path = folderPath;

            fsw.Created += fsw_created;

            fsw.EnableRaisingEvents = true;
        }
        //.raw_ready
        //.raw_alldone
        private void fsw_created(object sender, FileSystemEventArgs e)
        {
            string extension = Path.GetExtension(e.Name);
            string extensionLower = extension.ToLower();
            if (extensionLower == ".raw_ready")
            {
                string folder = Path.GetDirectoryName(e.FullPath);
                string basename = Path.GetFileNameWithoutExtension(e.Name);
                string actualFile = Path.Combine(folder,basename + extension.Replace("_ready", "",StringComparison.InvariantCultureIgnoreCase));
                if (!File.Exists(actualFile))
                {
                    throw new Exception($"Found {e.Name} but {actualFile} does not exist.");
                } else
                {
                    lock (rawImagePaths)
                    {
                        lock (alreadyUsedImages)
                        {
                            if (alreadyUsedImages.Add(actualFile)) // Quick check if it's already added
                            {
                                rawImagePaths.Add(actualFile);
                                Interlocked.Increment(ref highestIndex);
                            }
                        }
                    }
                    are.Set();
                }
            } else if (extensionLower == ".raw_alldone")
            {
                endReached = true; 
                are.Set();
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
        override public byte[] getRawImageData(int index, ref ISSMetaInfo metaInfo, ref ISSErrorInfo errorInfo)
        {
            if (imageExists(index))
            {
                lock (rawImagePaths)
                {
                    return File.ReadAllBytes(rawImagePaths[index]);
                }
            } else
            {
                return null;
            }
        }
        public override bool imageExists(int index)
        {
            // We block until either the end is reached or the index arrives
            while(!endReached && Interlocked.Read(ref highestIndex) < index)
            {
                Interlocked.Increment(ref waitingThreads);
                are.WaitOne();
                Interlocked.Decrement(ref waitingThreads);
                if(Interlocked.Read(ref waitingThreads) > 0 || endReached)
                {
                    are.Set(); // More threads might be waiting.
                }
            }
            if (index <= Interlocked.Read(ref highestIndex))
            {
                lock (rawImagePaths)
                {
                    return File.Exists(rawImagePaths[index]);
                }
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
                lock (rawImagePaths)
                {
                    return rawImagePaths[index];
                }
            }
            else
            {
                return "undefined file [index " + index + "]";
            }
        }
    }
}

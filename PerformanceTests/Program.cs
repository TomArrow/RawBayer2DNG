using System;
using System.ComponentModel;
using System.Net.WebSockets;
using OpenTK.Compute.OpenCL;
using OpenTK.Graphics.OpenGL;

namespace PerformanceTests // Note: actual namespace depends on the project name.
{


    static class OpenTKTests
    {
        public static byte[] convert16bitIntermediateTo12paddedto16bit(byte[] input)
        {
            int inputlengthBytes = input.Length;

            int tmpValue;
            for (long i = 0; i < inputlengthBytes; i += 2)
            {
                tmpValue = ((input[i] | input[i + 1] << 8) >> 4) & UInt16.MaxValue;// combine into one 16 bit int and shift 4 bits to the left
                input[i] = (byte)(tmpValue & byte.MaxValue);
                input[i + 1] = (byte)((tmpValue >> 8) & byte.MaxValue);
            }

            return input;
        }

        public static void exceptIfError(CLResultCode res, string errorMessage=null)
        {
            if(res != CLResultCode.Success)
            {
                throw new Exception($"Error with errorCode {res}. Details if available: {errorMessage}");
            }
        }

        static CLContext context;
        static CLProgram program;
        static CLKernel kernel;
        static CLDevice? deviceToUse = null;
        static CLBuffer buffer;
        static CLCommandQueue queue;

        public static void prepareTK(int Length, DeviceType deviceTypeToUse)
        {
            CLResultCode res;

            CLPlatform[] platforms;
            CL.GetPlatformIds(out platforms);


#if DEBUG
            Console.WriteLine("Devices:");
#endif
            foreach (CLPlatform platform in platforms)
            {


                byte[] platformName;
                CL.GetPlatformInfo(platform, PlatformInfo.Name, out platformName);
                string platformNameString = System.Text.Encoding.Default.GetString(platformName);

                CLDevice[] devices;
                CL.GetDeviceIds(platform, DeviceType.All, out devices);

                foreach (CLDevice device in devices)
                {
                    byte[] deviceName;
                    byte[] deviceType;
                    CL.GetDeviceInfo(device, DeviceInfo.Name, out deviceName);
                    CL.GetDeviceInfo(device, DeviceInfo.Type, out deviceType);
                    string deviceNameString = System.Text.Encoding.Default.GetString(deviceName);
                    UInt64 deviceTypeNum = BitConverter.ToUInt64(deviceType);//TODO Is this portable?
#if DEBUG
                    Console.Write(platformNameString);
                    Console.Write(": ");
                    Console.Write(deviceNameString);
                    Console.Write(" (");
                    Console.Write((DeviceType)deviceTypeNum);
                    Console.WriteLine(")");
#endif

                    if ((DeviceType)deviceTypeNum == deviceTypeToUse)
                    {
                        deviceToUse = device;
                    }
                }
            }

            if (deviceToUse == null)
            {
                throw new Exception("OpenCL device selection failure (no GPU found)");
            }

            context = CL.CreateContext(IntPtr.Zero, 1, new CLDevice[] { deviceToUse.Value }, IntPtr.Zero, IntPtr.Zero, out res);

            exceptIfError(res, "Error creating context");

            string kernelCode = @"
                    __kernel void convert16bitIntermediateTo12paddedto16bit_TK(__global uchar* input)
                    {
                        int gid = get_global_id(0);
                        int tmpValue = ((input[gid*2] | input[gid*2 + 1] << 8) >> 4) & 0xFFFF;
                        input[gid*2] = tmpValue & 0xFF;
                        input[gid*2 + 1] = (tmpValue >> 8) & 0xFF;
                        //input[gid*2] = input[gid*2]*2;
                        //input[gid*2+1] = input[gid*2+1]*2;
                    }
                ";

            program = CL.CreateProgramWithSource(context, kernelCode, out res);

            exceptIfError(res, "Error creating program");

            res = CL.BuildProgram(program, 1, new CLDevice[] { deviceToUse.Value }, "", 0, 0);

            //exceptIfError(res, "Error building program");

            if (res != CLResultCode.Success)
            {
                byte[] errorLog;
                CL.GetProgramBuildInfo(program, deviceToUse.Value, ProgramBuildInfo.Log, out errorLog);
                string errorLogString = System.Text.Encoding.Default.GetString(errorLog);
                Console.WriteLine(errorLogString);
                throw new Exception("OpenCL Kernel compilation failure");
            }

            kernel = CL.CreateKernel(program, "convert16bitIntermediateTo12paddedto16bit_TK", out res);

            exceptIfError(res, "Error creating kernel");

            //Console.WriteLine("Wtf it compiled?");

            buffer = CL.CreateBuffer(context, MemoryFlags.ReadWrite, (nuint)Length, IntPtr.Zero, out res);

            exceptIfError(res, "Error creating buffer");


            queue = CL.CreateCommandQueueWithProperties(context, deviceToUse.Value, IntPtr.Zero, out res);


            exceptIfError(res, "Error creating command queue");
        }

        public static byte[] convert16bitIntermediateTo12paddedto16bit_TK(byte[] input)
        {

            CLResultCode res;

            var watch = new System.Diagnostics.Stopwatch();

            byte[] resultDataBytes;


            CLEvent eventWhatever;

            watch.Start();
            res = CL.EnqueueWriteBuffer(queue, buffer, true, 0, input, null, out eventWhatever);
            watch.Stop();
            Console.WriteLine($"TK write buffer: {watch.Elapsed.TotalMilliseconds}");

            exceptIfError(res, "Error enqueueing buffer write.");

            watch.Restart();
            res = CL.SetKernelArg(kernel, 0, buffer);
            watch.Stop();
            Console.WriteLine($"TK set kernel arg: {watch.Elapsed.TotalMilliseconds}");

            exceptIfError(res, "Error setting kernel argument.");

            watch.Restart();
            res = CL.EnqueueNDRangeKernel(queue, kernel, 1, new nuint[] { 0 }, new nuint[] { (nuint)input.Length / 2 }, new nuint[] { 32 }, 0, null, out eventWhatever);
            watch.Stop();
            Console.WriteLine($"TK execute: {watch.Elapsed.TotalMilliseconds}");

            exceptIfError(res, "Error kernel execution.");

            watch.Restart();
            CL.Finish(queue);
            watch.Stop();
            Console.WriteLine($"TK finish: {watch.Elapsed.TotalMilliseconds}");

            watch.Restart();
            res = CL.EnqueueReadBuffer(queue, buffer, true, 0, input, null, out eventWhatever);
            watch.Stop();
            Console.WriteLine($"TK read buffer: {watch.Elapsed.TotalMilliseconds}");

            exceptIfError(res, "Error enqueueing buffer read.");

            return input;
        }
    }

    internal class Program
    {




        static void Main(string[] args)
        {

            UInt16[] testData = new UInt16[20000000];
            Random random = new Random();
            for(int i = 0; i < testData.Length; i++)
            {
                testData[i] = (ushort)random.Next(0, UInt16.MaxValue);
            }

            byte[] testDataBytes = new byte[testData.Length*sizeof(UInt16)];
            System.Buffer.BlockCopy(testData,0,testDataBytes,0,testDataBytes.Length);

            var watch = new System.Diagnostics.Stopwatch();

            byte[] resultDataBytes;

            watch.Start();
            OpenTKTests.prepareTK(testDataBytes.Length,DeviceType.Gpu);
            watch.Stop();
            Console.WriteLine($"TK Init: {watch.Elapsed.TotalMilliseconds}");

            watch.Restart();
            resultDataBytes = OpenTKTests.convert16bitIntermediateTo12paddedto16bit_TK((byte[])testDataBytes.Clone());
            watch.Stop();
            Console.WriteLine($"TK total: {watch.Elapsed.TotalMilliseconds}");

            watch.Restart();
            resultDataBytes = OpenTKTests.convert16bitIntermediateTo12paddedto16bit((byte[])testDataBytes.Clone());
            watch.Stop();
            Console.WriteLine($"CPU: {watch.Elapsed.TotalMilliseconds}");

            //UInt16[] resultData = new UInt16[testData.Length];
            //Buffer.BlockCopy(resultDataBytes, 0, resultData, 0, resultDataBytes.Length);
            Console.ReadKey();
        }
    }
}
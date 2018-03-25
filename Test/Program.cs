#pragma warning disable IDE0001
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using MemoryModuleSX;
using size_t = System.IntPtr;

namespace Test
{
    public static class MemoryModuleSXTest
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate size_t FastLzma2CompressProc(byte[] dst, size_t dstCapacity, byte[] src, size_t srcSize, int compressionLevel, uint nbThreads);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate size_t FastLzma2DecompressProc(byte[] dst, size_t dstCapacity, byte[] src, size_t compressedSize);

        private static FastLzma2CompressProc FastLzma2Compress;

        private static FastLzma2DecompressProc FastLzma2Decompress;

        public static void Test()
        {
            MemoryModule memoryModule;
            byte[] src;
            byte[] deced;

            using (BinaryReader binaryReader = new BinaryReader(Assembly.GetExecutingAssembly().GetManifestResourceStream($"Test.FLzma2_{(Environment.Is64BitProcess ? "64" : "32")}.dll")))
                memoryModule = MemoryModule.Create(binaryReader.ReadBytes((int)binaryReader.BaseStream.Length));
            FastLzma2Compress = memoryModule.GetProcDelegate<FastLzma2CompressProc>("FL2_compressMt");
            FastLzma2Decompress = memoryModule.GetProcDelegate<FastLzma2DecompressProc>("FL2_decompress");
            List<byte> byteList = new List<byte>();
            foreach (string filePath in Directory.EnumerateFiles(Environment.CurrentDirectory))
                byteList.AddRange(File.ReadAllBytes(filePath));
            src = byteList.ToArray();
            Compress(src);
            deced = Decompress((size_t)src.Length);
            Console.WriteLine(src.SequenceEqual(deced));
            GC.Collect();
            while (true)
                Thread.Sleep(int.MaxValue);
        }

        private static void Compress(byte[] src)
        {
            byte[] tmp;
            size_t size;
            byte[] dest;

            tmp = new byte[src.Length + 0x4000];
            size = FastLzma2Compress(tmp, (size_t)tmp.Length, src, (size_t)src.Length, 100, 0);
            dest = new byte[(ulong)size];
            Buffer.BlockCopy(tmp, 0, dest, 0, dest.Length);
            File.WriteAllBytes("enced", dest);
        }

        private static byte[] Decompress(size_t length)
        {
            byte[] src;
            byte[] dest;

            src = File.ReadAllBytes("enced");
            dest = new byte[(ulong)length];
            FastLzma2Decompress(dest, (size_t)dest.Length, src, (size_t)src.Length);
            return dest;
        }
    }
}
#pragma warning restore IDE0001

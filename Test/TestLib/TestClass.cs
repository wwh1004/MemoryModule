
using System;
using System.IO;
using System.Runtime.InteropServices;
using MemoryModules;
using size_t = System.IntPtr;

namespace TestLib
{
    public static unsafe class TestClass
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate size_t CompressProc(byte* dst, size_t dstCapacity, byte* src, size_t srcSize, int compressionLevel, uint nbThreads);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate size_t DecompressProc(byte* dst, size_t dstCapacity, byte* src, size_t compressedSize);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate uint IsErrorProc(size_t code);

        public static readonly byte[] Libflzma2_x64 = File.ReadAllBytes("libflzma2_x64.dll");

        public static readonly byte[] Libflzma2_x86 = File.ReadAllBytes("libflzma2_x86.dll");

        public static CompressProc Compress;

        public static DecompressProc Decompress;

        public static IsErrorProc IsError;

        static TestClass()
        {
            MemoryModule memoryModule;

            memoryModule = MemoryModule.Create(IntPtr.Size == 8 ? Libflzma2_x64 : Libflzma2_x86);
            Compress = memoryModule.GetProcDelegate<CompressProc>("FL2_compressMt");
            Decompress = memoryModule.GetProcDelegate<DecompressProc>("FL2_decompress");
            IsError = memoryModule.GetProcDelegate<IsErrorProc>("FL2_isError");
        }

        public static bool Test()
        {
            Random random;
            byte[] srcData;
            byte[] compressedData;
            byte[] decompressedData;

            random = new Random();
            srcData = new byte[random.Next(256 * 1024 * 1024, 1024 * 1024 * 1024)];
            random.NextBytes(srcData);
            compressedData = CompressData(srcData);
            decompressedData = DecompressData(compressedData, srcData.Length);
            return CompareData(srcData, decompressedData) == 0;
        }


        private static byte[] CompressData(byte[] data)
        {
            byte[] buffer;
            byte[] compressedData;

            buffer = new byte[(int)(data.Length * 1.05)];
            fixed (byte* pDest = buffer)
            fixed (byte* pSrc = data)
                compressedData = new byte[(int)Compress(pDest, (size_t)buffer.Length, pSrc, (size_t)data.Length, 12, 0)];
            Buffer.BlockCopy(buffer, 0, compressedData, 0, compressedData.Length);
            return compressedData;
        }

        private static byte[] DecompressData(byte[] compressedData, int rawSize)
        {
            byte[] data;

            data = new byte[rawSize];
            fixed (byte* pDest = data)
            fixed (byte* pSrc = compressedData)
                Decompress(pDest, (size_t)data.Length, pSrc, (size_t)compressedData.Length);
            return data;
        }

        private static int CompareData(byte[] left, byte[] right)
        {
            if (left == null)
                throw new ArgumentNullException(nameof(left));
            if (right == null)
                throw new ArgumentNullException(nameof(right));

            if (left.Length != right.Length)
                return left.Length - right.Length;
            for (int i = 0; i < left.Length; i++)
            {
                int difference;

                difference = left[i] - right[i];
                if (difference == 0)
                    continue;
                else
                    return difference;
            }
            return 0;
        }
    }
}

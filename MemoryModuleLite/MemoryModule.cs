using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace MemoryModules
{
    /// <summary>
    /// Load dll/exe from memory
    /// </summary>
    public unsafe class MemoryModule
    {
        private readonly MEMORYMODULE _internalModule;

        private MemoryModule(MEMORYMODULE internalModule) => _internalModule = internalModule;

        /// <summary>
        /// Create an instance of the <see cref="MemoryModule"/>
        /// </summary>
        /// <param name="data">A pointer to dll/exe</param>
        /// <param name="size">The size of data</param>
        /// <returns></returns>
        public static MemoryModule Create(void* data, uint size)
        {
            if (data == null)
                throw new ArgumentNullException();
            if (size <= 0)
                throw new ArgumentOutOfRangeException();

            MEMORYMODULE internalModule;

            internalModule = MemoryModuleC.MemoryLoadLibrary(data, (void*)size);
            return internalModule == null ? null : new MemoryModule(internalModule);
        }

        /// <summary>
        /// Create an instance of the <see cref="MemoryModule"/>
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static MemoryModule Create(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentNullException();

            fixed (byte* p = data)
                return Create(p, (uint)data.Length);
        }

        /// <summary>
        /// Create an instance of the <see cref="MemoryModule"/>
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static MemoryModule Create(Stream stream) => Create(StreamReadAllBytes(stream));

        private static byte[] StreamReadAllBytes(Stream stream)
        {
            if (!stream.CanRead)
                throw new ArgumentException("Can't read the stream");

            try
            {
                int length;
                byte[] buffer;
                List<byte> byteList;
                int count;

                try
                {
                    length = (int)stream.Length;
                    buffer = new byte[length];
                    stream.Read(buffer, 0, buffer.Length);
                    return buffer;
                }
                catch
                {
                    buffer = new byte[0x1000];
                    byteList = new List<byte>();
                    for (int i = 0; i < int.MaxValue; i++)
                    {
                        count = stream.Read(buffer, 0, buffer.Length);
                        if (count == 0x1000)
                            byteList.AddRange(buffer);
                        else if (count == 0)
                            return byteList.ToArray();
                        else
                            for (int j = 0; j < count; j++)
                                byteList.Add(buffer[j]);
                    }
                }
            }
            catch
            {
                return null;
            }
            throw new OutOfMemoryException();
        }

        /// <summary>
        /// Free current instance of the <see cref="MemoryModule"/>
        /// </summary>
        public void FreeLibrary() => MemoryModuleC.MemoryFreeLibrary(_internalModule);

        /// <summary>
        /// Retrieves the address of an exported function from current instance of the <see cref="MemoryModule"/>
        /// </summary>
        /// <param name="functionName">The function name</param>
        /// <returns></returns>
        public IntPtr GetProcAddress(string functionName) => (IntPtr)MemoryModuleC.MemoryGetProcAddress(_internalModule, functionName);

        /// <summary>
        /// Get the delegate of an exported function from current instance of the <see cref="MemoryModule"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="functionName">The function name</param>
        /// <returns></returns>
        public T GetProcDelegate<T>(string functionName) => (T)(object)Marshal.GetDelegateForFunctionPointer(GetProcAddress(functionName), typeof(T));

        /// <summary>
        /// Call the entrypoint. Return -1 if error
        /// </summary>
        /// <returns></returns>
        public int CallEntryPoint() => MemoryModuleC.MemoryCallEntryPoint(_internalModule);
    }
}

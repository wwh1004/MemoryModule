# MemoryModule
Load dll/exe from memory. And it supports "AnyCPU" platform!

Original code [MemoryModule](https://github.com/fancycode/MemoryModule) by fancycode.

# Examples
You can see the Test project for the examples.

# LoadLibrary
```csharp
byte[] data = xxxx;
MemoryModule memoryModule = MemoryModule.Create(data);
```

# GetProcAddress/GetProcDelegate
```csharp
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate size_t FastLzma2CompressProc(byte[] dst, size_t dstCapacity, byte[] src, size_t srcSize, int compressionLevel, uint nbThreads);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate size_t FastLzma2DecompressProc(byte[] dst, size_t dstCapacity, byte[] src, size_t compressedSize);
private static FastLzma2CompressProc FastLzma2Compress;
private static FastLzma2DecompressProc FastLzma2Decompress;

......

FastLzma2Compress = memoryModule.GetProcDelegate<FastLzma2CompressProc>("FL2_compressMt");
FastLzma2Decompress = memoryModule.GetProcDelegate<FastLzma2DecompressProc>("FL2_decompress");
```

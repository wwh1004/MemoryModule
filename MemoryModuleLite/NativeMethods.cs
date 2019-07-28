using System.Runtime.InteropServices;

namespace MemoryModules {
	internal static unsafe class NativeMethods {
		#region const
		public const uint MEM_COMMIT = 0x00001000;
		public const uint MEM_RESERVE = 0x00002000;
		public const uint MEM_DECOMMIT = 0x00004000;
		public const uint MEM_RELEASE = 0x00008000;
		public const uint PAGE_NOACCESS = 0x01;
		public const uint PAGE_READONLY = 0x02;
		public const uint PAGE_READWRITE = 0x04;
		public const uint PAGE_WRITECOPY = 0x08;
		public const uint PAGE_EXECUTE = 0x10;
		public const uint PAGE_EXECUTE_READ = 0x20;
		public const uint PAGE_EXECUTE_READWRITE = 0x40;
		public const uint PAGE_EXECUTE_WRITECOPY = 0x80;
		public const uint PAGE_NOCACHE = 0x200;
		public const ushort IMAGE_NUMBEROF_DIRECTORY_ENTRIES = 16;
		public const uint IMAGE_DIRECTORY_ENTRY_EXPORT = 0;
		public const uint IMAGE_DIRECTORY_ENTRY_IMPORT = 1;
		public const uint IMAGE_DIRECTORY_ENTRY_BASERELOC = 5;
		public const uint IMAGE_DIRECTORY_ENTRY_TLS = 9;
		public const ulong IMAGE_ORDINAL_FLAG64 = 0x8000000000000000;
		public const uint IMAGE_ORDINAL_FLAG32 = 0x80000000;
		public const uint IMAGE_SCN_MEM_EXECUTE = 0x20000000;
		public const uint IMAGE_SCN_MEM_READ = 0x40000000;
		public const uint IMAGE_SCN_MEM_WRITE = 0x80000000;
		public const uint DLL_PROCESS_ATTACH = 1;
		public const uint DLL_PROCESS_DETACH = 0;
		public const uint IMAGE_REL_BASED_ABSOLUTE = 0;
		public const uint IMAGE_REL_BASED_HIGHLOW = 3;
		public const uint IMAGE_REL_BASED_DIR64 = 10;
		public const ushort IMAGE_DOS_SIGNATURE = 0x5A4D;
		public const uint IMAGE_NT_SIGNATURE = 0x00004550;
		public const ushort IMAGE_FILE_MACHINE_I386 = 0x014c;
		public const ushort IMAGE_FILE_MACHINE_AMD64 = 0x8664;
		public const uint IMAGE_SCN_CNT_INITIALIZED_DATA = 0x00000040;
		public const uint IMAGE_SCN_CNT_UNINITIALIZED_DATA = 0x00000080;
		public const uint IMAGE_SCN_MEM_DISCARDABLE = 0x02000000;
		public const uint IMAGE_SCN_MEM_NOT_CACHED = 0x04000000;
		public const uint IMAGE_FILE_DLL = 0x2000;
		#endregion

		#region struct
		[StructLayout(LayoutKind.Sequential)]
		public struct IMAGE_DOS_HEADER {
			public ushort e_magic;
			public ushort e_cblp;
			public ushort e_cp;
			public ushort e_crlc;
			public ushort e_cparhdr;
			public ushort e_minalloc;
			public ushort e_maxalloc;
			public ushort e_ss;
			public ushort e_sp;
			public ushort e_csum;
			public ushort e_ip;
			public ushort e_cs;
			public ushort e_lfarlc;
			public ushort e_ovno;
			public fixed ushort e_res1[4];
			public ushort e_oemid;
			public ushort e_oeminfo;
			public fixed ushort e_res2[10];
			public uint e_lfanew;
			public static readonly uint UnmanagedSize = (uint)Marshal.SizeOf(typeof(IMAGE_DOS_HEADER));
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct IMAGE_NT_HEADERS32 {
			public uint Signature;
			public IMAGE_FILE_HEADER FileHeader;
			public IMAGE_OPTIONAL_HEADER32 OptionalHeader;
			public static readonly uint UnmanagedSize = (uint)Marshal.SizeOf(typeof(IMAGE_NT_HEADERS32));
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct IMAGE_NT_HEADERS64 {
			public uint Signature;
			public IMAGE_FILE_HEADER FileHeader;
			public IMAGE_OPTIONAL_HEADER64 OptionalHeader;
			public static readonly uint UnmanagedSize = (uint)Marshal.SizeOf(typeof(IMAGE_NT_HEADERS64));
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct IMAGE_FILE_HEADER {
			public ushort Machine;
			public ushort NumberOfSections;
			public uint TimeDateStamp;
			public uint PointerToSymbolTable;
			public uint NumberOfSymbols;
			public ushort SizeOfOptionalHeader;
			public ushort Characteristics;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct IMAGE_OPTIONAL_HEADER32 {
			public ushort Magic;
			public byte MajorLinkerVersion;
			public byte MinorLinkerVersion;
			public uint SizeOfCode;
			public uint SizeOfInitializedData;
			public uint SizeOfUninitializedData;
			public uint AddressOfEntryPoint;
			public uint BaseOfCode;
			public uint BaseOfData;
			public uint ImageBase;
			public uint SectionAlignment;
			public uint FileAlignment;
			public ushort MajorOperatingSystemVersion;
			public ushort MinorOperatingSystemVersion;
			public ushort MajorImageVersion;
			public ushort MinorImageVersion;
			public ushort MajorSubsystemVersion;
			public ushort MinorSubsystemVersion;
			public uint Win32VersionValue;
			public uint SizeOfImage;
			public uint SizeOfHeaders;
			public uint CheckSum;
			public ushort Subsystem;
			public ushort DllCharacteristics;
			public uint SizeOfStackReserve;
			public uint SizeOfStackCommit;
			public uint SizeOfHeapReserve;
			public uint SizeOfHeapCommit;
			public uint LoaderFlags;
			public uint NumberOfRvaAndSizes;
			public fixed ulong DataDirectory[IMAGE_NUMBEROF_DIRECTORY_ENTRIES];
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct IMAGE_OPTIONAL_HEADER64 {
			public ushort Magic;
			public byte MajorLinkerVersion;
			public byte MinorLinkerVersion;
			public uint SizeOfCode;
			public uint SizeOfInitializedData;
			public uint SizeOfUninitializedData;
			public uint AddressOfEntryPoint;
			public uint BaseOfCode;
			public ulong ImageBase;
			public uint SectionAlignment;
			public uint FileAlignment;
			public ushort MajorOperatingSystemVersion;
			public ushort MinorOperatingSystemVersion;
			public ushort MajorImageVersion;
			public ushort MinorImageVersion;
			public ushort MajorSubsystemVersion;
			public ushort MinorSubsystemVersion;
			public uint Win32VersionValue;
			public uint SizeOfImage;
			public uint SizeOfHeaders;
			public uint CheckSum;
			public ushort Subsystem;
			public ushort DllCharacteristics;
			public ulong SizeOfStackReserve;
			public ulong SizeOfStackCommit;
			public ulong SizeOfHeapReserve;
			public ulong SizeOfHeapCommit;
			public uint LoaderFlags;
			public uint NumberOfRvaAndSizes;
			public fixed ulong DataDirectory[IMAGE_NUMBEROF_DIRECTORY_ENTRIES];
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct IMAGE_DATA_DIRECTORY {
			public uint VirtualAddress;
			public uint Size;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct IMAGE_SECTION_HEADER {
			public fixed byte Name[8];
			public uint PhysicalAddress;
			public uint VirtualAddress;
			public uint SizeOfRawData;
			public uint PointerToRawData;
			public uint PointerToRelocations;
			public uint PointerToLinenumbers;
			public ushort NumberOfRelocations;
			public ushort NumberOfLinenumbers;
			public uint Characteristics;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct IMAGE_BASE_RELOCATION {
			public uint VirtualAddress;
			public uint SizeOfBlock;
			public static readonly uint UnmanagedSize = (uint)Marshal.SizeOf(typeof(IMAGE_BASE_RELOCATION));
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct IMAGE_IMPORT_DESCRIPTOR {
			public uint OriginalFirstThunk;
			public uint TimeDateStamp;
			public uint ForwarderChain;
			public uint Name;
			public uint FirstThunk;
			public static readonly uint UnmanagedSize = (uint)Marshal.SizeOf(typeof(IMAGE_IMPORT_DESCRIPTOR));
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct IMAGE_EXPORT_DIRECTORY {
			public uint Characteristics;
			public uint TimeDateStamp;
			public ushort MajorVersion;
			public ushort MinorVersion;
			public uint Name;
			public uint Base;
			public uint NumberOfFunctions;
			public uint NumberOfNames;
			public uint AddressOfFunctions;
			public uint AddressOfNames;
			public uint AddressOfNameOrdinals;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct IMAGE_IMPORT_BY_NAME {
			public ushort Hint;
			public fixed byte Name[1];
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct SYSTEM_INFO {
			public ushort wProcessorArchitecture;
			public ushort wReserved;
			public uint dwPageSize;
			public void* lpMinimumApplicationAddress;
			public void* lpMaximumApplicationAddress;
			public void* dwActiveProcessorMask;
			public uint dwNumberOfProcessors;
			public uint dwProcessorType;
			public uint dwAllocationGranularity;
			public ushort wProcessorLevel;
			public ushort wProcessorRevision;
		};

		[StructLayout(LayoutKind.Sequential)]
		public struct IMAGE_TLS_DIRECTORY {
			public void* StartAddressOfRawData;
			public void* EndAddressOfRawData;
			public void* AddressOfIndex;
			public void* AddressOfCallBacks;
			public void* SizeOfZeroFill;
			public uint Characteristics;
		}
		#endregion

		#region delegate
		public delegate void IMAGE_TLS_CALLBACK(void* DllHandle, uint Reason, void* Reserved);
		#endregion

		#region funcs
#pragma warning disable IDE1006
		[DllImport("kernel32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool FreeLibrary(void* hModule);

		[DllImport("kernel32.dll")]
		public static extern void GetNativeSystemInfo(SYSTEM_INFO* lpSystemInfo);

		[DllImport("kernel32.dll")]
		public static extern void* GetProcAddress(void* hModule, byte* lpProcName);

		[DllImport("kernel32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool IsBadReadPtr(void* lp, void* ucb);

		[DllImport("kernel32.dll")]
		public static extern void* LoadLibraryA(byte* lpLibFileName);

		[DllImport("kernel32.dll")]
		public static extern void* VirtualAlloc(void* lpAddress, void* dwSize, uint flAllocationType, uint flProtect);

		[DllImport("kernel32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool VirtualFree(void* lpAddress, void* dwSize, uint dwFreeType);

		[DllImport("kernel32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool VirtualProtect(void* lpAddress, void* dwSize, uint flNewProtect, uint* lpflOldProtect);

		[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void free(void* _Block);

		[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void* memcpy(void* _Dst, void* _Src, void* _Size);

		[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void* memset(void* _Dst, int _Val, void* _Size);

		[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void* realloc(void* _Block, void* _Size);

		[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int strcmp(byte* _Str1, byte* _Str2);
#pragma warning restore IDE1006
		#endregion
	}
}

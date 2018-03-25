/*
 * Memory DLL loading code
 * Version 0.0.4
 *
 * Copyright (c) 2004-2015 by Joachim Bauch / mail@joachim-bauch.de
 * http://www.joachim-bauch.de
 *
 * The contents of this file are subject to the Mozilla Public License Version
 * 2.0 (the "License"); you may not use this file except in compliance with
 * the License. You may obtain a copy of the License at
 * http://www.mozilla.org/MPL/
 *
 * Software distributed under the License is distributed on an "AS IS" basis,
 * WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License
 * for the specific language governing rights and limitations under the
 * License.
 *
 * The Original Code is MemoryModule.c
 *
 * The Initial Developer of the Original Code is Joachim Bauch.
 *
 * Portions created by Joachim Bauch are Copyright (C) 2004-2015
 * Joachim Bauch. All Rights Reserved.
 *
 *
 * THeller: Added binary search in MemoryGetProcAddress function
 * (#define USE_BINARY_SEARCH to enable it).  This gives a very large
 * speedup for libraries that exports lots of functions.
 *
 * These portions are Copyright (C) 2013 Thomas Heller.
 */

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using static MemoryModuleSX.NativeMethods;

namespace MemoryModuleSX
{
    #region native
    internal unsafe delegate void* CustomAllocFunc(void* a, void* b, uint c, uint d, void* e);

    internal unsafe delegate bool CustomFreeFunc(void* a, void* b, uint c, void* d);

    internal unsafe delegate void* CustomLoadLibraryFunc(byte* a, void* b);

    internal unsafe delegate void* CustomGetProcAddressFunc(void* a, byte* b, void* c);

    internal unsafe delegate void CustomFreeLibraryFunc(void* a, void* b);

    [return: MarshalAs(UnmanagedType.Bool)]
    internal unsafe delegate bool DllEntryProc(void* hinstDLL, uint fdwReason, void* lpReserved);

    internal unsafe delegate int ExeEntryProc();

    internal unsafe class ExportNameEntry : IComparer<ExportNameEntry>
    {
        public byte* name;
        public ushort idx;

        public int Compare(ExportNameEntry x, ExportNameEntry y)
        {
            return strcmp(x.name, y.name);
        }
    };

    internal unsafe class POINTER_LIST
    {
        public POINTER_LIST next;
        public void* address;
    }

    internal unsafe class MEMORYMODULE
    {
        public void* headers;
        public byte* codeBase;
        public void** modules;
        public int numModules;
        public bool initialized;
        public bool isDLL;
        public bool isRelocated;
        public CustomAllocFunc alloc;
        public CustomFreeFunc free;
        public CustomLoadLibraryFunc loadLibrary;
        public CustomGetProcAddressFunc getProcAddress;
        public CustomFreeLibraryFunc freeLibrary;
        public ExportNameEntry[] nameExportsTable;
        public void* userdata;
        public ExeEntryProc exeEntry;
        public uint pageSize;
        public POINTER_LIST blockedMemory;
    };

    internal unsafe class SECTIONFINALIZEDATA
    {
        public void* address;
        public void* alignedAddress;
        public void* size;
        public uint characteristics;
        public bool last;
    };
    #endregion

    /// <summary>
    /// Original MemoryModule
    /// </summary>
    public static unsafe class MemoryModuleC
    {
        #region macro
        private static ushort LOWORD(void* l)
        {
            return (ushort)((ulong)l & 0xffff);
        }

        private static ushort HIWORD(void* l)
        {
            return (ushort)(((ulong)l >> 16) & 0xffff);
        }

        private static bool IMAGE_SNAP_BY_ORDINAL(void* ordinal)
        {
            return ((ulong)ordinal & (WIN64 ? IMAGE_ORDINAL_FLAG64 : IMAGE_ORDINAL_FLAG32)) != 0;
        }

        private static void* IMAGE_ORDINAL(void* ordinal)
        {
            return (void*)((ulong)ordinal & 0xFFFF);
        }

        private static IMAGE_DATA_DIRECTORY* GET_HEADER_DICTIONARY(MEMORYMODULE module, uint idx)
        {
            return WIN64 ? (IMAGE_DATA_DIRECTORY*)&((IMAGE_NT_HEADERS64*)module.headers)->OptionalHeader.DataDirectory[idx] : (IMAGE_DATA_DIRECTORY*)&((IMAGE_NT_HEADERS32*)module.headers)->OptionalHeader.DataDirectory[idx];
        }

        private static IMAGE_SECTION_HEADER* IMAGE_FIRST_SECTION(void* ntheader)
        {
            return WIN64 ? IMAGE_FIRST_SECTION64((IMAGE_NT_HEADERS64*)ntheader) : IMAGE_FIRST_SECTION32((IMAGE_NT_HEADERS32*)ntheader);
        }

        private static IMAGE_SECTION_HEADER* IMAGE_FIRST_SECTION32(IMAGE_NT_HEADERS32* ntheader)
        {
            return (IMAGE_SECTION_HEADER*)((ulong)ntheader + 24 + ntheader->FileHeader.SizeOfOptionalHeader);
        }

        private static IMAGE_SECTION_HEADER* IMAGE_FIRST_SECTION64(IMAGE_NT_HEADERS64* ntheader)
        {
            return (IMAGE_SECTION_HEADER*)((ulong)ntheader + 24 + ntheader->FileHeader.SizeOfOptionalHeader);
        }
        #endregion

        private static readonly bool WIN64 = IntPtr.Size == 8;

        private static readonly ushort HOST_MACHINE = WIN64 ? IMAGE_FILE_MACHINE_AMD64 : IMAGE_FILE_MACHINE_I386;

        // Protection flags for memory pages (Executable, Readable, Writeable)
        private static uint[,,] ProtectionFlags = new uint[,,]
        {
            {
                // not executable
                { PAGE_NOACCESS, PAGE_WRITECOPY },
                { PAGE_READONLY, PAGE_READWRITE },
            },
            {
                // executable
                { PAGE_EXECUTE, PAGE_EXECUTE_WRITECOPY },
                { PAGE_EXECUTE_READ, PAGE_EXECUTE_READWRITE },
            },
        };

        private static void* AlignValueDown(void* value, void* alignment)
        {
            return (void*)((ulong)value & ~((ulong)alignment - 1));
        }

        private static void* AlignAddressDown(void* address, void* alignment)
        {
            return AlignValueDown(address, alignment);
        }

        private static void* AlignValueUp(void* value, void* alignment)
        {
            return (void*)(((ulong)value + (ulong)alignment - 1) & ~((ulong)alignment - 1));
        }

        private static void* OffsetPointer(void* data, void* offset)
        {
            return (void*)((ulong)data + (ulong)offset);
        }

        private static void FreePointerList(POINTER_LIST head, CustomFreeFunc freeMemory, void* userdata)
        {
            POINTER_LIST node = head;
            while (node != null)
            {
                POINTER_LIST next;
                freeMemory(node.address, null, MEM_RELEASE, userdata);
                next = node.next;
                node = next;
            }
        }

        private static bool CheckSize(void* size, void* expected)
        {
            if ((ulong)size < (ulong)expected)
                return false;
            return true;
        }

        private static bool CopySections(byte* data, void* size, void* old_headers, MEMORYMODULE module)
        {
            int i;
            uint section_size;
            byte* codeBase = module.codeBase;
            byte* dest;
            IMAGE_SECTION_HEADER* section = IMAGE_FIRST_SECTION(module.headers);
            for (i = 0; i < (WIN64 ? ((IMAGE_NT_HEADERS64*)module.headers)->FileHeader.NumberOfSections : ((IMAGE_NT_HEADERS32*)module.headers)->FileHeader.NumberOfSections); i++, section++)
            {
                if (section->SizeOfRawData == 0)
                {
                    // section doesn't contain data in the dll itself, but may define
                    // uninitialized data
                    section_size = (WIN64 ? ((IMAGE_NT_HEADERS64*)old_headers)->OptionalHeader.SectionAlignment : ((IMAGE_NT_HEADERS32*)old_headers)->OptionalHeader.SectionAlignment);
                    if (section_size > 0)
                    {
                        dest = (byte*)module.alloc(codeBase + section->VirtualAddress, (void*)section_size, MEM_COMMIT, PAGE_READWRITE, module.userdata);
                        if (dest == null)
                            return false;

                        // Always use position from file to support alignments smaller
                        // than page size (allocation above will align to page size).
                        dest = codeBase + section->VirtualAddress;
                        // NOTE: On 64bit systems we truncate to 32bit here but expand
                        // again later when "PhysicalAddress" is used.
                        section->PhysicalAddress = (uint)((ulong)dest & 0xffffffff);
                        memset(dest, 0, (void*)section_size);
                    }

                    // section is empty
                    continue;
                }

                if (!CheckSize(size, (void*)(section->PointerToRawData + section->SizeOfRawData)))
                    return false;

                // commit memory block and copy data from dll
                dest = (byte*)module.alloc(codeBase + section->VirtualAddress, (void*)section->SizeOfRawData, MEM_COMMIT, PAGE_READWRITE, module.userdata);
                if (dest == null)
                    return false;

                // Always use position from file to support alignments smaller
                // than page size (allocation above will align to page size).
                dest = codeBase + section->VirtualAddress;
                memcpy(dest, data + section->PointerToRawData, (void*)section->SizeOfRawData);
                // NOTE: On 64bit systems we truncate to 32bit here but expand
                // again later when "PhysicalAddress" is used.
                section->PhysicalAddress = (uint)((ulong)dest & 0xffffffff);
            }

            return true;
        }

        private static void* GetRealSectionSize(MEMORYMODULE module, IMAGE_SECTION_HEADER* section)
        {
            uint size = section->SizeOfRawData;
            if (size == 0)
            {
                if ((section->Characteristics & IMAGE_SCN_CNT_INITIALIZED_DATA) != 0)
                    size = (WIN64 ? ((IMAGE_NT_HEADERS64*)module.headers)->OptionalHeader.SizeOfInitializedData : ((IMAGE_NT_HEADERS32*)module.headers)->OptionalHeader.SizeOfInitializedData);
                else if ((section->Characteristics & IMAGE_SCN_CNT_UNINITIALIZED_DATA) != 0)
                    size = (WIN64 ? ((IMAGE_NT_HEADERS64*)module.headers)->OptionalHeader.SizeOfUninitializedData : ((IMAGE_NT_HEADERS32*)module.headers)->OptionalHeader.SizeOfUninitializedData);
            }
            return (void*)size;
        }

        private static bool FinalizeSection(MEMORYMODULE module, SECTIONFINALIZEDATA sectionData)
        {
            uint protect, oldProtect;
            bool executable;
            bool readable;
            bool writeable;

            if (sectionData.size == null)
                return true;

            if ((sectionData.characteristics & IMAGE_SCN_MEM_DISCARDABLE) != 0)
            {
                // section is not needed any more and can safely be freed
                if (sectionData.address == sectionData.alignedAddress && (sectionData.last || (WIN64 ? ((IMAGE_NT_HEADERS64*)module.headers)->OptionalHeader.SectionAlignment : ((IMAGE_NT_HEADERS32*)module.headers)->OptionalHeader.SectionAlignment) == module.pageSize || ((ulong)sectionData.size % module.pageSize) == 0))
                    // Only allowed to decommit whole pages
                    module.free(sectionData.address, sectionData.size, MEM_DECOMMIT, module.userdata);
                return true;
            }

            // determine protection flags based on characteristics
            executable = (sectionData.characteristics & IMAGE_SCN_MEM_EXECUTE) != 0;
            readable = (sectionData.characteristics & IMAGE_SCN_MEM_READ) != 0;
            writeable = (sectionData.characteristics & IMAGE_SCN_MEM_WRITE) != 0;
            protect = ProtectionFlags[executable ? 1 : 0, readable ? 1 : 0, writeable ? 1 : 0];
            if ((sectionData.characteristics & IMAGE_SCN_MEM_NOT_CACHED) != 0)
                protect |= PAGE_NOCACHE;

            // change memory access flags
            if (!VirtualProtect(sectionData.address, sectionData.size, protect, &oldProtect))
                return false;

            return true;
        }

        private static bool FinalizeSections(MEMORYMODULE module)
        {
            int i;
            void* imageOffset;
            IMAGE_SECTION_HEADER* section = IMAGE_FIRST_SECTION(module.headers);
            // "PhysicalAddress" might have been truncated to 32bit above, expand to
            // 64bits again.
            imageOffset = WIN64 ? (void*)(((IMAGE_NT_HEADERS64*)module.headers)->OptionalHeader.ImageBase & 0xffffffff00000000) : null;
            SECTIONFINALIZEDATA sectionData = new SECTIONFINALIZEDATA
            {
                address = (void*)(section->PhysicalAddress | (ulong)imageOffset),
                size = GetRealSectionSize(module, section),
                characteristics = section->Characteristics,
                last = false
            };
            sectionData.alignedAddress = AlignAddressDown(sectionData.address, (void*)module.pageSize);
            section++;

            // loop through all sections and change access flags
            for (i = 1; i < (WIN64 ? ((IMAGE_NT_HEADERS64*)module.headers)->FileHeader.NumberOfSections : ((IMAGE_NT_HEADERS32*)module.headers)->FileHeader.NumberOfSections); i++, section++)
            {
                void* sectionAddress = (void*)(section->PhysicalAddress | (ulong)imageOffset);
                void* alignedAddress = AlignAddressDown(sectionAddress, (void*)module.pageSize);
                void* sectionSize = GetRealSectionSize(module, section);
                // Combine access flags of all sections that share a page
                // TODO(fancycode): We currently share flags of a trailing large section
                //   with the page of a first small section. This should be optimized.
                if (sectionData.alignedAddress == alignedAddress || (ulong)sectionData.address + (ulong)sectionData.size > (ulong)alignedAddress)
                {
                    // Section shares page with previous
                    if ((section->Characteristics & IMAGE_SCN_MEM_DISCARDABLE) == 0 || (sectionData.characteristics & IMAGE_SCN_MEM_DISCARDABLE) == 0)
                        sectionData.characteristics = (sectionData.characteristics | section->Characteristics) & ~IMAGE_SCN_MEM_DISCARDABLE;
                    else
                        sectionData.characteristics |= section->Characteristics;
                    sectionData.size = (void*)((ulong)sectionAddress + (ulong)sectionSize - (ulong)sectionData.address);
                    continue;
                }

                if (!FinalizeSection(module, sectionData))
                    return false;
                sectionData.address = sectionAddress;
                sectionData.alignedAddress = alignedAddress;
                sectionData.size = sectionSize;
                sectionData.characteristics = section->Characteristics;
            }
            sectionData.last = true;
            if (!FinalizeSection(module, sectionData))
                return false;
            return true;
        }

        private static bool ExecuteTLS(MEMORYMODULE module)
        {
            byte* codeBase = module.codeBase;
            IMAGE_TLS_DIRECTORY tls;
            void** callback;

            IMAGE_DATA_DIRECTORY* directory = GET_HEADER_DICTIONARY(module, IMAGE_DIRECTORY_ENTRY_TLS);
            if (directory->VirtualAddress == 0)
            {
                return true;
            }

            tls = *(IMAGE_TLS_DIRECTORY*)((ulong)codeBase + directory->VirtualAddress);
            callback = (void**)tls.AddressOfCallBacks;
            if (callback != null)
            {
                while (*callback != null)
                {
                    ((IMAGE_TLS_CALLBACK)Marshal.GetDelegateForFunctionPointer((IntPtr)(*callback), typeof(IMAGE_TLS_CALLBACK)))((void*)codeBase, DLL_PROCESS_ATTACH, null);
                    callback++;
                }
            }
            return true;
        }

        private static bool PerformBaseRelocation(MEMORYMODULE module, void* delta)
        {
            byte* codeBase = module.codeBase;
            IMAGE_BASE_RELOCATION* relocation;

            IMAGE_DATA_DIRECTORY* directory = GET_HEADER_DICTIONARY(module, IMAGE_DIRECTORY_ENTRY_BASERELOC);
            if (directory->Size == 0)
            {
                return delta == null;
            }

            relocation = (IMAGE_BASE_RELOCATION*)((ulong)codeBase + directory->VirtualAddress);
            for (; relocation->VirtualAddress > 0;)
            {
                uint i;
                byte* dest = codeBase + relocation->VirtualAddress;
                ushort* relInfo = (ushort*)OffsetPointer(relocation, (void*)IMAGE_BASE_RELOCATION.UnmanagedSize);
                for (i = 0; i < ((relocation->SizeOfBlock - IMAGE_BASE_RELOCATION.UnmanagedSize) / 2); i++, relInfo++)
                {
                    // the upper 4 bits define the type of relocation
                    uint type = (uint)(*relInfo) >> 12;
                    // the lower 12 bits define the offset
                    uint offset = (uint)(*relInfo) & 0xfff;

                    switch (type)
                    {
                        case IMAGE_REL_BASED_ABSOLUTE:
                            // skip relocation
                            break;

                        case IMAGE_REL_BASED_HIGHLOW:
                            // change complete 32 bit address
                            uint* patchAddrHL = (uint*)(dest + offset);
                            *patchAddrHL += (uint)delta;
                            break;
                        case IMAGE_REL_BASED_DIR64:
                            if (WIN64)
                            {
                                ulong* patchAddr64 = (ulong*)(dest + offset);
                                *patchAddr64 += (ulong)delta;
                            }
                            break;
                        default:
                            break;
                    }
                }

                // advance to next relocation block
                relocation = (IMAGE_BASE_RELOCATION*)OffsetPointer(relocation, (void*)relocation->SizeOfBlock);
            }
            return true;
        }

        private static bool BuildImportTable(MEMORYMODULE module)
        {
            byte* codeBase = module.codeBase;
            IMAGE_IMPORT_DESCRIPTOR* importDesc;
            bool result = true;

            IMAGE_DATA_DIRECTORY* directory = GET_HEADER_DICTIONARY(module, IMAGE_DIRECTORY_ENTRY_IMPORT);
            if (directory->Size == 0)
            {
                return true;
            }

            importDesc = (IMAGE_IMPORT_DESCRIPTOR*)(codeBase + directory->VirtualAddress);
            for (; !IsBadReadPtr(importDesc, (void*)IMAGE_IMPORT_DESCRIPTOR.UnmanagedSize) && importDesc->Name != 0; importDesc++)
            {
                void** thunkRef;
                void** funcRef;
                void** tmp;
                void* handle = module.loadLibrary(codeBase + importDesc->Name, module.userdata);
                if (handle == null)
                {
                    result = false;
                    break;
                }

                tmp = (void**)realloc(module.modules, (void*)((module.numModules + 1) * sizeof(void*)));
                if (tmp == null)
                {
                    module.freeLibrary(handle, module.userdata);
                    result = false;
                    break;
                }
                module.modules = tmp;

                module.modules[module.numModules++] = handle;
                if (importDesc->OriginalFirstThunk != 0)
                {
                    thunkRef = (void**)(codeBase + importDesc->OriginalFirstThunk);
                    funcRef = (void**)(codeBase + importDesc->FirstThunk);
                }
                else
                {
                    // no hint table
                    thunkRef = (void**)(codeBase + importDesc->FirstThunk);
                    funcRef = (void**)(codeBase + importDesc->FirstThunk);
                }
                for (; *thunkRef != null; thunkRef++, funcRef++)
                {
                    if (IMAGE_SNAP_BY_ORDINAL(*thunkRef))
                    {
                        *funcRef = module.getProcAddress(handle, (byte*)IMAGE_ORDINAL(*thunkRef), module.userdata);
                    }
                    else
                    {
                        IMAGE_IMPORT_BY_NAME* thunkData = (IMAGE_IMPORT_BY_NAME*)(codeBase + (ulong)(*thunkRef));
                        *funcRef = module.getProcAddress(handle, thunkData->Name, module.userdata);
                    }
                    if (*funcRef == null)
                    {
                        result = false;
                        break;
                    }
                }

                if (!result)
                {
                    module.freeLibrary(handle, module.userdata);
                    break;
                }
            }

            return result;
        }

        private static void* MemoryDefaultAlloc(void* address, void* size, uint allocationType, uint protect, void* userdata)
        {
            return VirtualAlloc(address, size, allocationType, protect);
        }

        private static bool MemoryDefaultFree(void* lpAddress, void* dwSize, uint dwFreeType, void* userdata)
        {
            return VirtualFree(lpAddress, dwSize, dwFreeType);
        }

        private static void* MemoryDefaultLoadLibrary(byte* filename, void* userdata)
        {
            void* result;
            result = LoadLibraryA(filename);
            if (result == null)
            {
                return null;
            }

            return result;
        }

        private static void* MemoryDefaultGetProcAddress(void* module, byte* name, void* userdata)
        {
            return GetProcAddress(module, name);
        }

        private static void MemoryDefaultFreeLibrary(void* module, void* userdata)
        {
            FreeLibrary(module);
        }

        internal static MEMORYMODULE MemoryLoadLibrary(void* data, void* size)
        {
            return MemoryLoadLibraryEx(data, size, MemoryDefaultAlloc, MemoryDefaultFree, MemoryDefaultLoadLibrary, MemoryDefaultGetProcAddress, MemoryDefaultFreeLibrary, null);
        }

        internal static MEMORYMODULE MemoryLoadLibraryEx(void* data, void* size, CustomAllocFunc allocMemory, CustomFreeFunc freeMemory, CustomLoadLibraryFunc loadLibrary, CustomGetProcAddressFunc getProcAddress, CustomFreeLibraryFunc freeLibrary, void* userdata)
        {
            MEMORYMODULE result = null;
            IMAGE_DOS_HEADER* dos_header;
            void* old_header;
            byte* code, headers;
            void* locationDelta;
            SYSTEM_INFO sysInfo;
            IMAGE_SECTION_HEADER* section;
            uint i;
            void* optionalSectionSize;
            void* lastSectionEnd = null;
            void* alignedImageSize;
            POINTER_LIST blockedMemory = null;

            if (!CheckSize(size, (void*)IMAGE_DOS_HEADER.UnmanagedSize))
                return null;
            dos_header = (IMAGE_DOS_HEADER*)data;
            if (dos_header->e_magic != IMAGE_DOS_SIGNATURE)
                return null;

            if (!CheckSize(size, (void*)(dos_header->e_lfanew + (WIN64 ? IMAGE_NT_HEADERS64.UnmanagedSize : IMAGE_NT_HEADERS32.UnmanagedSize))))
                return null;
            old_header = &((byte*)data)[dos_header->e_lfanew];
            if ((WIN64 ? ((IMAGE_NT_HEADERS64*)old_header)->Signature : ((IMAGE_NT_HEADERS32*)old_header)->Signature) != IMAGE_NT_SIGNATURE)
                return null;

            if ((WIN64 ? ((IMAGE_NT_HEADERS64*)old_header)->FileHeader.Machine : ((IMAGE_NT_HEADERS32*)old_header)->FileHeader.Machine) != HOST_MACHINE)
                return null;

            if (((WIN64 ? ((IMAGE_NT_HEADERS64*)old_header)->OptionalHeader.SectionAlignment : ((IMAGE_NT_HEADERS32*)old_header)->OptionalHeader.SectionAlignment) & 1) != 0)
                // Only support section alignments that are a multiple of 2
                return null;

            section = IMAGE_FIRST_SECTION(old_header);
            optionalSectionSize = (void*)((WIN64 ? ((IMAGE_NT_HEADERS64*)old_header)->OptionalHeader.SectionAlignment : ((IMAGE_NT_HEADERS32*)old_header)->OptionalHeader.SectionAlignment));
            for (i = 0; i < (WIN64 ? ((IMAGE_NT_HEADERS64*)old_header)->FileHeader.NumberOfSections : ((IMAGE_NT_HEADERS32*)old_header)->FileHeader.NumberOfSections); i++, section++)
            {
                void* endOfSection;
                if (section->SizeOfRawData == 0)
                {
                    // Section without data in the DLL
                    endOfSection = (void*)(section->VirtualAddress + (ulong)optionalSectionSize);
                }
                else
                {
                    endOfSection = (void*)(section->VirtualAddress + (ulong)section->SizeOfRawData);
                }

                if ((ulong)endOfSection > (ulong)lastSectionEnd)
                    lastSectionEnd = endOfSection;
            }

            GetNativeSystemInfo(&sysInfo);
            alignedImageSize = AlignValueUp((void*)(WIN64 ? ((IMAGE_NT_HEADERS64*)old_header)->OptionalHeader.SizeOfImage : ((IMAGE_NT_HEADERS32*)old_header)->OptionalHeader.SizeOfImage), (void*)sysInfo.dwPageSize);
            if (alignedImageSize != AlignValueUp(lastSectionEnd, (void*)sysInfo.dwPageSize))
                return null;

            // reserve memory for image of library
            // XXX: is it correct to commit the complete memory region at once?
            //      calling DllEntry raises an exception if we don't...
            code = (byte*)allocMemory((void*)(WIN64 ? ((IMAGE_NT_HEADERS64*)old_header)->OptionalHeader.ImageBase : ((IMAGE_NT_HEADERS32*)old_header)->OptionalHeader.ImageBase), alignedImageSize, MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE, userdata);

            if (code == null)
            {
                // try to allocate memory at arbitrary position
                code = (byte*)allocMemory(null, alignedImageSize, MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE, userdata);
                if (code == null)
                    return null;
            }

            if (WIN64)
            {
                // Memory block may not span 4 GB boundaries.
                while ((ulong)code >> 32 < ((ulong)code + (ulong)alignedImageSize) >> 32)
                {
                    POINTER_LIST node = new POINTER_LIST
                    {
                        next = blockedMemory,
                        address = code
                    };
                    blockedMemory = node;

                    code = (byte*)allocMemory(null, alignedImageSize, MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE, userdata);
                    if (code == null)
                    {
                        FreePointerList(blockedMemory, freeMemory, userdata);
                        return null;
                    }
                }
            }

            result = new MEMORYMODULE
            {
                codeBase = code,
                isDLL = ((WIN64 ? ((IMAGE_NT_HEADERS64*)old_header)->FileHeader.Characteristics : ((IMAGE_NT_HEADERS32*)old_header)->FileHeader.Characteristics) & IMAGE_FILE_DLL) != 0,
                alloc = allocMemory,
                free = freeMemory,
                loadLibrary = loadLibrary,
                getProcAddress = getProcAddress,
                freeLibrary = freeLibrary,
                userdata = userdata,
                pageSize = sysInfo.dwPageSize
            };
            if (WIN64)
                result.blockedMemory = blockedMemory;

            if (!CheckSize(size, (void*)(WIN64 ? ((IMAGE_NT_HEADERS64*)old_header)->OptionalHeader.SizeOfHeaders : ((IMAGE_NT_HEADERS32*)old_header)->OptionalHeader.SizeOfHeaders)))
                goto error;

            // commit memory for headers
            headers = (byte*)allocMemory(code, (void*)(WIN64 ? ((IMAGE_NT_HEADERS64*)old_header)->OptionalHeader.SizeOfHeaders : ((IMAGE_NT_HEADERS32*)old_header)->OptionalHeader.SizeOfHeaders), MEM_COMMIT, PAGE_READWRITE, userdata);

            // copy PE header to code
            memcpy(headers, dos_header, (void*)(WIN64 ? ((IMAGE_NT_HEADERS64*)old_header)->OptionalHeader.SizeOfHeaders : ((IMAGE_NT_HEADERS32*)old_header)->OptionalHeader.SizeOfHeaders));
            result.headers = &headers[dos_header->e_lfanew];

            // update position
            if (WIN64)
                ((IMAGE_NT_HEADERS64*)result.headers)->OptionalHeader.ImageBase = (ulong)code;
            else
                ((IMAGE_NT_HEADERS32*)result.headers)->OptionalHeader.ImageBase = (uint)code;

            // copy sections from DLL file block to new memory location
            if (!CopySections((byte*)data, size, old_header, result))
                goto error;

            // adjust base address of imported data
            locationDelta = (void*)((WIN64 ? ((IMAGE_NT_HEADERS64*)result.headers)->OptionalHeader.ImageBase : ((IMAGE_NT_HEADERS32*)result.headers)->OptionalHeader.ImageBase) - (WIN64 ? ((IMAGE_NT_HEADERS64*)old_header)->OptionalHeader.ImageBase : ((IMAGE_NT_HEADERS32*)old_header)->OptionalHeader.ImageBase));
            if ((ulong)locationDelta != 0)
                result.isRelocated = PerformBaseRelocation(result, locationDelta);
            else
                result.isRelocated = true;

            // load required dlls and adjust function table of imports
            if (!BuildImportTable(result))
                goto error;

            // mark memory pages depending on section headers and release
            // sections that are marked as "discardable"
            if (!FinalizeSections(result))
                goto error;

            // TLS callbacks are executed BEFORE the main loading
            if (!ExecuteTLS(result))
                goto error;

            // get entry point of loaded library
            if ((WIN64 ? ((IMAGE_NT_HEADERS64*)result.headers)->OptionalHeader.AddressOfEntryPoint : ((IMAGE_NT_HEADERS32*)result.headers)->OptionalHeader.AddressOfEntryPoint) != 0)
            {
                if (result.isDLL)
                {
                    DllEntryProc DllEntry = (DllEntryProc)Marshal.GetDelegateForFunctionPointer((IntPtr)(code + (WIN64 ? ((IMAGE_NT_HEADERS64*)result.headers)->OptionalHeader.AddressOfEntryPoint : ((IMAGE_NT_HEADERS32*)result.headers)->OptionalHeader.AddressOfEntryPoint)), typeof(DllEntryProc));
                    // notify library about attaching to process
                    bool successfull = DllEntry(code, DLL_PROCESS_ATTACH, null);
                    if (!successfull)
                        goto error;
                    result.initialized = true;
                }
                else
                    result.exeEntry = (ExeEntryProc)Marshal.GetDelegateForFunctionPointer((IntPtr)(code + (WIN64 ? ((IMAGE_NT_HEADERS64*)result.headers)->OptionalHeader.AddressOfEntryPoint : ((IMAGE_NT_HEADERS32*)result.headers)->OptionalHeader.AddressOfEntryPoint)), typeof(ExeEntryProc));
            }
            else
                result.exeEntry = null;

            return result;

            error:
            // cleanup
            MemoryFreeLibrary(result);
            return null;
        }

        private static int _compare(ExportNameEntry a, ExportNameEntry b)
        {
            return strcmp(a.name, b.name);
        }

        internal static void* MemoryGetProcAddress(MEMORYMODULE mod, string s_name)
        {
            fixed (byte* p = Encoding.Convert(Encoding.Unicode, Encoding.ASCII, Encoding.Unicode.GetBytes(s_name)))
                return MemoryGetProcAddress(mod, p);
        }

        internal static void* MemoryGetProcAddress(MEMORYMODULE mod, byte* name)
        {
            MEMORYMODULE module = mod;
            byte* codeBase = module.codeBase;
            uint idx = 0;
            IMAGE_EXPORT_DIRECTORY* exports;
            IMAGE_DATA_DIRECTORY* directory = GET_HEADER_DICTIONARY(module, IMAGE_DIRECTORY_ENTRY_EXPORT);
            if (directory->Size == 0)
                // no export table found
                return null;

            exports = (IMAGE_EXPORT_DIRECTORY*)(codeBase + directory->VirtualAddress);
            if (exports->NumberOfNames == 0 || exports->NumberOfFunctions == 0)
                // DLL doesn't export anything
                return null;

            if (HIWORD(name) == 0)
            {
                // load function by ordinal value
                if (LOWORD(name) < exports->Base)
                    return null;

                idx = LOWORD(name) - exports->Base;
            }
            else if (exports->NumberOfNames == 0)
                return null;
            else
            {
                ExportNameEntry found;

                // Lazily build name table and sort it by names
                if (module.nameExportsTable == null)
                {
                    uint i;
                    uint* nameRef = (uint*)(codeBase + exports->AddressOfNames);
                    ushort* ordinal = (ushort*)(codeBase + exports->AddressOfNameOrdinals);
                    ExportNameEntry[] entry = new ExportNameEntry[exports->NumberOfNames];
                    module.nameExportsTable = entry;
                    for (i = 0; i < exports->NumberOfNames; i++, nameRef++, ordinal++)
                        entry[i] = new ExportNameEntry
                        {
                            name = codeBase + (*nameRef),
                            idx = *ordinal
                        };
                    Array.Sort(module.nameExportsTable, _compare);
                }

                // search function name in list of exported names with binary search
                ExportNameEntry tmp = new ExportNameEntry { name = name };
                int foundIndex = Array.BinarySearch(module.nameExportsTable, tmp, tmp);
                found = foundIndex < 0 ? null : module.nameExportsTable[foundIndex];
                if (found == null)
                    // exported symbol not found
                    return null;

                idx = found.idx;
            }

            if (idx > exports->NumberOfFunctions)
                // name <. ordinal number don't match
                return null;

            // AddressOfFunctions contains the RVAs to the "real" functions
            return (void*)(codeBase + (*(uint*)(codeBase + exports->AddressOfFunctions + (idx * 4))));
        }

        internal static void MemoryFreeLibrary(MEMORYMODULE mod)
        {
            MEMORYMODULE module = mod;

            if (module == null)
                return;
            if (module.initialized)
            {
                // notify library about detaching from process
                DllEntryProc DllEntry = (DllEntryProc)Marshal.GetDelegateForFunctionPointer((IntPtr)(module.codeBase + (WIN64 ? ((IMAGE_NT_HEADERS64*)module.headers)->OptionalHeader.AddressOfEntryPoint : ((IMAGE_NT_HEADERS32*)module.headers)->OptionalHeader.AddressOfEntryPoint)), typeof(DllEntryProc));
                DllEntry(module.codeBase, DLL_PROCESS_DETACH, null);
            }

            if (module.modules != null)
            {
                // free previously opened libraries
                int i;
                for (i = 0; i < module.numModules; i++)
                    if ((void*)((IntPtr*)module.modules)[i] != null)
                        module.freeLibrary((void*)((IntPtr*)module.modules)[i], module.userdata);

                free(module.modules);
            }

            if (module.codeBase != null)
            {
                // release memory of library
                module.free(module.codeBase, null, MEM_RELEASE, module.userdata);
            }

            if (WIN64)
                FreePointerList(module.blockedMemory, module.free, module.userdata);
        }

        internal static int MemoryCallEntryPoint(MEMORYMODULE mod)
        {
            MEMORYMODULE module = mod;

            if (module == null || module.isDLL || module.exeEntry == null || !module.isRelocated)
            {
                return -1;
            }

            return module.exeEntry();
        }
    }
}

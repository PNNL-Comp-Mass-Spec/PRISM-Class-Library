﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMember.Local

// ReSharper disable once CheckNamespace
namespace PRISM
{
    /// <summary>
    /// System information for Windows; obtained via P/Invoke
    /// </summary>
#if NET5_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
    public class WindowsSystemInfo : EventNotifier, ISystemInfo
    {
        // Ignore Spelling: hyperthreading, NumaNode, struct, tradeoff, typeof, uint, ull, ushort

        /// <summary>
        /// Constructor
        /// </summary>
        public WindowsSystemInfo()
        {
            var c = new OSVersionInfo();

            if (c.GetOSVersion().IndexOf("windows", StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new Exception("This class only works on Windows platforms");
            }

            pData = new WindowsSystemInfoInternal();
            RegisterEvents(pData);
        }

        private readonly WindowsSystemInfoInternal pData;

        /// <inheritdoc />
        /// <summary>
        /// Report the number of cores on this system
        /// </summary>
        /// <remarks>
        /// Should not be affected by hyperthreading, so a computer with two 8-core chips will report 16 cores, even if hyperthreading is enabled
        /// </remarks>
        /// <returns>The number of cores on this computer</returns>
        public int GetCoreCount()
        {
            return pData.GetCoreCount();
        }

        /// <inheritdoc />
        /// <summary>
        /// Report the number of logical cores on this system
        /// </summary>
        /// <remarks>
        /// Will be affected by hyperthreading, so a computer with two 8-core chips will report 32 cores if hyperthreading is enabled
        /// </remarks>
        /// <returns>The number of logical cores on this computer</returns>
        public int GetLogicalCoreCount()
        {
            return pData.GetLogicalCoreCount();
        }

        /// <summary>
        /// Report the number of cores on this system
        /// </summary>
        /// <remarks>
        /// Should not be affected by hyperthreading, so a computer with two 8-core chips will report 16 cores, even if hyperthreading is enabled
        /// </remarks>
        /// <returns>The number of cores on this computer</returns>
        public int GetPhysicalCoreCount()
        {
            return pData.GetPhysicalCoreCount();
        }

        /// <inheritdoc />
        /// <summary>
        /// Report the number of processor packages on this system
        /// </summary>
        /// <returns>The number of processor packages on this computer</returns>
        public int GetProcessorPackageCount()
        {
            return pData.GetProcessorPackageCount();
        }

        /// <inheritdoc />
        /// <summary>
        /// Report the number of NUMA Nodes on this system
        /// </summary>
        /// <returns>The number of NUMA Nodes on this computer</returns>
        public int GetNumaNodeCount()
        {
            return pData.GetNumaNodeCount();
        }

        /// <inheritdoc />
        /// <summary>
        /// Determine the free system memory, in MB
        /// </summary>
        /// <returns>Free memory, or -1 if an error</returns>
        public float GetFreeMemoryMB()
        {
            return pData.GetFreeMemoryMB();
        }

        /// <summary>
        /// Look for currently active processes
        /// </summary>
        /// <remarks>Command line lookup can be slow because it uses WMI; set lookupCommandLineInfo to false to speed things up</remarks>
        /// <param name="lookupCommandLineInfo">When true, the process info dictionary will include the exe path and command line arguments</param>
        /// <returns>Dictionary where keys are process ID and values are ProcessInfo</returns>
        public Dictionary<int, ProcessInfo> GetProcesses(bool lookupCommandLineInfo = true)
        {
            return pData.GetProcesses(lookupCommandLineInfo);
        }

        /// <inheritdoc />
        /// <summary>
        /// Determine the total system memory, in MB
        /// </summary>
        /// <returns>Total memory, or -1 if an error</returns>
        public float GetTotalMemoryMB()
        {
            return pData.GetTotalMemoryMB();
        }
    }

    /// <summary>
    /// Internal implementation of WindowsSystemInfo
    /// </summary>
    /// <remarks>
    /// Internal to avoid big errors when trying to instantiate
    /// </remarks>
#if NET5_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
    internal class WindowsSystemInfoInternal : EventNotifier, ISystemInfo
    {
        /// <summary>
        /// Matches strings surrounded by double quotes
        /// </summary>
        private readonly Regex mQuotedStringMatcher;

        // Memory P/Invoke
        // https://www.pinvoke.net/default.aspx/kernel32/GlobalMemoryStatusEx.html

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, EntryPoint = "GlobalMemoryStatusEx", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        private static bool GetGlobalMemoryStatusEx(out MEMORYSTATUSEX memStatus)
        {
            memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
            var ret = GlobalMemoryStatusEx(ref memStatus);
            totalMemoryMBCached = memStatus.ullTotalPhys / 1024f / 1024f;
            return ret;
        }

        /// <summary>
        /// Contains information about the current state of both physical and virtual memory, including extended memory
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            /// <summary>
            /// Size of the structure, in bytes
            /// </summary>
            /// <remarks>
            /// You must set this value before calling GlobalMemoryStatusEx
            /// </remarks>
            public uint dwLength;

            // ReSharper disable MemberCanBePrivate.Local
            // ReSharper disable FieldCanBeMadeReadOnly.Local

            /// <summary>
            /// Number between 0 and 100 that specifies the approximate percentage of physical memory that is in use
            /// </summary>
            /// <remarks>
            /// 0 indicates no memory use; 100 indicates full memory use
            /// </remarks>
            public uint dwMemoryLoad;

            /// <summary>
            /// Total size of physical memory, in bytes
            /// </summary>
            public ulong ullTotalPhys;

            /// <summary>
            /// Size of physical memory available, in bytes
            /// </summary>
            public ulong ullAvailPhys;

            /// <summary>
            /// Size of the committed memory limit, in bytes
            /// </summary>
            /// <remarks>
            /// This is physical memory plus the size of the page file, minus a small overhead
            /// </remarks>
            public ulong ullTotalPageFile;

            /// <summary>
            /// Size of available memory to commit, in bytes
            /// </summary>
            /// <remarks>
            /// The limit is <see cref="ullTotalPageFile"/>
            /// </remarks>
            public ulong ullAvailPageFile;

            /// <summary>
            /// Total size of the user mode portion of the virtual address space of the calling process, in bytes
            /// </summary>
            public ulong ullTotalVirtual;

            /// <summary>
            /// Size of unreserved and uncommitted memory in the user mode portion of the virtual address space of the calling process, in bytes
            /// </summary>
            public ulong ullAvailVirtual;

            /// <summary>
            /// Size of unreserved and uncommitted memory in the extended portion of the virtual address space of the calling process, in bytes
            /// </summary>
            public ulong ullAvailExtendedVirtual;

            // ReSharper restore FieldCanBeMadeReadOnly.Local
            // ReSharper restore MemberCanBePrivate.Local

            // /// <summary>
            // /// Initializes a new instance of the <see cref="T:MEMORYSTATUSEX"/> class.
            // /// </summary>
            // public MEMORYSTATUSEX()
            // {
            //     this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            // }
        }

        private static float totalMemoryMBCached;

        // Processor Info P/Invoke

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, EntryPoint = "GetLogicalProcessorInformationEx", SetLastError = true)]
        private static extern bool GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP RelationshipType, IntPtr buffer, ref uint returnedLength);

        /// <summary>
        /// Possible relationships between logical processors
        /// </summary>
        /// <remarks>
        /// See https://msdn.microsoft.com/en-us/library/windows/desktop/dd405488(v=vs.85).aspx
        /// and https://msdn.microsoft.com/en-us/library/windows/desktop/ms684197(v=vs.85).aspx
        /// </remarks>
        private enum LOGICAL_PROCESSOR_RELATIONSHIP
        {
            /// <summary>
            /// The specified logical processors share a single processor core
            /// </summary>
            RelationProcessorCore = 0,

            /// <summary>
            /// The specified logical processors are part of the same NUMA node
            /// </summary>
            RelationNumaNode = 1,

            /// <summary>
            /// The specified logical processors share a cache
            /// </summary>
            RelationCache = 2,

            /// <summary>
            /// The specified logical processors share a physical package
            /// (a single package socketed or soldered onto a motherboard may
            /// contain multiple processor cores or threads, each of which is
            /// treated as a separate processor by the operating system)
            /// </summary>
            RelationProcessorPackage = 3,

            /// <summary>
            /// The specified logical processors share a single processor group
            /// </summary>
            RelationGroup = 4,

            /// <summary>
            /// On input, retrieves information about all possible relationship types. This value is not used on output
            /// </summary>
            // ReSharper disable once UnusedMember.Local
            RelationAll = 0xfff
        }

        // https://stackoverflow.com/questions/6972437/pinvoke-for-getlogicalprocessorinformation-function

        // ReSharper disable MemberCanBePrivate.Local
        // ReSharper disable FieldCanBeMadeReadOnly.Local

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct GROUP_AFFINITY
        {
            /// <summary>
            /// A bitmap that specifies the affinity for zero or more processors within the specified group
            /// </summary>
            /// <remarks>
            /// Platform-specific: needs to be 32 for 32-bit systems and 64 for 64-bit systems
            /// </remarks>
            // ReSharper disable once UnusedMember.Local
            public UInt64 Mask => (UInt64)MaskPtr.ToInt64();

            /// <summary>
            /// A platform-dependent method to get the Mask
            /// </summary>
            [MarshalAs(UnmanagedType.SysUInt)]

            public IntPtr MaskPtr;

            /// <summary>
            /// The processor group number
            /// </summary>
            public ushort Group;

            /// <summary>
            /// This field is reserved; array of size 3
            /// </summary>
            //[MarshalAs(UnmanagedType.SafeArray)]
            //public ushort[] Reserved;

            public ushort Reserved0;
            public ushort Reserved1;
            public ushort Reserved2;
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct PROCESSOR_RELATIONSHIP
        {
            /// <summary>
            /// <para>
            /// If the Relationship field of the SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX structure is RelationProcessorCore,
            /// this field is LTP_PC_SMT if the core has more than one logical processor, or 0 if the core has one logical processor
            /// </para>
            /// <para>
            /// If the Relationship field of the SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX structure is RelationProcessorPackage,
            /// this field is always 0
            /// </para>
            /// </summary>
            public byte Flags;

            /// <summary>
            /// <para>
            /// If the Relationship field of the SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX structure is RelationProcessorCore,
            /// EfficiencyClass specifies the intrinsic tradeoff between performance and power for the applicable core.
            /// A core with a higher value for the efficiency class has intrinsically greater performance and less efficiency than a core with a lower value for the efficiency class.
            /// EfficiencyClass is only nonzero on systems with a heterogeneous set of cores.
            /// </para>
            /// <para>
            /// If the Relationship field of the SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX structure is RelationProcessorPackage,
            /// EfficiencyClass is always 0
            /// </para>
            /// </summary>
            /// <remarks>
            /// The minimum operating system version that supports this field is Windows 10
            /// </remarks>
            public byte EfficiencyClass;

            /// <summary>
            /// This field is reserved; array of size 21
            /// </summary>
            //[MarshalAs(UnmanagedType.SafeArray)]
            //public byte[] Reserved;
            public UInt64 Reserved0_7;
            public UInt64 Reserved8_15;
            public UInt32 Reserved16_19;
            //public byte Reserved20;

            /// <summary>
            /// This field specifies the number of entries in the GroupMask array
            /// </summary>
            public ushort GroupCount;

            /// <summary>
            /// An array of GROUP_AFFINITY structures
            /// </summary>
            /// <remarks>
            /// <para>
            /// The <see cref="GroupCount"/> field specifies the number of structures in this array
            /// </para>
            /// <para>
            /// Each structure in the array specifies a group number and processor affinity within the group
            /// </para>
            /// </remarks>
            // ReSharper disable once UnusedMember.Local
            public GROUP_AFFINITY[] GroupMask
            {
                get
                {
                    var data = new GROUP_AFFINITY[GroupCount];
                    var size = Marshal.SizeOf(typeof(GROUP_AFFINITY));
                    var ptr = GroupMaskPtr;
                    for (var i = 0; i < GroupCount; i++)
                    {
                        data[i] = (GROUP_AFFINITY)Marshal.PtrToStructure(ptr, typeof(GROUP_AFFINITY));
                        ptr += size;
                    }
                    return data;
                }
            }

            /// <summary>
            /// Pointer to the array of GroupMasks
            /// </summary>
            public IntPtr GroupMaskPtr;
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct NUMA_NODE_RELATIONSHIP
        {
            /// <summary>
            /// The number of the NUMA node
            /// </summary>
            public uint NodeNumber;

            /// <summary>
            /// This field is reserved; array of size 20
            /// </summary>
            //public byte[] Reserved;
            public UInt64 Reserved0_7;
            public UInt64 Reserved8_15;
            public UInt32 Reserved16_19;

            /// <summary>
            /// A GROUP_AFFINITY structure that specifies a group number and processor affinity within the group
            /// </summary>
            public GROUP_AFFINITY GroupMask;
        }

        private enum PROCESSOR_CACHE_TYPE
        {
            /// <summary>
            /// The cache is unified
            /// </summary>
            CacheUnified = 0,

            /// <summary>
            /// The cache is for processor instructions
            /// </summary>
            CacheInstruction = 1,

            /// <summary>
            /// The cache is for data
            /// </summary>
            CacheData = 2,

            /// <summary>
            /// The cache is for traces
            /// </summary>
            CacheTrace = 3
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct CACHE_RELATIONSHIP
        {
            /// <summary>
            /// The cache level
            /// </summary>
            /// <remarks>
            /// Allowed values: 1 for L1, 2 for L2, 3 for L3
            /// </remarks>
            public byte Level;

            /// <summary>
            /// The cache associativity
            /// </summary>
            /// <remarks>
            /// If the value is CACHE_FULLY_ASSOCIATIVE (0xFF), the cache is fully associative
            /// </remarks>
            public byte Associativity;

            /// <summary>
            /// The cache line size, in bytes
            /// </summary>
            public ushort LineSize;

            /// <summary>
            /// The cache size, in bytes
            /// </summary>
            public uint CacheSize;

            /// <summary>
            /// The cache type
            /// </summary>
            public PROCESSOR_CACHE_TYPE Type;

            /// <summary>
            /// This field is reserved; array of size 20
            /// </summary>
            //public byte[] Reserved;
            public UInt64 Reserved0_7;
            public UInt64 Reserved8_15;
            public UInt32 Reserved16_19;

            /// <summary>
            /// A GROUP_AFFINITY structure that specifies a group number and processor affinity within the group
            /// </summary>
            public GROUP_AFFINITY GroupMask;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct PROCESSOR_GROUP_INFO
        {
            /// <summary>
            /// The maximum number of processors in the group
            /// </summary>
            public byte MaximumProcessorCount;

            /// <summary>
            /// The number of active processors in the group
            /// </summary>
            public byte ActiveProcessorCount;

            /// <summary>
            /// This field is reserved; array of size 38
            /// </summary>
            //public byte[] Reserved;
            public UInt64 Reserved0_7;
            public UInt64 Reserved8_15;
            public UInt64 Reserved16_23;
            public UInt64 Reserved24_31;
            public UInt32 Reserved32_35;
            public UInt16 Reserved36_37;

            /// <summary>
            /// A bitmap that specifies the affinity for zero or more active processors within the group
            /// </summary>
            /// <remarks>
            /// Platform-specific: needs to be 32 for 32-bit systems and 64 for 64-bit systems
            /// </remarks>
            // ReSharper disable once UnusedMember.Local
            public UInt64 ActiveProcessorMask => (UInt64)ActiveProcessorMaskPtr.ToInt64();

            /// <summary>
            /// A platform-dependent method to get the Mask
            /// </summary>
            [MarshalAs(UnmanagedType.SysUInt)]
            public IntPtr ActiveProcessorMaskPtr;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct GROUP_RELATIONSHIP
        {
            /// <summary>
            /// The maximum number of processor groups on the system
            /// </summary>
            public ushort MaximumGroupCount;

            /// <summary>
            /// The number of active groups on the system
            /// </summary>
            /// <remarks>
            /// This field indicates the number of PROCESSOR_GROUP_INFO structures in the <see cref="GroupInfo"/> array
            /// </remarks>
            public ushort ActiveGroupCount;

            /// <summary>
            /// This field is reserved; array of size 20
            /// </summary>
            //public byte[] Reserved;
            public UInt64 Reserved0_7;
            public UInt64 Reserved8_15;
            public UInt32 Reserved16_19;

            /// <summary>
            /// An array of PROCESSOR_GROUP_INFO structures
            /// </summary>
            /// <remarks>
            /// Each structure represents the number and affinity of processors in an active group on the system
            /// </remarks>
            //[MarshalAs(UnmanagedType.LPArray)]
            // ReSharper disable once UnusedMember.Local
            public PROCESSOR_GROUP_INFO[] GroupInfo
            {
                get
                {
                    var data = new PROCESSOR_GROUP_INFO[ActiveGroupCount];
                    var size = Marshal.SizeOf(typeof(PROCESSOR_GROUP_INFO));
                    var ptr = GroupInfoPtr;
                    for (var i = 0; i < ActiveGroupCount; i++)
                    {
                        data[i] = (PROCESSOR_GROUP_INFO)Marshal.PtrToStructure(ptr, typeof(PROCESSOR_GROUP_INFO));
                        ptr += size;
                    }
                    return data;
                }
            }

            /// <summary>
            /// Pointer to the array of GroupInfos
            /// </summary>
            public IntPtr GroupInfoPtr;
        }

        // ReSharper restore FieldCanBeMadeReadOnly.Local
        // ReSharper restore MemberCanBePrivate.Local

        /*[StructLayout(LayoutKind.Explicit, Pack = 1)]
        private struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION_UNION
        {
            /// <summary>
            /// A PROCESSOR_RELATIONSHIP structure that describes processor affinity
            /// </summary>
            /// <remarks>
            /// This structure contains valid data only if the Relationship field is RelationProcessorCore or RelationProcessorPackage
            /// </remarks>
            [FieldOffset(0)]
            public PROCESSOR_RELATIONSHIP Processor;

            /// <summary>
            /// A NUMA_NODE_RELATIONSHIP structure that describes a NUMA node
            /// </summary>
            /// <remarks>
            /// This structure contains valid data only if the Relationship field is RelationNumaNode
            /// </remarks>
            [FieldOffset(0)]
            public NUMA_NODE_RELATIONSHIP NumaNode;

            /// <summary>
            /// A CACHE_RELATIONSHIP structure that describes cache attributes
            /// </summary>
            /// <remarks>
            /// This structure contains valid data only if the Relationship field is RelationCache
            /// </remarks>
            [FieldOffset(0)]
            public CACHE_RELATIONSHIP Cache;

            /// <summary>
            /// A GROUP_RELATIONSHIP structure that contains information about the processor groups
            /// </summary>
            /// <remarks>
            /// This structure contains valid data only if the Relationship field is RelationGroup
            /// </remarks>
            [FieldOffset(0)]
            public GROUP_RELATIONSHIP Group;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
        {
            /// <summary>
            /// The type of relationship between the logical processors
            /// </summary>
            public LOGICAL_PROCESSOR_RELATIONSHIP Relationship;

            /// <summary>
            /// The size of the structure
            /// </summary>
            public uint Size;

            /// <summary>
            /// Details: contents depend on Relationship
            /// </summary>
            public SYSTEM_LOGICAL_PROCESSOR_INFORMATION_UNION ProcessorInformation;
        }*/

        private interface ISYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
        {
            /// <summary>
            /// The type of relationship between the logical processors
            /// </summary>
            LOGICAL_PROCESSOR_RELATIONSHIP ProcRelationship { get; }

            /// <summary>
            /// The size of the structure
            /// </summary>
            uint StructSize { get; }
        }

        // ReSharper disable MemberCanBePrivate.Local
        // ReSharper disable FieldCanBeMadeReadOnly.Local

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SLPI_PROCESSOR_RELATIONSHIP : ISYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
        {
            /// <summary>
            /// The type of relationship between the logical processors
            /// </summary>
            public LOGICAL_PROCESSOR_RELATIONSHIP Relationship;

            /// <summary>
            /// The size of the structure
            /// </summary>
            public uint Size;

            public PROCESSOR_RELATIONSHIP Processor;

            public LOGICAL_PROCESSOR_RELATIONSHIP ProcRelationship => Relationship;

            public uint StructSize => Size;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SLPI_NUMA_NODE_RELATIONSHIP : ISYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
        {
            /// <summary>
            /// The type of relationship between the logical processors
            /// </summary>
            public LOGICAL_PROCESSOR_RELATIONSHIP Relationship;

            /// <summary>
            /// The size of the structure
            /// </summary>
            public uint Size;

            public NUMA_NODE_RELATIONSHIP NumaNode;

            public LOGICAL_PROCESSOR_RELATIONSHIP ProcRelationship => Relationship;

            public uint StructSize => Size;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SLPI_CACHE_RELATIONSHIP : ISYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
        {
            /// <summary>
            /// The type of relationship between the logical processors
            /// </summary>
            public LOGICAL_PROCESSOR_RELATIONSHIP Relationship;

            /// <summary>
            /// The size of the structure
            /// </summary>
            public uint Size;

            public CACHE_RELATIONSHIP Cache;

            public LOGICAL_PROCESSOR_RELATIONSHIP ProcRelationship => Relationship;

            public uint StructSize => Size;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SLPI_GROUP_RELATIONSHIP : ISYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
        {
            /// <summary>
            /// The type of relationship between the logical processors
            /// </summary>
            public LOGICAL_PROCESSOR_RELATIONSHIP Relationship;

            /// <summary>
            /// The size of the structure
            /// </summary>
            public uint Size;

            public GROUP_RELATIONSHIP Group;

            public LOGICAL_PROCESSOR_RELATIONSHIP ProcRelationship => Relationship;

            public uint StructSize => Size;
        }

        // ReSharper restore FieldCanBeMadeReadOnly.Local
        // ReSharper restore MemberCanBePrivate.Local

        private const int ERROR_INSUFFICIENT_BUFFER = 122;

        private static int GetProcessorInfo(LOGICAL_PROCESSOR_RELATIONSHIP coreCountType)
        {
            var buffer = new List<ISYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX>();
            uint returnLength = 0;

            GetLogicalProcessorInformationEx(coreCountType, IntPtr.Zero, ref returnLength);

            if (Marshal.GetLastWin32Error() != ERROR_INSUFFICIENT_BUFFER)
            {
                return buffer.Count;
            }

            var ptr = Marshal.AllocHGlobal((int)returnLength);
            try
            {
                if (GetLogicalProcessorInformationEx(coreCountType, ptr, ref returnLength))
                {
                    var item = ptr;
                    var readCount = 0;

                    //int size = Marshal.SizeOf(typeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX));
                    //int length = (int)returnLength / size;
                    //for (int i = 0; i < length; i++)

                    while (readCount < returnLength)
                    {
                        var type = (LOGICAL_PROCESSOR_RELATIONSHIP)Marshal.ReadInt32(item);
                        var size = Marshal.ReadInt32(item, 4);
                        var data = new byte[size];
                        Marshal.Copy(item, data, 0, size);

                        switch (type)
                        {
                            case LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorPackage:
                            case LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore:
                                buffer.Add((SLPI_PROCESSOR_RELATIONSHIP)Marshal.PtrToStructure(item, typeof(SLPI_PROCESSOR_RELATIONSHIP)));
                                break;

                            case LOGICAL_PROCESSOR_RELATIONSHIP.RelationNumaNode:
                                buffer.Add((SLPI_NUMA_NODE_RELATIONSHIP)Marshal.PtrToStructure(item, typeof(SLPI_NUMA_NODE_RELATIONSHIP)));
                                break;

                            case LOGICAL_PROCESSOR_RELATIONSHIP.RelationCache:
                                buffer.Add((SLPI_CACHE_RELATIONSHIP)Marshal.PtrToStructure(item, typeof(SLPI_CACHE_RELATIONSHIP)));
                                break;

                            case LOGICAL_PROCESSOR_RELATIONSHIP.RelationGroup:
                                buffer.Add((SLPI_GROUP_RELATIONSHIP)Marshal.PtrToStructure(item, typeof(SLPI_GROUP_RELATIONSHIP)));
                                break;
                        }

                        item += size;
                        readCount += size;
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            //for each (var process in buffer)
            //{
            //    Console.WriteLine(process.ProcRelationship.ToString());
            //}

            return buffer.Count;
        }

        private static void GetProcessorInformation()
        {
            if (loadedProcessorInformation)
            {
                return;
            }

            // NOTE: Can probably also pull the logical core count via GetProcessorInfo(LOGICAL_PROCESSOR_RELATIONSHIP.RelationCache), with an internal filter to only count objects that specify L1 cache
            logicalCoreCount = Environment.ProcessorCount;
            physicalCoreCount = GetProcessorInfo(LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore);
            processorPackageCount = GetProcessorInfo(LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorPackage);
            numaNodeCount = GetProcessorInfo(LOGICAL_PROCESSOR_RELATIONSHIP.RelationNumaNode);

            loadedProcessorInformation = true;
        }

        private static int logicalCoreCount;
        private static int physicalCoreCount;
        private static int processorPackageCount;
        private static int numaNodeCount;
        private static bool loadedProcessorInformation;

        /// <summary>
        /// Constructor
        /// </summary>
        public WindowsSystemInfoInternal()
        {
            mQuotedStringMatcher = new Regex("\"[^\"]+\"", RegexOptions.Compiled);
        }

        private Dictionary<uint, string> CachedWmiCmdLineData;

        private void CacheWmiCmdLineData()
        {
            CachedWmiCmdLineData = new Dictionary<uint, string>();

            using var searcher = new System.Management.ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process");

            // Store the results in a dictionary
            var matchEnum = searcher.Get().GetEnumerator();

            using var disposableMatchEnum = (IDisposable)matchEnum;

            while (matchEnum.MoveNext())
            {
                var processId = (uint)matchEnum.Current["ProcessId"];
                var cmdLine = matchEnum.Current["CommandLine"]?.ToString();
                CachedWmiCmdLineData.Add(processId, cmdLine);
            }
        }

        private void DumpCachedWmiCmdLineData()
        {
            CachedWmiCmdLineData?.Clear();
            CachedWmiCmdLineData = null;
        }

        /// <summary>
        /// Determine the command line of a process by ProcessID
        /// </summary>
        /// <param name="process">Process</param>
        /// <param name="exePath">Executable path</param>
        /// <param name="argumentList">List of command line arguments</param>
        /// <returns>Full command line: exePath (surrounded in double quotes if a space), then a space, then the arguments</returns>
        private string GetCommandLine(Process process, out string exePath, out List<string> argumentList)
        {
            argumentList = new List<string>();

            string cmdLine = null;

            if (CachedWmiCmdLineData == null)
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    string.Format("SELECT CommandLine FROM Win32_Process WHERE ProcessId = {0}", process.Id));

                var matchEnum = searcher.Get().GetEnumerator();

                using var disposableMatchEnum = (IDisposable)matchEnum;

                // Move to the 1st item
                if (matchEnum.MoveNext())
                {
                    cmdLine = matchEnum.Current["CommandLine"]?.ToString();
                }
            }
            else
            {
                CachedWmiCmdLineData.TryGetValue((uint)process.Id, out cmdLine);
            }

            if (cmdLine == null)
            {
                // Not having found a command line implies 1 of 2 exceptions, which the WMI query masked:
                // 1) An "Access denied" exception due to lack of privileges
                // 2) A "Cannot process request because the process (<pid>) has exited" exception, meaning the process has terminated

                // Force the exception to be raised by trying to access process.MainModule
                _ = process.MainModule;

                exePath = string.Empty;
            }
            else
            {
                string arguments;

                if (cmdLine.StartsWith("\""))
                {
                    var match = mQuotedStringMatcher.Match(cmdLine);

                    if (match.Success)
                    {
                        // Remove leading/trailing double quotes when defining exePath
                        exePath = match.Value.Trim('"');
                        arguments = cmdLine.Substring(match.Index + match.Length).Trim();
                    }
                    else
                    {
                        exePath = cmdLine;
                        arguments = string.Empty;
                    }
                }
                else
                {
                    // Command line does not start with double quotes
                    // If on Windows, look for the first /

                    int splitIndex;

                    if (System.IO.Path.DirectorySeparatorChar == '\\')
                    {
                        var slashIndex = cmdLine.IndexOf('/');

                        if (slashIndex > 0)
                        {
                            splitIndex = slashIndex - 1;
                        }
                        else
                        {
                            // Look for the first space
                            splitIndex = cmdLine.IndexOf(' ');
                        }
                    }
                    else
                    {
                        // Look for the first space
                        splitIndex = cmdLine.IndexOf(' ');
                    }

                    if (splitIndex > 0)
                    {
                        exePath = cmdLine.Substring(0, splitIndex);

                        if (splitIndex < cmdLine.Length - 1)
                        {
                            arguments = cmdLine.Substring(splitIndex + 1).Trim();
                        }
                        else
                        {
                            arguments = string.Empty;
                        }
                    }
                    else
                    {
                        exePath = cmdLine;
                        arguments = string.Empty;
                    }
                }

                var cleanedArguments = arguments;

                var argumentMatch = mQuotedStringMatcher.Match(arguments);

                while (argumentMatch.Success)
                {
                    argumentList.Add(argumentMatch.Value.Trim('"'));
                    cleanedArguments = cleanedArguments.Replace(argumentMatch.Value, string.Empty);

                    argumentMatch = argumentMatch.NextMatch();
                }

                if (!string.IsNullOrWhiteSpace(cleanedArguments))
                {
                    // cleanedArguments contains some unquoted arguments; add them
                    var remainingArgs = cleanedArguments.Split(' ');
                    argumentList.AddRange(remainingArgs);
                }
            }

            return cmdLine;
        }

        /// <summary>
        /// Report the number of cores on this system
        /// </summary>
        /// <remarks>
        /// Should not be affected by hyperthreading, so a computer with two 8-core chips will report 16 cores, even if hyperthreading is enabled
        /// </remarks>
        /// <returns>The number of cores on this computer</returns>
        public int GetCoreCount()
        {
            return GetPhysicalCoreCount();
        }

        /// <summary>
        /// Report the number of logical cores on this system
        /// </summary>
        /// <remarks>
        /// Will be affected by hyperthreading, so a computer with two 8-core chips will report 32 cores if hyperthreading is enabled
        /// </remarks>
        /// <returns>The number of logical cores on this computer</returns>
        public int GetLogicalCoreCount()
        {
            GetProcessorInformation();
            return logicalCoreCount;
        }

        /// <summary>
        /// Report the number of cores on this system
        /// </summary>
        /// <remarks>
        /// Should not be affected by hyperthreading, so a computer with two 8-core chips will report 16 cores, even if hyperthreading is enabled
        /// </remarks>
        /// <returns>The number of cores on this computer</returns>
        public int GetPhysicalCoreCount()
        {
            GetProcessorInformation();
            return physicalCoreCount;
        }

        /// <summary>
        /// Report the number of processor packages on this system
        /// </summary>
        /// <returns>The number of processor packages on this computer</returns>
        public int GetProcessorPackageCount()
        {
            GetProcessorInformation();
            return processorPackageCount;
        }

        /// <summary>
        /// Report the number of NUMA Nodes on this system
        /// </summary>
        /// <returns>The number of NUMA Nodes on this computer</returns>
        public int GetNumaNodeCount()
        {
            GetProcessorInformation();
            return numaNodeCount;
        }

        /// <summary>
        /// Determine the free system memory, in MB
        /// </summary>
        /// <remarks>This uses kernel32.dll and is very fast</remarks>
        /// <returns>Free memory, or -1 if an error</returns>
        public float GetFreeMemoryMB()
        {
            // ReSharper disable once UnusedVariable
            var result = GetGlobalMemoryStatusEx(out var memData);

            // Convert from bytes to MB
            return memData.ullAvailPhys / 1024f / 1024f;
        }

        /// <summary>
        /// Look for currently active processes
        /// </summary>
        /// <remarks>Command line lookup can be slow because it uses WMI; set lookupCommandLineInfo to false to speed things up</remarks>
        /// <param name="lookupCommandLineInfo">When true, the process info dictionary will include the exe path and command line arguments</param>
        /// <returns>Dictionary where keys are process ID and values are ProcessInfo</returns>
        public Dictionary<int, ProcessInfo> GetProcesses(bool lookupCommandLineInfo = true)
        {
            var processList = new Dictionary<int, ProcessInfo>();

            if (lookupCommandLineInfo)
            {
                CacheWmiCmdLineData();
            }

            foreach (var item in Process.GetProcesses())
            {
                if (!lookupCommandLineInfo)
                {
                    processList.Add(item.Id, new ProcessInfo(item.Id, item.ProcessName));
                    continue;
                }

                ProcessInfo process;

                try
                {
                    var cmdLine = GetCommandLine(item, out var exePath, out var argumentList);

                    var oldExePath = exePath;

                    // MainModule.FileName provides a nicer path format; try to access it
                    // This can throw an exception, but that's expected when examining processes that are elevated or belong to other users
                    exePath = item.MainModule.FileName;
                    cmdLine = cmdLine.Replace(oldExePath, exePath);

                    process = new ProcessInfo(item.Id, item.ProcessName, exePath, argumentList, cmdLine);
                }
                catch (System.ComponentModel.Win32Exception ex) when (ex.HResult == -2147467259)
                {
                    // Access is denied; simply store process ID and name
                    process = new ProcessInfo(item.Id, item.ProcessName);
                }
                catch (InvalidOperationException ex) when (ex.HResult == -2146233079)
                {
                    // The process has ended; skip it
                    continue;
                }
                catch (Exception)
                {
                    // Ignore all other exceptions, but still store the process
                    process = new ProcessInfo(item.Id, item.ProcessName);
                }

                processList.Add(item.Id, process);
            }

            if (lookupCommandLineInfo)
            {
                DumpCachedWmiCmdLineData();
            }

            return processList;
        }

        /// <summary>
        /// Determine the total system memory, in MB
        /// </summary>
        /// <returns>Total memory, or -1 if an error</returns>
        public float GetTotalMemoryMB()
        {
            if (totalMemoryMBCached > 0)
            {
                return totalMemoryMBCached;
            }

            // ReSharper disable once UnusedVariable
            var result = GetGlobalMemoryStatusEx(out var memData);

            // Convert from bytes to MB
            return memData.ullTotalPhys / 1024f / 1024f;
        }
    }
}

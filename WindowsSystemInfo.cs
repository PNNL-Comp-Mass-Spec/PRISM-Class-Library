using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PRISM
{
    /// <summary>
    /// System information for Windows, pulled via P/Invoke
    /// </summary>
    public class WindowsSystemInfo : clsEventNotifier, ISystemInfo
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public WindowsSystemInfo()
        {
            var c = new clsOSVersionInfo();
            if (!c.GetOSVersion().ToLower().Contains("windows"))
            {
                throw new Exception("This class only functions on Windows platforms");
            }

            pData = new WindowsSystemInfoInternal();
            RegisterEvents(pData);
        }

        private readonly WindowsSystemInfoInternal pData;

        /// <inheritdoc />
        /// <summary>
        /// Report the number of cores on this system
        /// </summary>
        /// <returns>The number of cores on this computer</returns>
        /// <remarks>
        /// Should not be affected by hyperthreading, so a computer with two 8-core chips will report 16 cores, even if Hyperthreading is enabled
        /// </remarks>
        public int GetCoreCount()
        {
            return pData.GetCoreCount();
        }

        /// <inheritdoc />
        /// <summary>
        /// Report the number of logical cores on this system
        /// </summary>
        /// <returns>The number of logical cores on this computer</returns>
        /// <remarks>
        /// Will be affected by hyperthreading, so a computer with two 8-core chips will report 32 cores if Hyperthreading is enabled
        /// </remarks>
        public int GetLogicalCoreCount()
        {
            return pData.GetLogicalCoreCount();
        }

        /// <summary>
        /// Report the number of cores on this system
        /// </summary>
        /// <returns>The number of cores on this computer</returns>
        /// <remarks>
        /// Should not be affected by hyperthreading, so a computer with two 8-core chips will report 16 cores, even if Hyperthreading is enabled
        /// </remarks>
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
        /// <param name="lookupCommandLineInfo">When true, the process info dictionary will include the exe path and command line arguments</param>
        /// <returns>Dictionary where keys are process ID and values are ProcessInfo</returns>
        /// <remarks>Command line lookup can be slow because it uses WMI; set lookupCommandLineInfo to false to speed things up</remarks>
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
    /// Internal implementation of WindowsSystemInfo. Internal to avoid big errors when trying to instantiate.
    /// </summary>
    internal class WindowsSystemInfoInternal : clsEventNotifier, ISystemInfo
    {

        /// <summary>
        /// Matches strings surrounded by double quotes
        /// </summary>
        private readonly Regex mQuotedStringMatcher;

        #region Memory P/Invoke

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
        public struct MEMORYSTATUSEX
        {
            /// <summary>
            /// Size of the structure, in bytes. You must set this member before calling GlobalMemoryStatusEx.
            /// </summary>
            public uint dwLength;

            /// <summary>
            /// Number between 0 and 100 that specifies the approximate percentage of physical memory that is in use (0 indicates no memory use and 100 indicates full memory use).
            /// </summary>
            public uint dwMemoryLoad;

            /// <summary>
            /// Total size of physical memory, in bytes.
            /// </summary>
            public ulong ullTotalPhys;

            /// <summary>
            /// Size of physical memory available, in bytes.
            /// </summary>
            public ulong ullAvailPhys;

            /// <summary>
            /// Size of the committed memory limit, in bytes. This is physical memory plus the size of the page file, minus a small overhead.
            /// </summary>
            public ulong ullTotalPageFile;

            /// <summary>
            /// Size of available memory to commit, in bytes. The limit is ullTotalPageFile.
            /// </summary>
            public ulong ullAvailPageFile;

            /// <summary>
            /// Total size of the user mode portion of the virtual address space of the calling process, in bytes.
            /// </summary>
            public ulong ullTotalVirtual;

            /// <summary>
            /// Size of unreserved and uncommitted memory in the user mode portion of the virtual address space of the calling process, in bytes.
            /// </summary>
            public ulong ullAvailVirtual;

            /// <summary>
            /// Size of unreserved and uncommitted memory in the extended portion of the virtual address space of the calling process, in bytes.
            /// </summary>
            public ulong ullAvailExtendedVirtual;

            ///// <summary>
            ///// Initializes a new instance of the <see cref="T:MEMORYSTATUSEX"/> class.
            ///// </summary>
            //public MEMORYSTATUSEX()
            //{
            //    this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            //}
        }

        private static float totalMemoryMBCached;

        #endregion

        #region Processor Info P/Invoke

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, EntryPoint = "GetLogicalProcessorInformationEx", SetLastError = true)]
        private static extern bool GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP RelationshipType, IntPtr buffer, ref uint returnedLength);

        /// <summary>
        /// Possible relationships between logical processors.
        /// See https://msdn.microsoft.com/en-us/library/windows/desktop/dd405488(v=vs.85).aspx
        /// See https://msdn.microsoft.com/en-us/library/windows/desktop/ms684197(v=vs.85).aspx
        /// </summary>
        private enum LOGICAL_PROCESSOR_RELATIONSHIP
        {
            /// <summary>
            /// The specified logical processors share a single processor core.
            /// </summary>
            RelationProcessorCore = 0,

            /// <summary>
            /// The specified logical processors are part of the same NUMA node.
            /// </summary>
            RelationNumaNode = 1,

            /// <summary>
            /// The specified logical processors share a cache.
            /// </summary>
            RelationCache = 2,

            /// <summary>
            /// The specified logical processors share a physical package (a single package socketed or soldered onto a motherboard may contain multiple processor cores or threads, each of which is treated as a separate processor by the operating system).
            /// </summary>
            RelationProcessorPackage = 3,

            /// <summary>
            /// The specified logical processors share a single processor group.
            /// </summary>
            RelationGroup = 4,

            /// <summary>
            /// On input, retrieves information about all possible relationship types. This value is not used on output.
            /// </summary>
            // ReSharper disable once UnusedMember.Local
            RelationAll = 0xfff
        }

        //https://stackoverflow.com/questions/6972437/pinvoke-for-getlogicalprocessorinformation-function
        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct GROUP_AFFINITY
        {
            /// <summary>
            /// A bitmap that specifies the affinity for zero or more processors within the specified group.
            /// Platform-specific, needs to be 32 bits for 32-bit systems and 64 bits for 64-bit systems
            /// </summary>
            // ReSharper disable once UnusedMember.Local
            public UInt64 Mask => (UInt64)MaskPtr.ToInt64();

            /// <summary>
            /// A platform-dependent method to get the Mask
            /// </summary>
            [MarshalAs(UnmanagedType.SysUInt)]
            public IntPtr MaskPtr;

            /// <summary>
            /// The processor group number.
            /// </summary>
            public ushort Group;

            /// <summary>
            /// This member is reserved. Array of size 3
            /// </summary>
            //[MarshalAs(UnmanagedType.SafeArray)]
            //public ushort[] Reserved;
            public ushort Reserved0;
            public ushort Reserved1;
            public ushort Reserved2;
        };

        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct PROCESSOR_RELATIONSHIP
        {
            /// <summary>
            /// If the Relationship member of the SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX structure is RelationProcessorCore, this member is LTP_PC_SMT if the core has more than one logical processor, or 0 if the core has one logical processor.
            /// If the Relationship member of the SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX structure is RelationProcessorPackage, this member is always 0.
            /// </summary>
            public byte Flags;

            /// <summary>
            /// If the Relationship member of the SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX structure is RelationProcessorCore, EfficiencyClass specifies the intrinsic tradeoff between performance and power for the applicable core. A core with a higher value for the efficiency class has intrinsically greater performance and less efficiency than a core with a lower value for the efficiency class. EfficiencyClass is only nonzero on systems with a heterogeneous set of cores.
            /// If the Relationship member of the SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX structure is RelationProcessorPackage, EfficiencyClass is always 0.
            /// The minimum operating system version that supports this member is Windows 10.
            /// </summary>
            public byte EfficiencyClass;

            /// <summary>
            /// This member is reserved. Array of size 21
            /// </summary>
            //[MarshalAs(UnmanagedType.SafeArray)]
            //public byte[] Reserved;
            public UInt64 Reserved0_7;
            public UInt64 Reserved8_15;
            public UInt32 Reserved16_19;
            //public byte Reserved20;

            /// <summary>
            /// This member specifies the number of entries in the GroupMask array. For more information, see Remarks.
            /// </summary>
            public ushort GroupCount;

            /// <summary>
            /// An array of GROUP_AFFINITY structures. The GroupCount member specifies the number of structures in the array. Each structure in the array specifies a group number and processor affinity within the group.
            /// </summary>
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

        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct NUMA_NODE_RELATIONSHIP
        {
            /// <summary>
            /// The number of the NUMA node.
            /// </summary>
            public uint NodeNumber;

            /// <summary>
            /// This member is reserved. Array of size 20
            /// </summary>
            //public byte[] Reserved;
            public UInt64 Reserved0_7;
            public UInt64 Reserved8_15;
            public UInt32 Reserved16_19;

            /// <summary>
            /// A GROUP_AFFINITY structure that specifies a group number and processor affinity within the group.
            /// </summary>
            public GROUP_AFFINITY GroupMask;
        }

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        private enum PROCESSOR_CACHE_TYPE
        {
            /// <summary>
            /// The cache is unified.
            /// </summary>
            CacheUnified = 0,

            /// <summary>
            /// The cache is for processor instructions.
            /// </summary>
            CacheInstruction = 1,

            /// <summary>
            /// The cache is for data.
            /// </summary>
            CacheData = 2,

            /// <summary>
            /// The cache is for traces.
            /// </summary>
            CacheTrace = 3
        }

        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct CACHE_RELATIONSHIP
        {
            /// <summary>
            /// The cache level. This member can be one of the following values: 1 for L1, 2 for L2, 3 for L3.
            /// </summary>
            public byte Level;

            /// <summary>
            /// The cache associativity. If this member is CACHE_FULLY_ASSOCIATIVE (0xFF), the cache is fully associative.
            /// </summary>
            public byte Associativity;

            /// <summary>
            /// The cache line size, in bytes.
            /// </summary>
            public ushort LineSize;

            /// <summary>
            /// The cache size, in bytes.
            /// </summary>
            public uint CacheSize;

            /// <summary>
            /// The cache type. This member is a PROCESSOR_CACHE_TYPE value.
            /// </summary>
            public PROCESSOR_CACHE_TYPE Type;

            /// <summary>
            /// This member is reserved. Array of size 20
            /// </summary>
            //public byte[] Reserved;
            public UInt64 Reserved0_7;
            public UInt64 Reserved8_15;
            public UInt32 Reserved16_19;

            /// <summary>
            /// A GROUP_AFFINITY structure that specifies a group number and processor affinity within the group.
            /// </summary>
            public GROUP_AFFINITY GroupMask;
        }

        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct PROCESSOR_GROUP_INFO
        {
            /// <summary>
            /// The maximum number of processors in the group.
            /// </summary>
            public byte MaximumProcessorCount;

            /// <summary>
            /// The number of active processors in the group.
            /// </summary>
            public byte ActiveProcessorCount;

            /// <summary>
            /// This member is reserved. Array of size 38
            /// </summary>
            //public byte[] Reserved;
            public UInt64 Reserved0_7;
            public UInt64 Reserved8_15;
            public UInt64 Reserved16_23;
            public UInt64 Reserved24_31;
            public UInt32 Reserved32_35;
            public UInt16 Reserved36_37;

            /// <summary>
            /// A bitmap that specifies the affinity for zero or more active processors within the group.
            /// Platform-specific, needs to be 32 bits for 32-bit systems and 64 bits for 64-bit systems
            /// </summary>
            // ReSharper disable once UnusedMember.Local
            public UInt64 ActiveProcessorMask => (UInt64)ActiveProcessorMaskPtr.ToInt64();

            /// <summary>
            /// A platform-dependent method to get the Mask
            /// </summary>
            [MarshalAs(UnmanagedType.SysUInt)]
            public IntPtr ActiveProcessorMaskPtr;
        }

        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct GROUP_RELATIONSHIP
        {
            /// <summary>
            /// The maximum number of processor groups on the system.
            /// </summary>
            public ushort MaximumGroupCount;

            /// <summary>
            /// The number of active groups on the system. This member indicates the number of PROCESSOR_GROUP_INFO structures in the GroupInfo array.
            /// </summary>
            public ushort ActiveGroupCount;

            /// <summary>
            /// This member is reserved. Array of size 20
            /// </summary>
            //public byte[] Reserved;
            public UInt64 Reserved0_7;
            public UInt64 Reserved8_15;
            public UInt32 Reserved16_19;

            /// <summary>
            /// An array of PROCESSOR_GROUP_INFO structures. Each structure represents the number and affinity of processors in an active group on the system.
            /// </summary>
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

        /*[StructLayout(LayoutKind.Explicit, Pack = 1)]
        private struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION_UNION
        {
            /// <summary>
            /// A PROCESSOR_RELATIONSHIP structure that describes processor affinity. This structure contains valid data only if the Relationship member is RelationProcessorCore or RelationProcessorPackage.
            /// </summary>
            [FieldOffset(0)]
            public PROCESSOR_RELATIONSHIP Processor;

            /// <summary>
            /// A NUMA_NODE_RELATIONSHIP structure that describes a NUMA node. This structure contains valid data only if the Relationship member is RelationNumaNode.
            /// </summary>
            [FieldOffset(0)]
            public NUMA_NODE_RELATIONSHIP NumaNode;

            /// <summary>
            /// A CACHE_RELATIONSHIP structure that describes cache attributes. This structure contains valid data only if the Relationship member is RelationCache.
            /// </summary>
            [FieldOffset(0)]
            public CACHE_RELATIONSHIP Cache;

            /// <summary>
            /// A GROUP_RELATIONSHIP structure that contains information about the processor groups. This structure contains valid data only if the Relationship member is RelationGroup.
            /// </summary>
            [FieldOffset(0)]
            public GROUP_RELATIONSHIP Group;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
        {
            /// <summary>
            /// The type of relationship between the logical processors.
            /// </summary>
            public LOGICAL_PROCESSOR_RELATIONSHIP Relationship;

            /// <summary>
            /// The size of the structure
            /// </summary>
            public uint Size;

            /// <summary>
            /// Details - contents depend on Relationship
            /// </summary>
            public SYSTEM_LOGICAL_PROCESSOR_INFORMATION_UNION ProcessorInformation;
        }*/

        private interface ISYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
        {
            /// <summary>
            /// The type of relationship between the logical processors.
            /// </summary>
            LOGICAL_PROCESSOR_RELATIONSHIP ProcRelationship { get; }

            /// <summary>
            /// The size of the structure
            /// </summary>
            uint StructSize { get; }
        }

        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SLPI_PROCESSOR_RELATIONSHIP : ISYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
        {
            /// <summary>
            /// The type of relationship between the logical processors.
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

        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SLPI_NUMA_NODE_RELATIONSHIP : ISYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
        {
            /// <summary>
            /// The type of relationship between the logical processors.
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

        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SLPI_CACHE_RELATIONSHIP : ISYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
        {
            /// <summary>
            /// The type of relationship between the logical processors.
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

        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SLPI_GROUP_RELATIONSHIP : ISYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
        {
            /// <summary>
            /// The type of relationship between the logical processors.
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

        private const int ERROR_INSUFFICIENT_BUFFER = 122;

        private int GetProcessorInfo(LOGICAL_PROCESSOR_RELATIONSHIP coreCountType)
        {
            var buffer = new List<ISYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX>();
            uint returnLength = 0;
            GetLogicalProcessorInformationEx(coreCountType, IntPtr.Zero, ref returnLength);
            if (Marshal.GetLastWin32Error() == ERROR_INSUFFICIENT_BUFFER)
            {
                var ptr = Marshal.AllocHGlobal((int)returnLength);
                try
                {
                    if (GetLogicalProcessorInformationEx(coreCountType, ptr, ref returnLength))
                    {
                        var item = ptr;
                        var readCount = 0;
                        //int size = Marshal.SizeOf(typeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX));
                        //int len = (int)returnLength / size;
                        //for (int i = 0; i < len; i++)
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
            }
            //foreach (var proc in buffer)
            //{
            //    Console.WriteLine(proc.ProcRelationship.ToString());
            //}

            return buffer.Count;
        }

        private void GetProcessorInformation()
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

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public WindowsSystemInfoInternal()
        {
            mQuotedStringMatcher = new Regex("\"[^\"]+\"", RegexOptions.Compiled);
        }

        /// <summary>
        /// Determine the command line of a process by ProcessID
        /// </summary>
        /// <param name="process"></param>
        /// <param name="exePath"></param>
        /// <param name="argumentList"></param>
        /// <returns>Full command line: exePath (surrounded in double quotes if a space), then a space, then the arguments</returns>
        private string GetCommandLine(Process process, out string exePath, out List<string> argumentList)
        {
            argumentList = new List<string>();

#if (NETSTANDARD2_0)
    exePath = string.Empty;
    return string.Empty;
#else

            string cmdLine = null;

            using (var searcher = new System.Management.ManagementObjectSearcher(
                string.Format("SELECT CommandLine FROM Win32_Process WHERE ProcessId = {0}", process.Id)))
            {
                var matchEnum = searcher.Get().GetEnumerator();

                // Move to the 1st item.
                if (matchEnum.MoveNext())
                {
                    cmdLine = matchEnum.Current["CommandLine"]?.ToString();
                }
            }

            if (cmdLine == null)
            {
                // Not having found a command line implies 1 of 2 exceptions, which the WMI query masked:
                // 1) An "Access denied" exception due to lack of privileges.
                // 2) A "Cannot process request because the process (<pid>) has exited." exception, meaning the process has terminated

                // Force the exception to be raised by trying to access process.MainModule.
                var dummy = process.MainModule;

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
                        arguments = "";
                    }
                }

                var cleanedArguments = string.Copy(arguments);

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
#endif
        }

        /// <summary>
        /// Report the number of cores on this system
        /// </summary>
        /// <returns>The number of cores on this computer</returns>
        /// <remarks>
        /// Should not be affected by hyperthreading, so a computer with two 8-core chips will report 16 cores, even if Hyperthreading is enabled
        /// </remarks>
        public int GetCoreCount()
        {
            return GetPhysicalCoreCount();
        }

        /// <summary>
        /// Report the number of logical cores on this system
        /// </summary>
        /// <returns>The number of logical cores on this computer</returns>
        /// <remarks>
        /// Will be affected by hyperthreading, so a computer with two 8-core chips will report 32 cores if Hyperthreading is enabled
        /// </remarks>
        public int GetLogicalCoreCount()
        {
            GetProcessorInformation();
            return logicalCoreCount;
        }

        /// <summary>
        /// Report the number of cores on this system
        /// </summary>
        /// <returns>The number of cores on this computer</returns>
        /// <remarks>
        /// Should not be affected by hyperthreading, so a computer with two 8-core chips will report 16 cores, even if Hyperthreading is enabled
        /// </remarks>
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
        /// <param name="lookupCommandLineInfo">When true, the process info dictionary will include the exe path and command line arguments</param>
        /// <returns>Dictionary where keys are process ID and values are ProcessInfo</returns>
        /// <remarks>Command line lookup can be slow because it uses WMI; set lookupCommandLineInfo to false to speed things up</remarks>
        public Dictionary<int, ProcessInfo> GetProcesses(bool lookupCommandLineInfo = true)
        {

            var processList = new Dictionary<int, ProcessInfo>();

            var lastProgress = DateTime.UtcNow;
            var notifiedLongRunning = false;

            foreach (var item in Process.GetProcesses())
            {
                ProcessInfo process;
                if (lookupCommandLineInfo)
                {
                    try
                    {
                        var cmdLine = GetCommandLine(item, out var exePath, out var argumentList);

                        process = new ProcessInfo(item.Id, item.ProcessName, exePath, argumentList, cmdLine);
                    }
                    catch (System.ComponentModel.Win32Exception ex) when (ex.HResult == -2147467259)
                    {
                        // OnDebugEvent(string.Format("Ignore Access Denied for process ID {0}", item.Id));
                        process = new ProcessInfo(item.Id, item.ProcessName);
                    }
                    catch (InvalidOperationException ex) when (ex.HResult == -2146233079)
                    {
                        // OnDebugEvent(string.Format("Ignore Cannot process the request for process ID {0} because it has ended", item.Id));
                        continue;
                    }
                    catch (Exception)
                    {
                        // Ignore all other exceptions
                        // OnDebugEvent(string.Format("Ignore exception for process ID {0}: {1}", item.Id, ex.Message));
                        process = new ProcessInfo(item.Id, item.ProcessName);
                    }
                }
                else
                {
                    process = new ProcessInfo(item.Id, item.ProcessName);
                }

                processList.Add(item.Id, process);

                if (DateTime.UtcNow.Subtract(lastProgress).TotalSeconds < 1)
                    continue;

                lastProgress = DateTime.UtcNow;
                if (!notifiedLongRunning)
                {
                    Console.Write("Enumerating system processes ");
                    notifiedLongRunning = true;
                }
                else
                {
                    Console.Write(".");
                }
            }

            if (notifiedLongRunning)
                Console.WriteLine();

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

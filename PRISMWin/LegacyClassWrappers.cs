using System;

namespace PRISMWin
{
    [Obsolete("Use the DiskInfo class")]
    public class clsDiskInfo : DiskInfo
    {
    }

    [Obsolete("Use the DotNETVersionChecker class")]
    public class clsDotNETVersionChecker : DotNETVersionChecker
    {
    }

    [Obsolete("Use the ProcessStats class")]
    public class clsProcessStats : ProcessStats
    {
        public clsProcessStats(bool limitLoggingByTimeOfDay = false) : base(limitLoggingByTimeOfDay)
        {
        }
    }

}

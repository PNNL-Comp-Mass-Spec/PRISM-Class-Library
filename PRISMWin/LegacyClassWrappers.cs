using System;

// ReSharper disable UnusedMember.Global

namespace PRISMWin
{
#pragma warning disable IDE1006 // Naming Styles

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

#pragma warning restore IDE1006 // Naming Styles
}

using System;

namespace PRISMWin
{
#pragma warning disable CS1591  // Missing XML comments
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
#pragma warning restore CS1591  // Missing XML comments

}

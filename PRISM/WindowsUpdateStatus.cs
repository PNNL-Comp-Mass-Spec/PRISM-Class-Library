using System;
using System.Diagnostics.CodeAnalysis;

namespace PRISM
{
    /// <summary>
    /// Utility methods for checking whether Windows updates are likely to be applied close to the current time
    /// Windows desktop computers have Windows updates applied around 3 am on the first Thursday after the third Tuesday of the month
    /// Windows servers have Windows updates applied around 3 am or 10 am on the first Sunday after the second Tuesday of the month
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public static class WindowsUpdateStatus
    {
        // Ignore Spelling: hh:mm:ss tt

        /// <summary>
        /// Checks whether Windows Updates are expected to occur close to the current time of day
        /// </summary>
        /// <returns>True if Windows updates are likely pending on this computer or the Windows servers</returns>
        public static bool UpdatesArePending()
        {
            return UpdatesArePending(DateTime.Now, out _);
        }

        /// <summary>
        /// Checks whether Windows Updates are expected to occur close to the current time of day
        /// </summary>
        /// <param name="pendingWindowsUpdateMessage">Output: description of the pending or recent Windows updates</param>
        /// <returns>True if Windows updates are likely pending on this computer or the Windows servers</returns>
        public static bool UpdatesArePending(out string pendingWindowsUpdateMessage)
        {
            return UpdatesArePending(DateTime.Now, out pendingWindowsUpdateMessage);
        }

        /// <summary>
        /// Checks whether Windows Updates are expected to occur close to currentTime
        /// </summary>
        /// <param name="currentTime">Current time of day</param>
        /// <param name="pendingWindowsUpdateMessage">Output: description of the pending or recent Windows updates</param>
        /// <returns>True if Windows updates are likely pending on this computer or the Windows servers</returns>
        public static bool UpdatesArePending(DateTime currentTime, out string pendingWindowsUpdateMessage)
        {
            // Previously, Windows 7 / Windows 8 processing machines installed updates around 3 am on the Thursday after the third Tuesday of the month
            // After migrating to a new OU in 2019, Windows 10 Pubs began installing updates at various times
            // Therefore, the processing box check is now disabled

            const bool CHECK_FOR_THURSDAY_UPDATES = false;

#pragma warning disable 162
            // ReSharper disable HeuristicUnreachableCode
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (CHECK_FOR_THURSDAY_UPDATES)
            {
                // Determine the third Tuesday in the current month
                var thirdTuesdayInMonth = GetNthTuesdayInMonth(currentTime, 3);

                // Return true between 12 am and 6:30 am on Thursday in the week with the third Tuesday of the month
                var exclusionStart = thirdTuesdayInMonth.AddDays(2);
                var exclusionEnd = thirdTuesdayInMonth.AddDays(2).AddHours(6).AddMinutes(30);

                if (currentTime >= exclusionStart && currentTime < exclusionEnd)
                {
                    var pendingUpdateTime = thirdTuesdayInMonth.AddDays(2).AddHours(3);

                    if (currentTime < pendingUpdateTime)
                    {
                        pendingWindowsUpdateMessage = "Processing boxes are expected to install Windows updates around " +
                                                      pendingUpdateTime.ToString("hh:mm:ss tt");
                    }
                    else
                    {
                        pendingWindowsUpdateMessage = "Processing boxes should have installed Windows updates at " +
                                                      pendingUpdateTime.ToString("hh:mm:ss tt");
                    }

                    return true;
                }
            }
            // ReSharper restore HeuristicUnreachableCode
#pragma warning restore 162

            // No processing box updates are scheduled
            // Check for server updates
            var pendingUpdates = ServerUpdatesArePending(currentTime, out pendingWindowsUpdateMessage);

            return pendingUpdates;
        }

        /// <summary>
        /// Checks whether Windows Updates are expected to occur on Windows Server machines close to the current time of day
        /// </summary>
        /// <returns>True if Windows updates are likely pending on the Windows servers</returns>
        public static bool ServerUpdatesArePending()
        {
            return ServerUpdatesArePending(DateTime.Now, out _);
        }

        /// <summary>
        /// Checks whether Windows Updates are expected to occur on Windows Server machines close currentTime
        /// </summary>
        /// <param name="currentTime">Current time of day</param>
        /// <param name="pendingWindowsUpdateMessage">Output: description of the pending or recent Windows updates</param>
        /// <returns>True if Windows updates are likely pending on the Windows servers</returns>
        public static bool ServerUpdatesArePending(DateTime currentTime, out string pendingWindowsUpdateMessage)
        {
            pendingWindowsUpdateMessage = "No pending update";

            // Determine the second Tuesday in the current month
            var secondTuesdayInMonth = GetNthTuesdayInMonth(currentTime, 2);

            // Windows servers install updates between 2 am and 6 am on the first Sunday after the second Tuesday of the month
            // Return true between 2 am and 6:30 am on the first Sunday after the second Tuesday of the month
            var exclusionStart = secondTuesdayInMonth.AddDays(5).AddHours(2);
            var exclusionEnd = secondTuesdayInMonth.AddDays(5).AddHours(6).AddMinutes(30);

            if (currentTime < exclusionStart || currentTime >= exclusionEnd) return false;

            var pendingUpdateTime = secondTuesdayInMonth.AddDays(5).AddHours(3);

            var pendingUpdateTimeText = pendingUpdateTime.ToString("hh:mm:ss tt");

            pendingWindowsUpdateMessage = "Servers are expected to install Windows updates around " + pendingUpdateTimeText;

            return true;
        }

        /// <summary>
        /// Return the first, second, third, fourth, or fifth Tuesday in the month
        /// </summary>
        /// <param name="currentTime"></param>
        /// <param name="occurrence">1 for the first Tuesday, 2 for the second, etc.</param>
        private static DateTime GetNthTuesdayInMonth(DateTime currentTime, int occurrence)
        {
            var candidateDate = new DateTime(currentTime.Year, currentTime.Month, 1);
            while (candidateDate.DayOfWeek != DayOfWeek.Tuesday)
            {
                candidateDate = candidateDate.AddDays(1);
            }

            var addOn = occurrence * 7 - 7;
            if (addOn == 0)
            {
                return candidateDate;
            }

            var targetTuesday = candidateDate.AddDays(addOn);
            return targetTuesday;
        }
    }
}

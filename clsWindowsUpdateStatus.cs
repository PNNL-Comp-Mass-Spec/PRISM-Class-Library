using System;

namespace PRISM
{
    /// <summary>
    /// Utility functions for checking whether Windows updates are likely to be applied close to the current time
    /// Windows desktop computers have Windows updates applied around 3 am on the first Thursday after the third Tuesday of the month
    /// Windows servers have Windows updates applied around 3 am or 10 am on the first Sunday after the second Tuesday of the month
    /// </summary>
    public class clsWindowsUpdateStatus
    {

        /// <summary>
        /// Checks whether Windows Updates are expected to occur close to the current time of day
        /// </summary>
        /// <returns>True if Windows updates are likely pending on this computer or the Windows servers</returns>
        /// <remarks></remarks>
        public static bool UpdatesArePending()
        {
            return UpdatesArePending(DateTime.Now, out _);
        }

        /// <summary>
        /// Checks whether Windows Updates are expected to occur close to the current time of day
        /// </summary>
        /// <param name="pendingWindowsUpdateMessage">Output: description of the pending or recent Windows updates</param>
        /// <returns>True if Windows updates are likely pending on this computer or the Windows servers</returns>
        /// <remarks></remarks>
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
        /// <remarks></remarks>
        public static bool UpdatesArePending(DateTime currentTime, out string pendingWindowsUpdateMessage)
        {

            // Determine the third Tuesday in the current month
            var thirdTuesdayInMonth = GetNthTuesdayInMonth(currentTime, 3);

            // Windows 7 / Windows 8 Pubs install updates around 3 am on the Thursday after the third Tuesday of the month
            // Return true between 12 am and 6:30 am on Thursday in the week with the third Tuesday of the month
            var exclusionStart = thirdTuesdayInMonth.AddDays(2);
            var exclusionEnd = thirdTuesdayInMonth.AddDays(2).AddHours(6).AddMinutes(30);

            if (currentTime >= exclusionStart && currentTime < exclusionEnd)
            {
                var pendingUpdateTime = thirdTuesdayInMonth.AddDays(2).AddHours(3);

                if (currentTime < pendingUpdateTime)
                {
                    pendingWindowsUpdateMessage = "Processing boxes are expected to install Windows updates around " + pendingUpdateTime.ToString("hh:mm:ss tt");
                }
                else
                {
                    pendingWindowsUpdateMessage = "Processing boxes should have installed Windows updates at " + pendingUpdateTime.ToString("hh:mm:ss tt");
                }

                return true;
            }

            // No processing box updates are scheduled
            // Check for server updates
            var pendingUpdates = ServerUpdatesArePending(currentTime, out pendingWindowsUpdateMessage);

            return pendingUpdates;

        }

        /// <summary>
        /// Checks whether Windows Updates are expected to occur on Windows Server machines close to the current time of day
        /// </summary>
        /// <returns>True if Windows updates are likely pending on the Windows servers</returns>
        /// <remarks></remarks>
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
        /// <remarks></remarks>
        public static bool ServerUpdatesArePending(DateTime currentTime, out string pendingWindowsUpdateMessage)
        {

            pendingWindowsUpdateMessage = "No pending update";

            // Determine the second Tuesday in the current month
            var secondTuesdayInMonth = GetNthTuesdayInMonth(currentTime, 2);

            // Windows servers install updates around either 3 am or 10 am on the first Sunday after the second Tuesday of the month
            // Return true between 2 am and 6:30 am or between 9:30 am and 11 am on the first Sunday after the second Tuesday of the month
            var exclusionStart = secondTuesdayInMonth.AddDays(5).AddHours(2);
            var exclusionEnd = secondTuesdayInMonth.AddDays(5).AddHours(6).AddMinutes(30);

            var exclusionStart2 = secondTuesdayInMonth.AddDays(5).AddHours(9).AddMinutes(30);
            var exclusionEnd2 = secondTuesdayInMonth.AddDays(5).AddHours(11);

            if (currentTime >= exclusionStart && currentTime < exclusionEnd ||
                currentTime >= exclusionStart2 && currentTime < exclusionEnd2)
            {
                var pendingUpdateTime1 = secondTuesdayInMonth.AddDays(5).AddHours(3);
                var pendingUpdateTime2 = secondTuesdayInMonth.AddDays(5).AddHours(10);

                var pendingUpdateTimeText = pendingUpdateTime1.ToString("hh:mm:ss tt") + " or " + pendingUpdateTime2.ToString("hh:mm:ss tt");

                if (currentTime < pendingUpdateTime2)
                {
                    pendingWindowsUpdateMessage = "Servers are expected to install Windows updates around " + pendingUpdateTimeText;
                }
                else
                {
                    pendingWindowsUpdateMessage = "Servers should have installed Windows updates around " + pendingUpdateTimeText;
                }

                return true;
            }

            return false;

        }

        /// <summary>
        /// Return the first, second, third, fourth, or fifth Tuesday in the month
        /// </summary>
        /// <param name="currentTime"></param>
        /// <param name="occurrence">1 for the first Tuesday, 2 for the second, etc.</param>
        /// <returns></returns>
        private static DateTime GetNthTuesdayInMonth(DateTime currentTime, int occurrence)
        {
            var candidateDate = new DateTime(currentTime.Year, currentTime.Month, 1);
            while (candidateDate.DayOfWeek != DayOfWeek.Tuesday)
            {
                candidateDate = candidateDate.AddDays(1);
            }

            var addon = occurrence * 7 - 7;
            if (addon == 0)
            {
                return candidateDate;
            }

            var targetTuesday = candidateDate.AddDays(addon);
            return targetTuesday;
        }

    }
}

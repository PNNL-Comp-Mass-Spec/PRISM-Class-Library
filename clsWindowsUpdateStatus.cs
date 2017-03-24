using System;

namespace PRISM
{
    /// <summary>
    /// Utility functions for checking whether Windows updates are likely to be applied close to the current time
    /// Windows desktop computers have Windows updates applied around 3 am on the first Thursday after the second Tuesday of the month
    /// Windows servers have Windows updates applied around 3 am or 10 am on the first Sunday after the second Tuesday of the month
    /// </summary>
    /// <remarks></remarks>
    public class clsWindowsUpdateStatus
    {

        /// <summary>
        /// Checks whether Windows Updates are expected to occur close to the current time of day
        /// </summary>
        /// <returns>True if Windows updates are likely pending on this computer or the Windows servers</returns>
        /// <remarks></remarks>
        public static bool UpdatesArePending()
        {
            string pendingWindowsUpdateMessage;
            return UpdatesArePending(DateTime.Now, out pendingWindowsUpdateMessage);
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

            // Determine the second Tuesday in the current month
            var secondTuesdayInMonth = GetSecondTuesdayInMonth(currentTime);

            // Windows 7 / Windows 8 Pubs install updates around 3 am on the Thursday after the second Tuesday of the month
            // Return true between 12 am and 6:30 am on Thursday in the week with the second Tuesday of the month
            var dtExclusionStart = secondTuesdayInMonth.AddDays(2);
            var dtExclusionEnd = secondTuesdayInMonth.AddDays(2).AddHours(6).AddMinutes(30);

            if (currentTime >= dtExclusionStart && currentTime < dtExclusionEnd)
            {
                var dtPendingUpdateTime = secondTuesdayInMonth.AddDays(2).AddHours(3);

                if (currentTime < dtPendingUpdateTime)
                {
                    pendingWindowsUpdateMessage = "Processing boxes are expected to install Windows updates around " + dtPendingUpdateTime.ToString("hh:mm:ss tt");
                }
                else
                {
                    pendingWindowsUpdateMessage = "Processing boxes should have installed Windows updates at " + dtPendingUpdateTime.ToString("hh:mm:ss tt");
                }

                return true;
            }

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
            string pendingWindowsUpdateMessage;
            return ServerUpdatesArePending(DateTime.Now, out pendingWindowsUpdateMessage);
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
            var secondTuesdayInMonth = GetSecondTuesdayInMonth(currentTime);

            // Windows servers install updates around either 3 am or 10 am on the first Sunday after the second Tuesday of the month
            // Return true between 2 am and 6:30 am or between 9:30 am and 11 am on the first Sunday after the second Tuesday of the month
            var dtExclusionStart = secondTuesdayInMonth.AddDays(5).AddHours(2);
            var dtExclusionEnd = secondTuesdayInMonth.AddDays(5).AddHours(6).AddMinutes(30);

            var dtExclusionStart2 = secondTuesdayInMonth.AddDays(5).AddHours(9).AddMinutes(30);
            var dtExclusionEnd2 = secondTuesdayInMonth.AddDays(5).AddHours(11);


            if ((currentTime >= dtExclusionStart && currentTime < dtExclusionEnd) || (currentTime >= dtExclusionStart2 && currentTime < dtExclusionEnd2))
            {
                var dtPendingUpdateTime1 = secondTuesdayInMonth.AddDays(5).AddHours(3);
                var dtPendingUpdateTime2 = secondTuesdayInMonth.AddDays(5).AddHours(10);

                var pendingUpdateTimeText = dtPendingUpdateTime1.ToString("hh:mm:ss tt") + " or " + dtPendingUpdateTime2.ToString("hh:mm:ss tt");

                if (currentTime < dtPendingUpdateTime2)
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

        private static DateTime GetSecondTuesdayInMonth(DateTime currentTime)
        {
            var firstTuesdayInMonth = new DateTime(currentTime.Year, currentTime.Month, 1);
            while (firstTuesdayInMonth.DayOfWeek != DayOfWeek.Tuesday)
            {
                firstTuesdayInMonth = firstTuesdayInMonth.AddDays(1);
            }

            var secondTuesdayInMonth = firstTuesdayInMonth.AddDays(7);
            return secondTuesdayInMonth;
        }

    }
}

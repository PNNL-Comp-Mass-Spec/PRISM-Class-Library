using System;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    class MiscellaneousTests
    {
        [TestCase("8/10/2017 2 am", false)]
        [TestCase("8/15/2017 2 am", false)]
        [TestCase("8/16/2017 11 pm", false)]
        [TestCase("8/17/2017 12 am", true)]
        [TestCase("8/17/2017 12:15 am", true)]
        [TestCase("8/17/2017 1 am", true)]
        [TestCase("8/17/2017 2 am", true)]
        [TestCase("8/17/2017 6 am", true)]
        [TestCase("8/17/2017 6:31 am", false)]
        [TestCase("8/17/2017 9 am", false)]
        [TestCase("8/18/2017 2 am", false)]
        public void TestUpdatesArePending(string dateToTest, bool expectedValue)
        {

            var currentDate = DateTime.Parse(dateToTest);
            var pendingUpdates = clsWindowsUpdateStatus.UpdatesArePending(currentDate, out _);

            Assert.AreEqual(expectedValue, pendingUpdates, "Unexpected value for {0}: {1}", dateToTest, pendingUpdates);
        }

        [TestCase("8/10/2017 2 am", false)]
        [TestCase("8/11/2017 2 am", false)]
        [TestCase("8/12/2017 11 pm", false)]
        [TestCase("8/13/2017 12 am", false)]
        [TestCase("8/13/2017 1:15 am", false)]
        [TestCase("8/13/2017 2 am", true)]
        [TestCase("8/13/2017 3 am", true)]
        [TestCase("8/13/2017 6:11 am", true)]
        [TestCase("8/13/2017 9 am", false)]
        [TestCase("8/13/2017 10 am", true)]
        [TestCase("8/13/2017 11:30 am", false)]
        [TestCase("8/14/2017 2 am", false)]
        [TestCase("8/17/2017 2 am", false)]
        public void ServerUpdatesArePending(string dateToTest, bool expectedValue)
        {

            var currentDate = DateTime.Parse(dateToTest);
            var pendingUpdates = clsWindowsUpdateStatus.ServerUpdatesArePending(currentDate, out _);

            Assert.AreEqual(expectedValue, pendingUpdates, "Unexpected value for {0}: {1}", dateToTest, pendingUpdates);
        }

    }
}

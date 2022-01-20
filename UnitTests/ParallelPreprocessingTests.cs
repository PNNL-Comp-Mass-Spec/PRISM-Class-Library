using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using PRISM;

#pragma warning disable CS1998

namespace PRISMTest
{
    [TestFixture]
    public class ParallelPreprocessingTests
    {
        // Ignore Spelling: async

        [Test]
        public void TestPreprocessing()
        {
            const int totalTasks = 12;
            const int simultaneous = 3;
            const int sleepTime = 5;
            const int randomMaxMs = 1000;
            var rng = new Random();
            Console.WriteLine("Running {0} tasks {1} at a time, each sleeping for {2} seconds...", totalTasks, simultaneous, sleepTime);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            //var items = Enumerable.Range(0, totalTasks).Select(async x => // non-parallel
            var items = Enumerable.Range(0, totalTasks).ParallelPreprocess(async x =>
            {
                var sleepMs = sleepTime * 1000 + rng.Next(0, randomMaxMs);
                // Note: using await Task.Delay actually causes the 'simultaneous' count to increase by one.
                // 'Why' is a question I don't have the answer to
                //await Task.Delay(sleepMs);
                Thread.Sleep(sleepMs);
                return x;
            }, simultaneous);

            foreach (var item in items)
            {
                Console.WriteLine("Got task {0} at time {1}", item.Result, sw.Elapsed);
            }
        }
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    public class ParallelPreprocessingTests
    {
        [Test]
        public void TestPreprocessing()
        {
            const int totalTasks = 12;
            const int simultaneous = 3;
            const int sleepTime = 5;
            Console.WriteLine("Running {0} tasks {1} at a time, each sleeping for {2} seconds...", totalTasks, simultaneous, sleepTime);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            //var items = Enumerable.Range(0, totalTasks).Select(async x => // non-parallel
            var items = Enumerable.Range(0, totalTasks).ParallelPreprocess(async x =>
            {
                await Task.Delay(sleepTime * 1000);
                return x;
            }, simultaneous);

            foreach (var item in items)
            {
                Console.WriteLine("Got task {0} at time {1}", item.Result, sw.Elapsed);
            }
        }
    }
}

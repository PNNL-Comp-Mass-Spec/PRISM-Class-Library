using System;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    class TestEvents
    {
        [Test]
        public void TestMonitorEvents()
        {
            var parentClass = new ClassA("Parent class");

            parentClass.DebugEvent += ParentClass_DebugEvent;
            parentClass.ErrorEvent += ParentClass_ErrorEvent;
            parentClass.StatusEvent += ParentClass_StatusEvent;
            parentClass.WarningEvent += ParentClass_WarningEvent;
            parentClass.ProgressUpdate += ParentClass_ProgressUpdate;

            DoWork(parentClass, false);
        }

        [Test]
        public void TestWriteToConsoleIfNoListeners()
        {
            var parentClass = new ClassA("Parent class");

            DoWork(parentClass, true);
        }

        private void DoWork(ClassA parentClass, bool disableDebugBeforeChainingTest)
        {
            parentClass.TestAllEvents();

            WriteSeparator();

            if (disableDebugBeforeChainingTest)
            {
                parentClass.SkipConsoleWriteIfNoProgressListener = true;
                Console.WriteLine("Progress event writing disabled");
                Console.WriteLine();
            }

            parentClass.TestEventChaining();
        }

        private void WriteSeparator()
        {
            Console.WriteLine();
            Console.WriteLine(new string('*', 60));
            Console.WriteLine();
        }

        #region "Event Handlers"

        private void ParentClass_DebugEvent(string message)
        {
            ConsoleMsgUtils.ShowDebug(message);
        }

        private void ParentClass_ErrorEvent(string message, Exception ex)
        {
            ConsoleMsgUtils.ShowError(message, ex);
        }

        private void ParentClass_StatusEvent(string message)
        {
            Console.WriteLine(message);
        }

        private void ParentClass_WarningEvent(string message)
        {
            ConsoleMsgUtils.ShowWarning(message);
        }

        private void ParentClass_ProgressUpdate(string progressMessage, float percentComplete)
        {
            Console.WriteLine("{0:F2}%: {1}", percentComplete, progressMessage);
        }

        #endregion

        #region "Classes for testing events"

        /// <summary>
        /// Class for testing events
        /// </summary>
        private class ClassA : EventNotifier
        {
            private string ClassID { get; }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="classId"></param>
            public ClassA(string classId)
            {
                ClassID = classId;
            }

            public void TestAllEvents()
            {
                OnStatusEvent("Testing all events in " + ClassID);
                OnProgressUpdate("Starting", 0);

                OnProgressUpdate("Testing", 20);
                OnDebugEvent("Debug event");

                OnProgressUpdate("Testing", 40);
                OnErrorEvent("Error Event");

                OnProgressUpdate("Testing", 60);
                OnErrorEvent("Error Event", new Exception("Test exception"));

                OnProgressUpdate("Testing", 80);
                OnWarningEvent("Warning Event");

                OnProgressUpdate("Complete", 100);
            }

            public void TestEventChaining()
            {
                var workerClass = new ClassB("Worker class");
                RegisterEvents(workerClass);

                workerClass.TestAllEvents();

                workerClass.TestDivideByZero();
            }
        }

        /// <summary>
        /// Second class for testing events
        /// </summary>
        private class ClassB : ClassA
        {
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="classId"></param>
            public ClassB(string classId) : base(classId)
            {
                OnDebugEvent("Instantiating " + classId);
            }

            public void TestDivideByZero()
            {
                try
                {
                    var value1 = 25 - 23 - 2;

                    var result = 5 / value1;

                    Console.WriteLine(result);
                }
                catch (Exception ex)
                {
                    OnErrorEvent("As expected, error in TestDivideByZero", ex);
                }
            }
        }

        #endregion
    }
}

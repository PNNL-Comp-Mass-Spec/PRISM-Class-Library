using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    class StackTraceFormatterTests
    {
        public enum ExceptionTypes
        {
            General = 0,
            FileNotFound = 1,
            MyTestException = 2,
            MyTestExceptionMultiInner = 3
        }

        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(3, false)]
        [TestCase(4, false)]
        [TestCase(5, false)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(5, true)]
        public void TestGetCurrentStackTrace(int depth, bool multiLine)
        {
            var parents = new List<string>();

            RecursiveMethodX(parents, 1, depth, multiLine);
        }

        [TestCase(ExceptionTypes.General, 1, false)]
        [TestCase(ExceptionTypes.General, 2, false)]
        [TestCase(ExceptionTypes.General, 3, false)]
        [TestCase(ExceptionTypes.General, 4, false)]
        [TestCase(ExceptionTypes.General, 5, false)]
        [TestCase(ExceptionTypes.General, 6, false)]
        [TestCase(ExceptionTypes.General, 7, false)]
        [TestCase(ExceptionTypes.General, 8, true)]
        [TestCase(ExceptionTypes.General, 8, true, true)]
        [TestCase(ExceptionTypes.FileNotFound, 3, false)]
        [TestCase(ExceptionTypes.FileNotFound, 10, true)]
        [TestCase(ExceptionTypes.MyTestException, 3, true)]
        [TestCase(ExceptionTypes.MyTestExceptionMultiInner, 3, true)]
        [TestCase(ExceptionTypes.MyTestException, 3, true, true)]
        public void VerifyExceptionStackTrace(ExceptionTypes targetException, int depth, bool multiLine, bool includeMethodParams = false)
        {
            var parents = new List<string>();

            try
            {
                RecursiveMethodA(targetException, parents, 1, depth);
            }
            catch (Exception ex)
            {
                string stackTrace;

                if (multiLine)
                    stackTrace = StackTraceFormatter.GetExceptionStackTraceMultiLine(ex, true, includeMethodParams);
                else
                    stackTrace = StackTraceFormatter.GetExceptionStackTrace(ex);

                Console.WriteLine(stackTrace);
            }
        }

        private void RecursiveMethodA(ExceptionTypes targetException, List<string> parents, int depth, int maxDepth)
        {
            parents.Add("Level " + depth);
            if (depth == maxDepth)
            {
                ThrowExceptionNow(targetException, parents, depth);
            }

            RecursiveMethodB(targetException, parents, depth + 1, maxDepth);
        }

        private void RecursiveMethodB(ExceptionTypes targetException, List<string> parents, int depth, int maxDepth)
        {
            parents.Add("Level " + depth);
            if (depth == maxDepth)
            {
                ThrowExceptionNow(targetException, parents, depth);
            }

            RecursiveMethodC(targetException, parents, depth + 1, maxDepth);
        }

        private void RecursiveMethodC(ExceptionTypes targetException, List<string> parents, int depth, int maxDepth)
        {
            parents.Add("Level " + depth);
            if (depth == maxDepth)
            {
                ThrowExceptionNow(targetException, parents, depth);
            }

            RecursiveMethodA(targetException, parents, depth + 1, maxDepth);
        }

        private void RecursiveMethodX(List<string> parents, int depth, int maxDepth, bool multiLine)
        {
            parents.Add("Level " + depth);
            if (depth == maxDepth)
            {
                ShowStackTraceNow(parents, depth, multiLine);
                return;
            }

            RecursiveMethodY(parents, depth + 1, maxDepth, multiLine);
        }

        private void RecursiveMethodY(List<string> parents, int depth, int maxDepth, bool multiLine)
        {
            parents.Add("Level " + depth);
            if (depth == maxDepth)
            {
                ShowStackTraceNow(parents, depth, multiLine);
                return;
            }

            RecursiveMethodZ( parents, depth + 1, maxDepth, multiLine);
        }

        private void RecursiveMethodZ(List<string> parents, int depth, int maxDepth, bool multiLine)
        {
            parents.Add("Level " + depth);
            if (depth == maxDepth)
            {
                ShowStackTraceNow(parents, depth, multiLine);
                return;
            }

            RecursiveMethodX(parents, depth + 1, maxDepth, multiLine);
        }

        private void ShowStackTraceNow(IReadOnlyCollection<string> parents, int depth, bool multiLine)
        {
            Assert.AreEqual(depth, parents.Count, "Parent list length invalid");

            string stackTrace;
            if (multiLine)
                stackTrace = StackTraceFormatter.GetCurrentStackTraceMultiLine();
            else
                stackTrace = StackTraceFormatter.GetCurrentStackTrace();

            Console.WriteLine(stackTrace);
        }

        private void ThrowExceptionNow(ExceptionTypes targetException, IReadOnlyCollection<string> parents, int depth)
        {
            Assert.AreEqual(depth, parents.Count, "Parent list length invalid");

            switch (targetException)
            {
                case ExceptionTypes.General:
                    throw new Exception("General exception at depth " + depth);

                case ExceptionTypes.FileNotFound:
                    throw new FileNotFoundException("FileNotFound exception at depth " + depth, @"C:\NotADirectory\NotAFile.txt");

                case ExceptionTypes.MyTestException:

                    var innerException = new ApplicationException("Test inner exception");

                    throw new MyTestException("Test exception at depth " + depth, innerException);

                case ExceptionTypes.MyTestExceptionMultiInner:

                    var innerException1 = new ApplicationException("Test inner exception 1");
                    var innerException2 = new MyTestException("Test inner exception 2", innerException1);

                    throw new MyTestException("Test exception at depth " + depth, innerException2);
                default:
                    return;
            }
        }
    }

    class MyTestException : Exception
    {
        public MyTestException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }

}

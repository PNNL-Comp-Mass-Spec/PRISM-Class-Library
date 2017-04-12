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
            FileNotFound = 1
        }

        [Test]
        [TestCase(ExceptionTypes.General, 1, false)]
        [TestCase(ExceptionTypes.General, 2, false)]
        [TestCase(ExceptionTypes.General, 3, false)]
        [TestCase(ExceptionTypes.General, 4, false)]
        [TestCase(ExceptionTypes.General, 5, false)]
        [TestCase(ExceptionTypes.General, 6, false)]
        [TestCase(ExceptionTypes.General, 7, false)]
        [TestCase(ExceptionTypes.General, 8, true)]
        [TestCase(ExceptionTypes.FileNotFound, 3, false)]
        [TestCase(ExceptionTypes.FileNotFound, 10, true)]
        public void VerifyStackTrace(ExceptionTypes targetException, int depth, bool multiLine)
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
                    stackTrace = clsStackTraceFormatter.GetExceptionStackTraceMultiLine(ex);
                else
                    stackTrace = clsStackTraceFormatter.GetExceptionStackTrace(ex);

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

        private void ThrowExceptionNow(ExceptionTypes targetException, IReadOnlyCollection<string> parents, int depth)
        {
            Assert.AreEqual(depth, parents.Count, "Parent list length invalid");

            switch (targetException)
            {
                case ExceptionTypes.General:
                    throw new Exception("General exception at depth " + depth);

                case ExceptionTypes.FileNotFound:
                    throw new FileNotFoundException("FileNotFound exception at depth " + depth, @"C:\NotAFolder\NotAFile.txt");
                default:
                    return;
            }
        }

    }
}

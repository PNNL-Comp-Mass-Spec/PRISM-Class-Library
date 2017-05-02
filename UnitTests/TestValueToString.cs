using System;
using System.Reflection;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    class TestValueToString
    {

        [Test]
        public void TestValueString()
        {
            TestValue(0, 0, "0");
            TestValue(0, 1, "0");
            TestValue(0, 2, "0");
            TestValue(0, 3, "0");
            Console.WriteLine();

            TestValue(1, 1, "1");
            TestValue(1, 3, "1");
            TestValue(5, 3, "5");
            Console.WriteLine();

            TestValue(10, 0, "10");
            TestValue(10, 1, "10");
            TestValue(10, 2, "10");
            TestValue(10, 3, "10");
            Console.WriteLine();

            TestValue(10.123, 0, "10");
            TestValue(10.123, 1, "10");
            TestValue(10.123, 2, "10");
            TestValue(10.123, 3, "10.1");
            TestValue(10.123, 5, "10.123");
            TestValue(10.123, 7, "10.123");
            Console.WriteLine();

            TestValue(50, 0, "50");
            TestValue(50, 2, "50");
            TestValue(50, 4, "50");
            Console.WriteLine();

            TestValue(50.653, 0, "51");
            TestValue(50.653, 1, "51");
            TestValue(50.653, 2, "51");
            TestValue(50.653, 3, "50.7");
            TestValue(50.653, 4, "50.65");
            TestValue(50.653, 5, "50.653");
            TestValue(50.653, 6, "50.653");
            Console.WriteLine();

            TestValue(54.753, 0, "55");
            TestValue(54.753, 1, "55");
            TestValue(54.753, 2, "55");
            TestValue(54.753, 3, "54.8");
            TestValue(54.753, 4, "54.75");
            TestValue(54.753, 5, "54.753");
            TestValue(54.753, 6, "54.753");
            Console.WriteLine();

            TestValue(110, 0, "110");
            TestValue(110, 1, "110");
            TestValue(110, 2, "110");
            Console.WriteLine();

            TestValue(9.99999, 6, "9.99999");
            TestValue(9.99999, 5, "10");
            TestValue(9.99999, 4, "10");
            TestValue(9.99999, 2, "10");
            TestValue(9.99999, 1, "10");
            TestValue(9.99999, 0, "10");
            Console.WriteLine();

            TestValue(9.98765, 6, "9.98765");
            TestValue(9.98765, 5, "9.9877");
            TestValue(9.98765, 4, "9.988");
            TestValue(9.98765, 3, "9.99");
            TestValue(9.98765, 2, "10");
            TestValue(9.98765, 1, "10");
            Console.WriteLine();

            TestValue(0.12345, 5, "0.12345");
            TestValue(5.12345, 5, "5.1235");
            TestValue(50.12345, 5, "50.123");
            TestValue(500.12345, 5, "500.12");
            TestValue(5000.12345, 5, "5000.1");
            TestValue(50000.12345, 5, "50000");
            TestValue(500000.12345, 5, "500000");
            Console.WriteLine();

            TestValue(0.12345, 7, "0.12345");
            TestValue(5.12345, 7, "5.12345");
            TestValue(50.12345, 7, "50.12345");
            TestValue(500.12345, 7, "500.1235");
            TestValue(5000.12345, 7, "5000.123");
            TestValue(50000.12345, 7, "50000.12");
            TestValue(500000.12345, 7, "500000.1");
            TestValue(5000000.12345, 7, "5.0E+06");
            Console.WriteLine();

            TestValue(9.98765, 3, "9.99");
            TestValue(99.98765, 3, "100");
            TestValue(998.98765, 3, "999");
            TestValue(9987.98765, 3, "9988");
            TestValue(99876.98765, 3, "99877");
            TestValue(998765.98765, 3, "998766");
            Console.WriteLine();

            TestValue(0.1, 0, "0.1");
            TestValue(0.1, 1, "0.1");
            TestValue(0.1, 2, "0.1");
            Console.WriteLine();

            TestValue(0.1234, 0, "0.1");
            TestValue(0.1234, 1, "0.1");
            TestValue(0.1234, 2, "0.12");
            TestValue(0.1234, 3, "0.123");
            TestValue(0.1234, 4, "0.1234");
            TestValue(0.1234, 5, "0.1234");
            TestValue(0.1234, 8, "0.1234");
            Console.WriteLine();

            TestValue(0.987654321, 0, "1");
            TestValue(0.987654321, 1, "1");
            TestValue(0.987654321, 2, "0.99");
            TestValue(0.987654321, 4, "0.9877");
            TestValue(0.987654321, 8, "0.98765432");
            TestValue(0.987654321, 9, "0.987654321");
            TestValue(0.987654321, 12, "0.987654321");
            Console.WriteLine();

            TestValue(-0.987654321, 0, "-1");
            TestValue(-0.987654321, 1, "-1");
            TestValue(-0.987654321, 2, "-0.99");
            TestValue(-0.987654321, 4, "-0.9877");
            TestValue(-0.987654321, 8, "-0.98765432");
            TestValue(-0.987654321, 9, "-0.987654321");
            TestValue(-0.987654321, 12, "-0.987654321");
            Console.WriteLine();

            TestValue(0.00009876, 0, "0.0001");
            TestValue(0.00009876, 1, "0.0001");
            TestValue(0.00009876, 2, "0.000099");
            TestValue(0.00009876, 3, "0.0000988");
            TestValue(0.00009876, 4, "0.00009876");
            TestValue(0.00009876, 5, "0.00009876");
            TestValue(0.00009876, 6, "0.00009876");
            Console.WriteLine();

            TestValue(0.00009876, 0, "9.9E-05", 10000);
            TestValue(0.00009876, 1, "9.9E-05", 10000);
            TestValue(0.00009876, 2, "9.9E-05", 10000);
            TestValue(0.00009876, 3, "9.88E-05", 10000);
            TestValue(0.00009876, 4, "9.876E-05", 10000);
            TestValue(0.00009876, 5, "9.876E-05", 10000);
            Console.WriteLine();

            TestValue(0.00004002, 0, "0.00004");
            TestValue(0.00004002, 1, "0.00004");
            TestValue(0.00004002, 2, "0.00004");
            TestValue(0.00004002, 3, "0.00004");
            TestValue(0.00004002, 4, "0.00004002");
            TestValue(0.00004002, 5, "0.00004002");
            TestValue(0.00004002, 6, "0.00004002");
            TestValue(0.00004002, 7, "0.00004002");
            TestValue(0.00004002, 8, "0.00004002");
            Console.WriteLine();

            TestValue(0.00004002, 0, "4.0E-05", 10000);
            TestValue(0.00004002, 1, "4.0E-05", 10000);
            TestValue(0.00004002, 2, "4.0E-05", 10000);
            TestValue(0.00004002, 3, "4.0E-05", 10000);
            TestValue(0.00004002, 4, "4.002E-05", 10000);
            Console.WriteLine();

            TestValue(-0.00004002, 0, "-0.00004");
            TestValue(-0.00004002, 1, "-0.00004");
            TestValue(-0.00004002, 2, "-0.00004");
            TestValue(-0.00004002, 3, "-0.00004");
            TestValue(-0.00004002, 4, "-0.00004002");
            TestValue(-0.00004002, 5, "-0.00004002");
            TestValue(-0.00004002, 6, "-0.00004002");
            Console.WriteLine();

            TestValue(-0.00004002, 0, "-4.0E-05", 1000);
            TestValue(-0.00004002, 1, "-4.0E-05", 1000);
            TestValue(-0.00004002, 2, "-4.0E-05", 1000);
            TestValue(-0.00004002, 3, "-4.0E-05", 1000);
            TestValue(-0.00004002, 4, "-4.002E-05", 1000);
            Console.WriteLine();

            TestValue(0.1234567, 0, "0.1", 1000);
            TestValue(0.01234567, 1, "0.01", 1000);
            TestValue(0.00123456, 2, "0.0012", 1000);
            TestValue(0.00012345, 3, "1.23E-04", 1000);
            TestValue(0.000012345, 4, "1.235E-05", 1000);
            TestValue(0.000001234, 4, "1.234E-06", 1000);
            Console.WriteLine();

            TestValue(0.1234567, 0, "0.1", 10);
            TestValue(0.01234567, 1, "1.2E-02", 10);
            TestValue(0.00123456, 2, "1.2E-03", 10);
            TestValue(0.00123456, 2, "0.0012");
            TestValue(0.00123456, 2, "0.0012", 100000);
            TestValue(0.00123456, 2, "0.0012", 10000);
            TestValue(0.00123456, 2, "0.0012", 1000);
            TestValue(0.00123456, 2, "1.2E-03", 100);
            TestValue(0.00123456, 2, "1.2E-03", 10);
            TestValue(0.00123456, 2, "1.2E-03", 1);
            Console.WriteLine();

            TestValue(4.94065645E-324, 6, "4.94066E-324");
            TestValue(4.94065645E-150, 6, "4.94066E-150");
            TestValue(4.94065645E-101, 6, "4.94066E-101");
            TestValue(4.02735019E-10, 6, "4.02735E-10");
            Console.WriteLine();

            TestValue(4.0273501E-5, 6, "0.0000402735");
            TestValue(4.0273501E-4, 6, "0.000402735");
            TestValue(4.0273501E-3, 6, "0.00402735");
            TestValue(4.0273501E-2, 6, "0.0402735");
            TestValue(4.0273501E-1, 6, "0.402735");
            TestValue(0.0134886, 6, "0.0134886");
            Console.WriteLine();

            TestValue(4.0273501E-10, 6, "4.02735E-10");
            TestValue(0.0134886, 6, "0.0134886");
            TestValue(7063.79431, 6, "7063.79");
            TestValue(6496286.95, 6, "6.49629E+06");
            Console.WriteLine();
        }

        private void TestValue(
            double value,
            byte digitsOfPrecision,
            string resultExpected,
            double scientificNotationThreshold = 1000000)
        {
            var result = StringUtilities.ValueToString(value, digitsOfPrecision, scientificNotationThreshold);
            var expectedResultFound = string.CompareOrdinal(result, resultExpected) == 0;

            Console.WriteLine(@"{0,20}, digits={1,2}: {2,-14}", value, digitsOfPrecision, result);

            if (expectedResultFound)
            {
                return;
            }

            var errMsg = "Result " + result + " did not match expected result (" + resultExpected + ")";
            Assert.IsTrue(expectedResultFound, errMsg);

        }

    }
}

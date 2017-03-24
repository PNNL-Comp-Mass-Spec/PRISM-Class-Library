using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    class TestDBTools
    {

        [TestCase("Gigasax", "DMS5", "SELECT U_PRN, U_Name, U_HID FROM T_Users WHERE U_Name = 'AutoUser'", 1, "H09090911,AutoUser,H09090911")]
        [TestCase("Gigasax", "DMS5",
            "SELECT Num_C, Num_H, Num_N, Num_O, Num_S FROM V_Residues WHERE (Residue_Symbol IN ('K', 'R')) ORDER BY Residue_Symbol", 2,
            "6, 12, 2, 1, 0")]
        public void TestQueryTable(string server, string database, string query, int expectedRowCount, string expectedValueList)
        {
            var connectionString = GetConnectionString(server, database);
            var dbTools = new clsDBTools(connectionString);

            Console.WriteLine("Running query " + query);

            List <List<string>> lstResults;
            dbTools.GetQueryResults(query, out lstResults, "Unit Tests");

            var expectedValues = expectedValueList.Split(',');

            Assert.AreEqual(lstResults.Count, expectedRowCount, "RowCount mismatch");

            var firstRow = lstResults.First();
            for (var colIndex = 0; colIndex < firstRow.Count; colIndex++)
            {
                if (colIndex >= expectedValues.Length)
                    break;

                Assert.AreEqual(firstRow[colIndex], expectedValues[colIndex].Trim(),
                    "Data value mismatch, column {0}, expected {1} but actually {2}",
                    colIndex + 1, expectedValues[colIndex], firstRow[colIndex]);
            }

            Console.WriteLine("Rows returned: " + lstResults.Count);
        }

        private string GetConnectionString(string server, string database)
        {
            return string.Format("Data Source={0};Initial Catalog={1};Integrated Security=SSPI;", server, database);
        }
    }
}

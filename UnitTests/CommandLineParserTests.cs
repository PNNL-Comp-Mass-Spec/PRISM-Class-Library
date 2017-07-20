using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    class CommandLineParserTests
    {
        [Test]
        public void TestBadKey1()
        {
            var options = new BadKey1();
            var result = CommandLineParser<BadKey1>.ParseArgs(new[] { "-bad-name", "b" }, options, "", "");
            Assert.IsFalse(result, "Parser did not fail with '-' at start of arg key");
        }

        [Test]
        public void TestBadKey2()
        {
            var options = new BadKey2();
            var result = CommandLineParser<BadKey2>.ParseArgs(new[] { "/bad/name", "b" }, options, "", "");
            Assert.IsFalse(result, "Parser did not fail with '/' at start of arg key");
        }

        [Test]
        public void TestBadKey3()
        {
            var options = new BadKey3();
            var result = CommandLineParser<BadKey3>.ParseArgs(new[] { "-badname", "b" }, options, "", "");
            Assert.IsFalse(result, "Parser did not fail with duplicate arg keys");
        }

        [Test]
        public void TestBadKey4()
        {
            var options = new BadKey4();
            var result = CommandLineParser<BadKey4>.ParseArgs(new[] { "-badname", "b" }, options, "", "");
            Assert.IsFalse(result, "Parser did not fail with duplicate arg keys");
        }
        [Test]
        public void TestBadKey5()
        {
            var options = new BadKey5();
            var result = CommandLineParser<BadKey5>.ParseArgs(new[] { "-bad name", "b" }, options, "", "");
            Assert.IsFalse(result, "Parser did not fail with ' ' in arg key");
        }

        [Test]
        public void TestBadKey6()
        {
            var options = new BadKey6();
            var result = CommandLineParser<BadKey6>.ParseArgs(new[] { "/bad:name", "b" }, options, "", "");
            Assert.IsFalse(result, "Parser did not fail with ':' in arg key");
        }

        [Test]
        public void TestBadKey7()
        {
            var options = new BadKey7();
            var result = CommandLineParser<BadKey7>.ParseArgs(new[] { "-bad=name", "b" }, options, "", "");
            Assert.IsFalse(result, "Parser did not fail with '=' in arg key");
        }

        private class BadKey1
        {
            [Option("-bad-name")]
            public string BadName { get; set; }
        }

        private class BadKey2
        {
            [Option("/bad/name")]
            public string BadName { get; set; }
        }

        private class BadKey3
        {
            [Option("badname")]
            public string BadName { get; set; }

            [Option("badname")]
            public string BadName2 { get; set; }
        }

        private class BadKey4
        {
            [Option("g")]
            public string GoodName { get; set; }

            [Option("G")]
            public string GoodNameUCase { get; set; }

            [Option("NoGood")]
            public string TheBadName { get; set; }

            [Option("NoGood")]
            public string TheBadName2 { get; set; }
        }

        private class BadKey5
        {
            [Option("-bad name")]
            public string BadName { get; set; }
        }

        private class BadKey6
        {
            [Option("/bad:name")]
            public string BadName { get; set; }
        }

        private class BadKey7
        {
            [Option("-bad=name")]
            public string BadName { get; set; }
        }

        [Test]
        public void TestOkayKey1()
        {
            var options = new OkayKey1();
            var result = CommandLineParser<OkayKey1>.ParseArgs(new[] { "-okay-name", "b" }, options, "", "");
            Assert.IsTrue(result, "Parser failed with '-' not at start of arg key");
        }

        [Test]
        public void TestOkayKey2()
        {
            var options = new OkayKey2();
            var result = CommandLineParser<OkayKey2>.ParseArgs(new[] { "/okay/name", "b" }, options, "", "");
            Assert.IsTrue(result, "Parser failed with '/' not at start of arg key");
        }

        private class OkayKey1
        {
            [Option("okay-name")]
            public string OkayName { get; set; }
        }

        private class OkayKey2
        {
            [Option("okay/name")]
            public string OkayName { get; set; }
        }

        [Test]
        public void TestGood()
        {
            var args = new string[]
            {
                "-minInt", "11",
                "-maxInt:5",
                "/minMaxInt", "2",
                "/minDbl:15",
                "-maxDbl", "5.5",
                "-minmaxdbl", "2.4",
                "-g", @"C:\Users\User",
                @"/G:C:\Users\User2\",
                "-over", "This string should be overwritten",
                "-ab", "TestAb1",
                "-aB", "TestAb2",
                "-Ab", "TestAb3",
                "-AB=TestAb4",
                "-b1", "true",
                "-b2", "False",
                "/b3",
                "-1",
                "-over", "This string should be used",
                "-strArray", "value1",
                "-strArray", "value2",
                "-strArray", "value3",
                "-intArray", "0",
                "-intArray", "1",
                "-intArray", "2",
                "-intArray", "3",
                "-intArray", "4",
                "-dblArray", "1.0",
            };
            var options = new ArgsVariety();
            var result = CommandLineParser<ArgsVariety>.ParseArgs(args, options, "", "");
            Assert.IsTrue(result, "Parser failed to parse valid args");
            Assert.AreEqual(11, options.IntMinOnly);
            Assert.AreEqual(5, options.IntMaxOnly);
            Assert.AreEqual(2, options.IntMinMax);
            Assert.AreEqual(15, options.DblMinOnly);
            Assert.AreEqual(5.5, options.DblMaxOnly);
            Assert.AreEqual(2.4, options.DblMinMax);
            Assert.AreEqual(@"C:\Users\User", options.LowerChar);
            Assert.AreEqual(@"C:\Users\User2\", options.UpperChar);
            Assert.AreEqual("TestAb1", options.Ab1);
            Assert.AreEqual("TestAb2", options.Ab2);
            Assert.AreEqual("TestAb3", options.Ab3);
            Assert.AreEqual("TestAb4", options.Ab4);
            Assert.AreEqual(true, options.BoolCheck1);
            Assert.AreEqual(false, options.BoolCheck2);
            Assert.AreEqual(true, options.BoolCheck3);
            Assert.AreEqual(true, options.NumericArg);
            Assert.AreEqual("This string should be used", options.Overrides);
            Assert.AreEqual(3, options.StringArray.Length);
            Assert.AreEqual("value1", options.StringArray[0]);
            Assert.AreEqual("value2", options.StringArray[1]);
            Assert.AreEqual("value3", options.StringArray[2]);
            Assert.AreEqual(5, options.IntArray.Length);
            Assert.AreEqual(0, options.IntArray[0]);
            Assert.AreEqual(1, options.IntArray[1]);
            Assert.AreEqual(2, options.IntArray[2]);
            Assert.AreEqual(3, options.IntArray[3]);
            Assert.AreEqual(4, options.IntArray[4]);
            Assert.AreEqual(1, options.DblArray.Length);
            Assert.AreEqual(1.0, options.DblArray[0]);
        }

        [Test]
        public void TestMinInt1()
        {
            var args = new string[]
            {
                "-minInt", "5",
            };
            var options = new ArgsVariety();
            var result = CommandLineParser<ArgsVariety>.ParseArgs(args, options, "", "");
            Assert.IsFalse(result, "Parser did not fail on invalid min");
        }

        [Test]
        public void TestMinInt2()
        {
            var args = new string[]
            {
                "-minMaxInt", "-15",
            };
            var options = new ArgsVariety();
            var result = CommandLineParser<ArgsVariety>.ParseArgs(args, options, "", "");
            Assert.IsFalse(result, "Parser did not fail on invalid min");
        }

        [Test]
        public void TestMinInt3()
        {
            var args = new string[]
            {
                "-minIntBad", "15",
            };
            var options = new ArgsVariety();
            var result = CommandLineParser<ArgsVariety>.ParseArgs(args, options, "", "");
            Assert.IsFalse(result, "Parser did not fail on invalid min");
        }

        [Test]
        public void TestBadMinInt()
        {
            var args = new string[]
            {
                "-minInt", "15.0",
            };
            var options = new ArgsVariety();
            var result = CommandLineParser<ArgsVariety>.ParseArgs(args, options, "", "");
            Assert.IsFalse(result, "Parser did not fail on invalid min");
        }

        [Test]
        public void TestMaxInt1()
        {
            var args = new string[]
            {
                "-maxInt", "15",
            };
            var options = new ArgsVariety();
            var result = CommandLineParser<ArgsVariety>.ParseArgs(args, options, "", "");
            Assert.IsFalse(result, "Parser did not fail on invalid min");
        }

        [Test]
        public void TestMaxInt2()
        {
            var args = new string[]
            {
                "-maxIntBad", "5",
            };
            var options = new ArgsVariety();
            var result = CommandLineParser<ArgsVariety>.ParseArgs(args, options, "", "");
            Assert.IsFalse(result, "Parser did not fail on invalid min");
        }

        [Test]
        public void TestBadMaxInt()
        {
            var args = new string[]
            {
                "-maxInt", "9.0",
            };
            var options = new ArgsVariety();
            var result = CommandLineParser<ArgsVariety>.ParseArgs(args, options, "", "");
            Assert.IsFalse(result, "Parser did not fail on invalid min");
        }

        [Test]
        public void TestMinDbl1()
        {
            var args = new string[]
            {
                "-minDbl", "5",
            };
            var options = new ArgsVariety();
            var result = CommandLineParser<ArgsVariety>.ParseArgs(args, options, "", "");
            Assert.IsFalse(result, "Parser did not fail on invalid min");
        }

        [Test]
        public void TestMinDbl2()
        {
            var args = new string[]
            {
                "-minMaxDbl", "-15",
            };
            var options = new ArgsVariety();
            var result = CommandLineParser<ArgsVariety>.ParseArgs(args, options, "", "");
            Assert.IsFalse(result, "Parser did not fail on invalid min");
        }

        [Test]
        public void TestMinDbl3()
        {
            var args = new string[]
            {
                "-minDblBad", "15",
            };
            var options = new ArgsVariety();
            var result = CommandLineParser<ArgsVariety>.ParseArgs(args, options, "", "");
            Assert.IsFalse(result, "Parser did not fail on invalid min");
        }

        [Test]
        public void TestBadMinDbl()
        {
            var args = new string[]
            {
                "-minDbl", "15n",
            };
            var options = new ArgsVariety();
            var result = CommandLineParser<ArgsVariety>.ParseArgs(args, options, "", "");
            Assert.IsFalse(result, "Parser did not fail on invalid min");
        }

        [Test]
        public void TestMaxDbl1()
        {
            var args = new string[]
            {
                "-maxDbl", "15",
            };
            var options = new ArgsVariety();
            var result = CommandLineParser<ArgsVariety>.ParseArgs(args, options, "", "");
            Assert.IsFalse(result, "Parser did not fail on invalid min");
        }

        [Test]
        public void TestMaxDbl2()
        {
            var args = new string[]
            {
                "-maxDblBad", "5",
            };
            var options = new ArgsVariety();
            var result = CommandLineParser<ArgsVariety>.ParseArgs(args, options, "", "");
            Assert.IsFalse(result, "Parser did not fail on invalid min");
        }

        [Test]
        public void TestBadMaxDbl()
        {
            var args = new string[]
            {
                "-maxDbl", "5t",
            };
            var options = new ArgsVariety();
            var result = CommandLineParser<ArgsVariety>.ParseArgs(args, options, "", "");
            Assert.IsFalse(result, "Parser did not fail on invalid min");
        }

        private class ArgsVariety
        {
            [Option("minInt", Min = 10)]
            public int IntMinOnly { get; set; }

            [Option("maxInt", Max = 10)]
            public int IntMaxOnly { get; set; }

            [Option("minmaxInt", Min = -5, Max = 5)]
            public int IntMinMax { get; set; }

            [Option("minIntBad", Min = 10.1)]
            public int IntMinBad { get; set; }

            [Option("maxIntBad", Max = 10.5)]
            public int IntMaxBad { get; set; }

            [Option("minDbl", Min = 10)]
            public double DblMinOnly { get; set; }

            [Option("maxDbl", Max = 10)]
            public double DblMaxOnly { get; set; }

            [Option("minmaxDbl", Min = -5, Max = 5)]
            public double DblMinMax { get; set; }

            [Option("minDblBad", Min = "bad")]
            public double DblMinBad { get; set; }

            [Option("maxDblBad", Max = "bad")]
            public double DblMaxBad { get; set; }

            [Option("g")]
            public string LowerChar { get; set; }

            [Option("G")]
            public string UpperChar { get; set; }

            [Option("ab")]
            public string Ab1 { get; set; }

            [Option("aB")]
            public string Ab2 { get; set; }

            [Option("Ab")]
            public string Ab3 { get; set; }

            [Option("AB")]
            public string Ab4 { get; set; }

            [Option("b1")]
            public bool BoolCheck1 { get; set; }

            [Option("b2")]
            public bool BoolCheck2 { get; set; }

            [Option("b3")]
            public bool BoolCheck3 { get; set; }

            [Option("1")]
            public bool NumericArg { get; set; }

            [Option("over")]
            public string Overrides { get; set; }

            [Option("strArray")]
            public string[] StringArray { get; set; }

            [Option("intArray")]
            public int[] IntArray { get; set; }

            [Option("dblArray")]
            public double[] DblArray { get; set; }
        }
    }
}

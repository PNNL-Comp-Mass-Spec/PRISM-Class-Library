﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PRISM;

// ReSharper disable StringLiteralTypo
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace PRISMTest
{
    [TestFixture]
    public class CommandLineParserTests
    {
        // ReSharper disable CommentTypo

        // Ignore Spelling: tda, arg, args, badname, Intellisense, minint, maxint, minmaxint, mindbl, maxdbl, minmaxdbl
        // Ignore Spelling: minintbad, maxintbad, mindblbad, maxdblbad, minmaxInt, minmaxDbl, minmax, wildcards

        // ReSharper restore CommentTypo

        private const bool showHelpOnError = false;
        private const bool outputErrors = false;

        [Test]
        public void TestBadKey1()
        {
            var parser = new CommandLineParser<BadKey1>();
            var result = parser.ParseArgs(new[] { "-bad-name", "b" }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail with '-' at start of arg key");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.Contains("bad character") && x.Message.Contains("char '-'")), Is.True,
                "Error message does not contain \"bad character\" and \"char '-'\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestBadKey2()
        {
            var parser = new CommandLineParser<BadKey2>();
            var result = parser.ParseArgs(new[] { "/bad/name", "b" }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail with '/' at start of arg key");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.Contains("bad character") && x.Message.Contains("char '/'")), Is.True,
                "Error message does not contain \"bad character\" and \"char '/'\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestBadKey3()
        {
            var parser = new CommandLineParser<BadKey3>();
            var result = parser.ParseArgs(new[] { "-badname", "b" }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail with duplicate arg keys");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0 && x.Message.Contains("badname")), Is.True,
                "Error message does not contain \"duplicate\" and \"badname\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestBadKey4()
        {
            var parser = new CommandLineParser<BadKey4>();
            var result = parser.ParseArgs(new[] { "-NoGood", "b" }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail with duplicate arg keys");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0 && x.Message.Contains("NoGood")), Is.True,
                "Error message does not contain \"duplicate\" and \"NoGood\"");

            Console.WriteLine("\nThis error message was expected");
        }
        [Test]
        public void TestBadKey5()
        {
            var parser = new CommandLineParser<BadKey5>();
            var result = parser.ParseArgs(new[] { "-bad name", "b" }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail with ' ' in arg key");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.Contains("bad character") && x.Message.Contains("char ' '")), Is.True,
                "Error message does not contain \"bad character\" and \"char ' '\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestBadKey6()
        {
            var parser = new CommandLineParser<BadKey6>();
            var result = parser.ParseArgs(new[] { "/bad:name", "b" }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail with ':' in arg key");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.Contains("bad character") && x.Message.Contains("char ':'")), Is.True,
                "Error message does not contain \"bad character\" and \"char ':'\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestBadKey7()
        {
            var parser = new CommandLineParser<BadKey7>();
            var result = parser.ParseArgs(new[] { "-bad=name", "b" }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail with '=' in arg key");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.Contains("bad character") && x.Message.Contains("char '='")), Is.True,
                "Error message does not contain \"bad character\" and \"char '='\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestBadKey8()
        {
            var parser = new CommandLineParser<BadKey8>();
            var result = parser.ParseArgs(new[] { "-bad/name", "b" }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail with '/' in arg key");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                    x.Message.Contains("bad character") && x.Message.Contains("char '/'")), Is.True,
                "Error message does not contain \"bad character\" and \"char '='\"");

            Console.WriteLine("\nThis error message was expected");
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
            [Option("bad name")]
            public string BadName { get; set; }
        }

        private class BadKey6
        {
            [Option("bad:name")]
            public string BadName { get; set; }
        }

        private class BadKey7
        {
            [Option("bad=name")]
            public string BadName { get; set; }
        }

        private class BadKey8
        {
            [Option("bad/name")]
            public string BadName { get; set; }
        }

        private class WrappingTestKeys
        {
            [Option("InputFile", "InputFilePath", "i", "input", ArgPosition = 1, HelpShowsDefault = false, IsInputFilePath = true,
                HelpText = "Input file path (UIMF File);\nsupports wildcards, e.g. *.uimf")]
            public string InputFilePath { get; set; }

            [Option("OutputFile", "OutputFilePath", "o", "output", ArgPosition = 2, HelpShowsDefault = false, HelpText =
                "Output file path; ignored if the input file path has a wildcard or if /S was used (or a parameter file has Recurse=True)")]
            public string OutputFilePath { get; set; }

            [Option("BaseFrameMode", "BaseFrame", HelpText =
                "Method for selecting the base frame to align all the other frames to")]
            public int BaseFrameSelectionMode { get; set; }

            [Option("Smooth", "ScanSmoothCount", HelpText =
                "Number of points to use when smoothing TICs before aligning. " +
                "If 0 or 1; no smoothing is applied.")]
            public int ScanSmoothCount { get; set; }

            [Option("tda", Min = -1, Max = 1,
                HelpText = "Database search mode:\n0: don't search decoy database, \n1: search shuffled decoy database\n")]
            public int TdaInt { get; set; }

            [Option("threads", Min = 0, HelpText = "Maximum number of threads, or 0 to set automatically")]
            public int MaxNumThreads { get; set; }

            [Option("cores", Min = 0, HelpText = "Maximum number of CPUs, or 0 to set automatically")]
            public int MaxNumCPUs { get; set; }

            [Option("n", "NumMatchesPerSpec", "MatchesPerSpectrumToReport", HelpText = "Number of results to report for each mass spectrum")]
            public int MatchesPerSpectrumToReport { get; set; }

            [Option("v", "verbose", "VerboseMode", HelpText = "When true, show additional debug messages")]
            public bool VerboseMode { get; set; }
        }

        [Test]
        public void TestOkayKey1()
        {
            var parser = new CommandLineParser<OkayKey1>();
            var result = parser.ParseArgs(new[] { "-okay-name", "b" }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.True, "Parser failed with '-' not at start of arg key");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors, Is.Empty, "Error list not empty");
        }

        [Test]
        public void TestOkayKey2()
        {
            var parser = new CommandLineParser<OkayKey2>();
            var result = parser.ParseArgs(new[] { "/okay-name", "b" }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.True, "Parser failed with '/' not at start of arg key");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors, Is.Empty, "Error list not empty");
        }

        [Test]
        public void TestHelpKey1()
        {
            var parser = new CommandLineParser<OkayKey2>();
            var result = parser.ParseArgs(new[] { "--help" }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not \"fail\" when user requested the help screen");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors, Is.Empty, "Error list not empty");
        }

        [Test]
        public void TestHelpKey2()
        {
            var parser = new CommandLineParser<OkayKey2>
            {
                ParamFlagCharacters = new[] { '/', '-' }
            };
            var result = parser.ParseArgs(new[] { "/?" }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not \"fail\" when user requested the help screen");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors, Is.Empty, "Error list not empty");
        }

        private class OkayKey1
        {
            [Option("okay-name")]
            public string OkayName { get; set; }
        }

        private class OkayKey2
        {
            [Option("Okay-name", HelpText = "This switch has a dash in the name; that's unusual, but allowed")]
            public string OkayName { get; set; }

            [Option("Verbose", "Wordy", "Detailed",
                HelpText = "Use this switch to include verbose output, in homage to which this help text includes\n" +
                           "lorem ipsum dolor sit amet, elit phasellus, penatibus sed eget quis suspendisse.\n" +
                           "Quam suspendisse accumsan in vestibulum, ante donec dolor nibh, " +
                           "mauris sodales, orci mollis et convallis felis porta.\n" +
                           "Felis eu, metus sed, a quam nulla commodo nulla sit, diam sed morbi " +
                           "ut euismod et, diam vestibulum cursus.\n" +
                           "Dolor sit scelerisque tellus, wisi nec, mauris etiam potenti laoreet non, " +
                           "leo aliquam nonummy. Pulvinar tortor, leo rutrum blandit velit, quis lacus.")]
            public string Verbose { get; set; }

            [Option("empty", HelpText = "Empty String test")]
            public string EmptyString { get; set; }

            [Option("space", HelpText = "Quoted WhiteSpace test")]
            public string QuotedWhitespace { get; set; }

            [Option("char", HelpText = "Test for char values")]
            public char CharValue { get; set; } = 't';

            [Option("charEmpty", HelpText = "Test for empty char values")]
            public char EmptyCharValue { get; set; }

            /// <summary>
            /// Note that ## should be updated at runtime by calling UpdatePropertyHelpText
            /// </summary>
            [Option("Smooth", "alternativeLongNameForSmooth", HelpText = "Number of points to smooth; default is ## points")]
            public int Smooth { get; set; }

            [Option("Smooth2", "alternativeLongNameForSmooth2", HelpText = "Number of points to smooth", DefaultValueFormatString = "; default is {0} points")]
            public int Smooth2 { get; set; }

            [Option("ExtraSpecialProcessingOption", "ExtraSpecialProcessing", "ExtraSpecialProcessingOptionLongerName", "ESP",
                HelpText = "When true, enable special processing options")]
            public bool ExtraSpecialProcessingOption { get; set; }

            [Option("VeryVerboseProcessingOption", "VeryVerboseProcessing", "VeryVerboseProcessingOptionTag",
                HelpText = "When true, enable verbose processing", SecondaryArg = true)]
            public bool VeryVerboseProcessingOption { get; set; }

            [Option("gnat", HelpText = "I'm a supported argument, but I don't get advertised.", Hidden = true)]
            // ReSharper disable once MemberCanBePrivate.Local
            public int NoSeeUm { get; set; }

            /// <summary>
            /// When true, recurse subdirectories
            /// </summary>
            /// <remarks>
            /// This will be auto-set to true if MaxLevelsToRecurse is defined in the parameter file
            /// </remarks>
            public bool RecurseDirectories { get; set; }

            /// <summary>
            /// Process files in subdirectories
            /// </summary>
            [Option("MaxLevelsToRecurse", "S", ArgExistsProperty = nameof(RecurseDirectories),
                HelpShowsDefault = false, SecondaryArg = true,
                HelpText = "Number of levels of subdirectories to examine when finding files\n" +
                           "(0 means to recurse infinitely)")]
            public int MaxLevelsToRecurse { get; set; }

            public void ShowProcessingOptions()
            {
                Console.WriteLine("{0,-30} {1}", "OkayName:", OkayName);
                Console.WriteLine("{0,-30} {1}", "Verbose:", Verbose);
                Console.WriteLine("{0,-30} {1}", "Smooth:", Smooth);
                Console.WriteLine("{0,-30} {1}", "Smooth2:", Smooth2);
                Console.WriteLine("{0,-30} {1}", "ExtraSpecialProcessingOption:", ExtraSpecialProcessingOption);
                Console.WriteLine("{0,-30} {1}", "NoSeeUm:", NoSeeUm);

                Console.WriteLine("{0,-30} {1}", "RecurseDirectories:", RecurseDirectories);
                Console.WriteLine("{0,-30} {1}", "MaxLevelsToRecurse:", RecurseDirectories ? MaxLevelsToRecurse.ToString() : "n/a");
            }
        }

        [Test]
        public void TestGood()
        {
            var args = new[]
            {
                "MyInputFile.txt",
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
                "RandomlyPlacedOutputFile.txt",
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
                "UnusedPositionalArg.txt",
                "-intArray", "0",
                "-intArray", "1",
                "-intArray", "2",
                "-intArray", "3",
                "-intArray", "4",
                "-dblArray", "1.0"
            };

            var parser = new CommandLineParser<ArgsVariety>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            var options = result.ParsedResults;
            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, "Parser failed to parse valid args");
                Assert.That(result.ParseErrors, Is.Empty, "Error list not empty");
            });

            Console.WriteLine("{0,-15} {1}", "InputFilePath", options.InputFilePath);
            Console.WriteLine("{0,-15} {1}", "OutputFilePath", options.OutputFilePath);
            Console.WriteLine("{0,-15} {1}", "LowerChar", options.LowerChar);
            Console.WriteLine("{0,-15} {1}", "UpperChar", options.UpperChar);

            for (var i = 0; i < options.IntArray.Length; i++)
            {
                Console.WriteLine("{0,-15} {1}", "IntArray[" + i + "]:", options.IntArray[i]);
            }

            Assert.Multiple(() =>
            {
                Assert.That(options.IntMinOnly, Is.EqualTo(11), "Unexpected value for IntMinOnly");
                Assert.That(options.IntMaxOnly, Is.EqualTo(5), "Unexpected value for IntMaxOnly");
                Assert.That(options.IntMinMax, Is.EqualTo(2), "Unexpected value for IntMinMax");
                Assert.That(options.DblMinOnly, Is.EqualTo(15), "Unexpected value for DblMinOnly");
                Assert.That(options.DblMaxOnly, Is.EqualTo(5.5), "Unexpected value for DblMaxOnly");
                Assert.That(options.DblMinMax, Is.EqualTo(2.4), "Unexpected value for DblMinMax");
                Assert.That(options.LowerChar, Is.EqualTo(@"C:\Users\User"), "Unexpected value for LowerChar");
                Assert.That(options.UpperChar, Is.EqualTo(@"C:\Users\User2\"), "Unexpected value for UpperChar");
                Assert.That(options.Ab1, Is.EqualTo("TestAb1"), "Unexpected value for Ab1");
                Assert.That(options.Ab2, Is.EqualTo("TestAb2"), "Unexpected value for Ab2");
                Assert.That(options.Ab3, Is.EqualTo("TestAb3"), "Unexpected value for Ab3");
                Assert.That(options.Ab4, Is.EqualTo("TestAb4"), "Unexpected value for Ab4");
                Assert.That(options.BoolCheck1, Is.EqualTo(true), "Unexpected value for BoolCheck1");
                Assert.That(options.BoolCheck2, Is.EqualTo(false), "Unexpected value for BoolCheck2");
                Assert.That(options.BoolCheck3, Is.EqualTo(true), "Unexpected value for BoolCheck3");
                Assert.That(options.InputFilePath, Is.EqualTo("MyInputFile.txt"), "Unexpected value for InputFilePath");
                Assert.That(options.OutputFilePath, Is.EqualTo("RandomlyPlacedOutputFile.txt"), "Unexpected value for OutputFilePath");
                Assert.That(options.NumericArg, Is.EqualTo(true), "Unexpected value for NumericArg");
                Assert.That(options.Overrides, Is.EqualTo("This string should be used"), "Unexpected value for Overrides");
                Assert.That(options.StringArray, Has.Length.EqualTo(3), "Unexpected value for StringArray.Length");
                Assert.That(options.StringArray[0], Is.EqualTo("value1"), "Unexpected value for StringArray[0]");
                Assert.That(options.StringArray[1], Is.EqualTo("value2"), "Unexpected value for StringArray[1]");
                Assert.That(options.StringArray[2], Is.EqualTo("value3"), "Unexpected value for StringArray[2]");
                Assert.That(options.IntArray, Has.Length.EqualTo(5), "Unexpected value for IntArray.Length");
                Assert.That(options.IntArray[0], Is.EqualTo(0), "Unexpected value for IntArray[0]");
                Assert.That(options.IntArray[1], Is.EqualTo(1), "Unexpected value for IntArray[1]");
                Assert.That(options.IntArray[2], Is.EqualTo(2), "Unexpected value for IntArray[2]");
                Assert.That(options.IntArray[3], Is.EqualTo(3), "Unexpected value for IntArray[3]");
                Assert.That(options.IntArray[4], Is.EqualTo(4), "Unexpected value for IntArray[4]");
                Assert.That(options.DblArray, Has.Length.EqualTo(1), "Unexpected value for DblArray.Length");
                Assert.That(options.DblArray[0], Is.EqualTo(1.0), "Unexpected value for DblArray[0]");
            });
        }

        [Test]
        public void TestPositionalArgs()
        {
            var args = new[]
            {
                "MyInputFile.txt",
                "OutputFile.txt",
                "UnusedPositionalArg.txt",
            };

            var parser = new CommandLineParser<ArgsPositionalOnly>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            var options = result.ParsedResults;

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, "Parser failed to parse valid args");
                Assert.That(result.ParseErrors, Is.Empty, "Error list not empty");
            });

            Console.WriteLine("Input file path: {0}", options.InputFilePath);
            Console.WriteLine("Output file path: {0}", options.OutputFilePath);

            Assert.Multiple(() =>
            {
                Assert.That(options.InputFilePath, Is.EqualTo("MyInputFile.txt"));
                Assert.That(options.OutputFilePath, Is.EqualTo("OutputFile.txt"));
            });
        }

        [Test]
        public void TestPositionalArgsLinuxPath()
        {
            // These file paths have multiple forward slashes, so they will be treated as values for the -i and -o arguments
            // See also test methods TestPositionalArgsLinuxPath2 and TestArgsLinuxPath2

            var args = new[]
            {
                "/home/user/MyInputFile.txt",
                "/home/user/OutputFile.txt",
                "UnusedPositionalArg.txt",
            };

            var parser = new CommandLineParser<ArgsPositionalOnly>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            var options = result.ParsedResults;

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, "Parser failed to parse valid args");
                Assert.That(result.ParseErrors, Is.Empty, "Error list not empty");
            });

            Console.WriteLine("Input file path: {0}", options.InputFilePath);
            Console.WriteLine("Output file path: {0}", options.OutputFilePath);

            Assert.Multiple(() =>
            {
                Assert.That(options.InputFilePath, Is.EqualTo("/home/user/MyInputFile.txt"));
                Assert.That(options.OutputFilePath, Is.EqualTo("/home/user/OutputFile.txt"));
            });
        }

        [Test]
        public void TestPositionalArgsLinuxPath2()
        {
            // ReSharper disable CommentTypo

            // These file paths only have one forward slash, so they will be assumed to be arguments
            // To avoid this issue, either use \/pagefile.sys or use ./pagefile.sys
            // See also test methods TestPositionalArgsLinuxPath and TestArgsLinuxPath2

            var args = new[]
            {
                "./pagefile.sys",
                "./ProgramData",
                "UnusedPositionalArg.txt",
            };

            // ReSharper restore CommentTypo

            var parser = new CommandLineParser<ArgsPositionalOnly>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            var options = result.ParsedResults;

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, "Parser failed to parse valid args");
                Assert.That(result.ParseErrors, Is.Empty, "Error list not empty");
            });

            Console.WriteLine("Input file path: {0}", options.InputFilePath);
            Console.WriteLine("Output file path: {0}", options.OutputFilePath);

            Assert.Multiple(() =>
            {
                Assert.That(options.InputFilePath, Is.EqualTo("./pagefile.sys"));
                Assert.That(options.OutputFilePath, Is.EqualTo("./ProgramData"));
            });
        }

        [Test]
        public void TestPositionalArgsLinuxPathBad()
        {
            var args = new[]
            {
                "/badPath",
                "/BadPath2",
                "UnusedPositionalArg.txt",
            };

            var parser = new CommandLineParser<ArgsPositionalOnly>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);

            // ReSharper disable once UnusedVariable
            var options = result.ParsedResults;

            Assert.That(result.Success, Is.False, "Parser did not fail with unrecognized argument name error");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x => x.Message.Contains("Unrecognized argument name")), Is.True);

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestArgsLinuxPath()
        {
            var args = new[]
            {
                "-i", "/home/user/MyInputFile.txt",
                "-o", "/home/user/OutputFile.txt"
            };

            var parser = new CommandLineParser<ArgsPositionalOnly>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            var options = result.ParsedResults;

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, "Parser failed to parse valid args");
                Assert.That(result.ParseErrors, Is.Empty, "Error list not empty");
            });

            Console.WriteLine("Input file path: {0}", options.InputFilePath);
            Console.WriteLine("Output file path: {0}", options.OutputFilePath);

            Assert.Multiple(() =>
            {
                Assert.That(options.InputFilePath, Is.EqualTo("/home/user/MyInputFile.txt"));
                Assert.That(options.OutputFilePath, Is.EqualTo("/home/user/OutputFile.txt"));
            });
        }

        [Test]
        public void TestArgsLinuxPath2()
        {
            // Use an equals sign after -i and -o since the file path only has one forward slash
            // See also test methods TestPositionalArgsLinuxPath and TestPositionalArgsLinuxPath2

            var args = new[]
            {
                "-i=/pagefile.sys",
                "-o=/ProgramData"
            };

            var parser = new CommandLineParser<ArgsPositionalOnly>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            var options = result.ParsedResults;

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, "Parser failed to parse valid args");
                Assert.That(result.ParseErrors, Is.Empty, "Error list not empty");
            });

            Console.WriteLine("Input file path: {0}", options.InputFilePath);
            Console.WriteLine("Output file path: {0}", options.OutputFilePath);

            Assert.Multiple(() =>
            {
                Assert.That(options.InputFilePath, Is.EqualTo("/pagefile.sys"));
                Assert.That(options.OutputFilePath, Is.EqualTo("/ProgramData"));
            });
        }

        [Test]
        public void TestArgsLinuxPathBad()
        {
            var args = new[]
            {
                "-i", "/BadPath",
                "-o", "/BadPath2"
            };

            var parser = new CommandLineParser<ArgsPositionalOnly>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);

            // ReSharper disable once UnusedVariable
            var options = result.ParsedResults;

            Assert.That(result.Success, Is.False, "Parser did not fail with unrecognized argument name error");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x => x.Message.Contains("Unrecognized argument name")), Is.True);

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestMinInt1()
        {
            var args = new[]
            {
                "-minInt", "5",
            };
            var parser = new CommandLineParser<ArgsVariety>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail on value less than min");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.IndexOf("minint", StringComparison.OrdinalIgnoreCase) >= 0 && x.Message.Contains("is less than minimum")), Is.True,
                "Error message does not contain \"minInt\" and \"is less than minimum\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestMinInt2()
        {
            var args = new[]
            {
                "-minMaxInt", "-15",
            };
            var parser = new CommandLineParser<ArgsVariety>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail on value less than min");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.IndexOf("minmaxint", StringComparison.OrdinalIgnoreCase) >= 0 && x.Message.Contains("is less than minimum")), Is.True,
                "Error message does not contain \"minMaxInt\" and \"is less than minimum\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestMinInt3()
        {
            var args = new[]
            {
                "-minIntBad", "15",
            };
            var parser = new CommandLineParser<ArgsVariety>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail on invalid min type");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.IndexOf("minintbad", StringComparison.OrdinalIgnoreCase) >= 0 && x.Message.Contains("cannot cast min or max to type")), Is.True,
                "Error message does not contain \"minIntBad\" and \"cannot cast min or max to type\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestBadMinInt()
        {
            var args = new[]
            {
                "-minInt", "15.0",
            };
            var parser = new CommandLineParser<ArgsVariety>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not on invalid type");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.IndexOf("minint", StringComparison.OrdinalIgnoreCase) >= 0 && x.Message.Contains("cannot cast") && x.Message.Contains("to type")), Is.True,
                "Error message does not contain \"minInt\", \"cannot cast\", and \"to type\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestMaxInt1()
        {
            var args = new[]
            {
                "-maxInt", "15",
            };
            var parser = new CommandLineParser<ArgsVariety>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail on value greater than max");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.IndexOf("maxint", StringComparison.OrdinalIgnoreCase) >= 0 && x.Message.Contains("is greater than maximum")), Is.True,
                "Error message does not contain \"maxInt\" and \"is greater than maximum\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestMaxInt2()
        {
            var args = new[]
            {
                "-maxIntBad", "5",
            };
            var parser = new CommandLineParser<ArgsVariety>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail on invalid max type");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.IndexOf("maxintbad", StringComparison.OrdinalIgnoreCase) >= 0 &&
                x.Message.Contains("cannot cast min or max to type")), Is.True,
                "Error message does not contain \"maxIntBad\" and \"cannot cast min or max to type\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestBadMaxInt()
        {
            var args = new[]
            {
                "-maxInt", "9.0",
            };
            var parser = new CommandLineParser<ArgsVariety>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not on invalid type");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.IndexOf("maxint", StringComparison.OrdinalIgnoreCase) >= 0 && x.Message.Contains("cannot cast") && x.Message.Contains("to type")), Is.True,
                "Error message does not contain \"maxInt\", \"cannot cast\", and \"to type\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestMinDbl1()
        {
            var args = new[]
            {
                "-minDbl", "5",
            };
            var parser = new CommandLineParser<ArgsVariety>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail on value less than min");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.IndexOf("mindbl", StringComparison.OrdinalIgnoreCase) >= 0 && x.Message.Contains("is less than minimum")), Is.True,
                "Error message does not contain \"minDbl\" and \"is less than minimum\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestMinDbl2()
        {
            var args = new[]
            {
                "-MinMaxDbl", "-15",
            };
            var parser = new CommandLineParser<ArgsVariety>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail on value less than min");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.IndexOf("minmaxdbl", StringComparison.OrdinalIgnoreCase) >= 0 && x.Message.Contains("is less than minimum")), Is.True,
                "Error message does not contain \"MinMaxDbl\" and \"is less than minimum\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestMinDbl3()
        {
            var args = new[]
            {
                "-minDblBad", "15",
            };
            var parser = new CommandLineParser<ArgsVariety>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail on invalid min type");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.IndexOf("mindblbad", StringComparison.OrdinalIgnoreCase) >= 0 && x.Message.Contains("cannot cast min or max to type")), Is.True,
                "Error message does not contain \"minDblBad\" and \"cannot cast min or max to type\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestBadMinDbl()
        {
            var args = new[]
            {
                "-minDbl", "15n",
            };
            var parser = new CommandLineParser<ArgsVariety>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail on invalid type");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.IndexOf("mindbl", StringComparison.OrdinalIgnoreCase) >= 0 && x.Message.Contains("cannot cast") && x.Message.Contains("to type")), Is.True,
                "Error message does not contain \"minDbl\", \"cannot cast\", and \"to type\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestMaxDbl1()
        {
            var args = new[]
            {
                "-maxDbl", "15",
            };
            var parser = new CommandLineParser<ArgsVariety>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail on value greater than max");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.IndexOf("maxdbl", StringComparison.OrdinalIgnoreCase) >= 0 && x.Message.Contains("is greater than maximum")), Is.True,
                "Error message does not contain \"maxDbl\" and \"is greater than maximum\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestMaxDbl2()
        {
            var args = new[]
            {
                "-maxDblBad", "5",
            };
            var parser = new CommandLineParser<ArgsVariety>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail on invalid max type");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.IndexOf("maxdblbad", StringComparison.OrdinalIgnoreCase) >= 0 && x.Message.Contains("cannot cast min or max to type")), Is.True,
                "Error message does not contain \"maxDblBad\" and \"cannot cast min or max to type\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestBadMaxDbl()
        {
            var args = new[]
            {
                "-maxDbl", "5t",
            };
            var parser = new CommandLineParser<ArgsVariety>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail on invalid type");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.IndexOf("maxdbl", StringComparison.OrdinalIgnoreCase) >= 0 && x.Message.Contains("cannot cast") && x.Message.Contains("to type")), Is.True,
                "Error message does not contain \"maxDbl\", \"cannot cast\", and \"to type\"");

            Console.WriteLine("\nThis error message was expected");
        }

        /// <summary>
        /// Test setting the value of a boolean parameter using true, false, 1, 0, yes, or no
        /// </summary>
        /// <remarks>
        /// Test cases using On and Off will produce an error;
        /// this is expected since the CommandLineParser does not support On or Off for boolean args
        /// </remarks>
        /// <param name="testCaseIndex">Test case index</param>
        /// <param name="verboseFlagValue">Verbose flag</param>
        /// <param name="expectedParseResult">Expected parse result</param>
        /// <param name="parseErrorExpected">True if we expect an error</param>
        [Test]
        [TestCase(0, "True", true)]
        [TestCase(1, "False", false)]
        [TestCase(2, "TRUE", true)]
        [TestCase(3, "false", false)]
        [TestCase(4, "1", true)]
        [TestCase(5, "0", false)]
        [TestCase(6, "Yes", true)]
        [TestCase(7, "No", false)]
        [TestCase(8, "On", false, true)]
        [TestCase(9, "Off", false, true)]
        public void TestBoolParameter(int testCaseIndex, string verboseFlagValue, bool expectedParseResult, bool parseErrorExpected = false)
        {
            var parser = new CommandLineParser<WrappingTestKeys> { ParamKeysFieldWidth = 20 };
            var random = new Random(314 + testCaseIndex);

            var tdaValue = random.Next(0, 2).ToString();
            var threads = random.Next(1, 17).ToString();
            var cores = random.Next(1, 5).ToString();
            var matchesPerSpectrum = random.Next(1, 4).ToString();

            var result = parser.ParseArgs(new[] {
                "/tda", tdaValue,
                "-threads", threads,
                "--cores", cores,
                "-n", matchesPerSpectrum,
                "--verbose", verboseFlagValue }, showHelpOnError, outputErrors);

            if (!result.Success)
            {
                result.OutputErrors();

                if (!parseErrorExpected)
                    Assert.That(result.Success, Is.True, "result.Success is false");
            }

            Console.WriteLine("{0,-25} {1}", "Tda Flag:", result.ParsedResults.TdaInt);
            Console.WriteLine("{0,-25} {1}", "Max Threads:", result.ParsedResults.MaxNumThreads);
            Console.WriteLine("{0,-25} {1}", "Max CPUs:", result.ParsedResults.MaxNumCPUs);
            Console.WriteLine("{0,-25} {1}", "Matches per Spectrum:", result.ParsedResults.MatchesPerSpectrumToReport);
            Console.WriteLine("{0,-25} {1}", "Verbose Mode:", result.ParsedResults.VerboseMode);

            Assert.That(result.ParsedResults.VerboseMode, Is.EqualTo(expectedParseResult), $"{verboseFlagValue} did not get parsed as {expectedParseResult}");
        }

        [Test]
        [TestCase(0, 0)]
        [TestCase(20, 56)]
        [TestCase(18, 60)]
        [TestCase(19, 60)]
        [TestCase(20, 60)]
        [TestCase(21, 60)]
        [TestCase(22, 60)]
        [TestCase(23, 60)]
        [TestCase(24, 60)]
        [TestCase(25, 60)]
        [TestCase(26, 60)]
        public void TestDescriptionWrapping(int keyNamesFieldWidth, int descriptionFieldWidth)
        {
            var parser = new CommandLineParser<WrappingTestKeys>();

            if (keyNamesFieldWidth > 0)
                parser.ParamKeysFieldWidth = keyNamesFieldWidth;

            if (descriptionFieldWidth > 0)
                parser.ParamDescriptionFieldWidth = descriptionFieldWidth;

            var result = parser.ParseArgs(new[] { "--help" }, showHelpOnError, outputErrors);

            if (result.Success)
                Assert.Fail("result.Success should be false, not true");
        }

        // ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
        // ReSharper disable MemberCanBePrivate.Local
        private class ArgsEnum
        {
            [Option("u", HelpText = "I Am Unknown")]
            public TestEnum BeUnknown { get; set; }

            [Option("2t", HelpText = "I Am Too True")]
            public TestEnum TooTrue { get; set; }

            [Option("l", HelpText = "I AM LEGEND!")]
            public TestEnum Legendary { get; set; }

            [Option("f", HelpText = "I lied.")]
            public TestEnum TooBad { get; set; }

            [Option("result", HelpText = "How bad will it be?")]
            public TestEnumFlags ResultEffect { get; set; }

            // ReSharper disable once ConvertConstructorToMemberInitializers
            public ArgsEnum()
            {
                BeUnknown = TestEnum.Unknown;
                TooTrue = TestEnum.CantBeTruer;
                Legendary = TestEnum.Legend;
                TooBad = TestEnum.False;
                ResultEffect = TestEnumFlags.Good;
            }
        }

        private enum TestEnum
        {
            False = -1,
            Unknown = 0,
            True = 1,
            DoublyTrue = 2,
            CantBeTruer = 3,
            Legend = 100
        }

        // Use of System.ComponentModel.Description in the following enum results in the given description being displayed when Intellisense shows available enum values
        // See also the output from unit test TestEnumHelp

        [Flags]
        private enum TestEnumFlags
        {
            [System.ComponentModel.Description("It's Okay")]
            Good = 0x0,
            [System.ComponentModel.Description("It's Bad")]
            Bad = 0x1,
            [System.ComponentModel.Description("It's really bad")]
            Ugly = 0x2,
            [System.ComponentModel.Description("It's the end of the world")]
            Apocalypse = 0x4,
            [System.ComponentModel.Description("You just blew up the universe")]
            EndOfUniverse = 0x8
        }

        [Test]
        public void TestEnumHelp()
        {
            var parser = new CommandLineParser<ArgsEnum>();
            parser.PrintHelp();
        }

        [Test]
        public void TestEnumArgs()
        {
            var args = new[]
            {
                "-u", "DoublyTrue",
                "-f", "100",
                "-result", "14"
            };
            var parser = new CommandLineParser<ArgsEnum>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            var options = result.ParsedResults;
            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, "Parser failed to parse valid args");
                Assert.That(result.ParseErrors, Is.Empty, "Error list not empty");
                Assert.That(options.BeUnknown, Is.EqualTo(TestEnum.DoublyTrue));
                Assert.That(options.TooBad, Is.EqualTo(TestEnum.Legend));
                Assert.That(options.ResultEffect.ToString(), Is.EqualTo("Ugly, Apocalypse, EndOfUniverse"));
                Assert.That(options.ResultEffect, Is.EqualTo(TestEnumFlags.Ugly | TestEnumFlags.Apocalypse | TestEnumFlags.EndOfUniverse));
            });
        }

        [Test]
        public void TestEnumArgsBadArg()
        {
            var args = new[]
            {
                "-u", "DoublyTrue",
                "-f", "100",
                "-2t", "5"
            };
            var parser = new CommandLineParser<ArgsEnum>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not on invalid type");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.IndexOf("2t", StringComparison.OrdinalIgnoreCase) >= 0 && x.Message.Contains("cannot cast") && x.Message.Contains("to type")), Is.True,
                "Error message does not contain \"2t\", \"cannot cast\", and \"to type\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestEnumArgsBadArgString()
        {
            var args = new[]
            {
                "-u", "DoublyTrue",
                "-f", "100",
                "-2t", "Legendary"
            };
            var parser = new CommandLineParser<ArgsEnum>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not on invalid type");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.IndexOf("2t", StringComparison.OrdinalIgnoreCase) >= 0 && x.Message.Contains("cannot cast") && x.Message.Contains("to type")), Is.True,
                "Error message does not contain \"2t\", \"cannot cast\", and \"to type\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        [TestCase(0, 0, false)]
        [TestCase(20, 0, false)]
        [TestCase(30, 0, false)]
        [TestCase(30, 40, false)]
        [TestCase(0, 0, true)]
        [TestCase(14, 0, true)]
        [TestCase(16, 0, true)]
        [TestCase(18, 0, true)]
        [TestCase(19, 0, true)]
        [TestCase(20, 0, true)]
        [TestCase(21, 0, true)]
        [TestCase(22, 0, true)]
        [TestCase(23, 0, true)]
        [TestCase(24, 0, true)]
        [TestCase(25, 0, true)]
        [TestCase(26, 0, true)]
        [TestCase(30, 0, true)]
        [TestCase(30, 40, true)]
        public void TestPrintHelp(int paramKeysWidth, int helpDescriptionWidth, bool hideLongKeyNames)
        {
            var exeName = "Test.exe";

            var parser = new CommandLineParser<OkayKey2>()
            {
                ProgramInfo = "This program sed tempor urna. Proin porta scelerisque nisi, " +
                              "non vestibulum elit varius vel. Sed sed tristique orci, sit amet " +
                              "feugiat risus. \n\n" +
                              "Vivamus ac fermentum eros. Aliquam accumsan est vitae quam rhoncus, " +
                              "et consectetur ante egestas. Donec in enim id arcu mollis sagittis. " +
                              "Nulla venenatis tellus at urna feugiat, et placerat tortor dapibus. " +
                              "Proin in bibendum dui. Phasellus bibendum purus non mi semper, vel rhoncus " +
                              "massa viverra. Aenean quis neque sit amet nisi posuere congue. \n\n" +
                              "Options for EnumTypeMode are:\n" +
                              "  0 for feugiat risu\n" +
                              "  1 for porttitor libero\n" +
                              "  2 for sapien maximus varius\n" +
                              "  3 for lorem luctus\n" +
                              "  4 for pulvinar quam at libero dapibus\n" +
                              "  5 for tortor loborti\n" +
                              "  6 for ante nec nisi consequat\n" +
                              "  7 for facilisis vestibulum risus",

                ContactInfo = "Program written by Maecenas cursus for fermentum ullamcorper velit in 2017" +
                              Environment.NewLine +
                              "E-mail: person@place.org or alternate@place.org" + Environment.NewLine +
                              "Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://www.pnnl.gov/integrative-omics",

                UsageExamples = {
                    exeName + " InputFile.txt",
                    exeName + " InputFile.txt /Start:2",
                    exeName + " InputFile.txt /Start:2 /EnumTypeMode:2 /Smooth:7"
                },
                HideLongParamKeyNamesAtConsole = hideLongKeyNames
            };

            parser.PrintHelp(paramKeysWidth, helpDescriptionWidth);
        }

        [Test]
        [TestCase(0, 0, false)]
        [TestCase(20, 0, false)]
        [TestCase(30, 40, false)]
        [TestCase(0, 0, true)]
        [TestCase(30, 0, true)]
        [TestCase(30, 40, true)]
        public void TestPrintHelpNoParamFile(int paramKeysWidth, int helpDescriptionWidth, bool hideLongKeyNames)
        {
            var exeName = "Test.exe";

            var parser = new CommandLineParser<OkayKey2>()
            {
                ProgramInfo = "This program sed tempor urna. Proin porta scelerisque nisi, " +
                              "non vestibulum elit varius vel. Sed sed tristique orci, sit amet " +
                              "feugiat risus. \n\n" +
                              "Vivamus ac fermentum eros. Aliquam accumsan est vitae quam rhoncus, " +
                              "et consectetur ante egestas. Donec in enim id arcu mollis sagittis. " +
                              "Nulla venenatis tellus at urna feugiat, et placerat tortor dapibus. " +
                              "Proin in bibendum dui. Phasellus bibendum purus non mi semper, vel rhoncus " +
                              "massa viverra. Aenean quis neque sit amet nisi posuere congue. \n\n" +
                              "Options for EnumTypeMode are:\n" +
                              "  0 for feugiat risu\n" +
                              "  1 for porttitor libero\n" +
                              "  2 for sapien maximus varius\n" +
                              "  3 for lorem luctus\n" +
                              "  4 for pulvinar quam at libero dapibus\n" +
                              "  5 for tortor loborti\n" +
                              "  6 for ante nec nisi consequat\n" +
                              "  7 for facilisis vestibulum risus",

                ContactInfo = "Program written by Maecenas cursus for fermentum ullamcorper velit in 2017" +
                              Environment.NewLine +
                              "E-mail: person@place.org or alternate@place.org" + Environment.NewLine +
                              "Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://www.pnnl.gov/integrative-omics",

                UsageExamples = {
                    exeName + " InputFile.txt",
                    exeName + " InputFile.txt /Start:2",
                    exeName + " InputFile.txt /Start:2 /EnumTypeMode:2 /Smooth:7"
                },
                HideLongParamKeyNamesAtConsole = hideLongKeyNames,
                DisableParameterFileSupport = true
            };

            parser.PrintHelp(paramKeysWidth, helpDescriptionWidth);
        }

        [Test]
        [TestCase("")]
        [TestCase("ParamFileExample.conf")]
        public void TestParamFileOutputConsole(string parameterFileName)
        {
            var parser = new CommandLineParser<OkayKey2>();
            var args = new List<string> { "-CreateParamFile" };

            if (!string.IsNullOrWhiteSpace(parameterFileName))
            {
                args.Add(new FileInfo(parameterFileName).FullName);
            }

            var result = parser.ParseArgs(args.ToArray());

            Console.WriteLine();
            Console.WriteLine("Class description of parsed results:");
            Console.WriteLine(result);
        }

        [Test]
        public void TestParamFileRoundTrip()
        {
            var parser = new CommandLineParser<OkayKey2>();
            var results = parser.Results.ParsedResults;
            results.Smooth = 5;
            results.Smooth2 = 10;
            results.OkayName = "A Real Value!";
            results.Verbose = "Concise";
            results.CharValue = 'c';
            results.EmptyString = "";
            results.EmptyCharValue = '\0';
            results.QuotedWhitespace = " ";
            results.ExtraSpecialProcessingOption = false;

            var paramFileName = "exampleParams.txt";
            var paramFile = new FileInfo(paramFileName);

            Console.WriteLine("Creating parameter file " + paramFile.FullName);
            Console.WriteLine();

            parser.CreateParamFile(paramFile.FullName);

            using (var reader = new StreamReader(new FileStream(paramFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                var contents = reader.ReadToEnd();
                Console.WriteLine(contents);
            }

            var parser2 = new CommandLineParser<OkayKey2>();
            var results2 = parser2.ParseArgs(new[] { "-ParamFile", paramFile.FullName }).ParsedResults;
            Assert.Multiple(() =>
            {
                Assert.That(results2.Smooth, Is.EqualTo(results.Smooth));
                Assert.That(results2.Smooth2, Is.EqualTo(results.Smooth2));
                Assert.That(results2.OkayName, Is.EqualTo(results.OkayName));
                Assert.That(results2.Verbose, Is.EqualTo(results.Verbose));
                Assert.That(results2.CharValue, Is.EqualTo(results.CharValue));
                Assert.That(results2.EmptyString, Is.EqualTo(results.EmptyString));
                Assert.That(results2.EmptyCharValue, Is.EqualTo(results.EmptyCharValue));
                Assert.That(results2.QuotedWhitespace, Is.EqualTo(results.QuotedWhitespace));
            });

            var parser3 = new CommandLineParser<OkayKey2>();
            var smooth2Override = 15;
            var okayNameOverride = "A Different Value?";
            var results3 = parser3.ParseArgs(
                new[] { "-smooth2", smooth2Override.ToString(), "-okay-name", okayNameOverride, "-ESP", "-ParamFile", paramFileName }).ParsedResults;
            Assert.Multiple(() =>
            {
                Assert.That(results3.Smooth, Is.EqualTo(results.Smooth));
                Assert.That(results3.Smooth2, Is.EqualTo(smooth2Override));
                Assert.That(results3.OkayName, Is.EqualTo(okayNameOverride));
                Assert.That(results3.Verbose, Is.EqualTo(results.Verbose));
                Assert.That(results3.ExtraSpecialProcessingOption, Is.EqualTo(true));
            });
        }

        [Test]
        public void TestParamFileRoundTripDuplicate()
        {
            var parser = new CommandLineParser<OkayKey2>();
            var results = parser.Results.ParsedResults;
            results.Smooth = 5;
            results.Smooth2 = 10;
            results.OkayName = "A Real Value!";
            results.Verbose = "Concise";
            results.ExtraSpecialProcessingOption = false;

            var paramFileName = "exampleParams.txt";
            var paramFile = new FileInfo(paramFileName);

            parser.CreateParamFile(paramFile.FullName);

            var parser2 = new CommandLineParser<OkayKey2>();
            var results2 = parser2.ParseArgs(new[] { "-ParamFile", paramFile.FullName }).ParsedResults;
            Assert.Multiple(() =>
            {
                Assert.That(results2.Smooth, Is.EqualTo(results.Smooth));
                Assert.That(results2.Smooth2, Is.EqualTo(results.Smooth2));
                Assert.That(results2.OkayName, Is.EqualTo(results.OkayName));
                Assert.That(results2.Verbose, Is.EqualTo(results.Verbose));
            });

            // "Duplicate parameter" parsing error with duplicated, non-array parameter
            File.AppendAllText(paramFile.FullName, "\nOkay-Name=Duplicated\n");
            var parser3 = new CommandLineParser<OkayKey2>();
            var results3 = parser3.ParseArgs(new[] { "-ParamFile", paramFile.FullName });
            Assert.Multiple(() =>
            {
                Assert.That(results3.Success, Is.EqualTo(false));
                Assert.That(results3.ParseErrors, Has.Count.LessThanOrEqualTo(1));
            });

            foreach (var error in results3.ParseErrors)
            {
                Console.WriteLine(error.Message);
            }

            Assert.That(results3.ParseErrors.Any(x => x.Message.Contains("Duplicated parameter")), Is.EqualTo(true));
        }

        [Test]
        public void TestParamFileRoundTripDuplicateNotParsed()
        {
            var parser = new CommandLineParser<OkayKey2>();
            var results = parser.Results.ParsedResults;
            results.Smooth = 5;
            results.Smooth2 = 10;
            results.OkayName = "A Real Value!";
            results.Verbose = "Concise";
            results.ExtraSpecialProcessingOption = false;

            var paramFileName = "exampleParams.txt";
            var paramFile = new FileInfo(paramFileName);

            Console.WriteLine("Creating parameter file " + paramFile.FullName);
            Console.WriteLine();

            parser.CreateParamFile(paramFile.FullName);

            using (var reader = new StreamReader(new FileStream(paramFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                var contents = reader.ReadToEnd();
                Console.WriteLine(contents);
            }

            var parser2 = new CommandLineParser<OkayKey2>();
            var results2 = parser2.ParseArgs(new[] { "-ParamFile", paramFile.FullName }).ParsedResults;
            Assert.Multiple(() =>
            {
                Assert.That(results2.Smooth, Is.EqualTo(results.Smooth));
                Assert.That(results2.Smooth2, Is.EqualTo(results.Smooth2));
                Assert.That(results2.OkayName, Is.EqualTo(results.OkayName));
                Assert.That(results2.Verbose, Is.EqualTo(results.Verbose));
            });

            // Add a couple arguments that do not match any of the argument names. These must be ignored by the duplicate-check code
            File.AppendAllText(paramFile.FullName, "\nOkayName=Duplicated\n");
            File.AppendAllText(paramFile.FullName, "\nOkayName=Duplicated\n");
            var parser3 = new CommandLineParser<OkayKey2>();
            var results3 = parser2.ParseArgs(new[] { "-ParamFile", paramFile.FullName }).ParsedResults;
            Assert.Multiple(() =>
            {
                Assert.That(parser3.Results.Success, Is.EqualTo(true));
                Assert.That(results3.Smooth, Is.EqualTo(results.Smooth));
                Assert.That(results3.Smooth2, Is.EqualTo(results.Smooth2));
                Assert.That(results3.OkayName, Is.EqualTo(results.OkayName));
                Assert.That(results3.Verbose, Is.EqualTo(results.Verbose));
            });
        }

        [Test]
        public void TestParamFileRoundTripDuplicateArray()
        {
            var parser = new CommandLineParser<ArgsArray>();
            var results = parser.Results.ParsedResults;
            results.IntMinOnly = 15;
            results.DblMinOnly = 25;
            results.LowerChar = "Something";
            results.BoolCheck = true;
            results.StringArray = new[] {"S1", "S2" , "S3" , "S4" , "S5"};
            results.IntArray = new[] {1, 2, 3, 4, 5};

            var paramFileName = "exampleParams.txt";
            var paramFile = new FileInfo(paramFileName);

            Console.WriteLine("Creating parameter file " + paramFile.FullName);
            Console.WriteLine();

            parser.CreateParamFile(paramFile.FullName);

            using (var reader = new StreamReader(new FileStream(paramFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                var contents = reader.ReadToEnd();
                Console.WriteLine(contents);
            }

            // No "duplicate parameter" error when parsing a parameter file with array entries
            var parser2 = new CommandLineParser<ArgsArray>();
            var results2 = parser2.ParseArgs(new[] { "-ParamFile", paramFile.FullName }).ParsedResults;
            Assert.Multiple(() =>
            {
                Assert.That(results2.IntMinOnly, Is.EqualTo(results.IntMinOnly));
                Assert.That(results2.DblMinOnly, Is.EqualTo(results.DblMinOnly));
                Assert.That(results2.LowerChar, Is.EqualTo(results.LowerChar));
                Assert.That(results2.BoolCheck, Is.EqualTo(results.BoolCheck));
                Assert.That(results.StringArray.SequenceEqual(results2.StringArray), "StringArray SequenceEqual");
                Assert.That(results.IntArray.SequenceEqual(results2.IntArray), "IntArray SequenceEqual");
            });
        }

        [Test]
        [TestCase(false)]
        [TestCase(true)]
        public void TestUpdateExistingOptionsUsingParamFile(bool includeSecondaryArgs)
        {
            const int LEVELS_TO_RECURSE = 2;

            var parameterFileName = includeSecondaryArgs ? "ExampleSparseParams2.txt" : "ExampleSparseParams.txt";

            var paramFile = new FileInfo(parameterFileName);

            Console.WriteLine("Creating file " + paramFile.FullName);
            Console.WriteLine();

            using (var writer = new StreamWriter(new FileStream(paramFile.FullName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)))
            {
                writer.WriteLine("Smooth=6");
                writer.WriteLine("Verbose=No");

                if (includeSecondaryArgs)
                {
                    writer.WriteLine("MaxLevelsToRecurse={0}", LEVELS_TO_RECURSE);
                }

                // Note that the following option is backed by a boolean property
                // The parser supports true, false, 1, 0, yes, or no for the value
                writer.WriteLine("ExtraSpecialProcessing=Yes");
            }

            var options = new OkayKey2
            {
                Smooth = 5,
                Smooth2 = 10,
                OkayName = "A Real Value!",
                Verbose = "Yes",
                ExtraSpecialProcessingOption = false
            };

            Console.WriteLine();
            Console.WriteLine("Class values before reading the parameter file");
            options.ShowProcessingOptions();

            Assert.Multiple(() =>
            {
                Assert.That(options.Verbose, Is.EqualTo("Yes"));
                Assert.That(options.Smooth, Is.EqualTo(5));
                Assert.That(options.Smooth2, Is.EqualTo(10));
                Assert.That(options.ExtraSpecialProcessingOption, Is.EqualTo(false));
            });

            var args = new List<string>
            {
                "-ParamFile",
                paramFile.FullName
            };

            var success = CommandLineParser<OkayKey2>.ParseArgs(args.ToArray(), options);

            Assert.That(success, Is.True, "Call to static instance of ParseArgs failed");

            Console.WriteLine();
            Console.WriteLine("Class values after reading the parameter file");
            options.ShowProcessingOptions();

            Assert.Multiple(() =>
            {
                Assert.That(options.Verbose, Is.EqualTo("No"));
                Assert.That(options.Smooth, Is.EqualTo(6));
                Assert.That(options.Smooth2, Is.EqualTo(10));
                Assert.That(options.ExtraSpecialProcessingOption, Is.EqualTo(true));
            });

            if (options.RecurseDirectories)
            {
                Assert.That(options.MaxLevelsToRecurse, Is.EqualTo(LEVELS_TO_RECURSE));
            }

            options.Smooth = 18;
            options.Smooth2 = 28;
            options.ExtraSpecialProcessingOption = false;
            options.RecurseDirectories = false;

            Console.WriteLine();
            Console.WriteLine("Class values after manually changing Smooth, Smooth2, and RecurseDirectories");
            options.ShowProcessingOptions();

            Assert.Multiple(() =>
            {
                Assert.That(options.Verbose, Is.EqualTo("No"));
                Assert.That(options.Smooth, Is.EqualTo(18));
                Assert.That(options.Smooth2, Is.EqualTo(28));
                Assert.That(options.ExtraSpecialProcessingOption, Is.EqualTo(false));
                Assert.That(options.RecurseDirectories, Is.EqualTo(false));
            });

            if (options.RecurseDirectories)
            {
                Assert.That(options.MaxLevelsToRecurse, Is.EqualTo(LEVELS_TO_RECURSE));
            }

            var parser = new CommandLineParser<OkayKey2>(options, "PRISMTest");
            var result = parser.ParseArgs(args.ToArray());
            var parsedOptions = result.ParsedResults;

            Console.WriteLine();
            Console.WriteLine("Class values after re-reading the parameter file");
            parsedOptions.ShowProcessingOptions();

            Assert.Multiple(() =>
            {
                Assert.That(parsedOptions.Verbose, Is.EqualTo("No"));
                Assert.That(parsedOptions.Smooth, Is.EqualTo(6));
                Assert.That(parsedOptions.Smooth2, Is.EqualTo(28));
                Assert.That(options.ExtraSpecialProcessingOption, Is.EqualTo(true));
                Assert.That(options.RecurseDirectories, Is.EqualTo(includeSecondaryArgs));

                if (options.RecurseDirectories)
                {
                    Assert.That(options.MaxLevelsToRecurse, Is.EqualTo(LEVELS_TO_RECURSE));
                }
            });

            Console.WriteLine();
            Console.WriteLine("Class description of parsed results:");
            Console.WriteLine(result);
        }

        [Test]
        public void TestUpdateHelpText()
        {
            var exeName = "Test.exe";

            var parser = new CommandLineParser<OkayKey2>()
            {
                ProgramInfo = "This program does some work",

                ContactInfo = "Program written by an actual human",

                UsageExamples = {
                    exeName + " InputFile.txt",
                    exeName + " InputFile.txt /Start:2",
                    exeName + " InputFile.txt /Start:2 /EnumTypeMode:2 /Smooth:7"
                }
            };

            parser.PrintHelp();

            parser.UpdatePropertyHelpText("Smooth", "##", "15");
            parser.PrintHelp();

            parser.UpdatePropertyHelpText("Smooth", "New help text");
            parser.PrintHelp();
        }

        private class ArgsVariety
        {
            // ReSharper disable once CommentTypo
            // Note that two of these public properties use lowercase minmax to let us confirm
            // that arguments with a different casing (/MinMaxInt or /MinMaxDbl) successfully match the properties

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

            [Option("i", ArgPosition = 1)]
            public string InputFilePath { get; set; }

            [Option("1")]
            public bool NumericArg { get; set; }

            [Option("o", ArgPosition = 2)]
            public string OutputFilePath { get; set; }

            [Option("over")]
            public string Overrides { get; set; }

            [Option("strArray")]
            public string[] StringArray { get; set; }

            [Option("intArray")]
            public int[] IntArray { get; set; }

            [Option("dblArray")]
            public double[] DblArray { get; set; }
        }

        private class ArgsArray
        {
            // ReSharper disable once CommentTypo
            // Note that two of these public properties use lowercase minmax to let us confirm
            // that arguments with a different casing (/MinMaxInt or /MinMaxDbl) successfully match the properties

            [Option("minInt", Min = 10)]
            public int IntMinOnly { get; set; }

            [Option("maxInt", Max = 10)]
            public int IntMaxOnly { get; set; }

            [Option("minmaxInt", Min = -5, Max = 5)]
            public int IntMinMax { get; set; }

            [Option("minDbl", Min = 10)]
            public double DblMinOnly { get; set; }

            [Option("maxDbl", Max = 10)]
            public double DblMaxOnly { get; set; }

            [Option("minmaxDbl", Min = -5, Max = 5)]
            public double DblMinMax { get; set; }

            [Option("g")]
            public string LowerChar { get; set; }

            [Option("G")]
            public string UpperChar { get; set; }

            [Option("b1")]
            public bool BoolCheck { get; set; }

            [Option("strArray")]
            public string[] StringArray { get; set; }

            [Option("intArray")]
            public int[] IntArray { get; set; }

            [Option("dblArray")]
            public double[] DblArray { get; set; }
        }

        private class ArgsPositionalOnly
        {
            [Option("i", ArgPosition = 1)]
            public string InputFilePath { get; set; }

            [Option("o", ArgPosition = 2)]
            public string OutputFilePath { get; set; }
        }

        [Test]
        public void TestArgExistsPropertyFail1()
        {
            var parser = new CommandLineParser<ArgExistsPropertyFail1>();
            var result = parser.ParseArgs(new[] { "-L", "bad" }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail with empty or whitespace ArgExistsProperty");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.Contains(nameof(OptionAttribute.ArgExistsProperty)) && x.Message.Contains("null") && x.Message.Contains("boolean")), Is.True,
                $"Error message does not contain \"{nameof(OptionAttribute.ArgExistsProperty)}\", \"null\", and \"boolean\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestArgExistsPropertyFail2()
        {
            var parser = new CommandLineParser<ArgExistsPropertyFail2>();
            var result = parser.ParseArgs(new[] { "-L", "bad" }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail with an ArgExistsProperty non-existent property");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.Contains(nameof(OptionAttribute.ArgExistsProperty)) && x.Message.Contains("not exist") && x.Message.Contains("not a boolean")), Is.True,
                $"Error message does not contain \"{nameof(OptionAttribute.ArgExistsProperty)}\", \"not exist\", and \"not a boolean\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestArgExistsPropertyFail3()
        {
            var parser = new CommandLineParser<ArgExistsPropertyFail3>();
            var result = parser.ParseArgs(new[] { "-L", "bad" }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail with an ArgExistsProperty non-boolean property");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.Contains(nameof(OptionAttribute.ArgExistsProperty)) && x.Message.Contains("not exist") && x.Message.Contains("not a boolean")), Is.True,
                $"Error message does not contain \"{nameof(OptionAttribute.ArgExistsProperty)}\", \"not exist\", and \"not a boolean\"");

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        public void TestArgExistsPropertyGood1()
        {
            var parser = new CommandLineParser<ArgExistsPropertyGood>();
            var result = parser.ParseArgs(new[] { "-L" }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.True, "Parser failed to process with valid and specified ArgExistsProperty");
            var defaults = new ArgExistsPropertyGood();

            var options = result.ParsedResults;

            Console.WriteLine("{0,-15} {1}", "LogEnabled:", options.LogEnabled);
            Console.WriteLine("{0,-15} {1}", "LogFilePath:", options.LogFilePath);

            Assert.Multiple(() =>
            {
                Assert.That(options.LogEnabled, Is.EqualTo(true), "LogEnabled should be true!!");
                Assert.That(options.LogFilePath, Is.EqualTo(defaults.LogFilePath), "LogFilePath should match the default value!!");
            });
        }

        [Test]
        public void TestArgExistsPropertyGood2()
        {
            var logFileName = "myLogFile.txt";
            var parser = new CommandLineParser<ArgExistsPropertyGood>();
            var result = parser.ParseArgs(new[] { "-L", logFileName }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.True, "Parser failed to process with valid and specified ArgExistsProperty");

            var options = result.ParsedResults;

            Console.WriteLine("{0,-15} {1}", "LogEnabled:", options.LogEnabled);
            Console.WriteLine("{0,-15} {1}", "LogFilePath:", options.LogFilePath);

            Assert.Multiple(() =>
            {
                Assert.That(options.LogEnabled, Is.EqualTo(true), "LogEnabled should be true!!");
                Assert.That(options.LogFilePath, Is.EqualTo(logFileName), "LogFilePath should match the provided value!!");
            });
        }

        private class ArgExistsPropertyFail1
        {
            public bool LogEnabled { get; set; }

            [Option("log", "L", HelpText = "If specified, write to a log file. Can optionally provide a log file path", ArgExistsProperty = " ")]
            public string LogFilePath { get; set; } = "log.txt";
        }

        private class ArgExistsPropertyFail2
        {
            public bool LogEnabled { get; set; }

            [Option("log", "L", HelpText = "If specified, write to a log file. Can optionally provide a log file path", ArgExistsProperty = "LogEnabled1")]
            public string LogFilePath { get; set; } = "log.txt";
        }

        private class ArgExistsPropertyFail3
        {
            [Option("log", "L", HelpText = "If specified, write to a log file. Can optionally provide a log file path", ArgExistsProperty = "LogEnabled1")]
            public string LogFilePath { get; set; } = "log.txt";
        }

        private class ArgExistsPropertyGood
        {
            public bool LogEnabled { get; set; }

            [Option("log", "L", HelpText = "If specified, write to a log file. Can optionally provide a log file path", ArgExistsProperty = nameof(LogEnabled))]
            public string LogFilePath { get; set; } = "log.txt";
        }

        [Test]
        [TestCase(@"/I:C:\CTM_Workdir\DatasetName.D /O:C:\CTM_Workdir\QC /MinInt:11 /MaxInt:4")]
        [TestCase(@"/I:C:\CTM_Workdir\DatasetName.D /O:C:\CTM_Workdir\QC /MinInt:12 /MaxInt:5 /MinMaxInt:-2")]
        [TestCase("/MaxInt:7 /MinMaxInt:-3\n")]
        [TestCase("/I:C:\\CTM_Workdir\\DatasetName.D /O:C:\\CTM_Workdir\\QC /MinInt:13 /MaxInt:7 /MinMaxInt:-3\n")]
        [TestCase("/I:C:\\CTM_Workdir\\DatasetName.D /O:C:\\CTM_Workdir\\QC /MinInt:14 /MaxInt:8 /MinMaxInt:-4\r\n")]
        public void TestArgumentListWithLineFeed(string argumentList)
        {
            var args = argumentList.Split(' ');
            var parser = new CommandLineParser<ArgsVariety>();
            var result = parser.ParseArgs(args, showHelpOnError, outputErrors);

            if (result.ParseErrors.Count == 0)
            {
                Console.WriteLine("Arguments successfully parsed");
                Console.WriteLine("Input path:  {0}", result.ParsedResults.InputFilePath);
                Console.WriteLine("Output path: {0}", result.ParsedResults.OutputFilePath);
                Console.WriteLine("IntMinOnly:  {0}", result.ParsedResults.IntMinOnly);
                Console.WriteLine("IntMaxOnly:  {0}", result.ParsedResults.IntMaxOnly);
                Console.WriteLine("IntMinMax:   {0}", result.ParsedResults.IntMinMax);
                return;
            }

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.Fail("One or more errors were found");
        }

        [Test]
        public void TestFileInfoProperty()
        {
            var parser = new CommandLineParser<FileInfoPropertyGood>();
            var result = parser.ParseArgs(new[] { @"-I:..\..\VisibleColors.tsv", "/S" }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.True, "Parser did not succeed");

            var inputFilePath = parser.Results.ParsedResults.InputFile;
            Console.WriteLine("Input file: " + inputFilePath);
        }

        [Test]
        [TestCase(@"C:\My Documents\Test\InputFile.txt")]
        [TestCase(@"""C:\My Documents\Test\InputFile.txt""")]
        [TestCase(@"'C:\My Documents\Test\InputFile.txt'")]
        public void TestFileInfoPropertyRemoveQuotes(string inputFilePath)
        {
            var parser = new CommandLineParser<FileInfoPropertyGood>();
            var result = parser.ParseArgs(new[] { "-I:" + inputFilePath, "/S" }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.True, "Parser did not succeed");

            var parsedInputFilePath = parser.Results.ParsedResults.InputFile;
            Console.WriteLine("Parsed input file path:");
            Console.WriteLine(parsedInputFilePath);

            Assert.That(parsedInputFilePath.IndexOfAny(new[] { '\'', '"' }) < 0, Is.True, "Quotes in the path were not removed");
        }

        [Test]
        [TestCase("This is a comment", 0)]
        [TestCase(@"""This is a comment surrounded by double quotes""", 0)]
        [TestCase("'This is a comment surrounded by single quotes'", 0)]
        [TestCase("This is a comment with a trailing unmatched double quote\"", 1)]
        [TestCase("This is a comment with a trailing unmatched single quote'", 1)]
        [TestCase("\"This is a comment with mismatched quote'", 2)]
        [TestCase("\"'This is a comment with doubled matching quotes'\"", 2)]
        [TestCase("'\"This is a comment with doubled matching quotes\"'", 2)]
        public void TestPropertyRemoveMatchingQuotes(string propertyValue, int expectedQuoteCount)
        {
            var parser = new CommandLineParser<FileInfoPropertyGood>();
            var result = parser.ParseArgs(new[] { "-Comment", propertyValue, "/S" }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.True, "Parser did not succeed");

            var parsedComment = parser.Results.ParsedResults.Comment;
            Console.WriteLine("Comment: " + parsedComment);

            var commentQuoteCount = 0;
            commentQuoteCount += parsedComment.StartsWith("'") ? 1 : 0;
            commentQuoteCount += parsedComment.EndsWith("'") ? 1 : 0;
            commentQuoteCount += parsedComment.StartsWith("\"") ? 1 : 0;
            commentQuoteCount += parsedComment.EndsWith("\"") ? 1 : 0;

            var commentHasMatchingQuotes = parsedComment.StartsWith("'") && parsedComment.EndsWith("'") ||
                                  parsedComment.StartsWith("\"") && parsedComment.EndsWith("\"");

            if (expectedQuoteCount == 0 && commentHasMatchingQuotes)
            {
                Assert.That(commentQuoteCount, Is.EqualTo(expectedQuoteCount), "Matching leading/trailing quotes were not removed from the comment, but they should have been");
            }
            else if (expectedQuoteCount == 0)
            {
                Assert.That(commentQuoteCount, Is.EqualTo(expectedQuoteCount), "The comment has quotes, but it shouldn't be quoted");
            }
            else
            {
                Assert.That(commentQuoteCount, Is.EqualTo(expectedQuoteCount), "The comment does not have the expected number of non-removed quotes");
            }
        }

        [Test]
        public void TestFileInfoPropertyFail()
        {
            var parser = new CommandLineParser<FileInfoPropertyBad>();
            var result = parser.ParseArgs(new[] { "-I", @"..\..\VisibleColors.tsv" }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.False, "Parser did not fail with empty or whitespace ArgExistsProperty");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x =>
                x.Message.Contains(nameof(OptionAttribute.IsInputFilePath)) && x.Message.Contains("must be of type")), Is.True);

            Console.WriteLine("\nThis error message was expected");
        }

        [Test]
        [Category("PNL_Domain")]
        public void TestFileInfoPropertyWithParameterFile()
        {
            var remoteParamFile = new FileInfo(@"\\proto-2\UnitTest_Files\PRISM\ParamFileTests\ExampleParamFile.conf");
            Assert.That(remoteParamFile.DirectoryName, Is.Not.Null, "Could not determine the parent directory of the remote parameter file");

            var parser = new CommandLineParser<FileInfoPropertyGood>();
            var result = parser.ParseArgs(new[] { "-ParamFile:" + remoteParamFile.FullName }, showHelpOnError, outputErrors);
            Assert.That(result.Success, Is.True, "Parser did not succeed");

            var paramFilePath = parser.Results.ParamFilePath;
            var inputFilePath = parser.Results.ParsedResults.InputFile;

            Assert.That(inputFilePath, Is.Not.Null, "InputFile parameter of the parsed results is null");

            var inputFilePathExpected = Path.Combine(remoteParamFile.DirectoryName, Path.GetFileName(inputFilePath));

            Console.WriteLine("Parameter file path: " + paramFilePath);
            Console.WriteLine("Input file path: " + inputFilePath);

            Assert.Multiple(() =>
            {
                Assert.That(paramFilePath, Is.EqualTo(remoteParamFile.FullName));
                Assert.That(inputFilePath, Is.EqualTo(inputFilePathExpected));
            });
        }

        [Test]
        [Category("PNL_Domain")]
        public void TestFileInfoPropertyWithParameterFileParseMethod()
        {
            var remoteParamFile = new FileInfo(@"\\proto-2\UnitTest_Files\PRISM\ParamFileTests\ExampleParamFile.conf");
            Assert.That(remoteParamFile.DirectoryName, Is.Not.Null, "Could not determine the parent directory of the remote parameter file");

            var parameterClass = new FileInfoPropertyGood();
            var parser = new CommandLineParser<FileInfoPropertyGood>(parameterClass);
            var result = parser.ParseParamFile(remoteParamFile.FullName);
            Assert.That(result.Success, Is.True, "Parser did not succeed");

            var paramFilePath = parser.Results.ParamFilePath;
            var inputFilePath = parser.Results.ParsedResults.InputFile;

            Assert.That(inputFilePath, Is.Not.Null, "InputFile parameter of the parsed results is null");

            var inputFilePathExpected = Path.Combine(remoteParamFile.DirectoryName, Path.GetFileName(inputFilePath));

            Console.WriteLine("Parameter file path: " + paramFilePath);
            Console.WriteLine("Input file path: " + inputFilePath);

            Assert.Multiple(() =>
            {
                Assert.That(paramFilePath, Is.EqualTo(remoteParamFile.FullName));
                Assert.That(inputFilePath, Is.EqualTo(inputFilePathExpected));
            });
        }

        [Test]
        public void TestMissingParameterFile()
        {
            var parser = new CommandLineParser<FileInfoPropertyGood>();
            var result = parser.ParseArgs(new[] { "-ParamFile", @"..\MissingFile.conf" }, showHelpOnError, outputErrors);

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False, "Parser did not fail with parameter file not found error");
                Assert.That(result.ParseErrors.Any(x => x.Message.Contains("parameter file was not found")), Is.True);
            });

            Console.WriteLine("\nThis error message was expected");
        }

        private class FileInfoPropertyGood
        {
            [Option("I", "InputFile", HelpText = "Input file path", IsInputFilePath = true)]
            public string InputFile { get; set; }

            [Option("O", "OutputFile", HelpText = "Output file path")]
            public string OutputFile { get; set; }

            [Option("S", "Recurse", HelpText = "Search in subdirectories")]
            public bool Recurse { get; set; }

            [Option("DebugMode", HelpText = "Enable debug mode")]
            public bool Debug { get; set; }

            [Option("Smooth", HelpText = "Number of points to smooth")]
            public int PointsToSmooth { get; set; }

            [Option("Comment", HelpText = "Optional comment")]
            public string Comment { get; set; }
        }

        private class FileInfoPropertyBad
        {
            [Option("S", HelpText = "Search in subdirectories", IsInputFilePath = true)]
            public bool Recurse { get; set; }
        }

        [Test]
        public void TestUnknownArgumentName()
        {
            var parser = new CommandLineParser<OkayKey2>();
            var result = parser.ParseArgs(
                new[] { "-verbose:\"Lots of text for this argument\"", "-smooth:25", "-smooth2:50", "-MaxValue=32" },
                showHelpOnError, outputErrors);

            Assert.That(result.Success, Is.False, "Parser did not fail with unrecognized argument name error");

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors.Any(x => x.Message.Contains("Unrecognized argument name")), Is.True);

            Console.WriteLine("\nThis error message was expected");
        }

        private interface ITestInherit
        {
            [Option("int", HelpText = "Test inherit int")]
            int InheritInt { get; set; }
            [Option("bool", HelpText = "Test inherit bool")]
            bool InheritBool { get; set; }
        }

        private abstract class TestInheritBase : ITestInherit
        {
            public int InheritInt { get; set; }
            public bool InheritBool { get; set; }

            [Option("double", HelpText = "Base class double")]
            public double BaseClassDouble { get; set; }
        }

        private class TestInherit : TestInheritBase
        {
            [Option("string", HelpText = "Not inherited string")]
            public string NotInheritedString { get; set; }
        }

        [Test]
        public void TestHelpInherited()
        {
            var parser = new CommandLineParser<TestInherit>();
            var cOut = Console.Out;
            //var cError = Console.Error;
            var sw = new StringWriter();
            Console.SetOut(sw);
            //Console.SetError(sw);
            var result = parser.ParseArgs(new[] { "--help" }, showHelpOnError, outputErrors);
            Console.SetOut(cOut);
            //Console.SetError(cError);
            var output = sw.ToString();
            Assert.That(result.Success, Is.False, "Parser did not \"fail\" when user requested the help screen");
            Console.WriteLine(output);
            Assert.Multiple(() =>
            {
                Assert.That(output, Does.Contain("-int"));
                Assert.That(output, Does.Contain("-bool"));
                Assert.That(output, Does.Contain("-double"));
                Assert.That(output, Does.Contain("-string"));
            });

            foreach (var message in result.ParseErrors)
            {
                Console.WriteLine(message);
            }

            Assert.That(result.ParseErrors, Is.Empty, "Error list not empty");
        }

        [Test]
        public void TestInherited()
        {
            var parser = new CommandLineParser<TestInherit>();
            var result = parser.ParseArgs(
                new[] { "-string:\"test string\"", "-int:17", "-bool", "-double=3.1415" },
                showHelpOnError, outputErrors);

            Assert.That(result.Success, Is.True, "Parser did not succeed");

            var options = result.ParsedResults;

            Console.WriteLine("{0,-15} {1}", "int:", options.InheritInt);
            Console.WriteLine("{0,-15} {1}", "bool:", options.InheritBool);
            Console.WriteLine("{0,-15} {1}", "double:", options.BaseClassDouble);
            Console.WriteLine("{0,-15} {1}", "string:", options.NotInheritedString);

            Assert.Multiple(() =>
            {
                Assert.That(options.InheritInt, Is.EqualTo(17), "Should be '17'");
                Assert.That(options.InheritBool, Is.EqualTo(true), "Should be 'True'");
                Assert.That(options.BaseClassDouble, Is.EqualTo(3.1415), "Should be '3.1415'");
                Assert.That(options.NotInheritedString, Is.EqualTo("test string"), "Should be 'test string'");
            });
        }

        private class TestParamFileKeyPreference
        {
            [Option("int1", "+FirstParameter")]
            public int Param1 { get; set; }

            [Option("int2", "+SecondParameter", "+ignoredSecond")]
            public int Param2 { get; set; }

            [Option("int3", "ThirdParameter")]
            public int Param3 { get; set; }
        }

        [Test]
        public void TestParamFileOutputKeyPreference()
        {
            var parser = new CommandLineParser<TestParamFileKeyPreference>();
            var args = new List<string> { "-CreateParamFile" };

            var result = parser.ParseArgs(args.ToArray());

            Console.WriteLine();
            Console.WriteLine("Class description of parsed results:");
            Console.WriteLine(result);
        }

        [Test]
        public void TestParamFileOutputKeyPreferenceHelp()
        {
            var parser = new CommandLineParser<TestParamFileKeyPreference>();

            // ReSharper disable once CollectionNeverUpdated.Local
            var args = new List<string>();

            var result = parser.ParseArgs(args.ToArray());

            Console.WriteLine();
            Console.WriteLine("Class description of parsed results:");
            Console.WriteLine(result);
        }
    }
}

using System;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    internal class ConsoleMsgUtilsTest
    {
        private const string TEXT_TO_WRAP1 =
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Quisque suscipit cursus nunc ut placerat. " +
            "Orci varius natoque penatibus et magnis dis parturient montes, nascetur ridiculus mus. Donec eu " +
            "lobortis nisl, at feugiat nisi. Sed quis pharetra lectus. Curabitur cursus consectetur mauris a " +
            "tincidunt. Curabitur sagittis metus et ante elementum, in bibendum mi molestie. Aliquam congue, " +
            "felis in luctus mattis, ex elit consequat neque, sit amet aliquet ex augue sit amet lorem. " +
            "Phasellus aliquam feugiat convallis.";

        private const string TEXT_TO_WRAP2 =
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Suspendisse in consectetur risus. Interdum " +
            "et malesuada fames ac ante ipsum primis in faucibus. Etiam eu euismod eros, eget pulvinar dolor. " +
            "Proin varius, nisl at maximus maximus, arcu augue porta ante, a posuere massa turpis eu mi. " +
            "Nunc vitae placerat libero";

        /// <summary>
        /// This paragraph includes 'alert' characters ('\a') which indicate a non-breaking space
        /// </summary>
        private const string TEXT_TO_WRAP3 =
            "Lorem ipsum dolor sit amet, consectetur New\aYork\aCity. Suspendisse in consectetur risus. Interdum " +
            "et malesuada Statue\aOf\aLiberty\aNational\aMonument";

        [TestCase(2)]
        [TestCase(5)]
        [TestCase(15)]
        public void TestSleep(int waitTimeSeconds)
        {
            var startTime = DateTime.UtcNow;
            Console.WriteLine("Sleeping for {0} seconds", waitTimeSeconds);

            ConsoleMsgUtils.SleepSeconds(waitTimeSeconds);

            var elapsedSeconds = DateTime.UtcNow.Subtract(startTime).TotalSeconds;
            Console.WriteLine("Done after {0:F3} seconds", elapsedSeconds);

            Assert.That(Math.Abs(elapsedSeconds - waitTimeSeconds), Is.EqualTo(0).Within(0.1), "Did not sleep as long as expected");
        }

        [TestCase(TEXT_TO_WRAP1, 40, 0, 14, 514)]
        [TestCase(TEXT_TO_WRAP1, 60, 0, 9, 514)]
        [TestCase(TEXT_TO_WRAP1, 60, 8, 11, 602)]
        [TestCase(TEXT_TO_WRAP1, 60, 30, 19, 1084)]
        [TestCase(TEXT_TO_WRAP3, 40, 0, 4, 146)]
        [TestCase(TEXT_TO_WRAP3, 60, 0, 3, 146)]
        [TestCase(TEXT_TO_WRAP3, 60, 8, 4, 178)]
        [TestCase(TEXT_TO_WRAP3, 60, 30, 5, 296)]
        public void TestWrapParagraph(string textToWrap, int wrapWidth, int spaceIndentCount, int expectedLineCount, int expectedCharacterCount)
        {
            if (spaceIndentCount > 0)
            {
                // Indent the wrapped lines by the given amount
                textToWrap = new string(' ', spaceIndentCount) + textToWrap;
            }

            var wrappedText = ConsoleMsgUtils.WrapParagraph(textToWrap, wrapWidth);
            Console.WriteLine(wrappedText);

            var wrappedLines = wrappedText.Split('\n');

            var charCount = 0;
            var lineCount = 0;

            foreach (var textLine in wrappedLines)
            {
                charCount += textLine.Length;
                lineCount++;
            }

            Console.WriteLine();
            Console.WriteLine("Wrapping to {0} characters per line gives {1} lines of wrapped text and {2} total characters", wrapWidth, lineCount, charCount);

            if (spaceIndentCount > 0)
            {
                Console.WriteLine("Indented text by {0} characters", spaceIndentCount);
            }

            if (expectedLineCount > 0)
            {
                Assert.That(lineCount, Is.EqualTo(expectedLineCount),
                            $"Text wrapped to {lineCount} lines instead of {expectedLineCount} lines");
            }
            else
            {
                Console.WriteLine("Skipped line count validation");
            }

            if (expectedCharacterCount > 0)
            {
                Assert.That(charCount, Is.EqualTo(expectedCharacterCount),
                            $"Wrapped text has {charCount} characters instead of {expectedCharacterCount} characters");
            }
            else
            {
                Console.WriteLine("Skipped character count validation");
            }
        }

        [TestCase(TEXT_TO_WRAP1, 40, 0, 14, 501)]
        [TestCase(TEXT_TO_WRAP1, 60, 0, 9, 506)]
        [TestCase(TEXT_TO_WRAP1, 60, 8, 11, 592)]
        [TestCase(TEXT_TO_WRAP1, 80, 0, 7, 508)]
        [TestCase(TEXT_TO_WRAP1, 95, 0, 6, 509)]
        [TestCase(TEXT_TO_WRAP1, 120, 0, 5, 510)]
        [TestCase(TEXT_TO_WRAP2, 40, 0, 9, 307)]
        [TestCase(TEXT_TO_WRAP2, 40, 8, 11, 393)]
        [TestCase(TEXT_TO_WRAP2, 60, 0, 6, 310)]
        [TestCase(TEXT_TO_WRAP2, 60, 30, 11, 635)]
        [TestCase(TEXT_TO_WRAP2, 80, 0, 5, 311)]
        [TestCase(TEXT_TO_WRAP2, 95, 0, 4, 312)]
        [TestCase(TEXT_TO_WRAP2, 120, 0, 3, 313)]
        [TestCase(TEXT_TO_WRAP3, 70, 30, 4, 263)]
        [TestCase(TEXT_TO_WRAP3, 65, 30, 5, 292)]
        [TestCase(TEXT_TO_WRAP3, 60, 30, 5, 292)]
        [TestCase(TEXT_TO_WRAP3, 55, 30, 6, 321)]
        [TestCase(TEXT_TO_WRAP3, 50, 30, 7, 350)]
        [TestCase(TEXT_TO_WRAP3, 45, 30, 9, 408)]
        [TestCase(TEXT_TO_WRAP3, 40, 30, 12, 495)]
        public void TestWrapParagraphAsList(string textToWrap, int wrapWidth, int spaceIndentCount, int expectedLineCount, int expectedCharacterCount)
        {
            if (spaceIndentCount > 0)
            {
                // Indent the wrapped lines by the given amount
                textToWrap = new string(' ', spaceIndentCount) + textToWrap;
            }

            var wrappedText = ConsoleMsgUtils.WrapParagraphAsList(textToWrap, wrapWidth);
            var charCount = 0;

            foreach (var textLine in wrappedText)
            {
                Console.WriteLine(textLine);
                charCount += textLine.Length;
            }

            Console.WriteLine();
            Console.WriteLine("Wrapping to {0} characters per line gives {1} lines of wrapped text and {2} total characters", wrapWidth, wrappedText.Count, charCount);

            if (spaceIndentCount > 0)
            {
                Console.WriteLine("Indented text by {0} characters", spaceIndentCount);
            }

            if (expectedLineCount > 0)
            {
                Assert.That(wrappedText.Count, Is.EqualTo(expectedLineCount),
                            $"Text wrapped to {wrappedText.Count} lines instead of {expectedLineCount} lines");
            }
            else
            {
                Console.WriteLine("Skipped line count validation");
            }

            if (expectedCharacterCount > 0)
            {
                Assert.That(charCount, Is.EqualTo(expectedCharacterCount),
                            $"Wrapped text has {charCount} characters instead of {expectedCharacterCount} characters");
            }
            else
            {
                Console.WriteLine("Skipped character count validation");
            }
        }
    }
}

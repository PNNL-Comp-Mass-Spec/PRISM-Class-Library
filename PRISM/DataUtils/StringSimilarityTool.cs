using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace PRISM.DataUtils
{
    // ReSharper disable once CommentTypo

    /// <summary>
    /// This class implements a string comparison algorithm based on character pair similarity
    /// Original algorithm developed by Simon White in 1992: http://www.catalysoft.com/articles/StrikeAMatch.html
    /// Converted to C# by Michael La Voie in 2009: https://stackoverflow.com/a/1663745/1179467
    /// Expanded by Matthew Monroe in 2019 to support single letter words (or digits)
    /// </summary>
    public static class StringSimilarityTool
    {
        private static readonly Regex mAlphaNumericMatcher = new("[a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex mLetterMatcher = new("[a-z]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex mLetterWhitespaceMatcher = new("[a-z \t\r\n]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Compares two strings based on letter pair matches
        /// </summary>
        /// <param name="text1"></param>
        /// <param name="text2"></param>
        /// <param name="removeNumbers">When true, remove digits from the text before comparing</param>
        /// <param name="removeSymbolsAndWhitespace">When true, remove symbols (anything not a letter or number) and whitespace from the text before comparing</param>
        /// <param name="caseSensitive">When true, require matching capitalization</param>
        /// <returns>Similarity score, ranging from 0.0 to 1.0 where 1.0 is a perfect match</returns>
        public static double CompareStrings(
            string text1,
            string text2,
            bool removeNumbers = false,
            bool removeSymbolsAndWhitespace = true,
            bool caseSensitive = false)
        {
            var pairs1 = WordLetterPairs(text1, removeNumbers, removeSymbolsAndWhitespace, caseSensitive);
            var pairs2 = WordLetterPairs(text2, removeNumbers, removeSymbolsAndWhitespace, caseSensitive);

            var intersection = 0;
            var union = pairs1.Count + pairs2.Count;

            if (union <= 0)
                return 0;

            foreach (var pair in pairs1)
            {
                for (var j = 0; j < pairs2.Count; j++)
                {
                    if (pair != pairs2[j])
                        continue;

                    intersection++;

                    // ReSharper disable CommentTypo

                    // Remove the match to prevent "GGGG" from appearing to match "GG" with 100% success
                    pairs2.RemoveAt(j);

                    // ReSharper restore CommentTypo

                    break;
                }
            }

            return 2.0 * intersection / union;
        }

        /// <summary>
        /// Concatenate all RegEx matches in text
        /// </summary>
        /// <param name="matcher"></param>
        /// <param name="text"></param>
        private static string CombineAllMatches(Regex matcher, string text)
        {
            var filteredText = new StringBuilder();

            var match = matcher.Match(text);

            while (match.Success)
            {
                filteredText.Append(match.Value);
                match = match.NextMatch();
            }

            return filteredText.ToString();
        }

        /// <summary>
        /// Generates an array containing every two consecutive letters in the input string
        /// </summary>
        /// <param name="text"></param>
        /// <returns>List of pairs</returns>
        /// <remarks>If the text is a single character, returns an array of length 1 with that single character</remarks>
        private static IEnumerable<string> LetterPairs(string text)
        {
            if (text.Length < 1)
                return Array.Empty<string>();

            if (text.Length == 1)
            {
                var singleChar = new string[1];
                singleChar[0] = text;
                return singleChar;
            }

            var numPairs = text.Length - 1;

            var pairs = new string[numPairs];

            for (var i = 0; i < numPairs; i++)
            {
                pairs[i] = text.Substring(i, 2);
            }

            return pairs;
        }

        /// <summary>
        /// Gets all letter pairs for each individual word in the string
        /// </summary>
        /// <param name="textBob"></param>
        /// <param name="removeNumbers">When true, remove digits from the text before comparing</param>
        /// <param name="removeSymbolsAndWhitespace">When true, remove symbols (anything not a letter or number) and whitespace from the text before comparing</param>
        /// <param name="caseSensitive">When true, require matching capitalization</param>
        /// <returns>List of word letter pairs</returns>
        private static List<string> WordLetterPairs(string textBob, bool removeNumbers = false, bool removeSymbolsAndWhitespace = true, bool caseSensitive = false)
        {
            var allPairs = new List<string>();

            var textToCheck = caseSensitive ? textBob : textBob.ToUpper();
            string filteredText;

            if (removeSymbolsAndWhitespace)
            {
                var alphanumericText = CombineAllMatches(mAlphaNumericMatcher, textToCheck);

                if (removeNumbers)
                {
                    filteredText = CombineAllMatches(mLetterMatcher, alphanumericText);
                }
                else
                {
                    filteredText = alphanumericText;
                }
            }
            else if (removeNumbers)
            {
                filteredText = CombineAllMatches(mLetterWhitespaceMatcher, textToCheck);
            }
            else
            {
                filteredText = textToCheck;
            }

            // Tokenize the string and put the tokens/words into an array
            var words = Regex.Split(filteredText ?? string.Empty, @"\s");

            foreach (var word in words)
            {
                if (string.IsNullOrEmpty(word))
                    continue;

                // Find the pairs of characters
                var pairsInWord = LetterPairs(word);

                allPairs.AddRange(pairsInWord);
            }

            return allPairs;
        }
    }
}

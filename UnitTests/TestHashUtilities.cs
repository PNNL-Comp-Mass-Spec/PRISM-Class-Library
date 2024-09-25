using System;
using System.IO;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    internal class TestHashUtilities
    {
        // Ignore Spelling: CRC, SHA

        private const string HASH_TEST_TEMP_DIR_PATH = @"C:\Temp\HashTest";

        private const string HASH_TEST_FILE_NAME = "HashTestFile.txt";

        private string HashTestFilePath => Path.Combine(HASH_TEST_TEMP_DIR_PATH, HASH_TEST_FILE_NAME);

        [OneTimeSetUp]
        public void Setup()
        {
            var sourceDirectory = new DirectoryInfo(HASH_TEST_TEMP_DIR_PATH);

            if (!sourceDirectory.Exists)
            {
                try
                {
                    // Create the missing directory
                    sourceDirectory.Create();
                }
                catch (Exception ex)
                {
                    Assert.Fail("Error creating test directory: " + sourceDirectory + ": " + ex.Message);
                }
            }

            var hashTestFile = new FileInfo(HashTestFilePath);

            if (hashTestFile.Exists && hashTestFile.Length > 1000000)
                return;

            var rand = new Random(314);

            using var testFile = new StreamWriter(new FileStream(hashTestFile.FullName, FileMode.Create, FileAccess.Write));

            testFile.WriteLine("X\tY");
            for (var i = 1; i <= 10000 * i; i++)
            {
                testFile.WriteLine("{0}\t{1}", i, rand.Next(0, 1000));
            }
        }

        [TestCase("6478B34B")]
        public void TestComputeFileHashCrc32(string expectedHash)
        {
            var fileHash = HashUtilities.ComputeFileHashCrc32(HashTestFilePath);
            Console.WriteLine("CRC32 hash for {0} is {1}", Path.GetFileName(HashTestFilePath), fileHash);
            Assert.That(fileHash, Is.EqualTo(expectedHash));
        }

        [TestCase("a", "0cc175b9c0f1b6a831c399e269772661")]
        [TestCase("Shakespeare", "cf038455252f25be05eccbb85a1e2dee")]
        [TestCase("The quick brown fox jumped over the lazy dog", "08a008a01d498c404b0c30852b39d3b8")]
        public void TestComputeStringHashMD5(string text, string expectedHash)
        {
            var md5 = HashUtilities.ComputeStringHashMD5(text);
            Console.WriteLine("MD5 hash for '{0}' is {1}", text, md5);
            Assert.That(md5, Is.EqualTo(expectedHash));
        }

        [TestCase("a", "0cc175b9c0f1b6a831c399e269772661", "DMF1ucDxtqgxw5niaXcmYQ==")]
        [TestCase("z", "fbade9e36a3f36d3d676c1b808451dd7", "+63p42o/NtPWdsG4CEUd1w==")]
        [TestCase("5", "e4da3b7fbbce2345d7772b0674a318d5", "5No7f7vOI0XXdysGdKMY1Q==")]
        [TestCase("", "d41d8cd98f00b204e9800998ecf8427e", "1B2M2Y8AsgTpgAmY7PhCfg==")]
        [TestCase("Shakespeare", "cf038455252f25be05eccbb85a1e2dee", "zwOEVSUvJb4F7Mu4Wh4t7g==")]
        [TestCase("The quick brown fox jumped over the lazy dog", "08a008a01d498c404b0c30852b39d3b8", "CKAIoB1JjEBLDDCFKznTuA==")]
        public void TestComputeStringHashMD5WithBase64(string text, string expectedHash, string expectedBase64Hash)
        {
            var md5 = HashUtilities.ComputeStringHashMD5(text, out var base64md5);

            Console.WriteLine("MD5 hash for '{0}' is {1}", text, md5);
            Console.WriteLine("MD5 hash is {0}", base64md5);

            Assert.Multiple(() =>
            {
                Assert.That(md5, Is.EqualTo(expectedHash));
                Assert.That(base64md5, Is.EqualTo(expectedBase64Hash));
            });
        }

        [TestCase("2962ffd5238c526f570a813188ae2aaa")]
        public void TestComputeFileHashMD5(string expectedHash)
        {
            var md5 = HashUtilities.ComputeFileHashMD5(HashTestFilePath);
            Console.WriteLine("MD5 hash for {0} is {1}", Path.GetFileName(HashTestFilePath), md5);
            Assert.That(md5, Is.EqualTo(expectedHash));
        }

        [TestCase("2962ffd5238c526f570a813188ae2aaa", "KWL/1SOMUm9XCoExiK4qqg==")]
        public void TestComputeFileHashMD5WithBase64(string expectedHash, string expectedBase64Hash)
        {
            var md5 = HashUtilities.ComputeFileHashMD5(HashTestFilePath, out var base64md5);

            Console.WriteLine("MD5 hash for {0} is {1}", Path.GetFileName(HashTestFilePath), md5);
            Console.WriteLine("Base64 MD5 hash is {0}", base64md5);

            Assert.Multiple(() =>
            {
                Assert.That(md5, Is.EqualTo(expectedHash));
                Assert.That(base64md5, Is.EqualTo(expectedBase64Hash));
            });
        }

        [TestCase(HashUtilities.HashTypeConstants.CRC32, "6478B34B")]
        [TestCase(HashUtilities.HashTypeConstants.MD5, "2962ffd5238c526f570a813188ae2aaa")]
        [TestCase(HashUtilities.HashTypeConstants.SHA1, "b773deac169e43c90e1a13d0b5bb4a0efcbd153b")]
        [TestCase(HashUtilities.HashTypeConstants.MD5Base64, "KWL/1SOMUm9XCoExiK4qqg==")]
        public void TestComputeFileHash(HashUtilities.HashTypeConstants hashType, string expectedHash)
        {
            var fileHash = HashUtilities.ComputeFileHash(HashTestFilePath, hashType);
            Console.WriteLine("{0} hash for {1} is {2}", hashType.ToString(), Path.GetFileName(HashTestFilePath), fileHash);
            Assert.That(fileHash, Is.EqualTo(expectedHash));
        }

        [TestCase("b773deac169e43c90e1a13d0b5bb4a0efcbd153b")]
        public void TestComputeFileHashSha1(string expectedHash)
        {
            var sha1 = HashUtilities.ComputeFileHashSha1(HashTestFilePath);
            Console.WriteLine("SHA-1 hash for {0} is {1}", Path.GetFileName(HashTestFilePath), sha1);
            Assert.That(sha1, Is.EqualTo(expectedHash));
        }

        [TestCase("a", "86f7e437faa5a7fce15d1ddcb9eaeaea377667b8")]
        [TestCase("z", "395df8f7c51f007019cb30201c49e884b46b92fa")]
        [TestCase("5", "ac3478d69a3c81fa62e60f5c3696165a4e5e6ac4")]
        [TestCase("", "da39a3ee5e6b4b0d3255bfef95601890afd80709")]
        [TestCase("Shakespeare", "48755adafe813d2835a19ada3f2985524dfd5e19")]
        [TestCase("The quick brown fox jumped over the lazy dog", "f6513640f3045e9768b239785625caa6a2588842")]
        public void TestComputeStringHashSha1(string text, string expectedHash)
        {
            var sha1 = HashUtilities.ComputeStringHashSha1(text);
            Console.WriteLine("SHA-1 hash for '{0}' is {1}", text, sha1);
            Assert.That(sha1, Is.EqualTo(expectedHash));
        }
    }
}

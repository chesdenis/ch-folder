using System.IO;
using System.Text;
using System.Threading.Tasks;
using shared_csharp;
using Xunit;

namespace shared_csharp_tests
{
    public class FileCalculationExtensionsTests
    {
        [Fact]
        public void Md5PrefixRegex_Should_Match_32_Hex_Digits_Only()
        {
            Assert.Matches(FileCalculationExtensions.Md5PrefixRegex, "d41d8cd98f00b204e9800998ecf8427e"); // lower
            Assert.Matches(FileCalculationExtensions.Md5PrefixRegex, "D41D8CD98F00B204E9800998ECF8427E"); // upper
            Assert.DoesNotMatch(FileCalculationExtensions.Md5PrefixRegex, "xyz");
            Assert.DoesNotMatch(FileCalculationExtensions.Md5PrefixRegex, "123"); // too short
            Assert.DoesNotMatch(FileCalculationExtensions.Md5PrefixRegex, "g41d8cd98f00b204e9800998ecf8427e"); // non-hex
            Assert.DoesNotMatch(FileCalculationExtensions.Md5PrefixRegex, "d41d8cd98f00b204e9800998ecf8427ex"); // extra char
        }

        [Fact]
        public async Task CalculateMd5Async_Should_Return_Known_Hash_For_Empty_File()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                // MD5("") = d41d8cd98f00b204e9800998ecf8427e
                var expected = "d41d8cd98f00b204e9800998ecf8427e";

                // Act
                var actual = await tempFile.CalculateMd5Async();

                // Assert
                Assert.Equal(expected, actual);
                Assert.Matches(FileCalculationExtensions.Md5PrefixRegex, actual);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task CalculateMd5Async_Should_Handle_Small_Text_File()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                // Write "hello" (no newline) => MD5 = 86c56c004b157e0e2b43d9561bada3b5
                await File.WriteAllTextAsync(tempFile, "hello", Encoding.UTF8);
                var expected = "86c56c004b157e0e2b43d9561bada3b5";

                // Act
                var actual = await tempFile.CalculateMd5Async();

                // Assert
                Assert.Equal(expected, actual);
                Assert.Matches(FileCalculationExtensions.Md5PrefixRegex, actual);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task CalculateMd5Async_Should_Produce_Lowercase_Hex()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                await File.WriteAllBytesAsync(tempFile, new byte[] { 0x00, 0xFF, 0x10, 0xAB, 0xCD });
                var actual = await tempFile.CalculateMd5Async();

                // Assert: all hex chars should be lowercase
                Assert.True(actual == actual.ToLowerInvariant(), "MD5 hex string should be lowercase.");
                Assert.Matches(FileCalculationExtensions.Md5PrefixRegex, actual);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
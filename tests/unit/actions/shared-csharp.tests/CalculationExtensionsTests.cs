using System.Text;
using shared_csharp.Extensions;
using Xunit;

namespace shared_csharp_tests
{
    public class CalculationExtensionsTests
    {
        [Fact]
        public void Md5PrefixRegex_Should_Match_32_Hex_Digits_Only()
        {
            Assert.Matches(CalculationExtensions.Md5PrefixRegex, "d41d8cd98f00b204e9800998ecf8427e"); // lower
            Assert.Matches(CalculationExtensions.Md5PrefixRegex, "D41D8CD98F00B204E9800998ECF8427E"); // upper
            Assert.DoesNotMatch(CalculationExtensions.Md5PrefixRegex, "xyz");
            Assert.DoesNotMatch(CalculationExtensions.Md5PrefixRegex, "123"); // too short
            Assert.DoesNotMatch(CalculationExtensions.Md5PrefixRegex, "g41d8cd98f00b204e9800998ecf8427e"); // non-hex
            Assert.DoesNotMatch(CalculationExtensions.Md5PrefixRegex, "d41d8cd98f00b204e9800998ecf8427ex"); // extra char
        }
          
    }
}
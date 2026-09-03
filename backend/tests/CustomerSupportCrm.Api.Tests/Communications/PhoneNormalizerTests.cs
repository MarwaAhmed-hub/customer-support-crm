using CustomerSupportCrm.Domain.Customers;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Communications;

public class PhoneNormalizerTests
{
    [Theory]
    [InlineData("201234500001", "+201234500001")]
    [InlineData("01234500001", "+201234500001")]
    [InlineData("+201234500001", "+201234500001")]
    [InlineData("0020 1234 500001", "+201234500001")]
    [InlineData("+20 123 450 0001", "+201234500001")]
    [InlineData("0123-450-0001", "+201234500001")]
    public void Normalize_reconciles_the_local_and_international_forms_of_the_same_number(string input, string expected)
    {
        Assert.Equal(expected, PhoneNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_of_an_empty_or_non_digit_string_returns_empty()
    {
        Assert.Equal("", PhoneNormalizer.Normalize("   "));
        Assert.Equal("", PhoneNormalizer.Normalize("abc"));
    }
}

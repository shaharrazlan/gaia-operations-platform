using Gaia.Core.Errors;
using Gaia.Core.Formulas;
using Gaia.Core.Operations;

namespace Gaia.Core.Tests.Operations;

public class InputParserTests
{
    [Theory]
    [InlineData("-2.5", -2.5)]
    [InlineData("10", 10)]
    [InlineData(" 7 ", 7)]
    [InlineData("0.125", 0.125)]
    public void R3_AC5_parses_integers_and_decimals_including_negatives(string raw, double expected)
    {
        var (a, _) = InputParser.Parse(InputType.Number, raw, "1");

        Assert.Equal(new NumberValue(expected), a);
    }

    [Theory]
    [InlineData("3,5")]
    [InlineData("1e3")]
    [InlineData("abc")]
    [InlineData("+5")]
    [InlineData("1.")]
    [InlineData("٣")]
    public void R3_AC5_rejects_values_that_are_not_plain_decimal_numbers(string raw)
    {
        var error = Assert.Throws<ValidationException>(() => InputParser.Parse(InputType.Number, "1", raw));

        Assert.Equal(ErrorCodes.Validation, error.Code);
        Assert.Equal("fieldB", error.Field);
    }

    [Fact]
    public void R2_AC4_rejects_latitude_95_naming_field_A()
    {
        var error = Assert.Throws<ValidationException>(() => InputParser.Parse(InputType.Coordinate, "95", "34.78"));

        Assert.Equal("fieldA", error.Field);
        Assert.Contains("latitude", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void R2_AC4_rejects_longitude_minus_181_naming_field_B()
    {
        var error = Assert.Throws<ValidationException>(() => InputParser.Parse(InputType.Coordinate, "32.08", "-181"));

        Assert.Equal("fieldB", error.Field);
        Assert.Contains("longitude", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void R2_AC4_accepts_coordinates_on_the_boundaries()
    {
        var (lat, lon) = InputParser.Parse(InputType.Coordinate, "-90", "180");

        Assert.Equal(new NumberValue(-90), lat);
        Assert.Equal(new NumberValue(180), lon);
    }

    [Fact]
    public void R2_AC4_text_input_accepts_any_string()
    {
        var (a, b) = InputParser.Parse(InputType.Text, "hello world!", "  ");

        Assert.Equal(new TextValue("hello world!"), a);
        Assert.Equal(new TextValue("  "), b);
    }

    [Theory]
    [InlineData(null, "1", "fieldA")]
    [InlineData("", "1", "fieldA")]
    [InlineData("1", null, "fieldB")]
    [InlineData("1", "   ", "fieldB")]
    public void R2_AC3_missing_field_is_a_validation_error_naming_the_field(string? a, string? b, string field)
    {
        var error = Assert.Throws<ValidationException>(() => InputParser.Parse(InputType.Number, a, b));

        Assert.Equal(field, error.Field);
        Assert.Contains("required", error.Message);
    }

    [Fact]
    public void T10_rejects_input_longer_than_200_characters()
    {
        var error = Assert.Throws<ValidationException>(() => InputParser.Parse(InputType.Text, new string('x', 201), "b"));

        Assert.Equal("fieldA", error.Field);
    }

    [Theory]
    [InlineData("4654654656464556556666666616")]   // the case found in user testing (28 digits)
    [InlineData("1234567890123456")]               // 16
    [InlineData("-1234567890123456")]
    [InlineData("0.1234567890123456")]             // 16 after the leading zero
    [InlineData("1234567890.123456")]              // 16 in total
    public void R3_AC6_more_than_15_significant_digits_is_rejected_naming_the_field(string raw)
    {
        var error = Assert.Throws<ValidationException>(() => InputParser.Parse(InputType.Number, raw, "1"));

        Assert.Equal("fieldA", error.Field);
        Assert.Contains("15 significant digits", error.Message);
    }

    [Theory]
    [InlineData("123456789012345", 123456789012345)]
    [InlineData("-123456789012345", -123456789012345)]
    [InlineData("100000000000000000000", 1e20)]          // trailing zeros don't count
    [InlineData("0.000000000000000001", 1e-18)]          // leading zeros don't count
    [InlineData("000123.4500", 123.45)]
    [InlineData("279279279387873000000000000000", 2.79279279387873e29)]   // a result the platform showed
    public void R3_AC6_up_to_15_significant_digits_is_accepted_and_zeros_around_them_dont_count(string raw, double expected)
    {
        var (a, _) = InputParser.Parse(InputType.Number, raw, "1");

        Assert.Equal(new NumberValue(expected), a);
    }

    [Fact]
    public void R3_AC6_coordinates_follow_the_same_digit_limit()
    {
        var error = Assert.Throws<ValidationException>(() => InputParser.Parse(InputType.Coordinate, "32.0812345678901234", "34.78"));

        Assert.Equal("fieldA", error.Field);
    }
}

using System.Globalization;
using System.Text.RegularExpressions;
using Gaia.Core.Errors;
using Gaia.Core.Formulas;

namespace Gaia.Core.Operations;

/// <summary>
/// Turns the two raw text inputs (Field A, Field B) into typed values according to the
/// operation's <see cref="InputType"/>, or throws a <see cref="ValidationException"/> naming the field.
/// </summary>
public static partial class InputParser
{
    public const int MaxLength = 200;

    /// <summary>The precision of a double, and the most digits a result is shown with (ResultFormatter).</summary>
    public const int MaxSignificantDigits = 15;

    public static (Value A, Value B) Parse(InputType type, string? fieldA, string? fieldB) =>
        (ParseOne(type, fieldA, "fieldA", "Field A"), ParseOne(type, fieldB, "fieldB", "Field B"));

    private static Value ParseOne(InputType type, string? raw, string field, string label)
    {
        // Text may be spaces (they can matter in text operations); for numbers, spaces only = missing.
        if (string.IsNullOrEmpty(raw) || (type != InputType.Text && string.IsNullOrWhiteSpace(raw)))
            throw new ValidationException($"{label} is required.", field);
        if (raw.Length > MaxLength)
            throw new ValidationException($"{label} is longer than {MaxLength} characters.", field);

        return type switch
        {
            InputType.Text => new TextValue(raw),
            InputType.Number => new NumberValue(ParseNumber(raw, field, label)),
            InputType.Coordinate => new NumberValue(ParseCoordinate(raw, field, label)),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }

    private static double ParseNumber(string raw, string field, string label)
    {
        var trimmed = raw.Trim();
        // Only plain decimals: optional minus, digits, optional ".digits". No "1e3", "3,5", "+5", "1.".
        if (!PlainDecimal().IsMatch(trimmed))
            throw new ValidationException($"{label} must be a number like 12 or -2.5.", field);

        // Numbers are calculated with ~15 significant digits (double). A longer input would give a result that looks
        // exact but isn't, so refuse it (R3.AC6). Zeros before the first and after the last other digit don't count:
        // "100000000000000000000" and "0.0000001" each have 1. Results are shown with at most 15, so they fit too.
        var significantDigits = trimmed.TrimStart('-').Replace(".", "", StringComparison.Ordinal).Trim('0').Length;
        if (significantDigits > MaxSignificantDigits)
            throw new ValidationException(
                $"{label} has more than {MaxSignificantDigits} significant digits. Calculations are precise to {MaxSignificantDigits} digits, so use at most {MaxSignificantDigits} (e.g. 123456789012345).",
                field);

        var number = double.Parse(trimmed, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        if (!double.IsFinite(number))
            throw new ValidationException($"{label} is too large.", field);
        return number;
    }

    private static double ParseCoordinate(string raw, string field, string label)
    {
        var number = ParseNumber(raw, field, label);
        var (name, limit) = field == "fieldA" ? ("latitude", 90) : ("longitude", 180);
        if (number < -limit || number > limit)
            throw new ValidationException($"{label} ({name}) must be between -{limit} and {limit}.", field);
        return number;
    }

    // [0-9], not \d: in .NET \d also matches non-ASCII digits (e.g. Arabic-Indic), which double.Parse rejects.
    [GeneratedRegex(@"^-?[0-9]+(\.[0-9]+)?$")]
    private static partial Regex PlainDecimal();
}

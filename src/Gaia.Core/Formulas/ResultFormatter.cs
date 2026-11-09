using System.Globalization;

namespace Gaia.Core.Formulas;

/// <summary>Turns a value into the text returned to the caller. Same culture everywhere: "." decimal.</summary>
public static class ResultFormatter
{
    public static string Format(Value value) => value switch
    {
        NumberValue n => FormatNumber(n.Number),
        TextValue t => t.Text,
        BoolValue b => b.Bool ? "true" : "false",
        _ => throw new InvalidOperationException($"Unknown value type {value.GetType().Name}."),
    };

    /// <summary>
    /// Plain decimal notation, never "1E+20", so every numeric result is also a valid numeric input.
    /// At most 15 significant digits hides binary noise (0.1 + 0.2 → "0.3"); at most 15 decimals.
    /// </summary>
    private static string FormatNumber(double number)
    {
        var rounded = double.Parse(number.ToString("G15", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        // "+ 0.0" turns -0 into 0, so "-0" is never shown.
        return (rounded + 0.0).ToString("0.###############", CultureInfo.InvariantCulture);
    }
}

using System.Globalization;

namespace Gaia.Core.Formulas;

/// <summary>Turns a value into the text returned to the caller. Same culture everywhere: "." decimal.</summary>
public static class ResultFormatter
{
    public static string Format(Value value) => value switch
    {
        // G15 = up to 15 significant digits, which hides binary noise: 0.1 + 0.2 → "0.3".
        // "+ 0.0" turns -0 into 0, so -0 is never shown.
        NumberValue n => (n.Number + 0.0).ToString("G15", CultureInfo.InvariantCulture),
        TextValue t => t.Text,
        BoolValue b => b.Bool ? "true" : "false",
        _ => throw new InvalidOperationException($"Unknown value type {value.GetType().Name}."),
    };
}

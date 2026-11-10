namespace Gaia.Core.Formulas;

/// <summary>
/// What each operator needs, worded once, so the save-time <see cref="TypeChecker"/> and the run-time
/// <see cref="Evaluator"/> explain the same mistake with the same message.
/// </summary>
internal static class OperatorRules
{
    /// <summary>Operators that only work on two numbers (+ also joins two texts).</summary>
    public static bool IsArithmetic(string op) => op is "-" or "*" or "/" or "%" or "^";

    public static string Needs(string op) => op switch
    {
        "+" or "<" or "<=" or ">" or ">=" => "two numbers or two texts",
        "==" or "!=" => "two values of the same type",
        _ => "two numbers",
    };

    public static string Mismatch(string op, int position, string left, string right) =>
        $"'{op}' at position {position} needs {Needs(op)}, but here it gets {left} and {right}.";

    public static string NegateMismatch(int position, string operand) =>
        $"'-' at position {position} needs a number, but here it gets {operand}.";
}

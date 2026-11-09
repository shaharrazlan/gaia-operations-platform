using Gaia.Core.Errors;

namespace Gaia.Core.Formulas;

/// <summary>
/// Step 3 of 3 (tokenize → parse → evaluate): walks the tree and computes a value.
/// Every rule that can make a result impossible (÷0, NaN, ∞, mixing number and text) is a
/// <see cref="DomainException"/>, so it reaches the caller as a clear 422, never a crash.
/// </summary>
public static class Evaluator
{
    public static Value Evaluate(Node node, Value a, Value b) => node switch
    {
        NumberNode n => new NumberValue(n.Value),
        TextNode t => new TextValue(t.Value),
        VariableNode v => v.Name == "A" ? a : b,
        UnaryNode u => Evaluate(u.Operand, a, b) is NumberValue x
            ? new NumberValue(-x.Number)
            : throw new DomainException("'-' can only be applied to a number."),
        BinaryNode op => EvaluateBinary(op, a, b),
        CallNode call => throw new NotSupportedException($"Function '{call.Name}' is not available yet."),
        _ => throw new InvalidOperationException($"Unknown node type {node.GetType().Name}."),
    };

    private static Value EvaluateBinary(BinaryNode op, Value a, Value b)
    {
        var left = Evaluate(op.Left, a, b);
        var right = Evaluate(op.Right, a, b);

        return (op.Operator, left, right) switch
        {
            ("+", NumberValue x, NumberValue y) => Number(x.Number + y.Number),
            ("+", TextValue x, TextValue y) => new TextValue(x.Text + y.Text),
            ("-", NumberValue x, NumberValue y) => Number(x.Number - y.Number),
            ("*", NumberValue x, NumberValue y) => Number(x.Number * y.Number),
            ("/", NumberValue _, NumberValue { Number: 0 }) => throw new DomainException("Division by zero."),
            ("/", NumberValue x, NumberValue y) => Number(x.Number / y.Number),
            ("%", NumberValue _, NumberValue { Number: 0 }) => throw new DomainException("Modulo by zero."),
            ("%", NumberValue x, NumberValue y) => Number(x.Number % y.Number),
            ("^", NumberValue x, NumberValue y) => Number(Math.Pow(x.Number, y.Number)),

            ("==", _, _) when left.GetType() == right.GetType() => new BoolValue(left.Equals(right)),
            ("!=", _, _) when left.GetType() == right.GetType() => new BoolValue(!left.Equals(right)),
            ("<" or "<=" or ">" or ">=", NumberValue x, NumberValue y) => Compare(op.Operator, x.Number.CompareTo(y.Number)),
            ("<" or "<=" or ">" or ">=", TextValue x, TextValue y) => Compare(op.Operator, string.CompareOrdinal(x.Text, y.Text)),

            _ => throw new DomainException(
                $"Operator '{op.Operator}' at position {op.Position} can't combine {Describe(left)} and {Describe(right)}."),
        };
    }

    /// <summary>Every computed number goes through here, so NaN and ±∞ never escape as a "result".</summary>
    internal static NumberValue Number(double value) =>
        double.IsFinite(value)
            ? new NumberValue(value)
            : throw new DomainException("The result is not a finite number.");

    private static BoolValue Compare(string op, int comparison) => new(op switch
    {
        "<" => comparison < 0,
        "<=" => comparison <= 0,
        ">" => comparison > 0,
        _ => comparison >= 0,
    });

    internal static string Describe(Value value) => value switch
    {
        NumberValue => "a number",
        TextValue => "a text",
        _ => "a true/false value",
    };
}

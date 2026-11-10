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
        UnaryNode u => Evaluate(u.Operand, a, b) switch
        {
            NumberValue x => new NumberValue(-x.Number),
            var other => throw new DomainException(OperatorRules.NegateMismatch(u.Position, Describe(other))),
        },
        BinaryNode op => EvaluateBinary(op, a, b),
        CallNode { Name: "if" } call => EvaluateIf(call, a, b),
        CallNode call => Functions.Invoke(call.Name, call.Arguments.Select(arg => Evaluate(arg, a, b)).ToList()),
        _ => throw new InvalidOperationException($"Unknown node type {node.GetType().Name}."),
    };

    private static Value EvaluateBinary(BinaryNode op, Value a, Value b)
    {
        var left = Evaluate(op.Left, a, b);
        var right = Evaluate(op.Right, a, b);

        return (op.Operator, left, right) switch
        {
            ("+", NumberValue x, NumberValue y) => Number(x.Number + y.Number),
            ("+", TextValue x, TextValue y) => Text(x.Text + y.Text),
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

            _ => throw new DomainException(OperatorRules.Mismatch(op.Operator, op.Position, Describe(left), Describe(right))),
        };
    }

    /// <summary>if(condition, then, else): only the chosen branch runs, so if(B == 0, 0, A / B) is safe.</summary>
    private static Value EvaluateIf(CallNode call, Value a, Value b) =>
        Evaluate(call.Arguments[0], a, b) switch
        {
            BoolValue { Bool: true } => Evaluate(call.Arguments[1], a, b),
            BoolValue { Bool: false } => Evaluate(call.Arguments[2], a, b),
            var other => throw NotACondition(other),
        };

    internal static DomainException NotACondition(Value condition) =>
        new($"if: the condition must be true/false, not {Describe(condition)}.");

    /// <summary>Every computed number goes through here, so NaN and ±∞ never escape as a "result".</summary>
    internal static NumberValue Number(double value) =>
        double.IsFinite(value)
            ? new NumberValue(value)
            : throw new DomainException("The result is not a finite number.");

    /// <summary>
    /// Longest text a formula may produce (also the size of the stored result).
    /// Every built text goes through <see cref="Text"/>. Inputs are at most 200 characters and each
    /// step's inputs are capped here, so nested replace/concat can't grow memory without bound (NFR6).
    /// </summary>
    public const int MaxTextLength = 1000;

    internal static TextValue Text(string value) =>
        value.Length <= MaxTextLength
            ? new TextValue(value)
            : throw new DomainException($"The text result is longer than {MaxTextLength} characters.");

    private static BoolValue Compare(string op, int comparison) => new(op switch
    {
        "<" => comparison < 0,
        "<=" => comparison <= 0,
        ">" => comparison > 0,
        _ => comparison >= 0,
    });

    /// <summary>Worded once, in <see cref="TypeChecker.Describe"/>, so save-time and run-time messages match.</summary>
    internal static string Describe(Value value) => TypeChecker.Describe(value switch
    {
        NumberValue => FormulaType.Number,
        TextValue => FormulaType.Text,
        _ => FormulaType.Bool,
    });
}

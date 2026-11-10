using Gaia.Core.Operations;

namespace Gaia.Core.Formulas;

/// <summary>What a part of a formula produces, as far as can be told before it runs.</summary>
internal enum FormulaType
{
    Number,
    Text,
    Bool,
    /// <summary>Known only when it runs: an <c>if</c> whose two branches have different types (A11).</summary>
    Any,
}

/// <summary>
/// Save-time check (R4.AC8): works out the type of every part of the tree from the operation's input type and
/// refuses a formula that could never run, e.g. <c>A ^ B</c> when A and B are text. Parts of type
/// <see cref="FormulaType.Any"/> are let through; the <see cref="Evaluator"/> still checks them when the formula runs.
/// </summary>
internal static class TypeChecker
{
    public static void Check(Node tree, InputType inputType) => TypeOf(tree, inputType);

    private static FormulaType TypeOf(Node node, InputType inputType) => node switch
    {
        NumberNode => FormulaType.Number,
        TextNode => FormulaType.Text,
        VariableNode => TypeOfInputs(inputType),
        UnaryNode u => TypeOfNegation(u, inputType),
        BinaryNode op => TypeOfOperator(op, inputType),
        CallNode { Name: "if" } call => TypeOfIf(call, inputType),
        CallNode call => TypeOfCall(call, inputType),
        _ => throw new InvalidOperationException($"Unknown node type {node.GetType().Name}."),
    };

    /// <summary>Number and Coordinate inputs are numbers inside a formula; Text inputs are texts.</summary>
    private static FormulaType TypeOfInputs(InputType inputType) =>
        inputType == InputType.Text ? FormulaType.Text : FormulaType.Number;

    private static FormulaType TypeOfNegation(UnaryNode negation, InputType inputType)
    {
        var operand = TypeOf(negation.Operand, inputType);
        return Fits(operand, FormulaType.Number)
            ? FormulaType.Number
            : throw Fail(OperatorRules.NegateMismatch(negation.Position, Describe(operand)), inputType, FormulaType.Number, negation.Operand);
    }

    private static FormulaType TypeOfOperator(BinaryNode op, InputType inputType)
    {
        var (left, right) = (TypeOf(op.Left, inputType), TypeOf(op.Right, inputType));
        var unknown = left == FormulaType.Any || right == FormulaType.Any;
        var hasBool = left == FormulaType.Bool || right == FormulaType.Bool;

        FormulaType? result = op.Operator switch
        {
            "+" when unknown => hasBool ? null : FormulaType.Any,
            "+" => left == right && !hasBool ? left : null,
            "==" or "!=" => unknown || left == right ? FormulaType.Bool : null,
            "<" or "<=" or ">" or ">=" => !hasBool && (unknown || left == right) ? FormulaType.Bool : null,
            _ => Fits(left, FormulaType.Number) && Fits(right, FormulaType.Number) ? FormulaType.Number : null,
        };

        var needed = OperatorRules.IsArithmetic(op.Operator) ? FormulaType.Number : (FormulaType?)null;
        return result ?? throw Fail(OperatorRules.Mismatch(op.Operator, op.Position, Describe(left), Describe(right)),
            inputType, needed, op.Left, op.Right);
    }

    private static FormulaType TypeOfIf(CallNode call, InputType inputType)
    {
        var condition = TypeOf(call.Arguments[0], inputType);
        if (!Fits(condition, FormulaType.Bool))
            throw Fail($"if at position {call.Position} needs a true/false condition (e.g. A > B) as argument 1, " +
                       $"but here it gets {Describe(condition)}.", inputType, null, call.Arguments[0]);

        var (then, otherwise) = (TypeOf(call.Arguments[1], inputType), TypeOf(call.Arguments[2], inputType));
        return then == otherwise ? then : FormulaType.Any;
    }

    private static FormulaType TypeOfCall(CallNode call, InputType inputType)
    {
        for (var i = 0; i < call.Arguments.Count; i++)
        {
            var (expected, actual) = (Functions.ParameterType(call.Name, i), TypeOf(call.Arguments[i], inputType));
            if (!Fits(actual, expected))
                throw Fail($"{call.Name} at position {call.Position} needs {Describe(expected)} as argument {i + 1}, " +
                           $"but here it gets {Describe(actual)}.", inputType, expected, call.Arguments[i]);
        }
        return Functions.ReturnType(call.Name);
    }

    private static bool Fits(FormulaType actual, FormulaType expected) =>
        actual == expected || actual == FormulaType.Any || expected == FormulaType.Any;

    internal static string Describe(FormulaType type) => type switch
    {
        FormulaType.Number => "a number",
        FormulaType.Text => "a text",
        FormulaType.Bool => "a true/false value",
        _ => "a value whose type is known only when it runs",
    };

    /// <summary>
    /// The error, plus — when A or B itself is at fault — which input type made them a number or a text,
    /// and what to choose instead. This is the part an operator can act on. When the needed type is known,
    /// A or B is at fault only if it isn't that type (in <c>A * "x"</c> with numbers, "x" is).
    /// </summary>
    private static Exception Fail(string message, InputType inputType, FormulaType? needed, params Node[] suspects)
    {
        var inputs = TypeOfInputs(inputType);
        if (!suspects.Any(node => node is VariableNode) || inputs == needed)
            return FormulaErrors.Invalid(message);

        var inputsAreText = inputs == FormulaType.Text;
        var hint = $" A and B are {(inputsAreText ? "text" : "numbers")} because the input type is {inputType}.";
        hint += (needed, inputsAreText) switch
        {
            (FormulaType.Number, true) => " Choose input type Number for calculations.",
            (FormulaType.Text, false) => " Choose input type Text for text operations, or turn a number into text with concat(A).",
            _ => "",
        };
        return FormulaErrors.Invalid(message + hint);
    }
}

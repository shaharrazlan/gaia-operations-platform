using Gaia.Core.Errors;

namespace Gaia.Core.Formulas;

/// <summary>
/// Save-time check (R4.AC9): refuses a formula with a fixed part (one that doesn't use A or B) that can never
/// produce a result — <c>sqrt(-1)</c>, <c>A / 0</c>, <c>round(A, 20)</c>, <c>count(A, "")</c>. Fixed parts are
/// simply run now, by the same <see cref="Evaluator"/> and <see cref="Functions"/> that run them later, so the
/// rules and messages are the ones the user would otherwise see in the Calculator.
/// Runs after <see cref="TypeChecker"/>, so the types already fit.
/// </summary>
internal static class ConstantCheck
{
    /// <summary>Stands in for A and B when running a fixed part (which never reads them).</summary>
    private static readonly Value Unused = new NumberValue(0);

    public static void Check(Node node)
    {
        if (IsFixed(node))
        {
            if (node is not (NumberNode or TextNode))
                Run(node);
            return;
        }

        switch (node)
        {
            case UnaryNode negation:
                Check(negation.Operand);
                break;

            case BinaryNode op:
                Check(op.Left);
                Check(op.Right);
                if (op.Operator is "/" or "%" && IsFixed(op.Right) && Run(op.Right) is NumberValue { Number: 0 })
                    throw FormulaErrors.Invalid($"'{op.Operator}' at position {op.Position} always divides by 0, whatever is typed.");
                break;

            // A fixed condition decides the branch now; only that branch can ever run.
            case CallNode { Name: "if" } choice when IsFixed(choice.Arguments[0]):
                var condition = Run(choice.Arguments[0]);
                if (condition is not BoolValue chosen)   // possible when its type is Any
                    throw NeverProducesResult(choice, Evaluator.NotACondition(condition));
                Check(chosen.Bool ? choice.Arguments[1] : choice.Arguments[2]);
                break;

            case CallNode call:
                foreach (var argument in call.Arguments)
                    Check(argument);
                if (call.Name != "if" && call.Arguments.Any(IsFixed))
                    RunWithSampleInputs(call);
                break;
        }
    }

    /// <summary>
    /// Some functions refuse certain fixed arguments whatever the other arguments are (round's digits, count's empty
    /// search text). Running the call with a harmless sample (1 or "x") for each part that uses A or B finds them.
    /// </summary>
    private static void RunWithSampleInputs(CallNode call)
    {
        var arguments = call.Arguments
            .Select((argument, i) => IsFixed(argument) ? Run(argument) : Sample(Functions.ParameterType(call.Name, i)))
            .ToList();
        try
        {
            Functions.Invoke(call.Name, arguments);
        }
        catch (DomainException e)
        {
            throw NeverProducesResult(call, e);
        }
    }

    private static Value Sample(FormulaType type) => type == FormulaType.Number ? new NumberValue(1) : new TextValue("x");

    private static Value Run(Node fixedPart)
    {
        try
        {
            return Evaluator.Evaluate(fixedPart, Unused, Unused);
        }
        catch (DomainException e)
        {
            throw NeverProducesResult(fixedPart, e);
        }
    }

    private static ValidationException NeverProducesResult(Node part, DomainException reason) =>
        FormulaErrors.Invalid($"The part at position {part.Position} can never produce a result, whatever is typed: {reason.Message}");

    /// <summary>True when the part doesn't use A or B, so it gives the same value on every run.</summary>
    private static bool IsFixed(Node node) => node switch
    {
        VariableNode => false,
        UnaryNode u => IsFixed(u.Operand),
        BinaryNode b => IsFixed(b.Left) && IsFixed(b.Right),
        CallNode c => c.Arguments.All(IsFixed),
        _ => true,
    };
}

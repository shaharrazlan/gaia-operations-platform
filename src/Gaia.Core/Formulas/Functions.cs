using Gaia.Core.Errors;

namespace Gaia.Core.Formulas;

/// <summary>
/// The whitelist of functions a formula may call. Nothing outside this table can be reached
/// from a formula (NFR6). Adding a function = adding one line here.
/// </summary>
public static class Functions
{
    private sealed record Definition(int MinArgs, int MaxArgs, Func<IReadOnlyList<Value>, string, Value> Invoke);

    private static readonly Dictionary<string, Definition> All = new(StringComparer.Ordinal)
    {
        // numbers
        ["sqrt"] = new(1, 1, (args, fn) => Evaluator.Number(Math.Sqrt(Num(args, 0, fn)))),
        ["abs"] = new(1, 1, (args, fn) => Evaluator.Number(Math.Abs(Num(args, 0, fn)))),
        ["floor"] = new(1, 1, (args, fn) => Evaluator.Number(Math.Floor(Num(args, 0, fn)))),
        ["ceil"] = new(1, 1, (args, fn) => Evaluator.Number(Math.Ceiling(Num(args, 0, fn)))),
        ["round"] = new(1, 2, Round),
        ["min"] = new(2, 10, (args, fn) => Evaluator.Number(args.Select((_, i) => Num(args, i, fn)).Min())),
        ["max"] = new(2, 10, (args, fn) => Evaluator.Number(args.Select((_, i) => Num(args, i, fn)).Max())),

        // text
        ["upper"] = new(1, 1, (args, fn) => Evaluator.Text(Text(args, 0, fn).ToUpperInvariant())),
        ["lower"] = new(1, 1, (args, fn) => Evaluator.Text(Text(args, 0, fn).ToLowerInvariant())),
        ["trim"] = new(1, 1, (args, fn) => Evaluator.Text(Text(args, 0, fn).Trim())),
        ["len"] = new(1, 1, (args, fn) => new NumberValue(Text(args, 0, fn).Length)),
        ["concat"] = new(1, 10, (args, _) => Evaluator.Text(string.Concat(args.Select(ResultFormatter.Format)))),
        ["contains"] = new(2, 2, (args, fn) => new BoolValue(Text(args, 0, fn).Contains(Text(args, 1, fn), StringComparison.Ordinal))),
        ["count"] = new(2, 2, Count),
        ["replace"] = new(3, 3, Replace),

        // "if" is evaluated lazily by the Evaluator (only the chosen branch runs);
        // it is listed here so the parser knows its name and argument count.
        ["if"] = new(3, 3, (_, _) => throw new InvalidOperationException("'if' is evaluated by the Evaluator.")),
    };

    public static IReadOnlyCollection<string> Names => All.Keys;

    /// <summary>Called by the parser, so unknown functions and wrong argument counts fail at save time.</summary>
    internal static void CheckCall(string name, int argumentCount, int position)
    {
        if (!All.TryGetValue(name, out var definition))
            throw Tokenizer.Error($"Unknown function '{name}' at position {position}. Available: {string.Join(", ", All.Keys)}.");

        if (argumentCount < definition.MinArgs || argumentCount > definition.MaxArgs)
        {
            var expected = definition.MinArgs == definition.MaxArgs
                ? $"{definition.MinArgs}"
                : $"{definition.MinArgs} to {definition.MaxArgs}";
            throw Tokenizer.Error($"Function '{name}' at position {position} expects {expected} argument(s) but got {argumentCount}.");
        }
    }

    internal static Value Invoke(string name, IReadOnlyList<Value> arguments) => All[name].Invoke(arguments, name);

    private static Value Round(IReadOnlyList<Value> args, string fn)
    {
        var digits = args.Count > 1 ? Num(args, 1, fn) : 0;
        if (digits != Math.Floor(digits) || digits is < 0 or > 15)
            throw new DomainException("round: the number of digits must be a whole number from 0 to 15.");
        return Evaluator.Number(Math.Round(Num(args, 0, fn), (int)digits, MidpointRounding.AwayFromZero));
    }

    private static Value Count(IReadOnlyList<Value> args, string fn)
    {
        var (text, part) = (Text(args, 0, fn), Text(args, 1, fn));
        if (part.Length == 0)
            throw new DomainException("count: the text to count must not be empty.");

        var count = 0;
        for (var i = text.IndexOf(part, StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + part.Length, StringComparison.Ordinal))
            count++;
        return new NumberValue(count);
    }

    private static Value Replace(IReadOnlyList<Value> args, string fn)
    {
        var (text, find, with) = (Text(args, 0, fn), Text(args, 1, fn), Text(args, 2, fn));
        if (find.Length == 0)
            throw new DomainException("replace: the text to find must not be empty.");
        return Evaluator.Text(text.Replace(find, with, StringComparison.Ordinal));
    }

    private static double Num(IReadOnlyList<Value> args, int index, string fn) =>
        args[index] is NumberValue n ? n.Number : throw WrongType(fn, index, "a number", args[index]);

    private static string Text(IReadOnlyList<Value> args, int index, string fn) =>
        args[index] is TextValue t ? t.Text : throw WrongType(fn, index, "a text", args[index]);

    private static DomainException WrongType(string fn, int index, string expected, Value actual) =>
        new($"{fn}: argument {index + 1} must be {expected}, not {Evaluator.Describe(actual)}.");
}

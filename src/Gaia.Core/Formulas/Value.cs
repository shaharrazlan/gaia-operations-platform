namespace Gaia.Core.Formulas;

/// <summary>A value inside a formula: a number, a text or a true/false result of a comparison.</summary>
public abstract record Value;

public sealed record NumberValue(double Number) : Value;

public sealed record TextValue(string Text) : Value;

public sealed record BoolValue(bool Bool) : Value;

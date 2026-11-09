namespace Gaia.Core.Operations;

/// <summary>How an operation reads its two text inputs (assumption A4).</summary>
public enum InputType
{
    /// <summary>Plain decimal numbers such as 12, -2.5 ('.' as the decimal separator).</summary>
    Number,

    /// <summary>Any text.</summary>
    Text,

    /// <summary>Field A = latitude (−90…90), Field B = longitude (−180…180).</summary>
    Coordinate,
}

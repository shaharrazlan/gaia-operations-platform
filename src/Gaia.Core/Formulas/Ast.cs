namespace Gaia.Core.Formulas;

/// <summary>
/// The formula as a tree (Abstract Syntax Tree). "A + B * 2" becomes
/// BinaryNode("+", VariableNode("A"), BinaryNode("*", VariableNode("B"), NumberNode(2))).
/// Position is 1-based and used in error messages.
/// </summary>
public abstract record Node(int Position);

public sealed record NumberNode(double Value, int Position) : Node(Position);

public sealed record TextNode(string Value, int Position) : Node(Position);

/// <summary>"A" or "B" — the only names a formula may use.</summary>
public sealed record VariableNode(string Name, int Position) : Node(Position);

/// <summary>Negation: -Operand.</summary>
public sealed record UnaryNode(Node Operand, int Position) : Node(Position);

/// <summary>Operator is the symbol: + - * / % ^ == != &lt; &lt;= &gt; &gt;=</summary>
public sealed record BinaryNode(string Operator, Node Left, Node Right, int Position) : Node(Position);

public sealed record CallNode(string Name, IReadOnlyList<Node> Arguments, int Position) : Node(Position);

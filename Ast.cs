using System.Collections.Generic;
using System.Numerics;

namespace MyExcelMAUIApp.Parsing
{
    public enum BinaryOp { Mod, Div, Eq, Lt, Gt, And, Or }
    public abstract record Node(int Position);

    public sealed record NumberNode(int Position, BigInteger Value) : Node(Position);

    public sealed record CellNode(int Position, string Name) : Node(Position);

    public sealed record NotNode(int Position, Node Operand) : Node(Position);

    public sealed record BinaryNode(int Position, BinaryOp Op, Node Left, Node Right) : Node(Position);

    public sealed record MinMaxNode(int Position, bool IsMax, IReadOnlyList<Node> Args) : Node(Position);
}
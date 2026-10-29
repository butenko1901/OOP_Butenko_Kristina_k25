using System.Numerics;

namespace MyExcelMAUIApp.Parsing
{
    public interface ICellProvider
    {
        object GetCellValue(string name, int position);
    }

    public sealed class Evaluator
    {
        private readonly ICellProvider _cells;

        public Evaluator(ICellProvider cells) => _cells = cells;

        public object Evaluate(Node node)
        {
            switch (node)
            {
                case NumberNode n:
                    return n.Value;

                case CellNode c:
                    return _cells.GetCellValue(c.Name, c.Position);

                case NotNode n:
                    return !AsBool(Evaluate(n.Operand), "not", n.Position);

                case BinaryNode b:
                    return EvaluateBinary(b);

                case MinMaxNode m:
                    return EvaluateMinMax(m);

                default:
                    throw new ExpressionException("Невідомий вузол виразу", node.Position);
            }
        }

        private object EvaluateBinary(BinaryNode n)
        {
            object left = Evaluate(n.Left);
            object right = Evaluate(n.Right);
            string op = OpText(n.Op);

            switch (n.Op)
            {
                case BinaryOp.Mod:
                case BinaryOp.Div:
                {
                    BigInteger a = AsNumber(left, op, n.Position);
                    BigInteger b = AsNumber(right, op, n.Position);
                    if (b.IsZero)
                        throw new ExpressionException("Ділення на нуль", n.Position);
                    return n.Op == BinaryOp.Mod ? BigInteger.Remainder(a, b) : BigInteger.Divide(a, b);
                }

                case BinaryOp.Lt:
                    return AsNumber(left, op, n.Position) < AsNumber(right, op, n.Position);

                case BinaryOp.Gt:
                    return AsNumber(left, op, n.Position) > AsNumber(right, op, n.Position);

                case BinaryOp.Eq:
                    if (left is BigInteger x && right is BigInteger y) return x == y;
                    if (left is bool p && right is bool q) return p == q;
                    throw new ExpressionException(
                        "Оператор '=' порівнює значення одного типу (обидва числа або обидва логічні)",
                        n.Position);

                case BinaryOp.And:
                {
                    bool a = AsBool(left, op, n.Position);
                    bool b = AsBool(right, op, n.Position);
                    return a && b;
                }

                case BinaryOp.Or:
                {
                    bool a = AsBool(left, op, n.Position);
                    bool b = AsBool(right, op, n.Position);
                    return a || b;
                }

                default:
                    throw new ExpressionException("Невідома операція", n.Position);
            }
        }

        private object EvaluateMinMax(MinMaxNode m)
        {
            string name = m.IsMax ? "mmax" : "mmin";
            BigInteger? best = null;

            foreach (var arg in m.Args)
            {
                BigInteger v = AsNumber(Evaluate(arg), name, arg.Position);
                if (best is null || (m.IsMax ? v > best : v < best))
                    best = v;
            }
            return best!.Value;
        }

        private static BigInteger AsNumber(object value, string op, int position)
        {
            if (value is BigInteger n) return n;
            throw new ExpressionException(
                $"'{op}' очікує числові операнди, а отримано логічне значення", position);
        }

        private static bool AsBool(object value, string op, int position)
        {
            if (value is bool b) return b;
            throw new ExpressionException(
                $"'{op}' очікує логічні операнди, а отримано число", position);
        }

        private static string OpText(BinaryOp op) => op switch
        {
            BinaryOp.Mod => "mod",
            BinaryOp.Div => "div",
            BinaryOp.Eq => "=",
            BinaryOp.Lt => "<",
            BinaryOp.Gt => ">",
            BinaryOp.And => "and",
            BinaryOp.Or => "or",
            _ => "?"
        };
    }
}
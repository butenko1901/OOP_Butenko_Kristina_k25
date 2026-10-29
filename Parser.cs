using System.Collections.Generic;
using System.Numerics;

namespace MyExcelMAUIApp.Parsing
{
    public sealed class Parser
    {
        private readonly List<Token> _tokens;
        private int _pos;

        private Parser(List<Token> tokens) => _tokens = tokens;

        public static Node Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ExpressionException("Порожній вираз", 0);

            var parser = new Parser(Lexer.Tokenize(text));
            var tree = parser.ParseOr();

            if (parser.Current.Type != TokenType.Eof)
                throw new ExpressionException(
                    $"Очікувався кінець виразу, а знайдено {Describe(parser.Current)}",
                    parser.Current.Position);

            return tree;
        }

        private Token Current => _tokens[_pos];

        private Token Advance()
        {
            var t = _tokens[_pos];
            if (t.Type != TokenType.Eof) _pos++;
            return t;
        }

        private Token Expect(TokenType type, string what)
        {
            if (Current.Type != type)
                throw new ExpressionException(
                    $"Очікувалось {what}, а знайдено {Describe(Current)}", Current.Position);
            return Advance();
        }

        private static string Describe(Token t) =>
            t.Type == TokenType.Eof ? "кінець виразу" : $"'{t.Text}'";

        // expr := and ('or' and)*
        private Node ParseOr()
        {
            var left = ParseAnd();
            while (Current.Type == TokenType.Or)
            {
                var op = Advance();
                var right = ParseAnd();
                left = new BinaryNode(op.Position, BinaryOp.Or, left, right);
            }
            return left;
        }
        private Node ParseAnd()
        {
            var left = ParseNot();
            while (Current.Type == TokenType.And)
            {
                var op = Advance();
                var right = ParseNot();
                left = new BinaryNode(op.Position, BinaryOp.And, left, right);
            }
            return left;
        }
        private Node ParseNot()
        {
            if (Current.Type == TokenType.Not)
            {
                var op = Advance();
                return new NotNode(op.Position, ParseNot());
            }
            return ParseCompare();
        }

        private Node ParseCompare()
        {
            var left = ParseModDiv();
            while (Current.Type is TokenType.Eq or TokenType.Lt or TokenType.Gt)
            {
                var op = Advance();
                var kind = op.Type switch
                {
                    TokenType.Eq => BinaryOp.Eq,
                    TokenType.Lt => BinaryOp.Lt,
                    _ => BinaryOp.Gt
                };
                var right = ParseModDiv();
                left = new BinaryNode(op.Position, kind, left, right);
            }
            return left;
        }

        private Node ParseModDiv()
        {
            var left = ParsePrimary();
            while (Current.Type is TokenType.Mod or TokenType.Div)
            {
                var op = Advance();
                var kind = op.Type == TokenType.Mod ? BinaryOp.Mod : BinaryOp.Div;
                var right = ParsePrimary();
                left = new BinaryNode(op.Position, kind, left, right);
            }
            return left;
        }

        private Node ParsePrimary()
        {
            var t = Current;
            switch (t.Type)
            {
                case TokenType.Number:
                    Advance();
                    return new NumberNode(t.Position, BigInteger.Parse(t.Text));

                case TokenType.Cell:
                    Advance();
                    return new CellNode(t.Position, t.Text);

                case TokenType.LParen:
                {
                    Advance();
                    var inner = ParseOr();
                    Expect(TokenType.RParen, "')'");
                    return inner;
                }

                case TokenType.MMax:
                case TokenType.MMin:
                {
                    Advance();
                    Expect(TokenType.LParen, $"'(' після {t.Text}");
                    var args = new List<Node> { ParseOr() };
                    while (Current.Type == TokenType.Comma)
                    {
                        Advance();
                        args.Add(ParseOr());
                    }
                    Expect(TokenType.RParen, "')' або ','");
                    return new MinMaxNode(t.Position, t.Type == TokenType.MMax, args);
                }

                default:
                    throw new ExpressionException(
                        $"Очікувалось число, клітинка, '(' або mmax/mmin, а знайдено {Describe(t)}",
                        t.Position);
            }
        }
    }
}
using System;

namespace MyExcelMAUIApp.Parsing
{
    public class ExpressionException : Exception
    {
        public int Position { get; }

        public ExpressionException(string message, int position) : base(message)
        {
            Position = position;
        }

        public override string ToString() => $"{Message} (позиція {Position + 1})";
    }
}
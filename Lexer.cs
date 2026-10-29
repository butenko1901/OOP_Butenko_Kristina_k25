using System;
using System.Collections.Generic;

namespace MyExcelMAUIApp.Parsing
{
    public enum TokenType
    {
        Number, Cell,
        LParen, RParen, Comma,
        Eq, Lt, Gt,
        Mod, Div,
        MMax, MMin,
        Not, And, Or,
        Eof
    }

    public readonly record struct Token(TokenType Type, string Text, int Position);

    public static class Lexer
    {
        private static readonly Dictionary<string, TokenType> Keywords = new()
        {
            ["mod"] = TokenType.Mod,
            ["div"] = TokenType.Div,
            ["mmax"] = TokenType.MMax,
            ["mmin"] = TokenType.MMin,
            ["not"] = TokenType.Not,
            ["and"] = TokenType.And,
            ["or"] = TokenType.Or,
        };

        public static List<Token> Tokenize(string text)
        {
            var tokens = new List<Token>();
            int i = 0;

            while (i < text.Length)
            {
                char c = text[i];

                if (char.IsWhiteSpace(c)) { i++; continue; }

                int start = i;

                if (IsDigit(c))
                {
                    while (i < text.Length && IsDigit(text[i])) i++;
                    if (i < text.Length && IsLetter(text[i]))
                        throw new ExpressionException(
                            $"Неприпустимий символ '{text[i]}' одразу після числа", i);
                    tokens.Add(new Token(TokenType.Number, text[start..i], start));
                }
                else if (IsLetter(c))
                {
                    while (i < text.Length && (IsLetter(text[i]) || IsDigit(text[i]))) i++;
                    string word = text[start..i];

                    if (Keywords.TryGetValue(word, out var kw))
                        tokens.Add(new Token(kw, word, start));
                    else if (IsCellName(word))
                        tokens.Add(new Token(TokenType.Cell, word, start));
                    else
                        throw new ExpressionException(
                            $"Невідомий ідентифікатор '{word}'. Імена клітинок: великі літери + номер рядка (A1, AB12)", start);
                }
                else
                {
                    TokenType type = c switch
                    {
                        '(' => TokenType.LParen,
                        ')' => TokenType.RParen,
                        ',' => TokenType.Comma,
                        '=' => TokenType.Eq,
                        '<' => TokenType.Lt,
                        '>' => TokenType.Gt,
                        _ => throw new ExpressionException($"Недопустимий символ '{c}'", i)
                    };
                    i++;
                    tokens.Add(new Token(type, c.ToString(), start));
                }
            }

            tokens.Add(new Token(TokenType.Eof, "", text.Length));
            return tokens;
        }

        private static bool IsDigit(char c) => c >= '0' && c <= '9';
        private static bool IsLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        private static bool IsCellName(string w)
        {
            int i = 0;
            while (i < w.Length && w[i] >= 'A' && w[i] <= 'Z') i++;
            if (i == 0 || i == w.Length) return false;
            if (w[i] < '1' || w[i] > '9') return false;
            for (int j = i + 1; j < w.Length; j++)
                if (!IsDigit(w[j])) return false;
            return true;
        }
    }
}
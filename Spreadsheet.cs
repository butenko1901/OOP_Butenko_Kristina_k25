using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

namespace MyExcelMAUIApp.Parsing
{
    public sealed class CellResult
    {
        public object? Value { get; }
        public string? Error { get; }
        public int ErrorPosition { get; }
        public bool IsEmpty { get; }
        public bool IsError => Error != null;

        private CellResult(object? value, string? error, int errorPosition, bool isEmpty)
        {
            Value = value;
            Error = error;
            ErrorPosition = errorPosition;
            IsEmpty = isEmpty;
        }

        public static readonly CellResult Empty = new(null, null, 0, true);
        public static CellResult FromValue(object value) => new(value, null, 0, false);
        public static CellResult FromError(string message, int position) => new(null, message, position, false);

        public string Display
        {
            get
            {
                if (IsEmpty) return "";
                if (IsError) return $"Помилка: {Error}";
                return Value switch
                {
                    bool b => b ? "true" : "false",
                    BigInteger n => n.ToString(),
                    _ => ""
                };
            }
        }
    }

    public sealed class Spreadsheet : ICellProvider
    {
        private readonly Dictionary<string, string> _cells = new();
        private readonly Dictionary<string, CellResult> _cache = new();
        private readonly List<string> _stack = new();

        public int RowCount { get; private set; }
        public int ColumnCount { get; private set; }
        public bool RequireLogicalResult { get; set; }

        public Spreadsheet(int rows, int columns)
        {
            RowCount = Math.Max(1, rows);
            ColumnCount = Math.Max(1, columns);
        }

        public static string ColumnName(int column)
        {
            string name = string.Empty;
            int n = column;
            while (n > 0)
            {
                n--;
                name = (char)('A' + n % 26) + name;
                n /= 26;
            }
            return name;
        }

        public static string CellName(int row, int column) => ColumnName(column) + row;

        public static bool TryParseCellName(string name, out int row, out int column)
        {
            row = 0;
            column = 0;
            int i = 0;
            while (i < name.Length && name[i] >= 'A' && name[i] <= 'Z')
            {
                column = column * 26 + (name[i] - 'A' + 1);
                i++;
                if (i > 6) return false;
            }
            if (i == 0 || i == name.Length || name[i] == '0') return false;
            return int.TryParse(name.AsSpan(i), NumberStyles.None, CultureInfo.InvariantCulture, out row) && row >= 1;
        }

        private bool InRange(int row, int column) =>
            row >= 1 && row <= RowCount && column >= 1 && column <= ColumnCount;

        public string GetExpression(int row, int column) =>
            _cells.TryGetValue(CellName(row, column), out var text) ? text : "";

        public void SetExpression(int row, int column, string? text)
        {
            if (!InRange(row, column)) return;
            string name = CellName(row, column);

            if (string.IsNullOrWhiteSpace(text)) _cells.Remove(name);
            else _cells[name] = text.Trim();

            _cache.Clear();
        }

        public CellResult Evaluate(int row, int column) => Evaluate(CellName(row, column));

        public CellResult Evaluate(string name)
        {
            if (_cache.TryGetValue(name, out var cached)) return cached;

            CellResult result;
            if (!_cells.TryGetValue(name, out var text))
            {
                result = CellResult.Empty;
            }
            else
            {
                try
                {
                    var tree = Parser.Parse(text);
                    object value;
                    _stack.Add(name);
                    try { value = new Evaluator(this).Evaluate(tree); }
                    finally { _stack.RemoveAt(_stack.Count - 1); }

                    if (RequireLogicalResult && value is not bool)
                        throw new ExpressionException("Результат виразу має бути логічним (true/false)", 0);

                    result = CellResult.FromValue(value);
                }
                catch (ExpressionException ex)
                {
                    result = CellResult.FromError(ex.Message, ex.Position);
                }
            }

            _cache[name] = result;
            return result;
        }

        public void Recalculate() => _cache.Clear();

        object ICellProvider.GetCellValue(string name, int position)
        {
            if (!TryParseCellName(name, out int row, out int col) || !InRange(row, col))
                throw new ExpressionException($"Клітинка {name} лежить поза межами таблиці", position);

            int idx = _stack.IndexOf(name);
            if (idx >= 0)
            {
                string path = string.Join(" → ", _stack.Skip(idx).Append(name));
                throw new ExpressionException($"Циклічне посилання: {path}", position);
            }

            var res = Evaluate(name);
            if (res.IsEmpty)
                throw new ExpressionException($"Клітинка {name} порожня", position);
            if (res.IsError)
                throw new ExpressionException($"Помилка в клітинці {name}: {res.Error}", position);

            return res.Value!;
        }

        public void AddRow()
        {
            RowCount++;
            _cache.Clear();
        }

        public void AddColumn()
        {
            ColumnCount++;
            _cache.Clear();
        }

        public bool RemoveLastRow()
        {
            if (RowCount <= 1) return false;
            RemoveCellsWhere((r, c) => r == RowCount);
            RowCount--;
            _cache.Clear();
            return true;
        }

        public bool RemoveLastColumn()
        {
            if (ColumnCount <= 1) return false;
            RemoveCellsWhere((r, c) => c == ColumnCount);
            ColumnCount--;
            _cache.Clear();
            return true;
        }

        private void RemoveCellsWhere(Func<int, int, bool> predicate)
        {
            var toRemove = _cells.Keys
                .Where(k => TryParseCellName(k, out int r, out int c) && predicate(r, c))
                .ToList();
            foreach (var k in toRemove) _cells.Remove(k);
        }

        public sealed record SheetData(int Rows, int Columns, Dictionary<string, string>? Cells);

        public string ToJson() =>
            JsonSerializer.Serialize(
                new SheetData(RowCount, ColumnCount, new Dictionary<string, string>(_cells)),
                new JsonSerializerOptions { WriteIndented = true });

        public static Spreadsheet FromJson(string json)
        {
            var data = JsonSerializer.Deserialize<SheetData>(json)
                       ?? throw new InvalidDataException("Файл порожній або пошкоджений");
            if (data.Rows < 1 || data.Columns < 1)
                throw new InvalidDataException("Некоректні розміри таблиці у файлі");

            var sheet = new Spreadsheet(data.Rows, data.Columns);
            if (data.Cells != null)
            {
                foreach (var (name, text) in data.Cells)
                {
                    if (TryParseCellName(name, out int r, out int c) && sheet.InRange(r, c)
                        && !string.IsNullOrWhiteSpace(text))
                        sheet._cells[name] = text;
                }
            }
            return sheet;
        }
    }
}
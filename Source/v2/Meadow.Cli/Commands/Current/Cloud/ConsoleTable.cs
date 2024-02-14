using System.Text;

namespace Meadow.CLI;

public class ConsoleTable
{
    private readonly string[] _columns;
    private IList<string[]> _rows;

    public ConsoleTable(params string[] columns)
    {
        _columns = columns;
        _rows = new List<string[]>();
    }

    public void AddRow(params object[] values)
    {
        if (values.Length != _columns.Length)
        {
            throw new InvalidOperationException("The number of values for the given row does not match the number of columns.");
        }

        _rows.Add(values.Select(v => Convert.ToString(v) ?? string.Empty).ToArray());
    }

    public static implicit operator string(ConsoleTable table) => table.Render();

    public string Render()
    {
        var maxWidths = new int[_columns.Length];
        for (var i = 0; i < _columns.Length; i++)
        {
            maxWidths[i] = _columns[i].Length;
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            for (var j = 0; j < _rows[i].Length; j++)
            {
                maxWidths[j] = Math.Max(maxWidths[j], _rows[i][j].Length);
            }
        }

        var sb = new StringBuilder();

        // Divider
        sb.AppendLine();
        for (var i = 0; i < _columns.Length; i++)
        {
            sb.Append(new string('-', maxWidths[i]));
            if (i < _columns.Length - 1)
            {
                sb.Append("-+-");
            }
        }

        // Header
        sb.AppendLine();
        for (var i = 0; i < _columns.Length; i++)
        {
            sb.Append(_columns[i].PadRight(maxWidths[i]));
            if (i < _columns.Length - 1)
            {
                sb.Append(" | ");
            }
        }

        // Divider
        sb.AppendLine();
        for (var i = 0; i < _columns.Length; i++)
        {
            sb.Append(new string('-', maxWidths[i]));
            if (i < _columns.Length - 1)
            {
                sb.Append("-|-");
            }
        }

        // Rows
        for (var i = 0; i < _rows.Count; i++)
        {
            sb.AppendLine();
            for (var j = 0; j < _rows[i].Length; j++)
            {
                sb.Append(_rows[i][j].PadRight(maxWidths[j]));
                if (j < _rows[i].Length - 1)
                {
                    sb.Append(" | ");
                }
            }
        }

        // Divider
        sb.AppendLine();
        for (var i = 0; i < _columns.Length; i++)
        {
            sb.Append(new string('-', maxWidths[i]));
            if (i < _columns.Length - 1)
            {
                sb.Append("-+-");
            }
        }

        sb.AppendLine();
        return sb.ToString();
    }
}

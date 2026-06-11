using AppAmbit.Models.Db;
using AppAmbit.Services.Interfaces;

namespace AppAmbit;

public sealed class DbQueryBuilder<T> where T : new()
{
    private static readonly HashSet<string> AllowedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "=", "!=", "<>", ">", ">=", "<", "<=", "LIKE", "NOT LIKE", "IS", "IS NOT"
    };

    private readonly string _table;
    private readonly IDbService _dbService;

    private readonly List<string> _selectedColumns = new();
    private readonly List<string> _whereConditions = new();
    private readonly List<object?> _whereParams = new();
    private string? _orderByColumn;
    private bool _orderByDesc;
    private int _limitValue = -1;
    private int _offsetValue = -1;
    private bool _consumed;

    internal DbQueryBuilder(string table, IDbService dbService)
    {
        _table = table;
        _dbService = dbService;
    }

    public DbQueryBuilder<T> Select(params string[] columns)
    {
        foreach (var col in columns)
            if (!_selectedColumns.Contains(col))
                _selectedColumns.Add(col);
        return this;
    }

    public DbQueryBuilder<T> Where(string column, object? value)
    {
        if (value is null)
            _whereConditions.Add($"{QuoteId(column)} IS NULL");
        else
        {
            _whereConditions.Add($"{QuoteId(column)} = ?");
            _whereParams.Add(value);
        }
        return this;
    }

    public DbQueryBuilder<T> Where(string column, string op, object? value)
    {
        if (!AllowedOperators.Contains(op))
            throw new ArgumentException($"Operator not allowed: {op}", nameof(op));
        if (value is null)
        {
            var rewritten = op switch
            {
                "=" => "IS NULL",
                "!=" or "<>" => "IS NOT NULL",
                _ => throw new ArgumentException($"Operator '{op}' cannot be used with null. Use IS or IS NOT.", nameof(op))
            };
            _whereConditions.Add($"{QuoteId(column)} {rewritten}");
        }
        else
        {
            _whereConditions.Add($"{QuoteId(column)} {op} ?");
            _whereParams.Add(value);
        }
        return this;
    }

    public DbQueryBuilder<T> OrWhere(string column, object? value)
    {
        if (value is null)
            _whereConditions.Add($"OR {QuoteId(column)} IS NULL");
        else
        {
            _whereConditions.Add($"OR {QuoteId(column)} = ?");
            _whereParams.Add(value);
        }
        return this;
    }

    public DbQueryBuilder<T> OrWhere(string column, string op, object? value)
    {
        if (!AllowedOperators.Contains(op))
            throw new ArgumentException($"Operator not allowed: {op}", nameof(op));
        if (value is null)
        {
            var rewritten = op switch
            {
                "=" => "IS NULL",
                "!=" or "<>" => "IS NOT NULL",
                _ => throw new ArgumentException($"Operator '{op}' cannot be used with null. Use IS or IS NOT.", nameof(op))
            };
            _whereConditions.Add($"OR {QuoteId(column)} {rewritten}");
        }
        else
        {
            _whereConditions.Add($"OR {QuoteId(column)} {op} ?");
            _whereParams.Add(value);
        }
        return this;
    }

    public DbQueryBuilder<T> WhereGroup(Action<DbQueryBuilder<T>> configure)
    {
        var group = new DbQueryBuilder<T>(_table, _dbService);
        configure(group);
        if (group._whereConditions.Count == 0) return this;
        _whereConditions.Add($"({group.JoinConditions()})");
        _whereParams.AddRange(group._whereParams);
        return this;
    }

    public DbQueryBuilder<T> WhereIn(string column, IEnumerable<object?> values)
    {
        var list = values.ToList();
        if (list.Count == 0)
        {
            _whereConditions.Add("1 = 0");
            return this;
        }
        var placeholders = string.Join(", ", list.Select(_ => "?"));
        _whereConditions.Add($"{QuoteId(column)} IN ({placeholders})");
        _whereParams.AddRange(list);
        return this;
    }

    public DbQueryBuilder<T> OrderBy(string column)
    {
        _orderByColumn = column;
        _orderByDesc = false;
        return this;
    }

    public DbQueryBuilder<T> OrderByDesc(string column)
    {
        _orderByColumn = column;
        _orderByDesc = true;
        return this;
    }

    public DbQueryBuilder<T> Limit(int n)
    {
        _limitValue = n;
        return this;
    }

    public DbQueryBuilder<T> Offset(int n)
    {
        _offsetValue = n;
        return this;
    }

    public async Task<List<T>> Get(CancellationToken ct = default)
    {
        EnsureNotConsumed();
        var sql = BuildSelectSql(-1);
        var result = await _dbService.QueryAsync(sql, new List<object?>(_whereParams), ct);
        if (result.HasError) return new List<T>();
        return typeof(T) == typeof(Dictionary<string, object?>)
            ? (List<T>)(object)result.ToMaps()
            : result.MapTo<T>();
    }

    public async Task<T?> First(CancellationToken ct = default)
    {
        EnsureNotConsumed();
        var sql = BuildSelectSql(1);
        var result = await _dbService.QueryAsync(sql, new List<object?>(_whereParams), ct);
        if (result.HasError) return default;
        var list = typeof(T) == typeof(Dictionary<string, object?>)
            ? (List<T>)(object)result.ToMaps()
            : result.MapTo<T>();
        return list.Count > 0 ? list[0] : default;
    }

    public async Task<int> Count(CancellationToken ct = default)
    {
        EnsureNotConsumed();
        var sb = new System.Text.StringBuilder($"SELECT COUNT(*) FROM {QuoteId(_table)}");
        if (_whereConditions.Count > 0)
            sb.Append($" WHERE {JoinConditions()}");

        var result = await _dbService.QueryAsync(sb.ToString(), new List<object?>(_whereParams), ct);
        if (result.HasError) return 0;
        if (result.Rows.Count == 0) return 0;

        var val = result.Rows[0].FirstOrDefault();
        if (val == null) return 0;
        if (val is IConvertible c) return Convert.ToInt32(c);
        throw new InvalidOperationException($"Unexpected COUNT result type: {val.GetType().Name} = '{val}'");
    }

    public async Task<DbResult> Insert(Dictionary<string, object?> data, CancellationToken ct = default)
    {
        EnsureNotConsumed();
        if (data.Count == 0)
            throw new ArgumentException("data cannot be empty.", nameof(data));
        var cols = data.Keys.ToList();
        var colList = string.Join(", ", cols.Select(QuoteId));
        var placeholders = string.Join(", ", cols.Select(_ => "?"));
        var sql = $"INSERT INTO {QuoteId(_table)} ({colList}) VALUES ({placeholders})";
        var parameters = cols.Select(c => data[c]).ToList();

        return await _dbService.QueryAsync(sql, parameters, ct);
    }

    public async Task<DbResult> Update(Dictionary<string, object?> data, CancellationToken ct = default)
    {
        EnsureNotConsumed();
        if (_whereConditions.Count == 0)
            throw new InvalidOperationException(
                "Update() without Where() would affect all rows. Use AppAmbitDb.Execute() for intentional full-table updates.");

        var cols = data.Keys.ToList();
        var setClauses = string.Join(", ", cols.Select(c => $"{QuoteId(c)} = ?"));
        var sql = $"UPDATE {QuoteId(_table)} SET {setClauses} WHERE {JoinConditions()}";
        var parameters = cols.Select(c => data[c]).Concat(_whereParams).ToList();

        return await _dbService.QueryAsync(sql, parameters, ct);
    }

    public async Task<DbResult> Delete(CancellationToken ct = default)
    {
        EnsureNotConsumed();
        if (_whereConditions.Count == 0)
            throw new InvalidOperationException(
                "Delete() without Where() would delete all rows. Use AppAmbitDb.Execute() for intentional full-table deletes.");

        var sql = $"DELETE FROM {QuoteId(_table)} WHERE {JoinConditions()}";
        return await _dbService.QueryAsync(sql, new List<object?>(_whereParams), ct);
    }

    private string BuildSelectSql(int overrideLimit)
    {
        var cols = _selectedColumns.Count > 0
            ? string.Join(", ", _selectedColumns.Select(QuoteId))
            : "*";

        var sb = new System.Text.StringBuilder($"SELECT {cols} FROM {QuoteId(_table)}");

        if (_whereConditions.Count > 0)
            sb.Append($" WHERE {JoinConditions()}");

        if (_orderByColumn != null)
        {
            sb.Append($" ORDER BY {QuoteId(_orderByColumn)}");
            if (_orderByDesc) sb.Append(" DESC");
        }

        int effectiveLimit = overrideLimit > 0 ? overrideLimit : _limitValue;
        if (effectiveLimit >= 0) sb.Append($" LIMIT {effectiveLimit}");
        if (_offsetValue >= 0) sb.Append($" OFFSET {_offsetValue}");

        return sb.ToString();
    }

    private string JoinConditions()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < _whereConditions.Count; i++)
        {
            var cond = _whereConditions[i];
            if (i == 0)
            {
                // Strip leading OR prefix on first condition (shouldn't happen in valid usage, but guard it)
                sb.Append(cond.StartsWith("OR ", StringComparison.OrdinalIgnoreCase)
                    ? cond[3..]
                    : cond);
            }
            else if (cond.StartsWith("OR ", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(' ');
                sb.Append(cond);
            }
            else
            {
                sb.Append(" AND ");
                sb.Append(cond);
            }
        }
        return sb.ToString();
    }

    private void EnsureNotConsumed()
    {
        if (_consumed)
            throw new InvalidOperationException(
                "DbQueryBuilder instance already executed. Create a new builder via AppAmbitDb.From().");
        _consumed = true;
    }

    private static string QuoteId(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Identifier cannot be null or empty.", nameof(name));
        if (name.Any(c => c < 32))
            throw new ArgumentException($"Identifier contains invalid characters.", nameof(name));
        return $"\"{name.Replace("\"", "\"\"")}\"";
    }
}

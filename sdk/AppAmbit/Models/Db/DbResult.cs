using System.Collections.Concurrent;
using System.Reflection;
using AppAmbit.Attributes;

namespace AppAmbit.Models.Db;

public sealed class DbResult
{
    public List<string> Columns { get; internal set; } = new();
    public List<List<object?>> Rows { get; internal set; } = new();
    public int RowsRead { get; internal set; }
    public int RowsWritten { get; internal set; }
    public string? Error { get; internal set; }

    public bool HasError => Error != null;
    public bool Succeeded => Error == null;

    internal DbResult() { }

    public List<Dictionary<string, object?>> ToMaps()
    {
        var result = new List<Dictionary<string, object?>>();
        foreach (var row in Rows)
        {
            var map = new Dictionary<string, object?>();
            for (int i = 0; i < Columns.Count && i < row.Count; i++)
                map[Columns[i]] = row[i];
            result.Add(map);
        }
        return result;
    }

    private static readonly ConcurrentDictionary<Type, Dictionary<string, MemberSetter>> _memberCache = new();

    private readonly record struct MemberSetter(MemberInfo Member, Type MemberType);

    private static Dictionary<string, MemberSetter> GetMemberMap(Type type) =>
        _memberCache.GetOrAdd(type, t =>
        {
            var map = new Dictionary<string, MemberSetter>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanWrite) continue;
                var attr = prop.GetCustomAttribute<DbColumnAttribute>();
                var name = attr?.Name ?? prop.Name;
                map[name] = new MemberSetter(prop, prop.PropertyType);
            }
            foreach (var field in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = field.GetCustomAttribute<DbColumnAttribute>();
                var name = attr?.Name ?? field.Name;
                map[name] = new MemberSetter(field, field.FieldType);
            }
            return map;
        });

    public List<T> MapTo<T>() where T : new()
    {
        var map = GetMemberMap(typeof(T));
        var result = new List<T>();
        foreach (var row in Rows)
        {
            var instance = new T();
            for (int colIdx = 0; colIdx < Columns.Count && colIdx < row.Count; colIdx++)
            {
                if (!map.TryGetValue(Columns[colIdx], out var setter)) continue;
                var value = CastValue(row[colIdx], setter.MemberType);
                if (setter.Member is PropertyInfo prop)
                    prop.SetValue(instance, value);
                else if (setter.Member is FieldInfo field)
                    field.SetValue(instance, value);
            }
            result.Add(instance);
        }
        return result;
    }

    private static object? CastValue(object? value, Type type)
    {
        if (value == null) return null;
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(string)) return value.ToString();
        if (underlying == typeof(int)) return value is IConvertible c ? Convert.ToInt32(c) : int.Parse(value.ToString()!);
        if (underlying == typeof(long)) return value is IConvertible c2 ? Convert.ToInt64(c2) : long.Parse(value.ToString()!);
        if (underlying == typeof(double)) return value is IConvertible c3 ? Convert.ToDouble(c3) : double.Parse(value.ToString()!);
        if (underlying == typeof(float)) return value is IConvertible c4 ? Convert.ToSingle(c4) : float.Parse(value.ToString()!);
        if (underlying == typeof(bool))
        {
            if (value is bool b) return b;
            if (value is IConvertible ic) return Convert.ToBoolean(ic); // handles SQLite 0/1
            return bool.Parse(value.ToString()!);
        }
        return value;
    }
}

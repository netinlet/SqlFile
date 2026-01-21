using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace SqlFile;

file static class SqlCache
{
    internal static readonly ConcurrentDictionary<Type, string> Cache = new();
}

public abstract class SqlQuery<T>
{
    private readonly Dictionary<string, string> _templates = new();
    private static readonly Regex _templateFields = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);
    private IReadOnlyList<string>? _templateFieldsCache;

    public SqlQuery<T> With(string name, string value)
    {
        _templates[name] = value;
        return this;
    }

    public SqlQuery<T> With(IEnumerable<KeyValuePair<string, string>> templates)
    {
        foreach (var kv in templates)
            _templates[kv.Key] = kv.Value;
        return this;
    }

    public IReadOnlyList<string> TemplateFields =>
        _templateFieldsCache ??= _templateFields
            .Matches(SqlCache.Cache.GetOrAdd(GetType(), LoadSqlFromResource))
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

    public async Task<List<T>> ExecuteAsync(DbContext db, params object[] parameters)
    {
        var sql = SqlCache.Cache.GetOrAdd(GetType(), LoadSqlFromResource);
        var interpolated = _templates.Aggregate(sql, (s, kv) => s.Replace($"{{{{{kv.Key}}}}}", kv.Value));
        return await db.Database.SqlQueryRaw<T>(interpolated, parameters).ToListAsync();
    }

    private static string LoadSqlFromResource(Type type)
    {
        var assembly = type.Assembly;
        var resourceName = assembly.GetManifestResourceNames()
                               .FirstOrDefault(n => n.EndsWith($"{type.Name}.sql", StringComparison.OrdinalIgnoreCase))
                           ?? throw new InvalidOperationException($"Embedded resource '{type.Name}.sql' not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

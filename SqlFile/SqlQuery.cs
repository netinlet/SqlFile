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
    private readonly Dictionary<string, string> _literals = new();
    private readonly Dictionary<string, object> _params = new();
    private static readonly Regex _templateFields = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);
    private IReadOnlyList<string>? _templateFieldsCache;

    public SqlQuery<T> WithLiteral(string name, string value)
    {
        _literals[name] = value;
        return this;
    }

    public SqlQuery<T> WithLiterals(IEnumerable<KeyValuePair<string, string>> literals)
    {
        foreach (var kv in literals)
            _literals[kv.Key] = kv.Value;
        return this;
    }

    public SqlQuery<T> WithParam(string name, object value)
    {
        _params[name] = value;
        return this;
    }

    public SqlQuery<T> WithParams(IEnumerable<KeyValuePair<string, object>> parameters)
    {
        foreach (var kv in parameters)
            _params[kv.Key] = kv.Value;
        return this;
    }

    public IReadOnlyList<string> TemplateFields =>
        _templateFieldsCache ??= _templateFields
            .Matches(SqlCache.Cache.GetOrAdd(GetType(), LoadSqlFromResource))
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

    public Task<List<T>> ExecuteAsync(DbContext db)
        => ExecuteAsync(db, Array.Empty<object>());

    public Task<List<T>> ExecuteAsync(DbContext db, params (string name, object value)[] parameters)
    {
        foreach (var (name, value) in parameters)
            _params[name] = value;
        return ExecuteAsync(db, Array.Empty<object>());
    }

    public async Task<List<T>> ExecuteAsync(DbContext db, params object[] parameters)
    {
        var sql = SqlCache.Cache.GetOrAdd(GetType(), LoadSqlFromResource);

        // Apply literal substitutions first
        var interpolated = _literals.Aggregate(sql, (s, kv) => s.Replace($"{{{{{kv.Key}}}}}", kv.Value));

        // Find remaining {{param}} placeholders that have values in _params
        var remainingPlaceholders = _templateFields.Matches(interpolated)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .Where(name => _params.ContainsKey(name))
            .ToList();

        if (remainingPlaceholders.Count == 0)
            return await db.Database.SqlQueryRaw<T>(interpolated, parameters).ToListAsync();

        // Find max existing positional parameter index
        var maxIndex = Regex.Matches(interpolated, @"\{(\d+)\}")
            .Select(m => int.Parse(m.Groups[1].Value))
            .DefaultIfEmpty(-1)
            .Max();

        // Assign indices to named params and replace {{param}} with {N}
        var paramIndex = maxIndex + 1;
        var paramValues = new List<object>();
        foreach (var name in remainingPlaceholders)
        {
            interpolated = interpolated.Replace($"{{{{{name}}}}}", $"{{{paramIndex++}}}");
            paramValues.Add(_params[name]);
        }

        // Combine original parameters with named param values
        var allArgs = new object[parameters.Length + paramValues.Count];
        parameters.CopyTo(allArgs, 0);
        paramValues.CopyTo(allArgs, parameters.Length);

        return await db.Database.SqlQueryRaw<T>(interpolated, allArgs).ToListAsync();
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

# SqlFile

A minimal library for co-locating SQL files with C# query classes using EF Core.

## Features

- Co-locate `.sql` files with `.cs` query classes
- File nesting in Visual Studio and Rider
- Safe parameterized values with `WithParam`
- Literal substitution for structural SQL with `WithLiteral`
- Fluent API or dictionary-based binding
- Introspect template fields at runtime
- SQL caching for performance

## Setup

Add the EmbeddedResource config to your `.csproj`:

```xml
<ItemGroup>
    <EmbeddedResource Include="**/*.sql">
        <DependentUpon>%(Filename).cs</DependentUpon>
        <LogicalName>%(Filename).sql</LogicalName>
    </EmbeddedResource>
</ItemGroup>
```

## Basic Usage

Create a query class and matching SQL file:

```csharp
// GetCustomersByRegion.cs
public record CustomerDto(int Id, string Name, string Region);

public class GetCustomersByRegion : SqlQuery<CustomerDto>;
```

```sql
-- GetCustomersByRegion.sql
SELECT Id, Name, Region
FROM Customers
WHERE Region = {0}
```

Execute with EF Core positional parameters:

```csharp
var customers = await new GetCustomersByRegion()
    .ExecuteAsync(dbContext, "West");
```

## Parameterized Values (Safe)

Use `{{placeholder}}` syntax with `WithParam` for SQL-injection-safe value parameters:

```sql
-- FilterCustomers.sql
SELECT Id, Name, Region
FROM Customers
WHERE Region = {{region}}
  AND Status = {{status}}
```

```csharp
var customers = await new FilterCustomers()
    .WithParam("region", userInput)    // Safe - becomes SQL parameter
    .WithParam("status", "Active")
    .ExecuteAsync(dbContext);
```

`WithParam` values are converted to proper SQL parameters (`@p0`, `@p1`, etc.), protecting against SQL injection.

## Inline Parameter Syntax

For simpler cases, pass parameters directly to `ExecuteAsync` using tuple syntax:

```csharp
var customers = await new FilterCustomers()
    .ExecuteAsync(dbContext, ("region", userInput), ("status", "Active"));
```

This is equivalent to chaining `WithParam` calls but more concise for straightforward queries.

Combine with `WithLiteral` for mixed structural/value queries:

```csharp
var customers = await new DynamicSearch()
    .WithLiteral("sortColumn", "Name DESC")
    .ExecuteAsync(dbContext, ("region", userInput));
```

## Literal Substitution (Structural)

Use `WithLiteral` for structural SQL parts that cannot be parameterized (column names, sort orders, complex expressions):

```sql
-- SearchCustomers.sql
SELECT Id, Name, Region
FROM Customers
WHERE {{activeFilter}}
  AND {{regionFilter}}
ORDER BY {{sortColumn}}
```

```csharp
var customers = await new SearchCustomers()
    .WithLiteral("activeFilter", "IsActive = 1")
    .WithLiteral("regionFilter", "Region = 'West'")
    .WithLiteral("sortColumn", "Name DESC")
    .ExecuteAsync(dbContext);
```

**Note:** Literal substitution does direct string replacement. Only use with trusted values, not user input.

## Mixed Usage

Combine safe parameters with literal substitution:

```sql
-- DynamicSearch.sql
SELECT Id, Name, Region
FROM Customers
WHERE Region = {{region}}
ORDER BY {{sortColumn}}
```

```csharp
var customers = await new DynamicSearch()
    .WithParam("region", userInput)         // Safe parameter
    .WithLiteral("sortColumn", "Name")      // Structural (validated/trusted)
    .ExecuteAsync(dbContext);
```

## Dictionary Binding

Pass multiple values at once:

```csharp
var parameters = new Dictionary<string, object>
{
    ["region"] = "West",
    ["status"] = "Active"
};

var literals = new Dictionary<string, string>
{
    ["sortColumn"] = "Name DESC"
};

var customers = await new SearchCustomers()
    .WithParams(parameters)
    .WithLiterals(literals)
    .ExecuteAsync(dbContext);
```

## Inspecting Template Fields

Use `TemplateFields` to discover placeholders in a query:

```csharp
var query = new SearchCustomers();
Console.WriteLine(string.Join(", ", query.TemplateFields));
// Output: activeFilter, regionFilter, sortColumn
```

## File Nesting

The `DependentUpon` metadata causes `.sql` files to nest under their matching `.cs` files in Visual Studio and Rider:

```
Queries/
├── GetCustomersByRegion.cs
│   └── GetCustomersByRegion.sql
└── SearchCustomers.cs
    └── SearchCustomers.sql
```

## API Reference

### `SqlQuery<T>`

| Member | Description |
|--------|-------------|
| `WithParam(string name, object value)` | Set a parameterized value (SQL-injection safe). Returns `this` for chaining. |
| `WithParams(IEnumerable<KeyValuePair<string, object>>)` | Set multiple parameterized values. Returns `this` for chaining. |
| `WithLiteral(string name, string value)` | Set a literal substitution value. Returns `this` for chaining. |
| `WithLiterals(IEnumerable<KeyValuePair<string, string>>)` | Set multiple literal values. Returns `this` for chaining. |
| `TemplateFields` | `IReadOnlyList<string>` of placeholder names found in the SQL file. |
| `ExecuteAsync(DbContext db, params object[] parameters)` | Execute the query and return `List<T>`. Positional parameters use EF Core's `{0}`, `{1}` syntax. |
| `ExecuteAsync(DbContext db, params (string, object)[] parameters)` | Execute with inline named parameters. Parameters are SQL-injection safe. |

# SqlFile

A minimal library for co-locating SQL files with C# query classes using EF Core.

## Features

- Co-locate `.sql` files with `.cs` query classes
- File nesting in Visual Studio and Rider
- Template substitution with `{{placeholder}}` syntax
- Fluent API or dictionary-based template binding
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

Execute with EF Core parameters:

```csharp
var customers = await new GetCustomersByRegion()
    .ExecuteAsync(dbContext, "West");
```

## Template Substitution

Use `{{placeholder}}` for structural SQL parts (column names, filters, sort orders):

```sql
-- SearchCustomers.sql
SELECT Id, Name, Region
FROM Customers
WHERE {{activeFilter}}
  AND {{regionFilter}}
ORDER BY {{sortColumn}}
```

### Fluent API

Chain `.With()` calls:

```csharp
var customers = await new SearchCustomers()
    .With("activeFilter", "IsActive = 1")
    .With("regionFilter", "Region = 'West'")
    .With("sortColumn", "Name")
    .ExecuteAsync(dbContext);
```

### Dictionary

Pass all templates at once:

```csharp
var templates = new Dictionary<string, string>
{
    ["activeFilter"] = "IsActive = 1",
    ["regionFilter"] = "Region = 'West'",
    ["sortColumn"] = "Name DESC"
};

var customers = await new SearchCustomers()
    .With(templates)
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
| `With(string name, string value)` | Set a template placeholder value. Returns `this` for chaining. |
| `With(IEnumerable<KeyValuePair<string, string>> templates)` | Set multiple template values from a dictionary. Returns `this` for chaining. |
| `TemplateFields` | `IReadOnlyList<string>` of placeholder names found in the SQL file. |
| `ExecuteAsync(DbContext db, params object[] parameters)` | Execute the query and return `List<T>`. Parameters use EF Core's `{0}`, `{1}` syntax. |

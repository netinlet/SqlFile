using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SqlFile.Tests.TestQueries;

namespace SqlFile.Tests;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<CustomerEntity>(e =>
        {
            e.ToTable("Customers");
            e.HasKey(c => c.Id);
        });
}

public class CustomerEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Region { get; set; } = "";
}

public class SqlQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContext _db;

    public SqlQueryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new TestDbContext(options);
        _db.Database.EnsureCreated();

        SeedData();
    }

    private void SeedData()
    {
        _db.Database.ExecuteSqlRaw("""
            INSERT INTO Customers (Id, Name, Region) VALUES (1, 'Alice', 'West');
            INSERT INTO Customers (Id, Name, Region) VALUES (2, 'Bob', 'East');
            INSERT INTO Customers (Id, Name, Region) VALUES (3, 'Charlie', 'West');
            """);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFilteredResults()
    {
        var query = new GetCustomers();

        var results = await query.ExecuteAsync(_db, "West");

        Assert.Equal(2, results.Count);
        Assert.All(results, c => Assert.Equal("West", c.Region));
    }

    [Fact]
    public async Task ExecuteAsync_WithTemplateReplacement_ReturnsResults()
    {
        var query = new SearchCustomers()
            .With("activeFilter", "1=1")
            .With("regionFilter", "Region = 'West'")
            .With("sortColumn", "Name");

        var results = await query.ExecuteAsync(_db);

        Assert.Equal(2, results.Count);
        Assert.Equal("Alice", results[0].Name);
        Assert.Equal("Charlie", results[1].Name);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoMatchingRows_ReturnsEmptyList()
    {
        var query = new GetCustomers();

        var results = await query.ExecuteAsync(_db, "North");

        Assert.Empty(results);
    }

    [Fact]
    public async Task With_IsChainable()
    {
        var query = new SearchCustomers()
            .With("activeFilter", "1=1")
            .With("regionFilter", "1=1")
            .With("sortColumn", "Id")
            .With("sortColumn", "Name DESC");

        var results = await query.ExecuteAsync(_db);

        Assert.Equal(3, results.Count);
        Assert.Equal("Charlie", results[0].Name);
    }

    [Fact]
    public async Task With_AcceptsDictionary()
    {
        var templates = new Dictionary<string, string>
        {
            ["activeFilter"] = "Name LIKE 'A%'",
            ["regionFilter"] = "1=1",
            ["sortColumn"] = "Id"
        };

        var results = await new SearchCustomers()
            .With(templates)
            .ExecuteAsync(_db);

        Assert.Single(results);
        Assert.Equal("Alice", results[0].Name);
    }

    [Fact]
    public void TemplateFields_ReturnsPlaceholderNames()
    {
        var query = new SearchCustomers();

        var fields = query.TemplateFields;

        Assert.Equal(3, fields.Count);
        Assert.Contains("activeFilter", fields);
        Assert.Contains("regionFilter", fields);
        Assert.Contains("sortColumn", fields);
    }

    [Fact]
    public void TemplateFields_ReturnsEmptyForNoPlaceholders()
    {
        var query = new GetCustomers();

        var fields = query.TemplateFields;

        Assert.Empty(fields);
    }

    [Fact]
    public async Task LoadSql_ThrowsWhenResourceNotFound()
    {
        var query = new MissingSqlQuery();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => query.ExecuteAsync(_db));

        Assert.Contains("not found", ex.Message);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}

public class MissingSqlQuery : SqlQuery<CustomerDto>;

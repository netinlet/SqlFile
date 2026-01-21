namespace SqlFile.Tests.TestQueries;

public record CustomerDto(int Id, string Name, string Region);

public class GetCustomers : SqlQuery<CustomerDto>;

using Microsoft.Extensions.Configuration;
using Npgsql;

namespace WrenchBox.Functions;

public sealed class CustomerDirectory
{
    private readonly string _connectionString;

    public CustomerDirectory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Customers")
            ?? configuration["Customers:ConnectionString"]
            ?? string.Empty;
    }

    public async Task<CustomerRecord?> FindByCpfAsync(string cpf, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            return null;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT "Id", "Status"
            FROM customers
            WHERE "Document" = @document AND "DocumentType" = 0
            LIMIT 1
            """,
            connection);
        command.Parameters.AddWithValue("document", cpf);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new CustomerRecord(reader.GetGuid(0), reader.GetInt32(1));
    }
}

using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace SISLAB.Infrastructure.Data;

/// <summary>
/// Fábrica de conexões Npgsql para o read-side Dapper.
/// Registrar como Scoped no DI — uma instância por requisição HTTP.
///
/// NOTA: A conexão só é aberta quando o handler Dapper invocar CreateConnection().
/// Se o PostgreSQL não estiver disponível, o health check reportará unhealthy,
/// mas o startup da API não é bloqueado por isso (ver SISLAB.Api/Program.cs).
/// </summary>
public sealed class DbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SislabDb")
            ?? throw new InvalidOperationException(
                "Connection string 'SislabDb' não encontrada. " +
                "Configure em appsettings.json ou variável de ambiente ConnectionStrings__SislabDb.");
    }

    /// <summary>
    /// Cria e abre uma nova conexão Npgsql.
    /// O chamador é responsável por dispor a conexão (using).
    /// </summary>
    public async Task<IDbConnection> CreateOpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}

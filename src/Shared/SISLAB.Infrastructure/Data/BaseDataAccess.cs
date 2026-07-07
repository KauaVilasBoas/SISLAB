using System.Data;

namespace SISLAB.Infrastructure.Data;

/// <summary>
/// Base para os QueryHandlers do read-side (Dapper). Convenção do projeto (ref. Branef.SGF):
/// a Query, o QueryHandler, o Result e o ResultItem ficam no MESMO arquivo <c>.cs</c>; o handler
/// herda desta classe e obtém a conexão via <see cref="OpenConnectionAsync"/>.
///
/// PADRÃO DE SQL (PostgreSQL — NÃO SQL Server):
/// <code>
/// WITH records AS (
///     SELECT s.id,
///            s.name,
///            ROW_NUMBER() OVER (ORDER BY s.name ASC) AS row_number,
///            COUNT(*)     OVER ()                     AS total_rows
///     FROM inventory.stock_item AS s
///     WHERE s.company_id = @CompanyId
///       AND (@Search IS NULL OR s.name ILIKE '%' || @Search || '%')
/// )
/// SELECT * FROM records
/// WHERE row_number BETWEEN @FirstResult AND @LastResult
/// ORDER BY row_number;
/// </code>
///
/// REGRAS DO DIALETO:
/// - Identificadores em lowercase/aspas duplas (nunca colchetes <c>[...]</c>).
/// - <c>ILIKE</c> em vez de <c>LIKE '%' + @x</c>; concatenação com <c>||</c>.
/// - Sem <c>WITH(NOLOCK)</c> (não existe no PostgreSQL).
/// - SEMPRE <c>WHERE company_id = @CompanyId</c> em tabelas multi-tenant (isolamento de tenant
///   no read-side; o EF cuida do write-side via global query filter).
/// - Paginação por <c>ROW_NUMBER()</c> + <c>COUNT(*) OVER()</c>, usando
///   <c>PagedQuery.FirstResult/LastResult</c>.
/// </summary>
public abstract class BaseDataAccess
{
    private readonly DbConnectionFactory _connectionFactory;

    protected BaseDataAccess(DbConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    /// <summary>
    /// Abre uma nova conexão Npgsql para a consulta Dapper.
    /// O chamador é responsável por dispor a conexão (padrão <c>using</c>).
    /// </summary>
    protected Task<IDbConnection> OpenConnectionAsync()
        => _connectionFactory.CreateOpenConnectionAsync();
}

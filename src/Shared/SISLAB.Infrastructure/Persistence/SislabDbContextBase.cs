using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace SISLAB.Infrastructure.Persistence;

/// <summary>
/// Base DbContext for all SISLAB modules.
/// Each module inherits this and registers its configurations via OnModelCreating.
/// Applies snake_case naming convention to all tables, columns, keys, and indexes.
/// </summary>
public abstract class SislabDbContextBase : DbContext
{
    protected SislabDbContextBase(DbContextOptions options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ApplySnakeCaseNamingConvention(modelBuilder);
    }

    private static void ApplySnakeCaseNamingConvention(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entity in modelBuilder.Model.GetEntityTypes())
        {
            if (entity.GetTableName() is { } tableName)
                entity.SetTableName(ToSnakeCase(tableName));

            foreach (IMutableProperty property in entity.GetProperties())
            {
                if (property.GetColumnName() is { } columnName)
                    property.SetColumnName(ToSnakeCase(columnName));
            }

            foreach (IMutableKey key in entity.GetKeys())
                key.SetName(ToSnakeCase(key.GetName() ?? string.Empty));

            foreach (IMutableForeignKey fk in entity.GetForeignKeys())
                fk.SetConstraintName(ToSnakeCase(fk.GetConstraintName() ?? string.Empty));

            foreach (IMutableIndex index in entity.GetIndexes())
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName() ?? string.Empty));
        }
    }

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;

        var snakeCaseChars = new List<char>(name.Length + 4);

        for (int i = 0; i < name.Length; i++)
        {
            char current = name[i];

            if (char.IsUpper(current))
            {
                bool isFirstChar = i == 0;
                bool previousIsLower = i > 0 && char.IsLower(name[i - 1]);
                bool nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);

                if (!isFirstChar && (previousIsLower || nextIsLower))
                    snakeCaseChars.Add('_');

                snakeCaseChars.Add(char.ToLowerInvariant(current));
            }
            else
            {
                snakeCaseChars.Add(current);
            }
        }

        return new string(snakeCaseChars.ToArray());
    }
}

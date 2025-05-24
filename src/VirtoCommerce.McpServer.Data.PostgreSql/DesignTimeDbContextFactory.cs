using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using VirtoCommerce.McpServer.Data.Repositories;

namespace VirtoCommerce.McpServer.Data.PostgreSql;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<McpServerDbContext>
{
    public McpServerDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<McpServerDbContext>();
        var connectionString = args.Length != 0 ? args[0] : "Server=localhost;Username=virto;Password=virto;Database=VirtoCommerce3;";

        builder.UseNpgsql(
            connectionString,
            options => options.MigrationsAssembly(typeof(PostgreSqlDataAssemblyMarker).Assembly.GetName().Name));

        return new McpServerDbContext(builder.Options);
    }
}

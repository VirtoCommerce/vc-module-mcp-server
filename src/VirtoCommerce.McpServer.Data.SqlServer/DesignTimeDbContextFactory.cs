using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using VirtoCommerce.McpServer.Data.Repositories;

namespace VirtoCommerce.McpServer.Data.SqlServer;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<McpServerDbContext>
{
    public McpServerDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<McpServerDbContext>();
        var connectionString = args.Length != 0 ? args[0] : "Server=(local);User=virto;Password=virto;Database=VirtoCommerce3;";

        builder.UseSqlServer(
            connectionString,
            options => options.MigrationsAssembly(typeof(SqlServerDataAssemblyMarker).Assembly.GetName().Name));

        return new McpServerDbContext(builder.Options);
    }
}

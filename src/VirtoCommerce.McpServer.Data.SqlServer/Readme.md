## Package manager
```
Add-Migration Initial -Context VirtoCommerce.McpServer.Data.Repositories.McpServerDbContext -Project VirtoCommerce.McpServer.Data.SqlServer -StartupProject VirtoCommerce.McpServer.Data.SqlServer -OutputDir Migrations -Verbose -Debug
```

### Entity Framework Core Commands
```
dotnet tool install --global dotnet-ef --version 8.*
```

**Generate Migrations**
```
dotnet ef migrations add Initial -- "{connection string}"
dotnet ef migrations add Update1 -- "{connection string}"
dotnet ef migrations add Update2 -- "{connection string}"
```
etc..

**Apply Migrations**
```
dotnet ef database update -- "{connection string}"
```

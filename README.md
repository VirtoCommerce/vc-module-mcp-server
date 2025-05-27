# VirtoCommerce MCP Server Module

A VirtoCommerce module that provides Model Context Protocol (MCP) server functionality, enabling AI agents like Claude Desktop to interact with VirtoCommerce platform APIs.

## Features

- **🤖 AI Integration**: Connect Claude Desktop and other MCP clients to VirtoCommerce
- **🔍 Customer Order Search**: Search orders by customer ID, email, order number, status, and more
- **🌐 Web-Based**: No separate console application required - runs within VirtoCommerce platform
- **🚀 SSE Transport**: Uses Server-Sent Events for real-time communication
- **🔧 Easy Setup**: Minimal configuration required

## Architecture

```
Claude Desktop ←→ mcp-remote proxy ←→ VirtoCommerce Web Module
     (MCP Protocol)        (HTTP)           (Business Logic)
```

## Quick Start

### 1. Install the Module

1. Build the solution:
   ```bash
   dotnet build
   ```

2. Deploy the `VirtoCommerce.McpServer.Web` module to your VirtoCommerce platform

### 2. Configure Claude Desktop

1. Install the mcp-remote proxy:
   ```bash
   npm install -g mcp-remote
   ```

2. Update your Claude Desktop configuration file (`claude_desktop_config.json`):
```json
{
  "mcpServers": {
    "virtocommerce": {
         "command": "npx",
         "args": [
           "mcp-remote",
           "http://localhost:5000/api/mcp"
         ]
    }
  }
}
```

3. Restart Claude Desktop

### 3. Test the Integration

1. Start your VirtoCommerce platform (ensure it's running on `http://localhost:5000`)

2. In Claude Desktop, you should now have access to the `search_customer_orders` tool

3. Test with a query like: *"Search for orders from customer email john@example.com"*

## Available Tools

### search_customer_orders

Search customer orders by various criteria.

**Parameters:**
- `customerId` (string, optional): Customer ID to search orders for
- `customerEmail` (string, optional): Customer email to search orders for
- `orderNumber` (string, optional): Order number to search for
- `status` (string, optional): Order status to filter by
- `storeId` (string, optional): Store ID to filter orders
- `startDate` (string, optional): Start date for order search (ISO 8601 format)
- `endDate` (string, optional): End date for order search (ISO 8601 format)
- `take` (integer, optional): Maximum number of orders to return (default: 20)
- `skip` (integer, optional): Number of orders to skip for pagination (default: 0)

## API Endpoints

The module exposes the following endpoints:

- `POST /api/mcp` - Main MCP protocol endpoint (handles initialize, tools/list, tools/call)
- `POST /api/mcp/sse` - Alternative MCP endpoint (same as above)
- `GET /api/mcp/status` - Server status and capabilities
- `GET /api/mcp/tools` - List available tools
- `POST /api/mcp/tools/call` - Execute tools directly

## Development

### Adding New Tools

1. Add a new method to the `VirtoCommerceMcpTools` class in `McpServerController.cs`
2. Decorate with `[McpServerTool, Description("Tool description")]`
3. Add parameter descriptions using `[Description("Parameter description")]`
4. Implement the business logic by calling `_mcpServerService.ExecuteToolAsync()`

Example:
```csharp
[McpServerTool, Description("Get customer information")]
public async Task<string> GetCustomer(
    [Description("Customer ID")] string customerId,
    CancellationToken cancellationToken = default)
{
    var arguments = new Dictionary<string, object> { ["customerId"] = customerId };
    var result = await _mcpServerService.ExecuteToolAsync("get_customer", arguments, cancellationToken);
    return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
}
   ```

### Project Structure

```
src/
├── VirtoCommerce.McpServer.Core/          # Core interfaces and services
├── VirtoCommerce.McpServer.Data/          # Data access layer
├── VirtoCommerce.McpServer.Data.MySql/    # MySQL data provider
├── VirtoCommerce.McpServer.Data.PostgreSql/ # PostgreSQL data provider
├── VirtoCommerce.McpServer.Data.SqlServer/   # SQL Server data provider
└── VirtoCommerce.McpServer.Web/           # Web module (controllers, MCP endpoints)
```

## Troubleshooting

### Common Issues

1. **Claude Desktop doesn't see the tools**
   - Verify VirtoCommerce is running on `http://localhost:5000`
   - Check that `mcp-remote` is installed: `npm list -g mcp-remote`
   - Restart Claude Desktop after configuration changes

2. **SSE connection fails**
   - Check firewall settings
   - Verify the `/api/mcp/sse` endpoint is accessible
   - Check VirtoCommerce logs for errors

3. **Tool execution errors**
   - Ensure the VirtoCommerce module is properly loaded
   - Check that required permissions are configured
   - Verify database connectivity

### Testing Endpoints

Test the status endpoint:
```bash
curl http://localhost:5000/api/mcp/status
```

Test tool listing:
```bash
curl http://localhost:5000/api/mcp/tools
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`
3. Commit your changes: `git commit -m 'Add amazing feature'`
4. Push to the branch: `git push origin feature/amazing-feature`
5. Open a Pull Request

## Support

For support, please create an issue in the GitHub repository or contact the VirtoCommerce team.

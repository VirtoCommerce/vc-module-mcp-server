using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<VirtoCommerceMcpTools>();

// Register HTTP client for calling VirtoCommerce APIs
builder.Services.AddHttpClient<VirtoCommerceApiClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5000/"); // VirtoCommerce platform URL
});

await builder.Build().RunAsync();

/// <summary>
/// VirtoCommerce MCP Tools that connect to VirtoCommerce platform APIs
/// </summary>
[McpServerToolType]
public class VirtoCommerceMcpTools
{
    private readonly VirtoCommerceApiClient _apiClient;
    private readonly ILogger<VirtoCommerceMcpTools> _logger;

    public VirtoCommerceMcpTools(VirtoCommerceApiClient apiClient, ILogger<VirtoCommerceMcpTools> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    [McpServerTool, Description("Search customer orders by various criteria")]
    public async Task<string> SearchCustomerOrders(
        [Description("Customer ID to search orders for")] string? customerId = null,
        [Description("Customer email to search orders for")] string? customerEmail = null,
        [Description("Order number to search for")] string? orderNumber = null,
        [Description("Order status to filter by")] string? status = null,
        [Description("Store ID to filter orders")] string? storeId = null,
        [Description("Start date for order search (ISO 8601 format)")] string? startDate = null,
        [Description("End date for order search (ISO 8601 format)")] string? endDate = null,
        [Description("Maximum number of orders to return (default: 20)")] int take = 20,
        [Description("Number of orders to skip for pagination (default: 0)")] int skip = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Searching customer orders with criteria: customerId={CustomerId}, customerEmail={CustomerEmail}, orderNumber={OrderNumber}, status={Status}",
                customerId, customerEmail, orderNumber, status);

            var result = await _apiClient.SearchCustomerOrdersAsync(
                customerId, customerEmail, orderNumber, status,
                storeId, startDate, endDate, take, skip, cancellationToken);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching customer orders");
            return $"Error searching customer orders: {ex.Message}";
        }
    }
}

/// <summary>
/// HTTP client for calling VirtoCommerce platform APIs
/// </summary>
public class VirtoCommerceApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VirtoCommerceApiClient> _logger;

    public VirtoCommerceApiClient(HttpClient httpClient, ILogger<VirtoCommerceApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<object> SearchCustomerOrdersAsync(
        string? customerId, string? customerEmail, string? orderNumber, string? status,
        string? storeId, string? startDate, string? endDate, int take, int skip,
        CancellationToken cancellationToken)
    {
        var queryParams = new List<string>();

        if (!string.IsNullOrEmpty(customerId)) queryParams.Add($"customerId={Uri.EscapeDataString(customerId)}");
        if (!string.IsNullOrEmpty(customerEmail)) queryParams.Add($"customerEmail={Uri.EscapeDataString(customerEmail)}");
        if (!string.IsNullOrEmpty(orderNumber)) queryParams.Add($"orderNumber={Uri.EscapeDataString(orderNumber)}");
        if (!string.IsNullOrEmpty(status)) queryParams.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrEmpty(storeId)) queryParams.Add($"storeId={Uri.EscapeDataString(storeId)}");
        if (!string.IsNullOrEmpty(startDate)) queryParams.Add($"startDate={Uri.EscapeDataString(startDate)}");
        if (!string.IsNullOrEmpty(endDate)) queryParams.Add($"endDate={Uri.EscapeDataString(endDate)}");
        queryParams.Add($"take={take}");
        queryParams.Add($"skip={skip}");

        var queryString = string.Join("&", queryParams);
        var url = $"api/mcp/tools/call?{queryString}";

        _logger.LogDebug("Calling VirtoCommerce API: {Url}", url);

        var requestBody = new
        {
            Name = "search_customer_orders",
            Arguments = new Dictionary<string, object?>
            {
                ["customerId"] = customerId,
                ["customerEmail"] = customerEmail,
                ["orderNumber"] = orderNumber,
                ["status"] = status,
                ["storeId"] = storeId,
                ["startDate"] = startDate,
                ["endDate"] = endDate,
                ["take"] = take,
                ["skip"] = skip
            }
        };

        var response = await _httpClient.PostAsJsonAsync("api/mcp/tools/call", requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<object>(responseContent) ?? new { error = "Empty response" };
    }
}

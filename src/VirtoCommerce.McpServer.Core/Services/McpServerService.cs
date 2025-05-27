using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.McpServer.Core.Models;

namespace VirtoCommerce.McpServer.Core.Services
{
    /// <summary>
    /// MCP Server service that runs as part of VirtoCommerce platform
    /// Exposes discovered APIs as MCP tools through HTTP endpoints
    /// </summary>
    public class McpServerService : IHostedService
    {
        private readonly ILogger<McpServerService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IApiDiscoveryService _apiDiscoveryService;
        private readonly IModuleManifestService _moduleManifestService;
        private readonly IXmlDocumentationService _xmlDocumentationService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private List<ApiEndpoint> _discoveredEndpoints;

        public McpServerService(
            ILogger<McpServerService> logger,
            IServiceProvider serviceProvider,
            IApiDiscoveryService apiDiscoveryService,
            IModuleManifestService moduleManifestService,
            IXmlDocumentationService xmlDocumentationService,
            IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _apiDiscoveryService = apiDiscoveryService;
            _moduleManifestService = moduleManifestService;
            _xmlDocumentationService = xmlDocumentationService;
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
                {
                // Discover API endpoints from MCP-enabled modules
                _discoveredEndpoints = _apiDiscoveryService.DiscoverAllApiEndpoints().ToList();

                // Add our custom customer order search tool
                AddCustomerOrderSearchTool();

                _logger.LogInformation("MCP Server started: Discovered {EndpointCount} API endpoints for MCP exposure",
                    _discoveredEndpoints.Count);

                // Log discovered tools for debugging
                foreach (var endpoint in _discoveredEndpoints)
                {
                    _logger.LogDebug("MCP Tool available: {ToolName} -> {Method} {Route} ({Description})",
                        endpoint.ToolName, endpoint.Method, endpoint.Route, endpoint.Description);
                }

                _logger.LogInformation("MCP Server service started successfully as VirtoCommerce module with {EndpointCount} tools",
                    _discoveredEndpoints.Count);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start MCP server service");
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("MCP Server service stopped");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Add custom customer order search tool implementation
        /// </summary>
        private void AddCustomerOrderSearchTool()
        {
            var orderSearchTool = new ApiEndpoint
            {
                ToolName = "search_customer_orders",
                Description = "Search for customer orders by various criteria such as customer ID, order number, date range, status, and more",
                Method = "POST",
                Route = "/api/order/customerOrders/search",
                ModuleId = "VirtoCommerce.Orders",
                ControllerName = "OrderModule",
                ActionName = "SearchCustomerOrders",
                Parameters = new Dictionary<string, object>
                {
                    ["customerId"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Single customer ID to search orders for",
                        ["required"] = false
                    },
                    ["customerIds"] = new Dictionary<string, object>
                    {
                        ["type"] = "array",
                        ["items"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["description"] = "Array of customer IDs to search orders for",
                        ["required"] = false
                    },
                    ["number"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Order number to search for",
                        ["required"] = false
                    },
                    ["numbers"] = new Dictionary<string, object>
                    {
                        ["type"] = "array",
                        ["items"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["description"] = "Array of order numbers to search for",
                        ["required"] = false
                    },
                    ["status"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Single order status to filter by",
                        ["required"] = false
                    },
                    ["statuses"] = new Dictionary<string, object>
                    {
                        ["type"] = "array",
                        ["items"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["description"] = "Array of order statuses to filter by",
                        ["required"] = false
                    },
                    ["storeIds"] = new Dictionary<string, object>
                    {
                        ["type"] = "array",
                        ["items"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["description"] = "Array of store IDs to filter orders",
                        ["required"] = false
                    },
                    ["organizationId"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Organization ID to filter orders",
                        ["required"] = false
                    },
                    ["employeeId"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Employee ID to filter orders",
                        ["required"] = false
                    },
                    ["startDate"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Start date for order search (ISO 8601 format)",
                        ["required"] = false
                    },
                    ["endDate"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "End date for order search (ISO 8601 format)",
                        ["required"] = false
                    },
                    ["withPrototypes"] = new Dictionary<string, object>
                    {
                        ["type"] = "boolean",
                        ["description"] = "Include prototype orders in search",
                        ["required"] = false
                    },
                    ["onlyRecurring"] = new Dictionary<string, object>
                    {
                        ["type"] = "boolean",
                        ["description"] = "Search only recurring orders created by subscription",
                        ["required"] = false
                    },
                    ["subscriptionId"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Search orders with given subscription ID",
                        ["required"] = false
                    },
                    ["keyword"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Keyword to search for in orders",
                        ["required"] = false
                    },
                    ["take"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = "Number of orders to return (default: 20)",
                        ["required"] = false
                    },
                    ["skip"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = "Number of orders to skip for pagination (default: 0)",
                        ["required"] = false
                    },
                    ["sort"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Sort expression (e.g., 'createdDate:desc')",
                        ["required"] = false
                    }
                },
                Security = new SecurityInfo
                {
                    RequiresAuthentication = true,
                    AllowAnonymous = false,
                    RequiredPermissions = new List<string> { "order:read" },
                    RequiredRoles = new List<string> { "Customer", "Manager", "Administrator" }
                }
            };

            _discoveredEndpoints.Add(orderSearchTool);
        }

        /// <summary>
        /// Get all discovered endpoints as MCP tools
        /// Called by MCP controller endpoints
        /// </summary>
        public IEnumerable<ApiEndpoint> GetDiscoveredEndpoints()
        {
            return _discoveredEndpoints ?? Enumerable.Empty<ApiEndpoint>();
        }

        /// <summary>
        /// Get MCP tools in the format expected by MCP protocol
        /// </summary>
        public IEnumerable<object> GetMcpTools()
        {
            return (_discoveredEndpoints ?? Enumerable.Empty<ApiEndpoint>())
                .Select(CreateMcpTool);
        }

        /// <summary>
        /// Execute an MCP tool by name with provided arguments
        /// </summary>
        public Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                var endpoint = _discoveredEndpoints?.FirstOrDefault(e => e.ToolName == toolName);
                if (endpoint == null)
                {
                    throw new ArgumentException($"Unknown tool: '{toolName}'");
                }

                // Handle our custom order search tool
                if (toolName == "search_customer_orders")
                {
                    return ExecuteCustomerOrderSearchAsync(arguments, cancellationToken);
                }

                return InvokeApiEndpointAsync(endpoint, arguments, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing MCP tool {ToolName}", toolName);
                throw;
            }
        }

        /// <summary>
        /// Execute customer order search using VirtoCommerce Order API via HTTP
        /// </summary>
        private async Task<object> ExecuteCustomerOrderSearchAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Executing customer order search with arguments: {Arguments}",
                    JsonSerializer.Serialize(arguments));

                // Build the search criteria object matching the Swagger schema
                var searchCriteria = new
                {
                    customerId = GetArgumentValue<string>(arguments, "customerId"),
                    customerIds = GetArgumentArrayValue<string>(arguments, "customerIds"),
                    number = GetArgumentValue<string>(arguments, "number"),
                    numbers = GetArgumentArrayValue<string>(arguments, "numbers"),
                    status = GetArgumentValue<string>(arguments, "status"),
                    statuses = GetArgumentArrayValue<string>(arguments, "statuses"),
                    storeIds = GetArgumentArrayValue<string>(arguments, "storeIds"),
                    organizationId = GetArgumentValue<string>(arguments, "organizationId"),
                    employeeId = GetArgumentValue<string>(arguments, "employeeId"),
                    startDate = ParseDateArgument(arguments, "startDate")?.ToString("O"),
                    endDate = ParseDateArgument(arguments, "endDate")?.ToString("O"),
                    withPrototypes = GetArgumentValue<bool?>(arguments, "withPrototypes"),
                    onlyRecurring = GetArgumentValue<bool?>(arguments, "onlyRecurring"),
                    subscriptionId = GetArgumentValue<string>(arguments, "subscriptionId"),
                    keyword = GetArgumentValue<string>(arguments, "keyword"),
                    take = GetArgumentValue<int?>(arguments, "take") ?? 20,
                    skip = GetArgumentValue<int?>(arguments, "skip") ?? 0,
                    sort = GetArgumentValue<string>(arguments, "sort")
                };

                // Create HTTP client and prepare request
                using var httpClient = _httpClientFactory.CreateClient();

                // Configure authentication and base URL
                var (baseUrl, requestUrl) = ConfigureAuthentication(httpClient, "/api/order/customerOrders/search");

                // Serialize search criteria to JSON
                var jsonContent = JsonSerializer.Serialize(searchCriteria, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogDebug("Making HTTP POST request to {RequestUrl} with criteria: {Criteria}", requestUrl, jsonContent);

                // Make the HTTP request (use requestUrl which may include query string API key)
                var response = await httpClient.PostAsync(requestUrl, content, cancellationToken);

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    // Parse the successful response
                    var apiResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                    var result = new
                    {
                        success = true,
                        message = "Customer orders retrieved successfully",
                        data = apiResponse,
                        metadata = new
                        {
                            timestamp = DateTime.UtcNow.ToString("O"),
                            toolName = "search_customer_orders",
                            httpStatusCode = (int)response.StatusCode,
                            searchCriteria = searchCriteria
                        }
                    };

                    // Extract count for logging
                    var totalCount = 0;
                    if (apiResponse.TryGetProperty("totalCount", out var countElement))
                    {
                        totalCount = countElement.GetInt32();
                    }

                    _logger.LogInformation("Customer order search completed successfully. Found {OrderCount} orders", totalCount);
                    return result;
                }
                else
                {
                    _logger.LogWarning("Customer order search failed with status {StatusCode}: {Response}",
                        response.StatusCode, responseContent);

                    return new
                    {
                        success = false,
                        error = $"API request failed with status {response.StatusCode}",
                        details = responseContent,
                        toolName = "search_customer_orders",
                        timestamp = DateTime.UtcNow.ToString("O"),
                        httpStatusCode = (int)response.StatusCode
                    };
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error executing customer order search");
                return new
                {
                    success = false,
                    error = "HTTP request failed: " + ex.Message,
                    toolName = "search_customer_orders",
                    timestamp = DateTime.UtcNow.ToString("O"),
                    errorType = "HttpRequestException"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing customer order search");
                return new
                {
                    success = false,
                    error = ex.Message,
                    toolName = "search_customer_orders",
                    timestamp = DateTime.UtcNow.ToString("O"),
                    errorType = ex.GetType().Name
                };
            }
        }

        private T GetArgumentValue<T>(Dictionary<string, object> arguments, string key)
        {
            if (arguments.TryGetValue(key, out var value) && value != null)
            {
                if (value is T directValue)
                    return directValue;

                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return default(T);
                }
            }
            return default(T);
        }

        private T[] GetArgumentArrayValue<T>(Dictionary<string, object> arguments, string key)
        {
            if (arguments.TryGetValue(key, out var value) && value != null)
            {
                if (value is T[] directArray)
                    return directArray;

                if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<T>();
                    foreach (var item in jsonElement.EnumerateArray())
                    {
                        try
                        {
                            if (typeof(T) == typeof(string))
                            {
                                list.Add((T)(object)item.GetString());
                            }
                            else
                            {
                                var converted = (T)Convert.ChangeType(item.ToString(), typeof(T));
                                list.Add(converted);
                            }
                        }
                        catch
                        {
                            // Skip invalid items
                        }
                    }
                    return list.ToArray();
                }

                if (value is IEnumerable<object> enumerable)
                {
                    var list = new List<T>();
                    foreach (var item in enumerable)
                    {
                        try
                        {
                            var converted = (T)Convert.ChangeType(item, typeof(T));
                            list.Add(converted);
                        }
                        catch
                        {
                            // Skip invalid items
                        }
                    }
                    return list.ToArray();
                }
            }
            return null;
        }

        private DateTime? ParseDateArgument(Dictionary<string, object> arguments, string key)
        {
            var dateStr = GetArgumentValue<string>(arguments, key);
            if (DateTime.TryParse(dateStr, out var date))
                return date;
            return null;
        }

        private object CreateMcpTool(ApiEndpoint endpoint)
            {
            var inputSchema = CreateInputSchema(endpoint);

            return new
            {
                name = endpoint.ToolName,
                description = endpoint.Description ?? $"{endpoint.Method} {endpoint.Route}",
                inputSchema = inputSchema
            };
        }

        private object CreateInputSchema(ApiEndpoint endpoint)
        {
            var schema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>(),
                ["required"] = new List<string>()
            };

            var properties = (Dictionary<string, object>)schema["properties"];
            var required = (List<string>)schema["required"];

            // Add parameters from the endpoint metadata
            foreach (var (paramName, paramInfo) in endpoint.Parameters)
            {
                properties[paramName] = paramInfo;

                if (paramInfo is Dictionary<string, object> paramDict &&
                    paramDict.TryGetValue("required", out var isRequired) &&
                    isRequired is true)
                {
                    required.Add(paramName);
                }
            }

            // Add route parameters
            var routeParams = ExtractRouteParameters(endpoint.Route);
            foreach (var routeParam in routeParams)
            {
                if (!properties.ContainsKey(routeParam))
                {
                    properties[routeParam] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = $"Route parameter: {routeParam}"
                    };
                    required.Add(routeParam);
                }
            }

            return schema;
        }

        private List<string> ExtractRouteParameters(string route)
        {
            var parameters = new List<string>();
            var matches = System.Text.RegularExpressions.Regex.Matches(route, @"\{([^}]+)\}");

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var paramName = match.Groups[1].Value;
                if (paramName.Contains(":"))
                {
                    paramName = paramName.Split(':')[0];
                }
                if (paramName.EndsWith("?"))
                {
                    paramName = paramName.TrimEnd('?');
                }
                parameters.Add(paramName);
            }

            return parameters;
        }

        private Task<object> InvokeApiEndpointAsync(ApiEndpoint endpoint, Dictionary<string, object> arguments, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Executing MCP tool: {ToolName} ({Method} {Route})",
                    endpoint.ToolName, endpoint.Method, endpoint.Route);

                // TODO: Implement actual API invocation through VirtoCommerce's controller infrastructure
                // This would involve:
                // 1. Creating HTTP context
                // 2. Calling the actual controller method
                // 3. Handling authentication/authorization through VirtoCommerce
                // 4. Returning the actual API response

                // For now, return structured information about the tool execution
                var response = new
                {
                    success = true,
                    message = $"MCP tool '{endpoint.ToolName}' executed successfully",
                    endpoint = new
                    {
                        toolName = endpoint.ToolName,
                        method = endpoint.Method,
                        route = endpoint.Route,
                        module = endpoint.ModuleId,
                        controller = endpoint.ControllerName,
                        action = endpoint.ActionName
                    },
                    arguments = arguments,
                    security = new
                    {
                        requiresAuthentication = endpoint.Security.RequiresAuthentication,
                        allowAnonymous = endpoint.Security.AllowAnonymous,
                        requiredPermissions = endpoint.Security.RequiredPermissions,
                        requiredRoles = endpoint.Security.RequiredRoles
                    },
                    timestamp = DateTime.UtcNow.ToString("O"),
                    note = "This is a simulation. Actual API invocation will be implemented to call VirtoCommerce controllers directly."
                };

                return Task.FromResult<object>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invoking API endpoint {ToolName}", endpoint.ToolName);

                var errorResponse = new
                {
                    success = false,
                    error = ex.Message,
                    toolName = endpoint.ToolName,
                    timestamp = DateTime.UtcNow.ToString("O")
                };

                return Task.FromResult<object>(errorResponse);
            }
        }

        /// <summary>
        /// Configure authentication for API requests
        /// Supports Bearer tokens, API keys (header or query string), and fallback to request headers
        /// </summary>
        /// <param name="httpClient">HTTP client to configure</param>
        /// <param name="endpoint">API endpoint path</param>
        /// <returns>Tuple of (baseUrl, requestUrl with potential query string)</returns>
        private (string baseUrl, string requestUrl) ConfigureAuthentication(HttpClient httpClient, string endpoint)
        {
            var request = _httpContextAccessor.HttpContext?.Request;

            // Environment variables for MCP client configuration
            var apiKey = Environment.GetEnvironmentVariable("VIRTOCOMMERCE_API_KEY");
            var apiKeyMode = Environment.GetEnvironmentVariable("VIRTOCOMMERCE_API_KEY_MODE") ?? "header"; // "header" or "query"
            var bearerToken = Environment.GetEnvironmentVariable("VIRTOCOMMERCE_API_TOKEN");
            var username = Environment.GetEnvironmentVariable("VIRTOCOMMERCE_USERNAME");
            var password = Environment.GetEnvironmentVariable("VIRTOCOMMERCE_PASSWORD");
            var apiUrl = Environment.GetEnvironmentVariable("VIRTOCOMMERCE_API_URL");

            // Determine base URL
            string baseUrl;
            if (!string.IsNullOrEmpty(apiUrl))
            {
                baseUrl = apiUrl;
                httpClient.BaseAddress = new Uri(baseUrl);
                _logger.LogDebug("Using API URL from environment: {ApiUrl}", apiUrl);
            }
            else if (request != null)
            {
                baseUrl = $"{request.Scheme}://{request.Host}";
                httpClient.BaseAddress = new Uri(baseUrl);
                _logger.LogDebug("Using API URL from current request: {ApiUrl}", baseUrl);
            }
            else
            {
                baseUrl = "http://localhost:5000";
                httpClient.BaseAddress = new Uri(baseUrl);
                _logger.LogDebug("Using fallback API URL: {ApiUrl}", baseUrl);
            }

            // Configure authentication in priority order
            bool authConfigured = false;
            string requestUrl = endpoint;

            // Priority 1: API Key (from environment - for MCP client configuration)
            if (!string.IsNullOrEmpty(apiKey))
            {
                if (apiKeyMode.ToLowerInvariant() == "query")
                {
                    // Add API key as query string parameter
                    var separator = endpoint.Contains('?') ? "&" : "?";
                    requestUrl = $"{endpoint}{separator}api_key={Uri.EscapeDataString(apiKey)}";
                    _logger.LogDebug("Using API key authentication via query string");
                }
                else
                {
                    // Add API key as header (default)
                    httpClient.DefaultRequestHeaders.Add("api_key", apiKey);
                    _logger.LogDebug("Using API key authentication via header");
                }
                authConfigured = true;
            }

            // Priority 2: Bearer Token (from environment - for MCP client configuration)
            if (!authConfigured && !string.IsNullOrEmpty(bearerToken))
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {bearerToken}");
                _logger.LogDebug("Using Bearer token authentication from environment");
                authConfigured = true;
            }

            // Priority 3: Username/Password (get token dynamically)
            if (!authConfigured && !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                // Note: In a real implementation, you might want to cache tokens
                // For now, we'll attempt to get a token but won't block the request if it fails
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var token = await GetAccessTokenAsync(username, password);
                        if (!string.IsNullOrEmpty(token))
                        {
                            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                            _logger.LogDebug("Using Bearer token obtained from username/password");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to obtain access token from username/password");
                    }
                });
                authConfigured = true;
            }

            // Priority 4: Current request headers (for direct API calls)
            if (!authConfigured && request != null)
            {
                // Check for API key in current request
                if (request.Headers.ContainsKey("api_key"))
                {
                    httpClient.DefaultRequestHeaders.Add("api_key", request.Headers["api_key"].ToString());
                    _logger.LogDebug("Using API key from current request header");
                    authConfigured = true;
                }
                else if (request.Query.ContainsKey("api_key"))
                {
                    var separator = endpoint.Contains('?') ? "&" : "?";
                    requestUrl = $"{endpoint}{separator}api_key={Uri.EscapeDataString(request.Query["api_key"])}";
                    _logger.LogDebug("Using API key from current request query string");
                    authConfigured = true;
                }
                else if (request.Headers.ContainsKey("Authorization"))
                {
                    httpClient.DefaultRequestHeaders.Add("Authorization", request.Headers["Authorization"].ToString());
                    _logger.LogDebug("Using Authorization header from current request");
                    authConfigured = true;
                }
            }

            if (!authConfigured)
            {
                _logger.LogWarning("No authentication configured. API calls may fail with 401 Unauthorized.");
            }

            return (baseUrl, requestUrl);
        }

        /// <summary>
        /// Get access token using username and password
        /// </summary>
        private async Task<string> GetAccessTokenAsync(string username, string password)
        {
            try
            {
                using var tokenClient = _httpClientFactory.CreateClient();
                var apiUrl = Environment.GetEnvironmentVariable("VIRTOCOMMERCE_API_URL") ?? "http://localhost:5000";
                tokenClient.BaseAddress = new Uri(apiUrl);

                var tokenRequest = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "password"),
                    new KeyValuePair<string, string>("username", username),
                    new KeyValuePair<string, string>("password", password),
                    new KeyValuePair<string, string>("scope", "offline_access")
                });

                var response = await tokenClient.PostAsync("/connect/token", tokenRequest);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var tokenResponse = JsonSerializer.Deserialize<JsonElement>(content);

                    if (tokenResponse.TryGetProperty("access_token", out var tokenElement))
                    {
                        return tokenElement.GetString();
                    }
                }

                _logger.LogWarning("Failed to obtain access token: {StatusCode}", response.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obtaining access token");
                return null;
            }
        }
    }
}

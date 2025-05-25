using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
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

        private List<ApiEndpoint> _discoveredEndpoints;

        public McpServerService(
            ILogger<McpServerService> logger,
            IServiceProvider serviceProvider,
            IApiDiscoveryService apiDiscoveryService,
            IModuleManifestService moduleManifestService,
            IXmlDocumentationService xmlDocumentationService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _apiDiscoveryService = apiDiscoveryService;
            _moduleManifestService = moduleManifestService;
            _xmlDocumentationService = xmlDocumentationService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Discover API endpoints from MCP-enabled modules
                _discoveredEndpoints = _apiDiscoveryService.DiscoverAllApiEndpoints().ToList();
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

                return InvokeApiEndpointAsync(endpoint, arguments, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing MCP tool {ToolName}", toolName);
                throw;
            }
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
    }
}

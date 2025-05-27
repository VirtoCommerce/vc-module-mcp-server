#nullable enable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text;
using System.Threading;
using VirtoCommerce.McpServer.Core;
using VirtoCommerce.McpServer.Core.Services;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol.Types;
using System.ComponentModel;
using System.IO;

namespace VirtoCommerce.McpServer.Web.Controllers.Api
{
    /// <summary>
    /// MCP Server API controller for managing Model Context Protocol server functionality
    /// </summary>
    [Route("api/mcp")]
    [ApiController]
    public class McpServerController : Controller
    {
        private readonly McpServerService _mcpServerService;
        private readonly IServiceProvider _serviceProvider;

        public McpServerController(McpServerService mcpServerService, IServiceProvider serviceProvider)
        {
            _mcpServerService = mcpServerService;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Simple test endpoint to verify basic functionality
        /// </summary>
        [HttpGet]
        [Route("test")]
        public IActionResult Test()
        {
            return Ok(new { message = "MCP Controller is working", timestamp = DateTime.UtcNow });
        }

        /// <summary>
        /// Simple POST test endpoint
        /// </summary>
        [HttpPost]
        [Route("test")]
        public IActionResult TestPost([FromBody] object data)
        {
            return Ok(new { message = "POST is working", receivedData = data?.ToString() ?? "null", timestamp = DateTime.UtcNow });
        }

        /// <summary>
        /// MCP HTTP endpoint for handling MCP protocol messages
        /// </summary>
        /// <remarks>
        /// This endpoint handles MCP protocol communication via HTTP POST.
        /// Compatible with mcp-remote proxy for Claude Desktop integration.
        /// </remarks>
        /// <returns>MCP protocol response</returns>
        [HttpPost]
        [Route("")]
        [Route("sse")]
        public async Task<IActionResult> HandleMcpRequest()
        {
            try
            {
                // Read the raw request body
                using var reader = new StreamReader(Request.Body);
                var requestBody = await reader.ReadToEndAsync();

                if (string.IsNullOrEmpty(requestBody))
                {
                    return BadRequest(new { error = "Empty request body" });
                }

                // Try to parse as JSON
                JsonElement request;
                try
                {
                    request = JsonDocument.Parse(requestBody).RootElement;
                }
                catch (JsonException ex)
                {
                    return BadRequest(new { error = "Invalid JSON", details = ex.Message });
                }

                // Get the method and id from the request
                var method = request.TryGetProperty("method", out var methodElement) ? methodElement.GetString() : null;
                var id = request.TryGetProperty("id", out var idElement) ? idElement.GetInt32() : 0;

                // Handle different MCP methods
                switch (method)
                {
                    case "initialize":
                        var initResponse = new
                        {
                            jsonrpc = "2.0",
                            id = id,
                            result = new
                            {
                                protocolVersion = "2024-11-05",
                                capabilities = new
                                {
                                    tools = new { }
                                },
                                serverInfo = new
                                {
                                    name = "VirtoCommerce MCP Server",
                                    version = "1.0.0"
                                }
                            }
                        };
                        return Ok(initResponse);

                    case "tools/list":
                        var tools = _mcpServerService.GetMcpTools();
                        var toolsResponse = new
                        {
                            jsonrpc = "2.0",
                            id = id,
                            result = new
                            {
                                tools = tools.ToArray()
                            }
                        };
                        return Ok(toolsResponse);

                    case "tools/call":
                        if (request.TryGetProperty("params", out var paramsElement))
                        {
                            var toolName = paramsElement.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                            var arguments = new Dictionary<string, object>();

                            if (paramsElement.TryGetProperty("arguments", out var argsElement))
                            {
                                foreach (var property in argsElement.EnumerateObject())
                                {
                                    // Handle different JSON value types
                                    object? value = property.Value.ValueKind switch
                                    {
                                        JsonValueKind.String => property.Value.GetString(),
                                        JsonValueKind.Number => property.Value.GetDouble(),
                                        JsonValueKind.True => true,
                                        JsonValueKind.False => false,
                                        JsonValueKind.Array => property.Value,
                                        JsonValueKind.Object => property.Value,
                                        JsonValueKind.Null => null,
                                        _ => property.Value.ToString()
                                    };
                                    arguments[property.Name] = value ?? "";
                                }
                            }

                            if (!string.IsNullOrEmpty(toolName))
                            {
                                try
                                {
                                    var result = await _mcpServerService.ExecuteToolAsync(toolName, arguments, HttpContext.RequestAborted);

                                    var callResponse = new
                                    {
                                        jsonrpc = "2.0",
                                        id = id,
                                        result = new
                                        {
                                            content = new[]
                                            {
                                                new
                                                {
                                                    type = "text",
                                                    text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                                                }
                                            }
                                        }
                                    };
                                    return Ok(callResponse);
                                }
                                catch (ArgumentException)
                                {
                                    return Ok(new
                                    {
                                        jsonrpc = "2.0",
                                        id = id,
                                        error = new
                                        {
                                            code = -32601,
                                            message = $"Tool '{toolName}' not found"
                                        }
                                    });
                                }
                                catch (Exception ex)
                                {
                                    return Ok(new
                                    {
                                        jsonrpc = "2.0",
                                        id = id,
                                        error = new
                                        {
                                            code = -32603,
                                            message = "Internal error",
                                            data = ex.Message
                                        }
                                    });
                                }
                            }
                        }

                        return Ok(new
                        {
                            jsonrpc = "2.0",
                            id = id,
                            error = new
                            {
                                code = -32602,
                                message = "Invalid tool call parameters"
                            }
                        });

                    default:
                        return Ok(new
                        {
                            jsonrpc = "2.0",
                            id = id,
                            error = new
                            {
                                code = -32601,
                                message = $"Method '{method}' not found"
                            }
                        });
                }
            }
            catch (Exception ex)
            {
                // Return a very simple error response
                return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
            }
        }

        /// <summary>
        /// Get MCP server capabilities and status
        /// </summary>
        /// <remarks>
        /// Returns server capabilities, discovered modules, and tool statistics.
        /// </remarks>
        /// <returns>Server status and capabilities</returns>
        [HttpGet]
        [Route("status")]
        [ProducesResponseType(typeof(object), 200)]
        public ActionResult<object> GetStatus()
        {
            var endpoints = _mcpServerService.GetDiscoveredEndpoints().ToList();
            var tools = _mcpServerService.GetMcpTools().ToList();

            return Ok(new {
                status = "running",
                protocolVersion = "2024-11-05",
                capabilities = new
                {
                    tools = new { },
                    resources = new { },
                    prompts = new { }
                },
                serverInfo = new
                {
                    name = "VirtoCommerce MCP Server",
                    version = "1.0.0"
                },
                statistics = new
                {
                    discoveredEndpoints = endpoints.Count,
                    availableTools = tools.Count,
                    modules = endpoints.Select(e => e.ModuleId).Distinct().Count()
                },
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// List all available MCP tools
        /// </summary>
        /// <remarks>
        /// Returns all tools discovered from VirtoCommerce modules that have MCP configuration enabled.
        /// Compatible with MCP protocol tools/list request.
        /// </remarks>
        /// <returns>List of available MCP tools</returns>
        [HttpGet]
        [Route("tools")]
        [ProducesResponseType(typeof(object), 200)]
        public ActionResult<object> ListTools()
        {
            var tools = _mcpServerService.GetMcpTools();

            return Ok(new
            {
                tools = tools.ToArray(),
                count = tools.Count()
            });
        }

        /// <summary>
        /// Execute an MCP tool
        /// </summary>
        /// <param name="request">Tool execution request</param>
        /// <remarks>
        /// Executes the specified MCP tool with provided arguments.
        /// Compatible with MCP protocol tools/call request.
        /// </remarks>
        /// <returns>Tool execution result</returns>
        [HttpPost]
        [Route("tools/call")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(object), 400)]
        [ProducesResponseType(typeof(object), 404)]
        public async Task<ActionResult<object>> CallTool([FromBody] McpToolCallRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Name))
                {
                    return BadRequest(new { error = "Tool name is required" });
                }

                var result = await _mcpServerService.ExecuteToolAsync(
                    request.Name,
                    request.Arguments ?? new Dictionary<string, object>(),
                    HttpContext.RequestAborted);

                return Ok(new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                        }
                    }
                });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    /// <summary>
    /// VirtoCommerce MCP Tools Implementation - simplified for web integration
    /// </summary>
    [McpServerToolType]
    public class VirtoCommerceMcpTools
    {
        private readonly McpServerService _mcpServerService;

        public VirtoCommerceMcpTools(McpServerService mcpServerService)
        {
            _mcpServerService = mcpServerService;
        }

        [McpServerTool, Description("Search customer orders by various criteria")]
        public async Task<string> SearchCustomerOrders(
            [Description("Customer ID to search orders for")] string? customerId = null,
            [Description("Array of customer IDs to search orders for")] string[]? customerIds = null,
            [Description("Order number to search for")] string? number = null,
            [Description("Array of order numbers to search for")] string[]? numbers = null,
            [Description("Order status to filter by")] string? status = null,
            [Description("Array of order statuses to filter by")] string[]? statuses = null,
            [Description("Array of store IDs to filter orders")] string[]? storeIds = null,
            [Description("Organization ID to filter orders")] string? organizationId = null,
            [Description("Employee ID to filter orders")] string? employeeId = null,
            [Description("Start date for order search (ISO 8601 format)")] string? startDate = null,
            [Description("End date for order search (ISO 8601 format)")] string? endDate = null,
            [Description("Include prototype orders in search")] bool? withPrototypes = null,
            [Description("Search only recurring orders created by subscription")] bool? onlyRecurring = null,
            [Description("Search orders with given subscription ID")] string? subscriptionId = null,
            [Description("Keyword to search for in orders")] string? keyword = null,
            [Description("Maximum number of orders to return (default: 20)")] int take = 20,
            [Description("Number of orders to skip for pagination (default: 0)")] int skip = 0,
            [Description("Sort expression (e.g., 'createdDate:desc')")] string? sort = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var arguments = new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(customerId)) arguments["customerId"] = customerId;
                if (customerIds?.Length > 0) arguments["customerIds"] = customerIds;
                if (!string.IsNullOrEmpty(number)) arguments["number"] = number;
                if (numbers?.Length > 0) arguments["numbers"] = numbers;
                if (!string.IsNullOrEmpty(status)) arguments["status"] = status;
                if (statuses?.Length > 0) arguments["statuses"] = statuses;
                if (storeIds?.Length > 0) arguments["storeIds"] = storeIds;
                if (!string.IsNullOrEmpty(organizationId)) arguments["organizationId"] = organizationId;
                if (!string.IsNullOrEmpty(employeeId)) arguments["employeeId"] = employeeId;
                if (!string.IsNullOrEmpty(startDate)) arguments["startDate"] = startDate;
                if (!string.IsNullOrEmpty(endDate)) arguments["endDate"] = endDate;
                if (withPrototypes.HasValue) arguments["withPrototypes"] = withPrototypes.Value;
                if (onlyRecurring.HasValue) arguments["onlyRecurring"] = onlyRecurring.Value;
                if (!string.IsNullOrEmpty(subscriptionId)) arguments["subscriptionId"] = subscriptionId;
                if (!string.IsNullOrEmpty(keyword)) arguments["keyword"] = keyword;
                if (!string.IsNullOrEmpty(sort)) arguments["sort"] = sort;
                arguments["take"] = take;
                arguments["skip"] = skip;

                var result = await _mcpServerService.ExecuteToolAsync(
                    "search_customer_orders",
                    arguments,
                    cancellationToken);

                return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                return $"Error searching customer orders: {ex.Message}";
            }
        }
    }

    public class McpToolCallRequest
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, object>? Arguments { get; set; }
    }
}

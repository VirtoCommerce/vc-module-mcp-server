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
                        var toolsResponse = new
                        {
                            jsonrpc = "2.0",
                            id = id,
                            result = new
                            {
                                tools = new[]
                                {
                                    new
                                    {
                                        name = "search_customer_orders",
                                        description = "Search customer orders by various criteria",
                                        inputSchema = new
                                        {
                                            type = "object",
                                            properties = new
                                            {
                                                customerId = new { type = "string", description = "Customer ID to search orders for" },
                                                customerEmail = new { type = "string", description = "Customer email to search orders for" },
                                                orderNumber = new { type = "string", description = "Order number to search for" },
                                                status = new { type = "string", description = "Order status to filter by" },
                                                storeId = new { type = "string", description = "Store ID to filter orders" },
                                                startDate = new { type = "string", description = "Start date for order search (ISO 8601 format)" },
                                                endDate = new { type = "string", description = "End date for order search (ISO 8601 format)" },
                                                take = new { type = "integer", description = "Maximum number of orders to return", @default = 20 },
                                                skip = new { type = "integer", description = "Number of orders to skip for pagination", @default = 0 }
                                            }
                                        }
                                    }
                                }
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
                                    arguments[property.Name] = property.Value.ToString() ?? "";
                                }
                            }

                            if (toolName == "search_customer_orders")
                            {
                                // Create a mock result for now
                                var mockResult = new
                                {
                                    message = "Customer order search executed successfully",
                                    toolName = toolName,
                                    arguments = arguments,
                                    results = new[]
                                    {
                                        new
                                        {
                                            orderId = "ORD-12345",
                                            orderNumber = "2024-001",
                                            customerEmail = arguments.ContainsKey("customerEmail") ? arguments["customerEmail"] : "customer@example.com",
                                            status = "Completed",
                                            total = 299.99,
                                            createdDate = "2024-01-15T10:30:00Z"
                                        },
                                        new
                                        {
                                            orderId = "ORD-12346",
                                            orderNumber = "2024-002",
                                            customerEmail = arguments.ContainsKey("customerEmail") ? arguments["customerEmail"] : "customer@example.com",
                                            status = "Processing",
                                            total = 149.50,
                                            createdDate = "2024-01-16T14:20:00Z"
                                        }
                                    },
                                    timestamp = DateTime.UtcNow
                                };

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
                                                text = JsonSerializer.Serialize(mockResult, new JsonSerializerOptions { WriteIndented = true })
                                            }
                                        }
                                    }
                                };
                                return Ok(callResponse);
                            }
                            else
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
                var arguments = new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(customerId)) arguments["customerId"] = customerId;
                if (!string.IsNullOrEmpty(customerEmail)) arguments["customerEmail"] = customerEmail;
                if (!string.IsNullOrEmpty(orderNumber)) arguments["orderNumber"] = orderNumber;
                if (!string.IsNullOrEmpty(status)) arguments["status"] = status;
                if (!string.IsNullOrEmpty(storeId)) arguments["storeId"] = storeId;
                if (!string.IsNullOrEmpty(startDate)) arguments["startDate"] = startDate;
                if (!string.IsNullOrEmpty(endDate)) arguments["endDate"] = endDate;
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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text;
using VirtoCommerce.McpServer.Core;
using VirtoCommerce.McpServer.Core.Services;

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

        public McpServerController(McpServerService mcpServerService)
        {
            _mcpServerService = mcpServerService;
        }

        /// <summary>
        /// MCP Server-Sent Events endpoint for protocol communication
        /// </summary>
        /// <remarks>
        /// This is the main MCP protocol endpoint that handles bidirectional communication
        /// using Server-Sent Events. MCP clients connect to this endpoint.
        /// </remarks>
        /// <returns>Server-Sent Events stream</returns>
        [HttpGet]
        [Route("sse")]
        [Produces("text/event-stream")]
        public async Task<IActionResult> SseEndpoint()
        {
            Response.Headers["Content-Type"] = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            Response.Headers["Access-Control-Allow-Headers"] = "Cache-Control";

            try
            {
                // Send initial server info
                var serverInfo = new
                {
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
                    }
                };

                await WriteEventAsync("server-info", serverInfo);

                // Send available tools
                var tools = _mcpServerService.GetMcpTools();
                await WriteEventAsync("tools", new { tools = tools });

                // Keep connection alive
                while (!HttpContext.RequestAborted.IsCancellationRequested)
                {
                    await WriteEventAsync("ping", new { timestamp = DateTime.UtcNow });
                    await Task.Delay(30000, HttpContext.RequestAborted); // Ping every 30 seconds
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected - normal
            }
            catch (Exception ex)
            {
                await WriteEventAsync("error", new { error = ex.Message });
            }

            return new EmptyResult();
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

        private async Task WriteEventAsync(string eventType, object data)
        {
            var json = JsonSerializer.Serialize(data);
            var eventData = $"event: {eventType}\ndata: {json}\n\n";
            var bytes = Encoding.UTF8.GetBytes(eventData);

            await Response.Body.WriteAsync(bytes);
            await Response.Body.FlushAsync();
        }
    }

    /// <summary>
    /// MCP Tool call request model
    /// </summary>
    public class McpToolCallRequest
    {
        public string Name { get; set; }
        public Dictionary<string, object> Arguments { get; set; }
    }
}

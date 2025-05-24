using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using VirtoCommerce.Platform.Core.Modularity;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Protocol.Transport;

namespace VirtoCommerce.McpServer.Core.Services
{
    public class McpServerService : IHostedService
    {
        private readonly ILogger<McpServerService> _logger;
        private readonly IModuleManager _moduleManager;
        private readonly IServiceProvider _serviceProvider;
        private IMcpServer _server;

        public McpServerService(
            ILogger<McpServerService> logger,
            IModuleManager moduleManager,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _moduleManager = moduleManager;
            _serviceProvider = serviceProvider;
        }

        private IEnumerable<ApiEndpoint> GetApiEndpoints(Assembly assembly)
        {
            var endpoints = new List<ApiEndpoint>();

            try
            {
                // Find all controller types in the assembly
                var controllerTypes = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Controller"))
                    .Where(t => t.GetCustomAttribute<ApiControllerAttribute>() != null || t.GetCustomAttribute<ControllerAttribute>() != null);

                foreach (var controllerType in controllerTypes)
                {
                    // Get the base route from the controller
                    var routeAttribute = controllerType.GetCustomAttribute<RouteAttribute>();
                    var baseRoute = routeAttribute?.Template ?? controllerType.Name.Replace("Controller", "").ToLower();

                    // Get all public methods that are actions
                    var actionMethods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Where(m => m.GetCustomAttribute<HttpGetAttribute>() != null ||
                                   m.GetCustomAttribute<HttpPostAttribute>() != null ||
                                   m.GetCustomAttribute<HttpPutAttribute>() != null ||
                                   m.GetCustomAttribute<HttpDeleteAttribute>() != null);

                    foreach (var method in actionMethods)
                    {
                        var httpMethod = GetHttpMethod(method);
                        var route = GetRoute(method, baseRoute);
                        var parameters = GetParameters(method);
                        var description = GetDescription(method);

                        endpoints.Add(new ApiEndpoint
                        {
                            Method = httpMethod,
                            Route = route,
                            Parameters = parameters,
                            Description = description
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to scan assembly {Assembly} for API endpoints", assembly.FullName);
            }

            return endpoints;
        }

        private string GetHttpMethod(MethodInfo method)
        {
            if (method.GetCustomAttribute<HttpGetAttribute>() != null) return "GET";
            if (method.GetCustomAttribute<HttpPostAttribute>() != null) return "POST";
            if (method.GetCustomAttribute<HttpPutAttribute>() != null) return "PUT";
            if (method.GetCustomAttribute<HttpDeleteAttribute>() != null) return "DELETE";
            return "GET"; // Default to GET if no attribute found
        }

        private string GetRoute(MethodInfo method, string baseRoute)
        {
            var routeAttribute = method.GetCustomAttribute<RouteAttribute>();
            if (routeAttribute != null)
            {
                return routeAttribute.Template.StartsWith("/") ? routeAttribute.Template : $"/{routeAttribute.Template}";
            }

            // If no route attribute, use the method name
            var methodName = method.Name.ToLower();
            if (methodName.StartsWith("get")) methodName = methodName[3..];
            if (methodName.StartsWith("post")) methodName = methodName[4..];
            if (methodName.StartsWith("put")) methodName = methodName[3..];
            if (methodName.StartsWith("delete")) methodName = methodName[6..];

            return $"/{baseRoute}/{methodName}";
        }

        private Dictionary<string, object> GetParameters(MethodInfo method)
        {
            var parameters = new Dictionary<string, object>();

            foreach (var param in method.GetParameters())
            {
                var paramType = param.ParameterType;
                var paramSchema = new Dictionary<string, object>
                {
                    ["type"] = GetJsonSchemaType(paramType),
                    ["description"] = GetDescription(param)
                };

                if (paramType.IsValueType && !paramType.IsEnum && !paramType.IsPrimitive)
                {
                    paramSchema["properties"] = GetTypeProperties(paramType);
                }

                parameters[param.Name] = paramSchema;
            }

            return parameters;
        }

        private string GetJsonSchemaType(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(int) || type == typeof(long) || type == typeof(short)) return "integer";
            if (type == typeof(decimal) || type == typeof(double) || type == typeof(float)) return "number";
            if (type == typeof(bool)) return "boolean";
            if (type == typeof(DateTime)) return "string";
            if (type.IsArray) return "array";
            if (type.IsEnum) return "string";
            return "object";
        }

        private Dictionary<string, object> GetTypeProperties(Type type)
        {
            var properties = new Dictionary<string, object>();

            foreach (var prop in type.GetProperties())
            {
                properties[prop.Name] = new Dictionary<string, object>
                {
                    ["type"] = GetJsonSchemaType(prop.PropertyType),
                    ["description"] = GetDescription(prop)
                };
            }

            return properties;
        }

        private string GetDescription(MemberInfo member)
        {
            var summaryAttribute = member.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
            if (summaryAttribute != null)
            {
                return summaryAttribute.Description;
            }

            return string.Empty;
        }

        private string GetDescription(ParameterInfo parameter)
        {
            var summaryAttribute = parameter.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
            if (summaryAttribute != null)
            {
                return summaryAttribute.Description;
            }

            return string.Empty;
        }

        private Task<CallToolResponse> InvokeControllerMethod(string moduleName, string httpMethod, string route, Dictionary<string, object> arguments)
        {
            // For now, return a simple response indicating the method is not implemented
            // TODO: Implement actual controller method invocation when VirtoCommerce API is clarified
            throw new NotImplementedException("Controller method invocation not yet implemented");
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var builder = Host.CreateApplicationBuilder();
                builder.Logging.AddConsole(consoleLogOptions =>
                {
                    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
                });

                builder.Services
                    .AddMcpServer()
                    .WithStdioServerTransport()
                    .WithToolsFromAssembly();

                _server = McpServerFactory.Create(new StdioServerTransport("VirtoCommerce"), new McpServerOptions
                {
                    ServerInfo = new() { Name = "VirtoCommerce MCP Server", Version = "1.0.0" },
                    Capabilities = new()
                    {
                        Tools = new()
                        {
                            ListToolsHandler = (request, cancellationToken) =>
                            {
                                var tools = new List<Tool>();

                                // Add a simple example tool for now
                                tools.Add(new Tool
                                {
                                    Name = "example_tool",
                                    Description = "An example tool",
                                    InputSchema = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("""
                                        {
                                            "type": "object",
                                            "properties": {
                                                "message": {
                                                    "type": "string",
                                                    "description": "The message to process"
                                                }
                                            },
                                            "required": ["message"]
                                        }
                                        """)
                                });

                                return Task.FromResult(new ListToolsResult { Tools = tools });
                            },
                            CallToolHandler = (request, cancellationToken) =>
                            {
                                if (request.Params?.Name == null)
                                    throw new ArgumentException("Tool name is required");

                                if (request.Params.Name == "example_tool")
                                {
                                    var message = "No message";
                                    if (request.Params.Arguments?.TryGetValue("message", out var msgValue) == true)
                                    {
                                        message = msgValue.ToString() ?? "No message";
                                    }

                                    return Task.FromResult(new CallToolResponse
                                    {
                                        Content = new List<Content>
                                        {
                                            new Content
                                            {
                                                Type = "text",
                                                Text = $"Processed message: {message}"
                                            }
                                        }
                                    });
                                }

                                throw new ArgumentException($"Unknown tool: {request.Params.Name}");
                            }
                        }
                    }
                });

                await _server.RunAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting MCP server");
                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_server != null)
            {
                await _server.DisposeAsync();
            }
        }
    }

    internal class ApiEndpoint
    {
        public string Method { get; set; }
        public string Route { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public string Description { get; set; }
    }
}

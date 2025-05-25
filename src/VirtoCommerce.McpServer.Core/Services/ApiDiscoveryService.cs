using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using VirtoCommerce.McpServer.Core.Models;
using VirtoCommerce.Platform.Core.Modularity;

namespace VirtoCommerce.McpServer.Core.Services
{
    /// <summary>
    /// Enhanced API discovery service that uses MCP configuration and XML documentation
    /// </summary>
    public class ApiDiscoveryService : IApiDiscoveryService
    {
        private readonly ILogger<ApiDiscoveryService> _logger;
        private readonly IModuleManifestService _moduleManifestService;
        private readonly IXmlDocumentationService _xmlDocumentationService;
        private readonly IModuleManager _moduleManager;

        public ApiDiscoveryService(
            ILogger<ApiDiscoveryService> logger,
            IModuleManifestService moduleManifestService,
            IXmlDocumentationService xmlDocumentationService,
            IModuleManager moduleManager)
        {
            _logger = logger;
            _moduleManifestService = moduleManifestService;
            _xmlDocumentationService = xmlDocumentationService;
            _moduleManager = moduleManager;
        }

        public IEnumerable<ApiEndpoint> DiscoverAllApiEndpoints()
        {
            var endpoints = new List<ApiEndpoint>();

            var mcpConfigurations = _moduleManifestService.GetAllMcpConfigurations();
            foreach (var (moduleId, mcpConfig) in mcpConfigurations)
            {
                // Get module info from module manager using available properties
                var moduleInfo = GetModuleInfo(moduleId);
                if (moduleInfo == null) continue;

                var moduleEndpoints = DiscoverApiEndpoints(moduleInfo);
                endpoints.AddRange(moduleEndpoints);
            }

            _logger.LogInformation("Discovered {EndpointCount} API endpoints from {ModuleCount} MCP-enabled modules",
                endpoints.Count, mcpConfigurations.Count);

            return endpoints;
        }

        private ManifestModuleInfo GetModuleInfo(string moduleId)
        {
            // Since GetAllModules doesn't exist, we'll use what's available in IModuleManager
            // This is a placeholder implementation - adjust based on actual VirtoCommerce API
            try
            {
                // Try to get module info through available methods
                // This may need to be adjusted based on the actual IModuleManager implementation
                return null; // TODO: Implement based on actual VirtoCommerce IModuleManager API
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not retrieve module info for {ModuleId}", moduleId);
                return null;
            }
        }

        public IEnumerable<ApiEndpoint> DiscoverApiEndpoints(ManifestModuleInfo moduleInfo)
        {
            if (moduleInfo == null) return Enumerable.Empty<ApiEndpoint>();

            var mcpConfig = _moduleManifestService.GetMcpConfiguration(moduleInfo);
            if (mcpConfig?.Enabled != true)
            {
                _logger.LogDebug("Module {ModuleId} does not have MCP enabled", moduleInfo.Id);
                return Enumerable.Empty<ApiEndpoint>();
            }

            try
            {
                var assembly = Assembly.LoadFrom(moduleInfo.Ref);
                return DiscoverApiEndpoints(assembly, mcpConfig, moduleInfo.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load assembly for module {ModuleId} from {AssemblyPath}",
                    moduleInfo.Id, moduleInfo.Ref);
                return Enumerable.Empty<ApiEndpoint>();
            }
        }

        public IEnumerable<ApiEndpoint> DiscoverApiEndpoints(Assembly assembly, McpConfiguration mcpConfig, string moduleId)
        {
            if (assembly == null || mcpConfig?.Enabled != true)
                return Enumerable.Empty<ApiEndpoint>();

            var endpoints = new List<ApiEndpoint>();

            try
            {
                // Load XML documentation for the assembly
                _xmlDocumentationService.LoadXmlDocumentation(assembly);

                // Find all controller types
                var controllerTypes = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && IsController(t))
                    .Where(t => ShouldIncludeController(t, mcpConfig));

                foreach (var controllerType in controllerTypes)
                {
                    var controllerEndpoints = DiscoverControllerEndpoints(controllerType, mcpConfig, moduleId);
                    endpoints.AddRange(controllerEndpoints);
                }

                _logger.LogDebug("Discovered {EndpointCount} API endpoints from assembly {AssemblyName} for module {ModuleId}",
                    endpoints.Count, assembly.GetName().Name, moduleId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to discover API endpoints from assembly {AssemblyName} for module {ModuleId}",
                    assembly.GetName().Name, moduleId);
            }

            return endpoints;
        }

        public bool ShouldExposeEndpoint(Type controllerType, MethodInfo methodInfo, McpConfiguration mcpConfig)
        {
            if (mcpConfig?.ApiExposure == null) return false;

            // Check if the HTTP method is allowed
            var httpMethod = GetHttpMethod(methodInfo);
            if (!IsHttpMethodAllowed(httpMethod, mcpConfig.ApiExposure.Methods))
            {
                return false;
            }

            // Check controller inclusion/exclusion patterns
            if (!ShouldIncludeController(controllerType, mcpConfig))
            {
                return false;
            }

            // Check if method has [NonAction] attribute
            if (methodInfo.GetCustomAttribute<NonActionAttribute>() != null)
            {
                return false;
            }

            return true;
        }

        public string GenerateToolName(string moduleId, string controllerName, string actionName, string httpMethod, ToolNaming toolNaming)
        {
            if (toolNaming == null) return $"{moduleId}_{controllerName}_{actionName}";

            var parts = new List<string>();

            // Add parts based on convention
            var convention = toolNaming.Convention?.ToLowerInvariant() ?? "module_controller_action";
            switch (convention)
            {
                case "module_controller_action":
                    parts.Add(moduleId);
                    parts.Add(controllerName);
                    parts.Add(actionName);
                    break;
                case "controller_action":
                    parts.Add(controllerName);
                    parts.Add(actionName);
                    break;
                case "module_action":
                    parts.Add(moduleId);
                    parts.Add(actionName);
                    break;
                case "method_controller_action":
                    parts.Add(httpMethod);
                    parts.Add(controllerName);
                    parts.Add(actionName);
                    break;
                default:
                    parts.Add(moduleId);
                    parts.Add(controllerName);
                    parts.Add(actionName);
                    break;
            }

            // Apply transformations
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];

                // Remove Controller suffix if specified
                if (toolNaming.RemoveControllerSuffix && part.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
                {
                    part = part.Substring(0, part.Length - "Controller".Length);
                }

                // Apply case conversion
                if (toolNaming.UseCamelCase)
                {
                    part = ToCamelCase(part);
                }
                else
                {
                    part = part.ToLowerInvariant();
                }

                parts[i] = part;
            }

            return string.Join(toolNaming.Separator ?? "_", parts);
        }

        private IEnumerable<ApiEndpoint> DiscoverControllerEndpoints(Type controllerType, McpConfiguration mcpConfig, string moduleId)
        {
            var endpoints = new List<ApiEndpoint>();

            // Get the base route from the controller
            var routeAttribute = controllerType.GetCustomAttribute<RouteAttribute>();
            var baseRoute = routeAttribute?.Template ?? GetDefaultControllerRoute(controllerType);

            // Get all action methods
            var actionMethods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => IsActionMethod(m))
                .Where(m => ShouldExposeEndpoint(controllerType, m, mcpConfig));

            foreach (var method in actionMethods)
            {
                try
                {
                    var endpoint = CreateApiEndpoint(controllerType, method, baseRoute, mcpConfig, moduleId);
                    if (endpoint != null)
                    {
                        endpoints.Add(endpoint);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create API endpoint for method {MethodName} in controller {ControllerName}",
                        method.Name, controllerType.Name);
                }
            }

            return endpoints;
        }

        private ApiEndpoint CreateApiEndpoint(Type controllerType, MethodInfo methodInfo, string baseRoute, McpConfiguration mcpConfig, string moduleId)
        {
            var httpMethod = GetHttpMethod(methodInfo);
            var route = GetRoute(methodInfo, baseRoute);
            var controllerName = GetControllerName(controllerType);
            var actionName = GetActionName(methodInfo);

            var endpoint = new ApiEndpoint
            {
                Method = httpMethod,
                Route = route,
                ModuleId = moduleId,
                ControllerName = controllerName,
                ActionName = actionName,
                ControllerType = controllerType,
                MethodInfo = methodInfo,
                ReturnType = methodInfo.ReturnType.GetTypeInfo(),
                ToolName = GenerateToolName(moduleId, controllerName, actionName, httpMethod, mcpConfig.ToolNaming)
            };

            // Extract documentation
            var xmlDoc = _xmlDocumentationService.GetDocumentation(methodInfo);
            if (!string.IsNullOrEmpty(xmlDoc))
            {
                endpoint.Description = _xmlDocumentationService.ExtractSummary(xmlDoc);
                endpoint.ReturnsDescription = _xmlDocumentationService.ExtractReturnsDescription(xmlDoc);
            }

            // Extract parameters with documentation
            endpoint.Parameters = GetParameters(methodInfo);

            // Extract security information
            endpoint.Security = ExtractSecurityInfo(controllerType, methodInfo, mcpConfig);

            return endpoint;
        }

        private SecurityInfo ExtractSecurityInfo(Type controllerType, MethodInfo methodInfo, McpConfiguration mcpConfig)
        {
            var security = new SecurityInfo();

            // Check for AllowAnonymous attribute
            security.AllowAnonymous = methodInfo.GetCustomAttribute<AllowAnonymousAttribute>() != null ||
                                     controllerType.GetCustomAttribute<AllowAnonymousAttribute>() != null;

            if (!security.AllowAnonymous)
            {
                security.RequiresAuthentication = mcpConfig.ApiExposure.Security.RequireAuthentication;

                // Extract authorization attributes
                var authAttributes = methodInfo.GetCustomAttributes<AuthorizeAttribute>()
                    .Concat(controllerType.GetCustomAttributes<AuthorizeAttribute>());

                foreach (var authAttr in authAttributes)
                {
                    if (!string.IsNullOrEmpty(authAttr.Policy))
                    {
                        security.AuthorizationPolicies.Add(authAttr.Policy);
                    }

                    if (!string.IsNullOrEmpty(authAttr.Roles))
                    {
                        security.RequiredRoles.AddRange(authAttr.Roles.Split(',').Select(r => r.Trim()));
                    }
                }

                // Add MCP-specific permissions
                if (mcpConfig.ApiExposure.Security.McpPermissions?.Permissions != null)
                {
                    security.RequiredPermissions.AddRange(mcpConfig.ApiExposure.Security.McpPermissions.Permissions);
                }
            }

            return security;
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
                    ["required"] = !param.HasDefaultValue
                };

                // Extract parameter description from XML documentation
                var paramDoc = _xmlDocumentationService.GetDocumentation(param);
                if (!string.IsNullOrEmpty(paramDoc))
                {
                    paramSchema["description"] = paramDoc;
                }

                // Handle complex types
                if (IsComplexType(paramType))
                {
                    paramSchema["properties"] = GetTypeProperties(paramType);
                }

                parameters[param.Name] = paramSchema;
            }

            return parameters;
        }

        private Dictionary<string, object> GetTypeProperties(Type type)
        {
            var properties = new Dictionary<string, object>();

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var propSchema = new Dictionary<string, object>
                {
                    ["type"] = GetJsonSchemaType(prop.PropertyType)
                };

                var propDoc = _xmlDocumentationService.GetDocumentation(prop);
                if (!string.IsNullOrEmpty(propDoc))
                {
                    propSchema["description"] = _xmlDocumentationService.ExtractSummary(propDoc);
                }

                properties[prop.Name] = propSchema;
            }

            return properties;
        }

        private bool IsController(Type type)
        {
            return type.Name.EndsWith("Controller") &&
                   (type.GetCustomAttribute<ApiControllerAttribute>() != null ||
                    type.GetCustomAttribute<ControllerAttribute>() != null ||
                    typeof(ControllerBase).IsAssignableFrom(type));
        }

        private bool IsActionMethod(MethodInfo method)
        {
            return method.IsPublic &&
                   !method.IsStatic &&
                   method.DeclaringType != typeof(object) &&
                   method.DeclaringType != typeof(ControllerBase) &&
                   !method.IsSpecialName &&
                   (method.GetCustomAttribute<HttpGetAttribute>() != null ||
                    method.GetCustomAttribute<HttpPostAttribute>() != null ||
                    method.GetCustomAttribute<HttpPutAttribute>() != null ||
                    method.GetCustomAttribute<HttpDeleteAttribute>() != null ||
                    method.GetCustomAttribute<HttpPatchAttribute>() != null);
        }

        private bool ShouldIncludeController(Type controllerType, McpConfiguration mcpConfig)
        {
            var controllerName = controllerType.Name;
            var controllers = mcpConfig.ApiExposure.Controllers;

            // Check exclusions first
            if (controllers.Exclude?.Any() == true)
            {
                foreach (var exclude in controllers.Exclude)
                {
                    if (MatchesPattern(controllerName, exclude.Pattern))
                    {
                        return false;
                    }
                }
            }

            // Check inclusions
            if (controllers.Include?.Any() == true)
            {
                foreach (var include in controllers.Include)
                {
                    if (MatchesPattern(controllerName, include.Pattern))
                    {
                        return true;
                    }
                }
                return false; // If includes are specified but none match
            }

            return true; // Default to include if no patterns specified
        }

        private bool IsHttpMethodAllowed(string httpMethod, MethodsConfig methodsConfig)
        {
            if (methodsConfig?.Include?.Any() != true) return true;

            return methodsConfig.Include.Any(m => string.Equals(m.HttpMethod, httpMethod, StringComparison.OrdinalIgnoreCase));
        }

        private bool MatchesPattern(string input, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return false;
            if (pattern == "*") return true;

            // Convert wildcard pattern to regex
            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
        }

        private string GetHttpMethod(MethodInfo method)
        {
            if (method.GetCustomAttribute<HttpGetAttribute>() != null) return "GET";
            if (method.GetCustomAttribute<HttpPostAttribute>() != null) return "POST";
            if (method.GetCustomAttribute<HttpPutAttribute>() != null) return "PUT";
            if (method.GetCustomAttribute<HttpDeleteAttribute>() != null) return "DELETE";
            if (method.GetCustomAttribute<HttpPatchAttribute>() != null) return "PATCH";
            return "GET"; // Default
        }

        private string GetRoute(MethodInfo method, string baseRoute)
        {
            var routeAttribute = method.GetCustomAttribute<RouteAttribute>();
            if (routeAttribute != null)
            {
                return routeAttribute.Template.StartsWith("/") ? routeAttribute.Template : $"/{routeAttribute.Template}";
            }

            // Get route from HTTP method attributes - check specific types instead of base class
            string template = null;

            var httpGetAttr = method.GetCustomAttribute<HttpGetAttribute>();
            if (httpGetAttr?.Template != null)
            {
                template = httpGetAttr.Template;
            }
            else
            {
                var httpPostAttr = method.GetCustomAttribute<HttpPostAttribute>();
                if (httpPostAttr?.Template != null)
                {
                    template = httpPostAttr.Template;
                }
                else
                {
                    var httpPutAttr = method.GetCustomAttribute<HttpPutAttribute>();
                    if (httpPutAttr?.Template != null)
                    {
                        template = httpPutAttr.Template;
                    }
                    else
                    {
                        var httpDeleteAttr = method.GetCustomAttribute<HttpDeleteAttribute>();
                        if (httpDeleteAttr?.Template != null)
                        {
                            template = httpDeleteAttr.Template;
                        }
                        else
                        {
                            var httpPatchAttr = method.GetCustomAttribute<HttpPatchAttribute>();
                            if (httpPatchAttr?.Template != null)
                            {
                                template = httpPatchAttr.Template;
                            }
                        }
                    }
                }
            }

            if (template != null)
            {
                return template.StartsWith("/") ? template : $"/{baseRoute.TrimStart('/')}/{template}";
            }

            // Default route generation
            var actionName = GetActionName(method);
            return $"/{baseRoute.TrimStart('/')}/{actionName}";
        }

        private string GetDefaultControllerRoute(Type controllerType)
        {
            var controllerName = GetControllerName(controllerType);
            return $"api/{controllerName}";
        }

        private string GetControllerName(Type controllerType)
        {
            var name = controllerType.Name;
            if (name.EndsWith("Controller"))
            {
                name = name.Substring(0, name.Length - "Controller".Length);
            }
            return name;
        }

        private string GetActionName(MethodInfo method)
        {
            var actionAttribute = method.GetCustomAttribute<ActionNameAttribute>();
            return actionAttribute?.Name ?? method.Name;
        }

        private string GetJsonSchemaType(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(int) || type == typeof(long) || type == typeof(short)) return "integer";
            if (type == typeof(decimal) || type == typeof(double) || type == typeof(float)) return "number";
            if (type == typeof(bool)) return "boolean";
            if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return "string";
            if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))) return "array";
            if (type.IsEnum) return "string";
            return "object";
        }

        private bool IsComplexType(Type type)
        {
            return !type.IsPrimitive &&
                   type != typeof(string) &&
                   type != typeof(DateTime) &&
                   type != typeof(DateTimeOffset) &&
                   type != typeof(decimal) &&
                   !type.IsEnum &&
                   !type.IsArray;
        }

        private string ToCamelCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            if (input.Length == 1) return input.ToLowerInvariant();
            return char.ToLowerInvariant(input[0]) + input.Substring(1);
        }
    }
}

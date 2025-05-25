using System;
using System.Collections.Generic;
using System.Reflection;

namespace VirtoCommerce.McpServer.Core.Models
{
    /// <summary>
    /// Represents a discovered API endpoint with metadata for MCP tool generation
    /// </summary>
    public class ApiEndpoint
    {
        /// <summary>
        /// HTTP method (GET, POST, PUT, DELETE, etc.)
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// API route/path
        /// </summary>
        public string Route { get; set; }

        /// <summary>
        /// Description extracted from XML documentation
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Parameters with schema information
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Generated MCP tool name
        /// </summary>
        public string ToolName { get; set; }

        /// <summary>
        /// Module ID this endpoint belongs to
        /// </summary>
        public string ModuleId { get; set; }

        /// <summary>
        /// Controller name
        /// </summary>
        public string ControllerName { get; set; }

        /// <summary>
        /// Action/method name
        /// </summary>
        public string ActionName { get; set; }

        /// <summary>
        /// Security information
        /// </summary>
        public SecurityInfo Security { get; set; } = new SecurityInfo();

        /// <summary>
        /// Reflection information for method invocation
        /// </summary>
        public MethodInfo MethodInfo { get; set; }

        /// <summary>
        /// Controller type
        /// </summary>
        public Type ControllerType { get; set; }

        /// <summary>
        /// Return type information
        /// </summary>
        public TypeInfo ReturnType { get; set; }

        /// <summary>
        /// Returns description from XML documentation
        /// </summary>
        public string ReturnsDescription { get; set; }

        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Security information for an API endpoint
    /// </summary>
    public class SecurityInfo
    {
        /// <summary>
        /// Whether authentication is required
        /// </summary>
        public bool RequiresAuthentication { get; set; }

        /// <summary>
        /// Required permissions
        /// </summary>
        public List<string> RequiredPermissions { get; set; } = new List<string>();

        /// <summary>
        /// Authorization policy names
        /// </summary>
        public List<string> AuthorizationPolicies { get; set; } = new List<string>();

        /// <summary>
        /// Whether anonymous access is allowed
        /// </summary>
        public bool AllowAnonymous { get; set; }

        /// <summary>
        /// Roles required for access
        /// </summary>
        public List<string> RequiredRoles { get; set; } = new List<string>();
    }
}

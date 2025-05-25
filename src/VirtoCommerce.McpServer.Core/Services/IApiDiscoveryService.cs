using System.Collections.Generic;
using System.Reflection;
using VirtoCommerce.McpServer.Core.Models;
using VirtoCommerce.Platform.Core.Modularity;

namespace VirtoCommerce.McpServer.Core.Services
{
    /// <summary>
    /// Enhanced API discovery service that uses MCP configuration and XML documentation
    /// </summary>
    public interface IApiDiscoveryService
    {
        /// <summary>
        /// Discover API endpoints from all MCP-enabled modules
        /// </summary>
        /// <returns>Collection of discovered API endpoints</returns>
        IEnumerable<ApiEndpoint> DiscoverAllApiEndpoints();

        /// <summary>
        /// Discover API endpoints from a specific module
        /// </summary>
        /// <param name="moduleInfo">Module to discover APIs from</param>
        /// <returns>Collection of discovered API endpoints</returns>
        IEnumerable<ApiEndpoint> DiscoverApiEndpoints(ManifestModuleInfo moduleInfo);

        /// <summary>
        /// Discover API endpoints from an assembly using MCP configuration
        /// </summary>
        /// <param name="assembly">Assembly to scan</param>
        /// <param name="mcpConfig">MCP configuration to use for filtering</param>
        /// <param name="moduleId">Module ID for naming</param>
        /// <returns>Collection of discovered API endpoints</returns>
        IEnumerable<ApiEndpoint> DiscoverApiEndpoints(Assembly assembly, McpConfiguration mcpConfig, string moduleId);

        /// <summary>
        /// Check if an API endpoint should be exposed based on MCP configuration
        /// </summary>
        /// <param name="controllerType">Controller type</param>
        /// <param name="methodInfo">Method info</param>
        /// <param name="mcpConfig">MCP configuration</param>
        /// <returns>True if the endpoint should be exposed</returns>
        bool ShouldExposeEndpoint(System.Type controllerType, MethodInfo methodInfo, McpConfiguration mcpConfig);

        /// <summary>
        /// Generate MCP tool name for an API endpoint
        /// </summary>
        /// <param name="moduleId">Module ID</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="actionName">Action name</param>
        /// <param name="httpMethod">HTTP method</param>
        /// <param name="toolNaming">Tool naming configuration</param>
        /// <returns>Generated tool name</returns>
        string GenerateToolName(string moduleId, string controllerName, string actionName, string httpMethod, ToolNaming toolNaming);
    }
}

using System.Collections.Generic;
using VirtoCommerce.McpServer.Core.Models;
using VirtoCommerce.Platform.Core.Modularity;

namespace VirtoCommerce.McpServer.Core.Services
{
    /// <summary>
    /// Service for parsing module manifests and extracting MCP configuration
    /// </summary>
    public interface IModuleManifestService
    {
        /// <summary>
        /// Extract MCP configuration from a module manifest
        /// </summary>
        /// <param name="moduleInfo">Module information</param>
        /// <returns>MCP configuration or null if not found</returns>
        McpConfiguration GetMcpConfiguration(ManifestModuleInfo moduleInfo);

        /// <summary>
        /// Get MCP configurations for all loaded modules that have MCP enabled
        /// </summary>
        /// <returns>Dictionary of module ID to MCP configuration</returns>
        Dictionary<string, McpConfiguration> GetAllMcpConfigurations();

        /// <summary>
        /// Check if a module has MCP configuration
        /// </summary>
        /// <param name="moduleInfo">Module information</param>
        /// <returns>True if module has MCP configuration</returns>
        bool HasMcpConfiguration(ManifestModuleInfo moduleInfo);

        /// <summary>
        /// Check if a module has MCP enabled
        /// </summary>
        /// <param name="moduleInfo">Module information</param>
        /// <returns>True if module has MCP enabled</returns>
        bool IsMcpEnabled(ManifestModuleInfo moduleInfo);
    }
}

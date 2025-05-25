using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using VirtoCommerce.McpServer.Core.Models;
using VirtoCommerce.Platform.Core.Modularity;

namespace VirtoCommerce.McpServer.Core.Services
{
    /// <summary>
    /// Service for parsing module manifests and extracting MCP configuration
    /// </summary>
    public class ModuleManifestService : IModuleManifestService
    {
        private readonly ILogger<ModuleManifestService> _logger;
        private readonly IModuleManager _moduleManager;
        private readonly ConcurrentDictionary<string, McpConfiguration> _mcpConfigurations = new();

        public ModuleManifestService(ILogger<ModuleManifestService> logger, IModuleManager moduleManager)
        {
            _logger = logger;
            _moduleManager = moduleManager;
        }

        public McpConfiguration GetMcpConfiguration(ManifestModuleInfo moduleInfo)
        {
            if (moduleInfo == null) return null;

            // Check cache first
            if (_mcpConfigurations.TryGetValue(moduleInfo.Id, out var cachedConfig))
                return cachedConfig;

            try
            {
                var mcpConfig = ExtractMcpConfigurationFromManifest(moduleInfo);
                if (mcpConfig != null)
                {
                    _mcpConfigurations[moduleInfo.Id] = mcpConfig;
                    _logger.LogDebug("Loaded MCP configuration for module {ModuleId}", moduleInfo.Id);
                }
                return mcpConfig;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract MCP configuration from module {ModuleId}", moduleInfo.Id);
                return null;
            }
        }

        public Dictionary<string, McpConfiguration> GetAllMcpConfigurations()
        {
            var result = new Dictionary<string, McpConfiguration>();

            // TODO: Implement based on actual VirtoCommerce IModuleManager API
            // Since GetAllModules doesn't exist, we need to find the correct method
            // For now, return empty dictionary until we can determine the correct API
            try
            {
                // Placeholder implementation - adjust based on actual VirtoCommerce API
                _logger.LogWarning("GetAllMcpConfigurations: GetAllModules method not available in IModuleManager. " +
                                 "This needs to be implemented based on the actual VirtoCommerce API.");

                // If there's a different method available, use it here
                // var modules = _moduleManager.SomeOtherMethod();
                // foreach (var module in modules)
                // {
                //     var mcpConfig = GetMcpConfiguration(module);
                //     if (mcpConfig?.Enabled == true)
                //     {
                //         result[module.Id] = mcpConfig;
                //     }
                // }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all MCP configurations");
            }

            return result;
        }

        public bool HasMcpConfiguration(ManifestModuleInfo moduleInfo)
        {
            return GetMcpConfiguration(moduleInfo) != null;
        }

        public bool IsMcpEnabled(ManifestModuleInfo moduleInfo)
        {
            var config = GetMcpConfiguration(moduleInfo);
            return config?.Enabled == true;
        }

        private McpConfiguration ExtractMcpConfigurationFromManifest(ManifestModuleInfo moduleInfo)
        {
            if (string.IsNullOrEmpty(moduleInfo.FullPhysicalPath))
                return null;

            var manifestPath = Path.Combine(moduleInfo.FullPhysicalPath, "module.manifest");
            if (!File.Exists(manifestPath))
            {
                _logger.LogDebug("Manifest file not found for module {ModuleId} at {ManifestPath}",
                    moduleInfo.Id, manifestPath);
                return null;
            }

            try
            {
                var manifestXml = File.ReadAllText(manifestPath);
                return ParseMcpConfigurationFromXml(manifestXml);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read manifest file for module {ModuleId} at {ManifestPath}",
                    moduleInfo.Id, manifestPath);
                return null;
            }
        }

        private McpConfiguration ParseMcpConfigurationFromXml(string manifestXml)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(manifestXml);

            var mcpConfigNode = xmlDoc.SelectSingleNode("//mcpConfiguration");
            if (mcpConfigNode == null)
                return null;

            try
            {
                var serializer = new XmlSerializer(typeof(McpConfiguration));
                using var reader = new StringReader(mcpConfigNode.OuterXml);
                var mcpConfig = (McpConfiguration)serializer.Deserialize(reader);

                // Apply defaults if not specified
                ApplyDefaults(mcpConfig);

                return mcpConfig;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize MCP configuration from manifest XML");
                return null;
            }
        }

        private void ApplyDefaults(McpConfiguration config)
        {
            if (config == null) return;

            // Apply default tool naming convention if not specified
            if (config.ToolNaming == null)
            {
                config.ToolNaming = new ToolNaming();
            }

            // Apply default API exposure settings if not specified
            if (config.ApiExposure == null)
            {
                config.ApiExposure = new ApiExposure();
            }

            // Apply default controller patterns if none specified
            if (config.ApiExposure.Controllers?.Include?.Count == 0)
            {
                config.ApiExposure.Controllers.Include.Add(new PatternConfig { Pattern = "*Controller" });
            }

            // Apply default HTTP methods if none specified
            if (config.ApiExposure.Methods?.Include?.Count == 0)
            {
                config.ApiExposure.Methods.Include.AddRange(new[]
                {
                    new HttpMethodConfig { HttpMethod = "GET" },
                    new HttpMethodConfig { HttpMethod = "POST" },
                    new HttpMethodConfig { HttpMethod = "PUT" },
                    new HttpMethodConfig { HttpMethod = "DELETE" }
                });
            }

            // Apply default security settings if not specified
            if (config.ApiExposure.Security == null)
            {
                config.ApiExposure.Security = new SecurityConfig();
            }

            // Apply default capabilities if not specified
            if (config.Capabilities == null)
            {
                config.Capabilities = new McpCapabilities();
            }
        }
    }
}

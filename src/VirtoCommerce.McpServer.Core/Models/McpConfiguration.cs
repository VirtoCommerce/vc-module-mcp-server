using System.Collections.Generic;
using System.Xml.Serialization;

namespace VirtoCommerce.McpServer.Core.Models
{
    [XmlRoot("mcpConfiguration")]
    public class McpConfiguration
    {
        [XmlElement("enabled")]
        public bool Enabled { get; set; } = true;

        [XmlElement("description")]
        public string Description { get; set; }

        [XmlElement("version")]
        public string Version { get; set; } = "1.0.0";

        [XmlElement("capabilities")]
        public McpCapabilities Capabilities { get; set; } = new McpCapabilities();

        [XmlElement("apiExposure")]
        public ApiExposure ApiExposure { get; set; } = new ApiExposure();

        [XmlElement("toolNaming")]
        public ToolNaming ToolNaming { get; set; } = new ToolNaming();
    }

    public class McpCapabilities
    {
        [XmlElement("tools")]
        public bool Tools { get; set; } = true;

        [XmlElement("resources")]
        public bool Resources { get; set; } = false;

        [XmlElement("prompts")]
        public bool Prompts { get; set; } = false;
    }

    public class ApiExposure
    {
        [XmlElement("controllers")]
        public ControllersConfig Controllers { get; set; } = new ControllersConfig();

        [XmlElement("methods")]
        public MethodsConfig Methods { get; set; } = new MethodsConfig();

        [XmlElement("security")]
        public SecurityConfig Security { get; set; } = new SecurityConfig();
    }

    public class ControllersConfig
    {
        [XmlElement("include")]
        public List<PatternConfig> Include { get; set; } = new List<PatternConfig>();

        [XmlElement("exclude")]
        public List<PatternConfig> Exclude { get; set; } = new List<PatternConfig>();
    }

    public class PatternConfig
    {
        [XmlAttribute("pattern")]
        public string Pattern { get; set; }
    }

    public class MethodsConfig
    {
        [XmlElement("include")]
        public List<HttpMethodConfig> Include { get; set; } = new List<HttpMethodConfig>();

        [XmlElement("exclude")]
        public List<HttpMethodConfig> Exclude { get; set; } = new List<HttpMethodConfig>();
    }

    public class HttpMethodConfig
    {
        [XmlAttribute("httpMethod")]
        public string HttpMethod { get; set; }
    }

    public class SecurityConfig
    {
        [XmlElement("requireAuthentication")]
        public bool RequireAuthentication { get; set; } = true;

        [XmlElement("respectExistingAuthorization")]
        public bool RespectExistingAuthorization { get; set; } = true;

        [XmlElement("mcpPermissions")]
        public McpPermissions McpPermissions { get; set; } = new McpPermissions();
    }

    public class McpPermissions
    {
        [XmlElement("permission")]
        public List<string> Permissions { get; set; } = new List<string>();
    }

    public class ToolNaming
    {
        [XmlElement("convention")]
        public string Convention { get; set; } = "module_controller_action";

        [XmlElement("removeControllerSuffix")]
        public bool RemoveControllerSuffix { get; set; } = true;

        [XmlElement("useCamelCase")]
        public bool UseCamelCase { get; set; } = false;

        [XmlElement("separator")]
        public string Separator { get; set; } = "_";
    }
}

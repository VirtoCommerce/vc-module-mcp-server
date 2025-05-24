using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.McpServer.Core;

public static class ModuleConstants
{
    public static class Security
    {
        public static class Permissions
        {
            public const string Access = "McpServer:access";
            public const string Create = "McpServer:create";
            public const string Read = "McpServer:read";
            public const string Update = "McpServer:update";
            public const string Delete = "McpServer:delete";

            public static string[] AllPermissions { get; } =
            [
                Access,
                Create,
                Read,
                Update,
                Delete,
            ];
        }
    }

    public static class Settings
    {
        public static class General
        {
            public static SettingDescriptor McpServerEnabled { get; } = new()
            {
                Name = "McpServer.Enabled",
                GroupName = "McpServer|General",
                ValueType = SettingValueType.Boolean,
                DefaultValue = false,
            };

            public static IEnumerable<SettingDescriptor> AllGeneralSettings
            {
                get
                {
                    yield return McpServerEnabled;
                }
            }
        }

        public static IEnumerable<SettingDescriptor> AllSettings
        {
            get
            {
                return General.AllGeneralSettings;
            }
        }
    }
}

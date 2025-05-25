using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace VirtoCommerce.McpServer.Core.Services
{
    /// <summary>
    /// Service for extracting XML documentation from assemblies at runtime
    /// Similar to how Swagger extracts documentation
    /// </summary>
    public class XmlDocumentationService : IXmlDocumentationService
    {
        private readonly ILogger<XmlDocumentationService> _logger;
        private readonly ConcurrentDictionary<string, string> _loadedXmlDocumentation = new();
        private readonly HashSet<Assembly> _loadedAssemblies = new();
        private readonly object _lock = new object();

        public XmlDocumentationService(ILogger<XmlDocumentationService> logger)
        {
            _logger = logger;
        }

        public void LoadXmlDocumentation(Assembly assembly)
        {
            if (assembly == null) return;

            lock (_lock)
            {
                if (_loadedAssemblies.Contains(assembly))
                    return; // Already loaded

                try
                {
                    var directoryPath = GetAssemblyDirectoryPath(assembly);
                    var xmlFilePath = Path.Combine(directoryPath, assembly.GetName().Name + ".xml");

                    if (File.Exists(xmlFilePath))
                    {
                        LoadXmlDocumentation(xmlFilePath);
                        _loadedAssemblies.Add(assembly);
                        _logger.LogDebug("Loaded XML documentation for assembly {AssemblyName} from {FilePath}",
                            assembly.GetName().Name, xmlFilePath);
                    }
                    else
                    {
                        _logger.LogDebug("XML documentation file not found for assembly {AssemblyName} at {FilePath}",
                            assembly.GetName().Name, xmlFilePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load XML documentation for assembly {AssemblyName}",
                        assembly.GetName().Name);
                }
            }
        }

        public void LoadXmlDocumentation(string xmlFilePath)
        {
            if (string.IsNullOrEmpty(xmlFilePath) || !File.Exists(xmlFilePath))
                return;

            try
            {
                var xmlContent = File.ReadAllText(xmlFilePath);
                LoadXmlDocumentationFromString(xmlContent);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load XML documentation from file {FilePath}", xmlFilePath);
            }
        }

        private void LoadXmlDocumentationFromString(string xmlDocumentation)
        {
            using var xmlReader = XmlReader.Create(new StringReader(xmlDocumentation));

            while (xmlReader.Read())
            {
                if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "member")
                {
                    var rawName = xmlReader["name"];
                    if (!string.IsNullOrEmpty(rawName))
                    {
                        var innerXml = xmlReader.ReadInnerXml();
                        _loadedXmlDocumentation[rawName] = innerXml;
                    }
                }
            }
        }

        public string GetDocumentation(Type type)
        {
            if (type == null) return null;

            LoadXmlDocumentation(type.Assembly);
            var key = "T:" + XmlDocumentationKeyHelper(type.FullName, null);
            return _loadedXmlDocumentation.TryGetValue(key, out var documentation) ? documentation : null;
        }

        public string GetDocumentation(MethodInfo methodInfo)
        {
            if (methodInfo == null) return null;

            LoadXmlDocumentation(methodInfo.DeclaringType?.Assembly);

            var key = "M:" + GetMethodXmlKey(methodInfo);
            return _loadedXmlDocumentation.TryGetValue(key, out var documentation) ? documentation : null;
        }

        public string GetDocumentation(ParameterInfo parameterInfo)
        {
            if (parameterInfo?.Member == null) return null;

            var memberDocumentation = GetDocumentation(parameterInfo.Member);
            if (string.IsNullOrEmpty(memberDocumentation)) return null;

            return ExtractParameterDescription(memberDocumentation, parameterInfo.Name);
        }

        public string GetDocumentation(PropertyInfo propertyInfo)
        {
            if (propertyInfo == null) return null;

            LoadXmlDocumentation(propertyInfo.DeclaringType?.Assembly);
            var key = "P:" + XmlDocumentationKeyHelper(propertyInfo.DeclaringType?.FullName, propertyInfo.Name);
            return _loadedXmlDocumentation.TryGetValue(key, out var documentation) ? documentation : null;
        }

        public string GetDocumentation(MemberInfo memberInfo)
        {
            if (memberInfo == null) return null;

            return memberInfo switch
            {
                MethodInfo methodInfo => GetDocumentation(methodInfo),
                PropertyInfo propertyInfo => GetDocumentation(propertyInfo),
                Type type => GetDocumentation(type),
                _ => null
            };
        }

        public string ExtractSummary(string xmlDocumentation)
        {
            if (string.IsNullOrEmpty(xmlDocumentation)) return string.Empty;

            var match = Regex.Match(xmlDocumentation, @"<summary>(.*?)</summary>", RegexOptions.Singleline);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim()
                    .Replace("\r\n", " ")
                    .Replace("\n", " ")
                    .Replace("\r", " ")
                    .Trim();
            }

            return string.Empty;
        }

        public string ExtractParameterDescription(string xmlDocumentation, string parameterName)
        {
            if (string.IsNullOrEmpty(xmlDocumentation) || string.IsNullOrEmpty(parameterName))
                return string.Empty;

            var regexPattern = Regex.Escape($@"<param name=""{parameterName}"">") + ".*?" + Regex.Escape(@"</param>");
            var match = Regex.Match(xmlDocumentation, regexPattern, RegexOptions.Singleline);

            if (match.Success)
            {
                var content = match.Value;
                var innerMatch = Regex.Match(content, $@"<param name=""{parameterName}"">(.*?)</param>", RegexOptions.Singleline);
                if (innerMatch.Success)
                {
                    return innerMatch.Groups[1].Value.Trim()
                        .Replace("\r\n", " ")
                        .Replace("\n", " ")
                        .Replace("\r", " ")
                        .Trim();
                }
            }

            return string.Empty;
        }

        public string ExtractReturnsDescription(string xmlDocumentation)
        {
            if (string.IsNullOrEmpty(xmlDocumentation)) return string.Empty;

            var match = Regex.Match(xmlDocumentation, @"<returns>(.*?)</returns>", RegexOptions.Singleline);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim()
                    .Replace("\r\n", " ")
                    .Replace("\n", " ")
                    .Replace("\r", " ")
                    .Trim();
            }

            return string.Empty;
        }

        private static string GetAssemblyDirectoryPath(Assembly assembly)
        {
            var codeBase = assembly.Location;
            if (string.IsNullOrEmpty(codeBase))
            {
                // Fallback for single-file deployments or when Location is not available
                return AppDomain.CurrentDomain.BaseDirectory;
            }

            return Path.GetDirectoryName(codeBase);
        }

        private static string XmlDocumentationKeyHelper(string typeFullNameString, string memberNameString)
        {
            if (string.IsNullOrEmpty(typeFullNameString)) return string.Empty;

            var key = Regex.Replace(typeFullNameString, @"\[.*\]", string.Empty).Replace('+', '.');
            if (!string.IsNullOrEmpty(memberNameString))
            {
                key += "." + memberNameString;
            }
            return key;
        }

        private string GetMethodXmlKey(MethodInfo methodInfo)
        {
            var key = XmlDocumentationKeyHelper(methodInfo.DeclaringType?.FullName, methodInfo.Name);

            // Handle generic type parameters
            var typeGenericMap = new Dictionary<string, int>();
            var tempTypeGeneric = 0;
            if (methodInfo.DeclaringType?.GetGenericArguments() is { } typeGenerics)
            {
                foreach (var arg in typeGenerics)
                {
                    typeGenericMap[arg.Name] = tempTypeGeneric++;
                }
            }

            // Handle method generic parameters
            var methodGenericMap = new Dictionary<string, int>();
            var tempMethodGeneric = 0;
            if (methodInfo.GetGenericArguments() is { } methodGenerics)
            {
                foreach (var arg in methodGenerics)
                {
                    methodGenericMap[arg.Name] = tempMethodGeneric++;
                }
            }

            // Handle parameters
            var parameters = methodInfo.GetParameters();
            if (parameters.Length > 0)
            {
                var parameterStrings = new List<string>();
                foreach (var parameter in parameters)
                {
                    parameterStrings.Add(GetParameterTypeString(parameter.ParameterType, typeGenericMap, methodGenericMap));
                }
                key += "(" + string.Join(",", parameterStrings) + ")";
            }

            return key;
        }

        private string GetParameterTypeString(Type parameterType, Dictionary<string, int> typeGenericMap, Dictionary<string, int> methodGenericMap)
        {
            if (parameterType.HasElementType)
            {
                var elementType = parameterType.GetElementType();
                var elementTypeString = GetParameterTypeString(elementType, typeGenericMap, methodGenericMap);

                if (parameterType.IsArray)
                {
                    var rank = parameterType.GetArrayRank();
                    if (rank == 1)
                    {
                        return elementTypeString + "[]";
                    }
                    else
                    {
                        var dimensions = string.Join(",", Enumerable.Repeat("0:", rank));
                        return elementTypeString + "[" + dimensions.TrimEnd(':') + "]";
                    }
                }
                else if (parameterType.IsPointer)
                {
                    return elementTypeString + "*";
                }
                else if (parameterType.IsByRef)
                {
                    return elementTypeString + "@";
                }
            }
            else if (parameterType.IsGenericParameter)
            {
                if (methodGenericMap.TryGetValue(parameterType.Name, out var methodIndex))
                {
                    return "``" + methodIndex;
                }
                else if (typeGenericMap.TryGetValue(parameterType.Name, out var typeIndex))
                {
                    return "`" + typeIndex;
                }
            }

            return XmlDocumentationKeyHelper(parameterType.FullName, null);
        }
    }
}

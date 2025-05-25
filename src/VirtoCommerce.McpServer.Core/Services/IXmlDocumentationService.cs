using System;
using System.Reflection;

namespace VirtoCommerce.McpServer.Core.Services
{
    /// <summary>
    /// Service for extracting XML documentation from assemblies at runtime
    /// </summary>
    public interface IXmlDocumentationService
    {
        /// <summary>
        /// Load XML documentation for the specified assembly
        /// </summary>
        /// <param name="assembly">Assembly to load documentation for</param>
        void LoadXmlDocumentation(Assembly assembly);

        /// <summary>
        /// Load XML documentation from a file path
        /// </summary>
        /// <param name="xmlFilePath">Path to the XML documentation file</param>
        void LoadXmlDocumentation(string xmlFilePath);

        /// <summary>
        /// Get documentation for a type
        /// </summary>
        /// <param name="type">Type to get documentation for</param>
        /// <returns>XML documentation or null if not found</returns>
        string GetDocumentation(Type type);

        /// <summary>
        /// Get documentation for a method
        /// </summary>
        /// <param name="methodInfo">Method to get documentation for</param>
        /// <returns>XML documentation or null if not found</returns>
        string GetDocumentation(MethodInfo methodInfo);

        /// <summary>
        /// Get documentation for a parameter
        /// </summary>
        /// <param name="parameterInfo">Parameter to get documentation for</param>
        /// <returns>XML documentation or null if not found</returns>
        string GetDocumentation(ParameterInfo parameterInfo);

        /// <summary>
        /// Get documentation for a property
        /// </summary>
        /// <param name="propertyInfo">Property to get documentation for</param>
        /// <returns>XML documentation or null if not found</returns>
        string GetDocumentation(PropertyInfo propertyInfo);

        /// <summary>
        /// Get documentation for any member
        /// </summary>
        /// <param name="memberInfo">Member to get documentation for</param>
        /// <returns>XML documentation or null if not found</returns>
        string GetDocumentation(MemberInfo memberInfo);

        /// <summary>
        /// Extract the summary text from XML documentation
        /// </summary>
        /// <param name="xmlDocumentation">Raw XML documentation</param>
        /// <returns>Summary text or empty string</returns>
        string ExtractSummary(string xmlDocumentation);

        /// <summary>
        /// Extract parameter descriptions from XML documentation
        /// </summary>
        /// <param name="xmlDocumentation">Raw XML documentation</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <returns>Parameter description or empty string</returns>
        string ExtractParameterDescription(string xmlDocumentation, string parameterName);

        /// <summary>
        /// Extract returns description from XML documentation
        /// </summary>
        /// <param name="xmlDocumentation">Raw XML documentation</param>
        /// <returns>Returns description or empty string</returns>
        string ExtractReturnsDescription(string xmlDocumentation);
    }
}

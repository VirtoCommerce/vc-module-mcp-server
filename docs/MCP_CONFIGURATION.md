# MCP Configuration for VirtoCommerce Modules

This document explains how to configure Model Context Protocol (MCP) support in VirtoCommerce modules, enabling AI applications to interact with your module's APIs.

## Overview

The VirtoCommerce MCP Server automatically discovers and exposes API endpoints from modules that have MCP configuration in their `module.manifest` file. It extracts API descriptions, parameter information, and security settings from:

1. **Module Manifest**: MCP configuration and exposure rules
2. **XML Documentation**: API descriptions and parameter documentation
3. **Controller Attributes**: Security and routing information

## Adding MCP Configuration to Your Module

### 1. Update module.manifest

Add the `<mcpConfiguration>` section to your module's `module.manifest` file:

```xml
<?xml version="1.0" encoding="utf-8"?>
<module>
  <!-- ... existing module configuration ... -->

  <!-- MCP Configuration Section -->
  <mcpConfiguration>
    <!-- Enable MCP for this module -->
    <enabled>true</enabled>

    <!-- Description of what this module provides via MCP -->
    <description>Product management APIs for AI applications</description>

    <!-- MCP version -->
    <version>1.0.0</version>

    <!-- Capabilities this module supports -->
    <capabilities>
      <tools>true</tools>
      <resources>false</resources>
      <prompts>false</prompts>
    </capabilities>

    <!-- API exposure configuration -->
    <apiExposure>
      <!-- Which controllers to include/exclude -->
      <controllers>
        <include pattern="*Controller" />
        <exclude pattern="InternalController" />
      </controllers>

      <!-- Which HTTP methods to expose -->
      <methods>
        <include httpMethod="GET" />
        <include httpMethod="POST" />
        <include httpMethod="PUT" />
        <include httpMethod="DELETE" />
      </methods>

      <!-- Security configuration -->
      <security>
        <requireAuthentication>true</requireAuthentication>
        <respectExistingAuthorization>true</respectExistingAuthorization>
        <mcpPermissions>
          <permission>YourModule:access</permission>
        </mcpPermissions>
      </security>
    </apiExposure>

    <!-- Tool naming configuration -->
    <toolNaming>
      <convention>module_controller_action</convention>
      <removeControllerSuffix>true</removeControllerSuffix>
      <useCamelCase>false</useCamelCase>
      <separator>_</separator>
    </toolNaming>
  </mcpConfiguration>
</module>
```

### 2. Add XML Documentation to Controllers

The MCP server extracts API descriptions from XML documentation comments. Add comprehensive documentation to your controllers:

```csharp
/// <summary>
/// Product management API controller
/// </summary>
[Route("api/products")]
[ApiController]
public class ProductController : Controller
{
    /// <summary>
    /// Get a list of products with optional filtering
    /// </summary>
    /// <param name="skip">Number of products to skip for pagination</param>
    /// <param name="take">Number of products to return (max 100)</param>
    /// <param name="categoryId">Filter by category ID</param>
    /// <param name="searchTerm">Search term to filter products</param>
    /// <remarks>
    /// This endpoint returns a paginated list of products. You can filter by category
    /// and search term. The response includes product details, pricing, and availability.
    /// </remarks>
    /// <returns>List of products matching the criteria</returns>
    /// <response code="200">Returns the list of products successfully</response>
    /// <response code="400">Invalid parameters provided</response>
    [HttpGet]
    [Authorize("catalog:read")]
    [ProducesResponseType(typeof(ProductSearchResult), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<ProductSearchResult>> GetProducts(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string categoryId = null,
        [FromQuery] string searchTerm = null)
    {
        // Implementation...
    }

    /// <summary>
    /// Create a new product
    /// </summary>
    /// <param name="product">Product data to create</param>
    /// <remarks>
    /// Creates a new product with the provided information. The product will be
    /// validated and assigned a unique ID upon successful creation.
    /// </remarks>
    /// <returns>The created product with assigned ID</returns>
    /// <response code="201">Product created successfully</response>
    /// <response code="400">Invalid product data</response>
    /// <response code="403">Insufficient permissions</response>
    [HttpPost]
    [Authorize("catalog:create")]
    [ProducesResponseType(typeof(Product), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    public async Task<ActionResult<Product>> CreateProduct([FromBody] Product product)
    {
        // Implementation...
    }
}
```

### 3. Enable XML Documentation Generation

Ensure your project generates XML documentation files:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <!-- Enable XML documentation generation -->
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <DocumentationFile>bin\$(Configuration)\$(TargetFramework)\$(AssemblyName).xml</DocumentationFile>
  </PropertyGroup>
</Project>
```

## Configuration Options Reference

### mcpConfiguration Element

| Element | Required | Description |
|---------|----------|-------------|
| `enabled` | Yes | Whether MCP is enabled for this module |
| `description` | No | Human-readable description of module's MCP capabilities |
| `version` | No | MCP configuration version (default: "1.0.0") |
| `capabilities` | No | Supported MCP capabilities |
| `apiExposure` | No | Rules for which APIs to expose |
| `toolNaming` | No | How to generate MCP tool names |

### capabilities Element

| Element | Default | Description |
|---------|---------|-------------|
| `tools` | true | Whether to expose APIs as MCP tools |
| `resources` | false | Whether to expose resources (future feature) |
| `prompts` | false | Whether to expose prompts (future feature) |

### apiExposure Element

#### controllers Section

```xml
<controllers>
  <!-- Include patterns (wildcard support) -->
  <include pattern="*Controller" />
  <include pattern="ProductApiController" />

  <!-- Exclude patterns (takes precedence over includes) -->
  <exclude pattern="InternalController" />
  <exclude pattern="Debug*" />
</controllers>
```

#### methods Section

```xml
<methods>
  <!-- HTTP methods to expose -->
  <include httpMethod="GET" />
  <include httpMethod="POST" />
  <include httpMethod="PUT" />
  <include httpMethod="DELETE" />

  <!-- HTTP methods to exclude -->
  <exclude httpMethod="PATCH" />
</methods>
```

#### security Section

```xml
<security>
  <!-- Require authentication for all MCP tools -->
  <requireAuthentication>true</requireAuthentication>

  <!-- Honor existing [Authorize] attributes on controllers/actions -->
  <respectExistingAuthorization>true</respectExistingAuthorization>

  <!-- Additional MCP-specific permissions -->
  <mcpPermissions>
    <permission>YourModule:access</permission>
    <permission>mcp:use</permission>
  </mcpPermissions>
</security>
```

### toolNaming Element

| Element | Default | Description |
|---------|---------|-------------|
| `convention` | "module_controller_action" | Naming convention for tools |
| `removeControllerSuffix` | true | Remove "Controller" from names |
| `useCamelCase` | false | Use camelCase instead of lowercase |
| `separator` | "_" | Separator between name parts |

#### Naming Conventions

- `module_controller_action`: `yourmodule_product_get`
- `controller_action`: `product_get`
- `module_action`: `yourmodule_get`
- `method_controller_action`: `get_product_list`

## Security Integration

The MCP server automatically extracts security information from your controllers:

### Authorization Attributes

```csharp
[Authorize("catalog:read")]  // Extracted as required permission
[Authorize(Roles = "Admin")] // Extracted as required role
[AllowAnonymous]             // Allows anonymous access
```

### VirtoCommerce Permissions

The system integrates with VirtoCommerce's permission system:

```csharp
[Authorize(ModuleConstants.Security.Permissions.Read)]
```

These permissions are automatically included in the MCP tool's security requirements.

## Generated MCP Tools

For each discovered API endpoint, the MCP server generates a tool with:

1. **Name**: Generated based on your naming configuration
2. **Description**: Extracted from XML `<summary>` comments
3. **Input Schema**: Automatically generated from method parameters
4. **Security Requirements**: Extracted from authorization attributes

### Example Generated Tool

```json
{
  "name": "catalog_product_get",
  "description": "Get a list of products with optional filtering",
  "inputSchema": {
    "type": "object",
    "properties": {
      "skip": {
        "type": "integer",
        "description": "Number of products to skip for pagination",
        "required": false
      },
      "take": {
        "type": "integer",
        "description": "Number of products to return (max 100)",
        "required": false
      },
      "categoryId": {
        "type": "string",
        "description": "Filter by category ID",
        "required": false
      },
      "searchTerm": {
        "type": "string",
        "description": "Search term to filter products",
        "required": false
      }
    }
  }
}
```

## Best Practices

### 1. Documentation Quality

- Write clear, comprehensive XML documentation
- Include parameter descriptions and examples
- Document return types and possible responses
- Use `<remarks>` for detailed explanations

### 2. Security Configuration

- Always require authentication unless specifically needed
- Use specific permissions rather than broad access
- Honor existing authorization attributes
- Document security requirements in API comments

### 3. API Design for AI

- Use descriptive parameter names
- Provide sensible defaults
- Include validation and error handling
- Design for discoverability

### 4. Tool Naming

- Choose consistent naming conventions
- Use descriptive, action-oriented names
- Avoid technical jargon in tool names
- Consider the AI application user experience

## Testing Your Configuration

1. **Enable MCP in your module manifest**
2. **Build and deploy your module**
3. **Check the MCP server logs** for discovered endpoints
4. **Use an MCP client** to list and test tools
5. **Verify security and permissions** work correctly

## Troubleshooting

### No Tools Generated

- Check that `enabled` is set to `true`
- Verify controller patterns match your controllers
- Ensure HTTP method attributes are present
- Check MCP server logs for errors

### Missing Descriptions

- Verify XML documentation file is generated
- Check that XML comments are properly formatted
- Ensure documentation file is in the same directory as assembly

### Security Issues

- Verify permission constants are correct
- Check that authorization attributes are properly configured
- Test with appropriate user roles and permissions

### Tool Naming Issues

- Review naming convention configuration
- Check for naming conflicts between modules
- Verify separator and casing settings

## Example Modules

See the `docs/examples/` directory for complete examples of modules with MCP configuration.

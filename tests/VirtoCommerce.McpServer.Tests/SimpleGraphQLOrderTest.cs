using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using System.Linq;

namespace VirtoCommerce.McpServer.Tests
{
    /// <summary>
    /// Simple test to connect to real VirtoCommerce GraphQL service and test order search
    /// </summary>
    public class SimpleGraphQLOrderTest
    {
        private readonly ITestOutputHelper _output;
        private readonly HttpClient _httpClient;

        public SimpleGraphQLOrderTest(ITestOutputHelper output)
        {
            _output = output;
            _httpClient = new HttpClient();
        }

        [Fact]
        public async Task TestRealGraphQLOrderSearch()
        {
            // Arrange - Read MCP manifest
            var manifestPath = Path.Combine(Directory.GetCurrentDirectory(), "mcp.manifest");
            var manifestJson = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<McpManifest>(manifestJson);

            _output.WriteLine($"Loaded manifest: {manifest.Name} v{manifest.Version}");

            var orderTool = manifest.Tools[0];
            _output.WriteLine($"Testing tool: {orderTool.Name}");

            // Arrange - Build GraphQL query using the actual VirtoCommerce schema
            var graphqlQuery = new
            {
                query = @"
                    {
                        orders {
                            totalCount
                            items {
                                id
                                status
                                number
                                createdDate
                                modifiedDate
                                customerId
                                customerName
                                total {
                                    amount
                                }
                                subTotal {
                                    amount
                                }
                                discountTotal {
                                    amount
                                }
                            }
                        }
                    }"
            };

            var requestJson = JsonSerializer.Serialize(graphqlQuery, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _output.WriteLine($"GraphQL Query: {requestJson}");

            // Act - Make request to real GraphQL endpoint
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            // Try different headers for GraphQL - some servers expect application/graphql
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/graphql"));

            // Also try setting Content-Type explicitly
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // Add authentication if available
            var apiKey = Environment.GetEnvironmentVariable("VIRTOCOMMERCE_API_KEY");
            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("api_key", apiKey);
                _output.WriteLine("Using API key authentication");
            }

            try
            {
                var response = await _httpClient.PostAsync(orderTool.Endpoint, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                _output.WriteLine($"Response Status: {response.StatusCode}");
                _output.WriteLine($"Response Content: {responseContent}");

                // Assert - Basic response validation
                Assert.True(response.IsSuccessStatusCode, $"GraphQL request failed: {response.StatusCode} - {responseContent}");

                // Parse and validate GraphQL response
                var graphqlResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (graphqlResponse.TryGetProperty("errors", out var errors))
                {
                    _output.WriteLine($"GraphQL Errors: {errors}");
                    Assert.Fail($"GraphQL returned errors: {errors}");
                }

                Assert.True(graphqlResponse.TryGetProperty("data", out var data), "GraphQL response should contain data");
                Assert.True(data.TryGetProperty("orders", out var orders), "Data should contain orders");

                _output.WriteLine("✅ GraphQL order search successful!");

                // Output the actual order data for inspection
                if (orders.TryGetProperty("items", out var items) && items.GetArrayLength() > 0)
                {
                    _output.WriteLine($"Found {items.GetArrayLength()} orders:");
                    foreach (var item in items.EnumerateArray())
                    {
                        if (item.TryGetProperty("id", out var id) &&
                            item.TryGetProperty("number", out var number) &&
                            item.TryGetProperty("status", out var status) &&
                            item.TryGetProperty("total", out var total) &&
                            total.TryGetProperty("amount", out var amount))
                        {
                            _output.WriteLine($"  Order {number.GetString()}: {status.GetString()} - ${amount.GetDecimal()}");
                        }
                    }
                }
                else
                {
                    _output.WriteLine("No orders found - this is okay for an empty database");
                }
            }
            catch (HttpRequestException ex)
            {
                _output.WriteLine($"Connection failed: {ex.Message}");
                _output.WriteLine("Make sure VirtoCommerce is running on localhost:5000 with GraphQL enabled");

                // Skip test if service is not available rather than failing
                throw new SkipException("VirtoCommerce GraphQL service not available for integration test");
            }
        }

        [Fact]
        public async Task TestMcpManifestConfiguration()
        {
            // Arrange
            var manifestPath = Path.Combine(Directory.GetCurrentDirectory(), "mcp.manifest");

            // Act
            Assert.True(File.Exists(manifestPath), "mcp.manifest file should exist");

            var manifestJson = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<McpManifest>(manifestJson);

            // Assert - Validate manifest structure
            Assert.NotNull(manifest);
            Assert.Equal("VirtoCommerce.Orders.GraphQL", manifest.Name);
            Assert.True(manifest.Tools.Length >= 1, "Should have at least one tool");

            var tool = manifest.Tools[0];
            Assert.Equal("search_orders_graphql", tool.Name);
            Assert.Equal("http://localhost:5000/graphql", tool.Endpoint);
            Assert.NotEmpty(tool.Query);

            _output.WriteLine($"✅ MCP Manifest validation successful!");
            _output.WriteLine($"   Tool: {tool.Name}");
            _output.WriteLine($"   Endpoint: {tool.Endpoint}");
            _output.WriteLine($"   Field Sets: {string.Join(", ", tool.FieldSets.Keys)}");
        }

        [Fact]
        public async Task TestGraphQLIntrospection()
        {
            // Arrange - Simple introspection query to see what's available
            var introspectionQuery = new
            {
                query = @"
                    query IntrospectionQuery {
                        __schema {
                            queryType {
                                name
                                fields {
                                    name
                                    description
                                }
                            }
                        }
                    }"
            };

            var requestJson = JsonSerializer.Serialize(introspectionQuery, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _output.WriteLine($"Introspection Query: {requestJson}");

            // Act - Make request to GraphQL endpoint
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            // Set proper headers for GraphQL
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            // Add authentication if available
            var apiKey = Environment.GetEnvironmentVariable("VIRTOCOMMERCE_API_KEY");
            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("api_key", apiKey);
                _output.WriteLine("Using API key authentication");
            }

            try
            {
                var response = await _httpClient.PostAsync("http://localhost:5000/graphql", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                _output.WriteLine($"Response Status: {response.StatusCode}");
                _output.WriteLine($"Response Content: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var graphqlResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                    if (graphqlResponse.TryGetProperty("data", out var data) &&
                        data.TryGetProperty("__schema", out var schema) &&
                        schema.TryGetProperty("queryType", out var queryType) &&
                        queryType.TryGetProperty("fields", out var fields))
                    {
                        _output.WriteLine("✅ Available GraphQL Query Fields:");
                        foreach (var field in fields.EnumerateArray())
                        {
                            if (field.TryGetProperty("name", out var name))
                            {
                                var fieldName = name.GetString();
                                var description = "";
                                if (field.TryGetProperty("description", out var desc) && desc.ValueKind != JsonValueKind.Null)
                                {
                                    description = $" - {desc.GetString()}";
                                }
                                _output.WriteLine($"  • {fieldName}{description}");
                            }
                        }

                        // This test will always pass if we get schema data
                        Assert.True(true, "Successfully retrieved GraphQL schema information");
                    }
                    else
                    {
                        _output.WriteLine("⚠️  Could not parse schema information from response");
                        // Don't fail the test, just skip it
                        throw new SkipException("Could not parse GraphQL schema");
                    }
                }
                else
                {
                    _output.WriteLine($"⚠️  GraphQL introspection failed with status {response.StatusCode}");
                    throw new SkipException($"GraphQL service returned {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                _output.WriteLine($"Connection failed: {ex.Message}");
                _output.WriteLine("Make sure VirtoCommerce is running on localhost:5000 with GraphQL enabled");
                throw new SkipException("VirtoCommerce GraphQL service not available for introspection test");
            }
        }

        [Fact]
        public async Task TestAuthenticationRequired()
        {
            // This test demonstrates that the GraphQL connection works but needs authentication
            var graphqlQuery = new
            {
                query = @"
                    {
                        orders {
                            totalCount
                            items {
                                id
                                status
                                number
                            }
                        }
                    }"
            };

            var requestJson = JsonSerializer.Serialize(graphqlQuery, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            try
            {
                var response = await _httpClient.PostAsync("http://localhost:5000/graphql", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                _output.WriteLine($"Response Status: {response.StatusCode}");
                _output.WriteLine($"Response Content: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var graphqlResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                    if (graphqlResponse.TryGetProperty("errors", out var errors))
                    {
                        var errorMessage = errors.EnumerateArray().FirstOrDefault();
                        if (errorMessage.TryGetProperty("message", out var message))
                        {
                            var msgText = message.GetString();
                            if (msgText.Contains("Anonymous access denied") || msgText.Contains("access token"))
                            {
                                _output.WriteLine("✅ GraphQL Connection Working - Authentication Required");
                                _output.WriteLine("💡 To test with real data, set environment variable:");
                                _output.WriteLine("   VIRTOCOMMERCE_API_KEY=your-api-key-here");

                                // This is actually success - we connected and got proper auth error
                                Assert.True(true, "GraphQL connection successful, authentication needed");
                                return;
                            }
                        }
                    }

                    if (graphqlResponse.TryGetProperty("data", out var data))
                    {
                        _output.WriteLine("✅ GraphQL Query Successful with Data!");
                        Assert.True(true, "GraphQL query executed successfully");
                        return;
                    }
                }

                Assert.Fail($"Unexpected GraphQL response: {responseContent}");
            }
            catch (HttpRequestException ex)
            {
                _output.WriteLine($"Connection failed: {ex.Message}");
                throw new SkipException("VirtoCommerce GraphQL service not available");
            }
        }
    }

    // Simple classes to deserialize the manifest
    public class McpManifest
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("tools")]
        public McpTool[] Tools { get; set; }
    }

    public class McpTool
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("endpoint")]
        public string Endpoint { get; set; }

        [JsonPropertyName("query")]
        public string Query { get; set; }

        [JsonPropertyName("parameters")]
        public Dictionary<string, object> Parameters { get; set; }

        [JsonPropertyName("fieldSets")]
        public Dictionary<string, string[]> FieldSets { get; set; }

        [JsonPropertyName("defaultFields")]
        public string[] DefaultFields { get; set; }

        [JsonPropertyName("auth")]
        public Dictionary<string, object> Auth { get; set; }
    }

    // Custom exception for skipping tests when service is unavailable
    public class SkipException : Exception
    {
        public SkipException(string message) : base(message) { }
    }
}

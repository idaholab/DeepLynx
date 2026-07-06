using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

namespace deeplynx.api.OpenApi;

internal static class NexusOpenApiExtensions
{
    public static IServiceCollection AddNexusOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info = new OpenApiInfo
                {
                    Version = "v1",
                    Title = "DeepLynx Nexus API",
                    Description =
                        "DeepLynx Nexus API for managing organizational data and relationships. Endpoints are organized by Organization-level (/api/organizations/{organizationId}) and Project-level (/api/projects/{projectId}) scopes.",
                    Contact = new OpenApiContact
                    {
                        Name = "Nexus Support",
                        Email = "Jaren.Brownlee@inl.gov"
                    }
                };

                document.Servers = new List<OpenApiServer>
                {
                    new()
                    {
                        Url = "http://localhost:5095/api/v1/",
                        Description = "Local Development"
                    },
                    new()
                    {
                        Url = "http://localhost:5000/api/v1/",
                        Description = "Docker Environment"
                    },
                    new()
                    {
                        Url = "https://deeplynx.inl.gov/api/v1/",
                        Description = "Production"
                    },
                    new()
                    {
                        Url = "https://deeplynx.dev.inl.gov/api/v1/",
                        Description = "Develop"
                    },
                    new()
                    {
                        Url = "https://deeplynx-test.dev.inl.gov/api/v1/",
                        Description = "Test"
                    },
                    new()
                    {
                        Url = "http://localhost:5095/api/v1/",
                        Description = "Local Development"
                    }
                };
                document.ExternalDocs = new OpenApiExternalDocs
                {
                    Description = "Nexus Documentation",
                    Url = new Uri("https://deeplynx.inl.gov/docs")
                };

                document.Tags = new HashSet<OpenApiTag>
                {
                    new() { Name = "Organization", Description = "Organization management" },
                    new() { Name = "Project", Description = "Project management" },
                    new() { Name = "User", Description = "User management" },
                    new() { Name = "Group", Description = "Group management" },
                    new() { Name = "Service Accounts", Description = "Service account management" },
                    new() { Name = "Test Accounts", Description = "Test account management (System Administrators)" },
                    new() { Name = "Lattice", Description = "Useful data views for DeepLynx Lattice use" },
                    new() { Name = "Organization - AI Model Config", Description = "AI model configuration management" },
                    new() { Name = "Project - AI Model Config", Description = "AI model configuration management" },
                    new() { Name = "User Model Token", Description = "User AI model token management" },
                    new() { Name = "Insight", Description = "Deeplynx Insight management" },
                    new() { Name = "OauthHandshake", Description = "OAuth2 authorization flow" },
                    new() { Name = "Token", Description = "API key and JWT token management" },
                    new() { Name = "OauthApplication", Description = "OAuth apps" },
                    new() { Name = "Organization - Class", Description = "Organization-level class operations" },
                    new() { Name = "Project - Class", Description = "Project-level class operations" },
                    new() { Name = "Record", Description = "Record management" },
                    new() { Name = "Record Collection", Description = "Record Collection management"},
                    new() { Name = "File", Description = "File operations" },
                    new() { Name = "Provenance", Description = "Data Provenance" },
                    new() { Name = "Metadata", Description = "Metadata operations" },
                    new() { Name = "Historical Record", Description = "Record history" },
                    new() { Name = "Historical Edge", Description = "Edge history" },
                    new() { Name = "Edge", Description = "Edges" },
                    new() { Name = "Organization - DataSource", Description = "Organization-level data sources" },
                    new() { Name = "Project - DataSource", Description = "Project-level data sources" },
                    new() { Name = "Event", Description = "Event logs" },
                    new() { Name = "Organization - Object Storage", Description = "Organization-level storage" },
                    new() { Name = "Project - Object Storage", Description = "Project-level storage" },
                    new() { Name = "Organization - Permission", Description = "Organization-level permissions" },
                    new() { Name = "Project - Permission", Description = "Project-level permissions" },
                    new() { Name = "Query", Description = "Search and filtering" },
                    new() { Name = "Saved Search", Description = "Saved searches" },
                    new() { Name = "Organization - Relationship", Description = "Organization-level relationships" },
                    new() { Name = "Project - Relationship", Description = "Project-level relationships" },
                    new() { Name = "Organization - Role", Description = "Organization-level roles" },
                    new() { Name = "Project - Role", Description = "Project-level roles" },
                    new() { Name = "Organization - Sensitivity Label", Description = "Organization-level labels" },
                    new() { Name = "Project - Sensitivity Label", Description = "Project-level labels" },
                    new() { Name = "Organization - Tag", Description = "Organization-level tags" },
                    new() { Name = "Project - Tag", Description = "Project-level tags" },
                    new() { Name = "Olap", Description = "OLAP tabular file operations" },
                    new() { Name = "Metrics", Description = "System Statistics" },
                    new() { Name = "Airflow", Description = "Apache Airflow DAG management" },
                    new() { Name = "Notification", Description = "Notifications" },
                    new() { Name = "Maintenance", Description = "Maintenance" }
                };

                var tagGroups = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "Administration",
                        ["tags"] = new JsonArray { "Organization", "Project", "User", "Group", "Service Accounts", "Test Accounts" }
                    },
                    new JsonObject
                    {
                        ["name"] = "AI Services",
                        ["tags"] = new JsonArray
                        {
                            "Lattice", "Organization - AI Model Config", "Project - AI Model Config", "User Model Token",
                            "Insight"
                        }
                    },
                    new JsonObject
                    {
                        ["name"] = "Authentication",
                        ["tags"] = new JsonArray { "OauthHandshake", "Token", "OauthApplication" }
                    },
                    new JsonObject
                    {
                        ["name"] = "Class",
                        ["tags"] = new JsonArray { "Organization - Class", "Project - Class" }
                    },
                    new JsonObject
                    {
                        ["name"] = "Data",
                        ["tags"] = new JsonArray
                            { "Record", "Record Collection", "Historical Record", "Edge", "Historical Edge", "File", "Metadata", "Provenance" }
                    },
                    new JsonObject
                    {
                        ["name"] = "DataSource",
                        ["tags"] = new JsonArray { "Organization - DataSource", "Project - DataSource" }
                    },
                    new JsonObject
                    {
                        ["name"] = "Events",
                        ["tags"] = new JsonArray { "Event" }
                    },
                    new JsonObject
                    {
                        ["name"] = "Object Storage",
                        ["tags"] = new JsonArray { "Organization - Object Storage", "Project - Object Storage" }
                    },
                    new JsonObject
                    {
                        ["name"] = "Permission",
                        ["tags"] = new JsonArray { "Organization - Permission", "Project - Permission" }
                    },
                    new JsonObject
                    {
                        ["name"] = "Query",
                        ["tags"] = new JsonArray { "Query", "Saved Search" }
                    },
                    new JsonObject
                    {
                        ["name"] = "Relationship",
                        ["tags"] = new JsonArray { "Organization - Relationship", "Project - Relationship" }
                    },
                    new JsonObject
                    {
                        ["name"] = "Role",
                        ["tags"] = new JsonArray { "Organization - Role", "Project - Role" }
                    },
                    new JsonObject
                    {
                        ["name"] = "Sensitivity Label",
                        ["tags"] = new JsonArray { "Organization - Sensitivity Label", "Project - Sensitivity Label" }
                    },
                    new JsonObject
                    {
                        ["name"] = "Tag",
                        ["tags"] = new JsonArray { "Organization - Tag", "Project - Tag" }
                    },
                    new JsonObject
                    {
                        ["name"] = "Olap",
                        ["tags"] = new JsonArray { "Olap" }
                    },
                    new JsonObject
                    {
                        ["name"] = "Metrics",
                        ["tags"] = new JsonArray { "Metrics", "Organization - Metrics", "Project - Metrics" }
                    },
                    new JsonObject
                    {
                        ["name"] = "Integrations",
                        ["tags"] = new JsonArray { "Airflow" }
                    },
                    new JsonObject
                    {
                        ["name"] = "Other",
                        ["tags"] = new JsonArray { "Notification", "Maintenance" }
                    }
                };

                document.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                document.Extensions["x-tagGroups"] = new JsonNodeExtension(tagGroups);

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Enter your JWT token"
                };
                document.Security = new List<OpenApiSecurityRequirement>
                {
                    new()
                    {
                        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                    }
                };

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, cancellationToken) =>
            {
                operation.Responses ??= new OpenApiResponses();
                operation.Responses.TryAdd("401", new OpenApiResponse
                {
                    Description = "Unauthorized - Invalid or missing authentication token"
                });
                operation.Responses.TryAdd("403", new OpenApiResponse
                {
                    Description = "Forbidden - Insufficient permissions"
                });
                operation.Responses.TryAdd("500", new OpenApiResponse
                {
                    Description = "Internal Server Error"
                });

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, cancellationToken) =>
            {
                RemoveRedundantJsonContentTypes(operation.RequestBody?.Content);

                if (operation.Responses is not null)
                {
                    foreach (var response in operation.Responses.Values)
                        RemoveRedundantJsonContentTypes(response.Content, removePlainText: true);
                }

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, cancellationToken) =>
            {
                var endpointMetadata = context.Description.ActionDescriptor.EndpointMetadata;
                if (endpointMetadata.OfType<IAllowAnonymous>().Any())
                    operation.Security = [];

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, cancellationToken) =>
            {
                if (operation.Parameters is null) return Task.CompletedTask;

                foreach (var parameter in operation.Parameters.OfType<OpenApiParameter>())
                {
                    if (parameter.In != ParameterLocation.Query) continue;

                    var paramDesc = context.Description.ParameterDescriptions
                        .FirstOrDefault(p => p.Name == parameter.Name);

                    if (paramDesc?.Type is { IsValueType: true } t
                        && Nullable.GetUnderlyingType(t) is null)
                        parameter.Required = true;
                }

                return Task.CompletedTask;
            });

            options.AddSchemaTransformer((schema, context, cancellationToken) =>
            {
                if (context.JsonPropertyInfo?.Name.Equals("data", StringComparison.OrdinalIgnoreCase) == true
                    && context.JsonTypeInfo.Type == typeof(object[][]))
                {
                    schema.Type = JsonSchemaType.Array;
                    schema.Items = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Array,
                        Items = new OpenApiSchema()
                    };
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }

    private static void RemoveRedundantJsonContentTypes(IDictionary<string, OpenApiMediaType>? content, bool removePlainText = false)
    {
        if (content is null || !content.TryGetValue("application/json", out var jsonMediaType)) return;

        content.Remove("text/json");
        content.Remove("application/*+json");
        if (removePlainText && jsonMediaType.Schema?.Type != JsonSchemaType.String)
            content.Remove("text/plain");
    }
}

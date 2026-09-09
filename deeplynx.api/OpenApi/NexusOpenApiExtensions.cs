using System.Text.Json.Nodes;
using deeplynx.api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace deeplynx.api.OpenApi;

internal static class NexusOpenApiExtensions
{
    public static IServiceCollection AddNexusOpenApi(this IServiceCollection services)
    {
        foreach (var documentName in NexusApiVersions.OpenApiDocumentNames)
            services.AddNexusOpenApiDocument(documentName);

        return services;
    }

    private static IServiceCollection AddNexusOpenApiDocument(this IServiceCollection services, string documentName)
    {
        services.AddOpenApi(documentName, options =>
        {
            options.AddScalarTransformers();

            options.ShouldInclude = apiDescription =>
                string.Equals(apiDescription.GroupName, documentName, StringComparison.OrdinalIgnoreCase);

            options.AddOperationTransformer((operation, context, cancellationToken) =>
            {
                ApplyDeprecation(operation, context.Description.IsDeprecated());
                return Task.CompletedTask;
            });

            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info = new OpenApiInfo
                {
                    Version = context.DocumentName,
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
                        Url = "http://localhost:5095",
                        Description = "Local Development"
                    },
                    new()
                    {
                        Url = "http://localhost:5000",
                        Description = "Docker Environment"
                    },
                    new()
                    {
                        Url = "https://deeplynx.inl.gov",
                        Description = "Production"
                    },
                    new()
                    {
                        Url = "https://deeplynx.dev.inl.gov",
                        Description = "Develop"
                    },
                    new()
                    {
                        Url = "https://deeplynx-test.zba.inl.gov",
                        Description = "Test"
                    }
                };
                document.ExternalDocs = new OpenApiExternalDocs
                {
                    Description = "Nexus Documentation",
                    Url = new Uri("https://deeplynx.inl.gov/docs")
                };

                var tags = new List<OpenApiTag>
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
                    new() { Name = "Record Collection", Description = "Record Collection management" },
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

                var usedTagNames = GetUsedTagNames(document);
                document.Tags = tags
                    .Where(tag => tag.Name is not null && usedTagNames.Contains(tag.Name))
                    .ToHashSet();

                var tagGroups = CreateTagGroups(usedTagNames);

                document.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                if (tagGroups.Count > 0)
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

                if (documentName == "v2")
                {
                    document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();

                    document.Components.Schemas["ProblemDetails"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["type"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "A URI reference identifying the problem type" },
                            ["title"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "A short, human-readable summary of the problem" },
                            ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Description = "The HTTP status code" },
                            ["detail"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "A human-readable explanation specific to this occurrence" },
                            ["instance"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "A URI reference identifying the specific occurrence" }
                        }
                    };
                }

                return Task.CompletedTask;
            });

            if (documentName == "v2")
            {
                options.AddOperationTransformer((operation, context, cancellationToken) =>
                {
                    operation.Responses ??= new OpenApiResponses();

                    operation.Responses.TryAdd("401", CreateProblemDetailsResponse("Unauthorized - Invalid or missing authentication token"));
                    operation.Responses.TryAdd("403", CreateProblemDetailsResponse("Forbidden - Insufficient permissions"));
                    operation.Responses.TryAdd("404", CreateProblemDetailsResponse("Not Found - The requested resource does not exist"));
                    operation.Responses.TryAdd("409", CreateProblemDetailsResponse("Conflict - The request could not be completed due to a conflict with current state"));
                    operation.Responses.TryAdd("500", CreateProblemDetailsResponse("Internal Server Error"));

                    return Task.CompletedTask;
                });
            }
            else
            {
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
            }


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

            options.AddSchemaTransformer((schema, context, cancellationToken) =>
            {
                var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type)
                        ?? context.JsonTypeInfo.Type;

                if (type == typeof(DateTime)
                    || type == typeof(DateTimeOffset)
                    || type == typeof(DateTime?)
                    || type == typeof(DateTimeOffset?))
                {
                    schema.Type = JsonSchemaType.String;
                    schema.Format = "date-time";
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }

    internal static void ApplyDeprecation(OpenApiOperation operation, bool isDeprecated)
    {
        if (isDeprecated)
            operation.Deprecated = true;
    }

    private static HashSet<string> GetUsedTagNames(OpenApiDocument document)
    {
        var usedTagNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (document.Paths is null)
            return usedTagNames;

        foreach (var pathItem in document.Paths.Values)
        {
            if (pathItem is null)
                continue;

            if (pathItem.Operations is null)
                continue;

            foreach (var operation in pathItem.Operations.Values)
            {
                if (operation is null)
                    continue;

                foreach (var tag in operation.Tags ?? Enumerable.Empty<OpenApiTagReference>())
                {
                    if (!string.IsNullOrWhiteSpace(tag.Name))
                        usedTagNames.Add(tag.Name);
                }
            }
        }

        return usedTagNames;
    }

    private static JsonArray CreateTagGroups(ISet<string> usedTagNames)
    {
        var tagGroups = new (string Name, string[] Tags)[]
        {
            ("Administration", ["Organization", "Project", "User", "Group", "Service Accounts", "Test Accounts"]),
            ("AI Services",
            [
                "Lattice", "Organization - AI Model Config", "Project - AI Model Config", "User Model Token", "Insight"
            ]),
            ("Authentication", ["OauthHandshake", "Token", "OauthApplication"]),
            ("Class", ["Organization - Class", "Project - Class"]),
            ("Data", ["Record", "Record Collection", "Historical Record", "Edge", "Historical Edge", "File", "Metadata", "Provenance"]),
            ("DataSource", ["Organization - DataSource", "Project - DataSource"]),
            ("Events", ["Event"]),
            ("Object Storage", ["Organization - Object Storage", "Project - Object Storage"]),
            ("Permission", ["Organization - Permission", "Project - Permission"]),
            ("Query", ["Query", "Saved Search"]),
            ("Relationship", ["Organization - Relationship", "Project - Relationship"]),
            ("Role", ["Organization - Role", "Project - Role"]),
            ("Sensitivity Label", ["Organization - Sensitivity Label", "Project - Sensitivity Label"]),
            ("Tag", ["Organization - Tag", "Project - Tag"]),
            ("Olap", ["Olap"]),
            ("Metrics", ["Metrics", "Organization - Metrics", "Project - Metrics"]),
            ("Integrations", ["Airflow"]),
            ("Other", ["Notification", "Maintenance"])
        };

        var filteredGroups = new JsonArray();
        foreach (var (name, tags) in tagGroups)
        {
            var usedTags = tags
                .Where(usedTagNames.Contains)
                .Select(tag => JsonValue.Create(tag))
                .ToArray<JsonNode?>();

            if (usedTags.Length == 0)
                continue;

            filteredGroups.Add(new JsonObject
            {
                ["name"] = name,
                ["tags"] = new JsonArray(usedTags)
            });
        }

        return filteredGroups;
    }

    private static void RemoveRedundantJsonContentTypes(IDictionary<string, OpenApiMediaType>? content, bool removePlainText = false)
    {
        if (content is null || !content.TryGetValue("application/json", out var jsonMediaType)) return;

        content.Remove("text/json");
        content.Remove("application/*+json");
        if (removePlainText && jsonMediaType.Schema?.Type != JsonSchemaType.String)
            content.Remove("text/plain");
    }

    private static OpenApiResponse CreateProblemDetailsResponse(string description)
    {
        return new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new()
                {
                    Schema = new OpenApiSchemaReference("ProblemDetails")
                }
            }
        };
    }
}

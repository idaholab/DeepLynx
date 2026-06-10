using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.BigData;
using deeplynx.helpers.ExceptionHandlers;
using deeplynx.helpers.Hubs;
using deeplynx.interfaces;
using deeplynx.api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Apache.Arrow.Flight.Server;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Scalar.AspNetCore;
using Serilog;
using Log = Serilog.Log;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => { options.Limits.MaxRequestBodySize = 2L * 1024 * 1024 * 1024; });

builder.Services.Configure<FormOptions>(options => { options.MultipartBodyLengthLimit = 2L * 1024 * 1024 * 1024; });

builder.Services.AddGrpc().AddFlightServer<NexusFlightServer>();
builder.Services.AddGrpcReflection();

var connectionString = ConnectionStringsProvider.GetPostgresConnectionString(builder.Configuration);

// ----------------------------------
// Logger Setup
// ----------------------------------
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.PostgreSQL(
        connectionString,
        "logs",
        schemaName: "deeplynx",
        needAutoCreateTable: true,
        batchSizeLimit: 50,
        period: TimeSpan.FromSeconds(15))
    .CreateLogger();
try
{
    Log.Information("Application starting up");

    builder.Services.AddLogging(loggingBuilder =>
    {
        loggingBuilder.ClearProviders();
        loggingBuilder.AddSerilog(dispose: true);
    });

    // ----------------------------------
    // CORS Configuration
    // ----------------------------------
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy
                .WithOrigins(
                    "http://localhost:5095",
                    "http://localhost:3000",
                    "http://localhost:3001",
                    "http://ui:3000",
                    "http://localhost:5173",
                    "https://*.cluster.local",
                    "http://*.cluster.local",
                    "https://*.svc.cluster.local",
                    "http://*.svc.cluster.local",
                    "https://deeplynx.*.inl.gov", // Matches deeplynx.dev.inl.gov, deeplynx.acc.inl.gov, etc.
                    "https://deeplynx.inl.gov",
                    "https://deeplynx-*.*.inl.gov") // Matches "deeplynx-thing.domain" namespaces like deeplynx-test.dev
                .SetIsOriginAllowedToAllowWildcardSubdomains()
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });

    // ----------------------------------
    // Authentication
    // ----------------------------------
    var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
    var secret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
    var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE");
    var localDevelopment = Environment.GetEnvironmentVariable("DISABLE_BACKEND_AUTHENTICATION");

    if (string.IsNullOrWhiteSpace(issuer))
        throw new InvalidOperationException("JWT_ISSUER not configured");
    if (string.IsNullOrWhiteSpace(secret))
        throw new InvalidOperationException("JWT_SECRET_KEY not configured");
    if (string.IsNullOrWhiteSpace(audience))
        throw new InvalidOperationException("JWT_AUDIENCE not configured");

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddScheme<JwtBearerOptions, NexusAuthenticationMiddleware>(
            JwtBearerDefaults.AuthenticationScheme,
            options =>
            {
                options.Authority = issuer;
                if (localDevelopment == "true") options.RequireHttpsMetadata = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
                };
            });

    builder.Services.AddAuthorization();

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            options.JsonSerializerOptions.MaxDepth = 64;
        })
        .ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                return new BadRequestObjectResult(
                    BadRequestProblemDetailsFactory.CreateForModelState(
                        context.ModelState,
                        context.ActionDescriptor.Parameters))
                {
                    ContentTypes = { "application/problem+json" }
                };
            };
        });

    /*
    ╔════════════════════════════╗
    ║  Dependency Injection      ║
    ╚════════════════════════════╝
    */
    builder.Services.AddHttpContextAccessor();

    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.UseVector();
    var dataSource = dataSourceBuilder.Build();

    builder.Services.AddDbContext<DeeplynxContext>(
        options => options.UseNpgsql(dataSource),
        ServiceLifetime.Transient
    );

    builder.Services.AddDbContext<LatticeContext>(
        options => options.UseNpgsql(connectionString),
        ServiceLifetime.Transient
    );

    builder.Services.AddSignalR(); // Used for event system pub/sub and notifications

    builder.Services.AddTransient<IRecordBusiness, RecordBusiness>();
    builder.Services.AddTransient<IRecordCollectionBusiness, RecordCollectionBusiness>();
    builder.Services.AddTransient<IObjectStorageBusiness, ObjectStorageBusiness>();
    builder.Services.AddTransient<IClassBusiness, ClassBusiness>();
    builder.Services.AddTransient<IProjectBusiness, ProjectBusiness>();
    builder.Services.AddTransient<IEdgeBusiness, EdgeBusiness>();
    builder.Services.AddTransient<IDataSourceBusiness, DataSourceBusiness>();
    builder.Services.AddTransient<IRelationshipBusiness, RelationshipBusiness>();
    builder.Services.AddTransient<ITagBusiness, TagBusiness>();
    builder.Services.AddTransient<IOlapBusiness, OlapBusiness>();
    builder.Services.AddTransient<IMetricsBusiness, MetricsBusiness>();
    builder.Services.AddTransient<IMaintenanceBusiness, MaintenanceBusiness>();
    builder.Services.AddTransient<IUserBusiness, UserBusiness>();
    builder.Services.AddTransient<INotificationBusiness, NotificationBusiness>();
    builder.Services.AddTransient<IInvitationBusiness, InvitationBusiness>();
    builder.Services.AddTransient<ITokenBusiness, TokenBusiness>();
    builder.Services.AddTransient<IOauthApplicationBusiness, OauthApplicationBusiness>();

    builder.Services.AddTransient<IQueryBusiness, QueryBusiness>();
    builder.Services.AddTransient<IMetadataBusiness, MetadataBusiness>();
    builder.Services.AddTransient<IHistoricalRecordBusiness, HistoricalRecordBusiness>();
    builder.Services.AddTransient<IHistoricalEdgeBusiness, HistoricalEdgeBusiness>();
    builder.Services.AddTransient<IEventBusiness, EventBusiness>();
    // builder.Services.AddTransient<ISubscriptionBusiness, SubscriptionBusiness>();
    builder.Services.AddTransient<FileBusiness>();
    builder.Services.AddTransient<FileFilesystemBusiness>();
    builder.Services.AddTransient<FileAzureBusiness>();
    builder.Services.AddTransient<FileS3Business>();
    builder.Services.AddTransient<IFileBusinessFactory, FileBusinessFactory>();
    builder.Services.AddTransient<IOrganizationBusiness, OrganizationBusiness>();
    builder.Services.AddTransient<IGroupBusiness, GroupBusiness>();
    builder.Services.AddTransient<IRoleBusiness, RoleBusiness>();
    builder.Services.AddTransient<ISensitivityLabelBusiness, SensitivityLabelBusiness>();
    builder.Services.AddTransient<IPermissionBusiness, PermissionBusiness>();
    builder.Services.AddTransient<IProjectRolePermissionService, ProjectRolePermissionService>();
    builder.Services.AddTransient<IOrgRolePermissionService, OrgRolePermissionService>();
    builder.Services.AddScoped<IBulkCopyUpsertExecutor, BulkCopyUpsertExecutor>();
    builder.Services.AddTransient<IAdminService, AdminService>();
    builder.Services.AddTransient<IOauthHandshakeBusiness, OauthHandshakeBusiness>();
    builder.Services.AddTransient<IOrganizationService, OrganizationService>();
    builder.Services.AddTransient<ISavedSearchBusiness, SavedSearchBusiness>();
    builder.Services.AddTransient<IGraphBusiness, GraphBusiness>();
    builder.Services.AddTransient<IUserModelTokenBusiness, UserModelTokenBusiness>();
    builder.Services.AddTransient<IAiModelConfigBusiness, AiModelConfigBusiness>();
    builder.Services.AddScoped<ISensitivityLabelService, SensitivityLabelService>();
    builder.Services.AddScoped<FileBusiness>();
    builder.Services.AddTransient<IInsightBusiness, InsightBusiness>();
    builder.Services.AddTransient<IExtractionValidation, ExtractionValidation>();
    builder.Services.AddTransient<ILatticeExtractionBusiness, LatticeExtractionBusiness>();
    builder.Services.AddMemoryCache();
    builder.Services.AddHttpClient<InsightServiceClient>();
    builder.Services.AddHttpClient<AirflowServiceClient>();
    builder.Services.AddSingleton<EncryptionHelper>();

    /*
    ╔════════════════════════════╗
    ║  Global Exception Handling ║
    ╚════════════════════════════╝
    Specific handlers are registered first; the InternalServerError fallback runs
    last and always handles whatever the others reject. RFC 7807 ProblemDetails
    is used for the response envelope.
    */
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
    builder.Services.AddExceptionHandler<NotFoundExceptionHandler>();
    builder.Services.AddExceptionHandler<ConflictExceptionHandler>();
    builder.Services.AddExceptionHandler<InternalServerErrorExceptionHandler>();

    //OpenApi Documentation
    builder.Services.AddOpenApi(options =>
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

            // Define all tags with hierarchical names (alphabetized)
            document.Tags = new HashSet<OpenApiTag>
            {
                // Administration
                new() { Name = "Organization", Description = "Organization management" },
                new() { Name = "Project", Description = "Project management" },
                new() { Name = "User", Description = "User management" },
                new() { Name = "Group", Description = "Group management" },

                // AI Services
                new() { Name = "Lattice", Description = "Useful data views for DeepLynx Lattice use" },
                new() { Name = "Organization - AI Model Config", Description = "AI model configuration management" },
                new() { Name = "Project - AI Model Config", Description = "AI model configuration management" },
                new() { Name = "User Model Token", Description = "User AI model token management" },
                new() { Name = "Insight", Description = "Deeplynx Insight management" },

                // Authentication
                new() { Name = "OauthHandshake", Description = "OAuth2 authorization flow" },
                new() { Name = "Token", Description = "API key and JWT token management" },
                new() { Name = "OauthApplication", Description = "OAuth apps" },

                // Class tags
                new() { Name = "Organization - Class", Description = "Organization-level class operations" },
                new() { Name = "Project - Class", Description = "Project-level class operations" },

                // Data
                new() { Name = "Record", Description = "Record management" },
                new() { Name = "Record Collection", Description = "Record Collection management"},
                new() { Name = "File", Description = "File operations" },
                new() { Name = "Metadata", Description = "Metadata operations" },
                new() { Name = "Historical Record", Description = "Record history" },
                new() { Name = "Historical Edge", Description = "Edge history" },
                new() { Name = "Edge", Description = "Edges" },

                // DataSource tags
                new() { Name = "Organization - DataSource", Description = "Organization-level data sources" },
                new() { Name = "Project - DataSource", Description = "Project-level data sources" },

                // Events
                new() { Name = "Event", Description = "Event logs" },

                // ObjectStorage tags
                new() { Name = "Organization - Object Storage", Description = "Organization-level storage" },
                new() { Name = "Project - Object Storage", Description = "Project-level storage" },

                // Permission tags
                new() { Name = "Organization - Permission", Description = "Organization-level permissions" },
                new() { Name = "Project - Permission", Description = "Project-level permissions" },

                // Query
                new() { Name = "Query", Description = "Search and filtering" },
                new() { Name = "Saved Search", Description = "Saved searches" },

                // Relationship tags
                new() { Name = "Organization - Relationship", Description = "Organization-level relationships" },
                new() { Name = "Project - Relationship", Description = "Project-level relationships" },

                // Role tags
                new() { Name = "Organization - Role", Description = "Organization-level roles" },
                new() { Name = "Project - Role", Description = "Project-level roles" },

                // SensitivityLabel tags
                new() { Name = "Organization - Sensitivity Label", Description = "Organization-level labels" },
                new() { Name = "Project - Sensitivity Label", Description = "Project-level labels" },

                // Tag tags
                new() { Name = "Organization - Tag", Description = "Organization-level tags" },
                new() { Name = "Project - Tag", Description = "Project-level tags" },

                // Timeseries
                new() { Name = "Olap", Description = "OLAP tabular file operations" },

                // Metrics
                new() { Name = "Metrics", Description = "System Statistics" },

                // Integrations
                new() { Name = "Airflow", Description = "Apache Airflow DAG management" },

                // Other
                new() { Name = "Notification", Description = "Notifications" },
                new() { Name = "Maintenance", Description = "Maintenance" }
            };

            // Create x-tagGroups for nested folder structure (alphabetized)
            var tagGroups = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "Administration",
                    ["tags"] = new JsonArray { "Organization", "Project", "User", "Group" }
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
                        { "Record", "Record Collection", "Historical Record", "Edge", "Historical Edge", "File", "Metadata" }
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

            // Initialize Extensions if null, then wrap in JsonNodeExtension for v2
            document.Extensions ??= new Dictionary<string, IOpenApiExtension>();
            document.Extensions["x-tagGroups"] = new JsonNodeExtension(tagGroups);

            // Wrap in JsonNodeExtension for v2
            document.Extensions["x-tagGroups"] = new JsonNodeExtension(tagGroups);

            // Security scheme
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Enter your JWT token"
            };

            return Task.CompletedTask;
        });
        // Add operation transformer for common responses
        options.AddOperationTransformer((operation, context, cancellationToken) =>
        {
            // Add common error responses to all operations
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
        // Mark non-nullable value-type query parameters as required in the OpenAPI spec.
        // Path parameters are already required by default; query parameters are not, even when
        // their C# type is non-nullable (e.g. long, int, bool). Scalar renders this distinction
        // visually, so this transformer ensures the spec matches the actual binding behaviour.
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
    });

    /* ╔════════════════════════════╗
       ║      Check DB Version      ║
       ╚════════════════════════════╝ */
    await DatabaseVersionChecker.CheckDatabaseVersion(connectionString);

    /* ╔════════════════════════════╗
       ║   Check Encryption Keys    ║
       ╚════════════════════════════╝ */
    EncryptionHelper.CheckEncryptionConfig();

    /* ╔════════════════════════════╗
       ║      App Configurations    ║
       ╚════════════════════════════╝ */
    var app = builder.Build();

    /* ╔════════════════════════════╗
       ║      gRPC Configurations   ║
       ╚════════════════════════════╝ */
    app.MapFlightEndpoint();

    if (app.Environment.IsDevelopment())
    {
        app.MapGrpcReflectionService();
    }

    /* ╔════════════════════════════╗
       ║      Apply Migrations      ║
       ╚════════════════════════════╝ */
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<DeeplynxContext>();
        await dbContext.Database.MigrateAsync();

        var latticeContext = scope.ServiceProvider.GetRequiredService<LatticeContext>();
        await latticeContext.Database.MigrateAsync();
    }

    Log.Information("Migrations applied successfully.");

    /* ╔════════════════════════════╗
       ║      App Base Path         ║
       ╚════════════════════════════╝ */
    PathString basePath = "/api/v1";
    app.UsePathBase(basePath);

    app.UseStaticFiles();
    app.UseRouting();
    app.UseExceptionHandler(); // Runs registered IExceptionHandlers; must precede middleware that may throw
    app.UseCors("AllowAll");
    app.UseAuthentication(); // Must be first
    app.UseMiddleware<UserContextMiddleware>(); // Second - sets UserId/Email
    app.UseMiddleware<AuthMiddleware>(); // Third - sets OrganizationId
    app.UseMiddleware<SensitivityMiddleware>();
    app.UseAuthorization(); // Fourth
    app.MapControllers(); // Last

    //Health check endpoint
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
        .ExcludeFromDescription(); // hide from docs

    // Check if the notification service is enabled (defaults to false if not set)
    if (Environment.GetEnvironmentVariable("ENABLE_NOTIFICATION_SERVICE") == "true")
        app.MapHub<EventNotificationHub>("/eventNotificationHub"); // endpoint for real-time notifications with SignalR

    /* ╔════════════════════════════╗
       ║   Scalar Configuration     ║
       ╚════════════════════════════╝ */
    // Always using scalar:
    //if (app.Environment.IsDevelopment()) { ...
    // app.UseOpenApi();
    app.MapOpenApi();

    var customcss = File.ReadAllText("moon.css");
    var hostedLink = Environment.GetEnvironmentVariable("HOSTED_LINK");

    // Conditional image hosting
    var imageSrc = "/images/lynx-white.png";

    // Build the HTML content with our image src string interpolation
    var scalarHeaderContent = $@"
    <div class='references-header'>
      <header class='header t-doc__header'>
        <div class='header-container'>
          <div class='header-item header-item-meta'>
            <a class='header-item-logo'>
              <img
                alt='lynx'
                class='header-item-logo-image'
                src='{imageSrc}'
                style='height: 50px; position: sticky; z-index: 1000; padding-left: 20px;' />
            </a>
          </div>
        </div>
      </header>
    </div>";

    app.MapScalarApiReference(options =>
    {
        options
            .WithDarkMode()
            .WithBaseServerUrl(basePath.ToString())
            .WithTheme(ScalarTheme.Kepler)
            .WithTitle("DeepLynx Nexus API")
            .WithCustomCss(customcss)
            .AddHeaderContent(scalarHeaderContent);


        if (!string.IsNullOrEmpty(hostedLink))
        {
            var hostedLinkWithApi = string.Concat(hostedLink + "/api/v1");
            options.Servers = new List<ScalarServer> { new(hostedLinkWithApi) };
        }
    });

    app.Run();
}
// ignore entity framework aborting in design. See https://github.com/dotnet/efcore/issues/29923
catch (Exception ex) when (ex is not HostAbortedException && ex.Source != "Microsoft.EntityFrameworkCore.Design")
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

using System.Text.Json.Serialization;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.BigData;
using deeplynx.helpers.ExceptionHandlers;
using deeplynx.helpers.Hubs;
using deeplynx.helpers.Json;
using deeplynx.interfaces;
using deeplynx.api.Services;
using deeplynx.api.OpenApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Apache.Arrow.Flight.Server;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Scalar.AspNetCore;
using Serilog;
using Log = Serilog.Log;

var builder = WebApplication.CreateBuilder(args);
var isOpenApiDocumentGeneration = OpenApiGenerationMode.IsActive();
var isRuntimeStartup = !isOpenApiDocumentGeneration;

builder.WebHost.ConfigureKestrel(options => { options.Limits.MaxRequestBodySize = 2L * 1024 * 1024 * 1024; });

builder.Services.Configure<FormOptions>(options => { options.MultipartBodyLengthLimit = 2L * 1024 * 1024 * 1024; });

builder.Services.AddGrpc().AddFlightServer<NexusFlightServer>();
builder.Services.AddGrpcReflection();

var connectionString = ConnectionStringsProvider.GetPostgresConnectionString(builder.Configuration);

// ----------------------------------
// Logger Setup
// ----------------------------------
var loggerConfiguration = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console();

if (isRuntimeStartup)
{
    loggerConfiguration.WriteTo.PostgreSQL(
        connectionString,
        "logs",
        schemaName: "deeplynx",
        needAutoCreateTable: true,
        batchSizeLimit: 50,
        period: TimeSpan.FromSeconds(15));
}

Log.Logger = loggerConfiguration.CreateLogger();
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
                .AllowCredentials()
                .WithExposedHeaders("Content-Disposition");
        });
    });

    // ----------------------------------
    // Authentication
    // ----------------------------------
    if (isRuntimeStartup)
    {
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
    }

    builder.Services.AddAuthorization();

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            options.JsonSerializerOptions.MaxDepth = 64;
            options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
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

    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
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
    builder.Services.AddTransient<IEdgeBusiness, EdgeBusiness>();
    builder.Services.AddTransient<IDataSourceBusiness, DataSourceBusiness>();
    builder.Services.AddTransient<IRelationshipBusiness, RelationshipBusiness>();
    builder.Services.AddTransient<ITagBusiness, TagBusiness>();
    builder.Services.AddTransient<IOlapBusiness, OlapBusiness>();
    builder.Services.AddTransient<IMetricsBusiness, MetricsBusiness>();
    builder.Services.AddTransient<IUserBusiness, UserBusiness>();
    builder.Services.AddTransient<INotificationBusiness, NotificationBusiness>();
    builder.Services.AddTransient<ITokenBusiness, TokenBusiness>();
    builder.Services.AddTransient<IOauthApplicationBusiness, OauthApplicationBusiness>();
    builder.Services.AddTransient<IProvenanceBusiness, ProvenanceBusiness>();
    builder.Services.AddTransient<IQueryBusiness, QueryBusiness>();
    builder.Services.AddTransient<IMetadataBusiness, MetadataBusiness>();
    builder.Services.AddTransient<IHistoricalRecordBusiness, HistoricalRecordBusiness>();
    builder.Services.AddTransient<IHistoricalEdgeBusiness, HistoricalEdgeBusiness>();
    builder.Services.AddTransient<IEventBusiness, EventBusiness>();
    // builder.Services.AddTransient<ISubscriptionBusiness, SubscriptionBusiness>();
    builder.Services.AddTransient<FileBusiness>();
    builder.Services.AddTransient<FileFilesystemBusiness>();
    builder.Services.AddTransient<IFileBusiness, FileAzureBusiness>();
    builder.Services.AddTransient<FileAzureBusiness>();
    builder.Services.AddTransient<FileS3Business>();
    builder.Services.AddTransient<IFileBusinessFactory, FileBusinessFactory>();
    builder.Services.AddTransient<IOrganizationBusiness, OrganizationBusiness>();
    builder.Services.AddTransient<IProjectBusiness, ProjectBusiness>();
    builder.Services.AddTransient<IInvitationBusiness, InvitationBusiness>();
    builder.Services.AddTransient<IGroupBusiness, GroupBusiness>();
    builder.Services.AddTransient<IRoleBusiness, RoleBusiness>();
    builder.Services.AddTransient<ISensitivityLabelBusiness, SensitivityLabelBusiness>();
    builder.Services.AddTransient<IMaintenanceBusiness, MaintenanceBusiness>();
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
    builder.Services.AddScoped<IFileControllerBusiness, FileBusiness>();
    builder.Services.AddTransient<IInsightBusiness, InsightBusiness>();
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
    builder.Services.AddNexusOpenApi();

    /* ╔════════════════════════════╗
       ║ Runtime Startup Checks     ║
       ╚════════════════════════════╝ */
    if (isRuntimeStartup)
    {
        await DatabaseVersionChecker.CheckDatabaseVersion(connectionString);
        EncryptionHelper.CheckEncryptionConfig();
    }

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
    if (isRuntimeStartup)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DeeplynxContext>();
        await dbContext.Database.MigrateAsync();

        var latticeContext = scope.ServiceProvider.GetRequiredService<LatticeContext>();
        await latticeContext.Database.MigrateAsync();

        Log.Information("Migrations applied successfully.");
    }

    /* ╔════════════════════════════╗
       ║      App Base Path         ║
       ╚════════════════════════════╝ */
    PathString basePath = "/api/v1";
    app.UsePathBase(basePath);

    app.UseStaticFiles();
    app.UseRouting();
    app.UseExceptionHandler(); // Runs registered IExceptionHandlers; must precede middleware that may throw
    app.UseCors("AllowAll");

    if (isRuntimeStartup)
    {
        app.UseAuthentication(); // Must be first
        app.UseMiddleware<UserContextMiddleware>(); // Second - sets UserId/Email
        app.UseMiddleware<AuthMiddleware>(); // Third - sets OrganizationId
        app.UseMiddleware<FeatureFlagMiddleware>();
        app.UseMiddleware<SensitivityMiddleware>();
        app.UseAuthorization(); // Fourth
    }

    app.MapControllers(); // Last


    // Check if the notification service is enabled (defaults to false if not set)
    if (Environment.GetEnvironmentVariable("ENABLE_NOTIFICATION_SERVICE") == "true")

        /* ╔════════════════════════════╗
           ║   Scalar Configuration     ║
           ╚════════════════════════════╝ */
        // Always using scalar:
        //if (app.Environment.IsDevelopment()) { ...
        // app.UseOpenApi();

        if (isRuntimeStartup)
        {
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
        }

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
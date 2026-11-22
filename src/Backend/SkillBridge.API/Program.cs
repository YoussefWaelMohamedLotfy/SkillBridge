using Asp.Versioning;
using FastEndpoints;
using FastEndpoints.AspVersioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using SkillBridge.API.Data;
using SkillBridge.API.OpenAPI;
using ZiggyCreatures.Caching.Fusion;

var builder = WebApplication.CreateBuilder(args);

VersionSets.CreateApi("SkillBridge", v => v
    .HasApiVersion(new ApiVersion(1, 0))
    .HasApiVersion(new ApiVersion(2, 0)));

builder.AddServiceDefaults();

builder.WebHost.ConfigureKestrel(x => x.AddServerHeader = false);

builder.Services.AddProblemDetails();

builder.AddRedisDistributedCache("garnet");
builder.Services.AddFusionCache()
    .WithSystemTextJsonSerializer()
    .WithRegisteredDistributedCache()
    .WithStackExchangeRedisBackplane(x => x.Configuration = builder.Configuration.GetConnectionString("garnet"))
    .AsHybridCache();

builder.Services.AddFastEndpoints()
    .AddVersioning(o =>
    {
        o.DefaultApiVersion = new ApiVersion(1, 0);
        o.AssumeDefaultVersionWhenUnspecified = true;
        o.ReportApiVersions = true;
        // Header versioning: X-Api-Version: 1
        // URL prefix (/v1/...) is handled by FastEndpoints' built-in PrependToRoute.
        o.ApiVersionReader = new HeaderApiVersionReader("X-Api-Version");
    })
    .AddOpenApi(options =>
    {
        options.AddScalarTransformers();
        options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
        options.AddDocumentTransformer<AuthorizedOperationTransformer>();

        options.AddDocumentTransformer(
            (document, context, cancellationToken) =>
            {
                document.Info.Contact = new()
                {
                    Name = "SkillBridge Support",
                    Email = "info@skillbridge.com",
                };
                return Task.CompletedTask;
            }
        );
    });

builder.AddNpgsqlDbContext<AppDbContext>("SkillBridge-Db", configureDbContextOptions: opts =>
{
    opts.UseAsyncSeeding((dbContext, y, ct) => AppDbSeeder.SeedAsync(dbContext, y, ct))
        .UseSeeding(AppDbSeeder.Seed);
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddKeycloakJwtBearer(
        serviceName: "keycloak",
        realm: "skillbridge",
        options =>
        {
            options.Audience = "skillbridge-api";
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        });

builder.Services.AddAuthorizationBuilder();

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseExceptionHandler(_ => { });
app.UseStatusCodePages();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpoints(c =>
{
    c.Versioning.Prefix = "v";
    c.Versioning.DefaultVersion = 1;
    c.Versioning.PrependToRoute = true;
});

app.MapOpenApi().AllowAnonymous();
app.MapScalarApiReference(options =>
{
    options
        .WithTheme(ScalarTheme.Default)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
        .AddAuthorizationCodeFlow(
            OpenIdConnectDefaults.AuthenticationScheme,
            x =>
            {
                x.AuthorizationUrl =
                    "https://localhost:8081/realms/skillbridge/protocol/openid-connect/auth";
                x.TokenUrl = "https://localhost:8081/realms/skillbridge/protocol/openid-connect/token";
                x.Pkce = Pkce.Sha256;
                x.RedirectUri = "https://localhost:7157/scalar/v1";
                x.ClientId = "skillbridge-api";
                x.ClientSecret = "test";
                x.SelectedScopes = ["openid", "profile", "email", "offline_access"];
            }
        )
        .AddPreferredSecuritySchemes(OpenIdConnectDefaults.AuthenticationScheme);

    var descriptions = app.DescribeApiVersions();

    for (var i = 0; i < descriptions.Count; i++)
    {
        var description = descriptions[i];
        var isDefault = i == descriptions.Count - 1;
        options.AddDocument(description.GroupName, description.GroupName, isDefault: isDefault);
    }
}).AllowAnonymous();

await app.RunAsync();

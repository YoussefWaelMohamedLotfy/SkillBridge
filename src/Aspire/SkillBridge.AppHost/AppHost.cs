var builder = DistributedApplication.CreateBuilder(args);

var adminPassword = builder.AddParameter("Password", secret: true);

var garnet = builder
    .AddGarnet("garnet", 6379, adminPassword)
    .WithImage("microsoft/garnet-alpine")
    .WithLifetime(ContainerLifetime.Persistent);

var postgres = builder
    .AddPostgres("postgres", password: adminPassword, port: 5432)
    .WithImageTag("alpine")
    //.WithDataVolume()
    .WithVolume("teleq-pg-data", "/var/lib/postgresql")
    .WithLifetime(ContainerLifetime.Persistent);

postgres.WithPgAdmin(x => x.WithImageTag("latest").WithHostPort(5050).WithParentRelationship(postgres).WithLifetime(ContainerLifetime.Persistent));

var skillbridgeDb = postgres.AddDatabase("SkillBridge-Db");
var KeycloakDb = postgres.AddDatabase("Keycloak-Db");

var keycloak = builder
    .AddKeycloak("keycloak", 8081, adminPassword: adminPassword)
    .WithImageTag("latest")
    .WithPostgres(KeycloakDb)
    .WaitFor(KeycloakDb)
    .WithRealmImport("./Realms")
    .WithLifetime(ContainerLifetime.Persistent);

var rabbitmq = builder.AddRabbitMQ("rabbitmq", password: adminPassword, port: 5672)
    .WithImageTag("management-alpine")
    .WithManagementPlugin(15672)
    .WithLifetime(ContainerLifetime.Persistent);

var api = builder.AddDotnetProject("skillbridge-api", "..\\..\\Backend\\SkillBridge.API\\SkillBridge.API.csproj")
    .WithReference(skillbridgeDb)
    .WithReference(keycloak)
    .WithReference(garnet)
    .WithReference(rabbitmq)
    .WaitFor(skillbridgeDb)
    .WaitFor(keycloak)
    .WaitFor(rabbitmq)
    .WaitFor(garnet);

var apiMigrations = api.AddEFMigrations("api-migrations", "SkillBridge.Api.Data.AppDbContext")
    .WaitFor(skillbridgeDb)
    .WithMigrationsProject("../../Backend/SkillBridge.API/SkillBridge.API.csproj")
    .RunDatabaseUpdateOnStart();

api.WaitForCompletion(apiMigrations)
    .WithChildRelationship(apiMigrations);

var blazor = builder
    .AddDotnetProject("blazor", "../../Apps/SkillBridge.Blazor/SkillBridge.Blazor.csproj")
    .WithReference(api)
    .WithReference(keycloak)
    .WithReference(garnet)
    .WaitFor(api)
    .WaitFor(garnet);

await builder.Build().RunAsync();

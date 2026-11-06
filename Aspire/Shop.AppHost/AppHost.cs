var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("shop-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent);

var identityDb = postgres.AddDatabase("IdentityDb", databaseName: "shop_identity");
var shopDb = postgres.AddDatabase("ShopDb", databaseName: "shop");

var messaging = builder.AddRabbitMQ("messaging")
    .WithManagementPlugin()
    .WithDataVolume("shop-rabbitmq-data")
    .WithLifetime(ContainerLifetime.Persistent);

var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "latest")
    .WithHttpEndpoint(targetPort: 8025, name: "ui")
    .WithEndpoint(targetPort: 1025, name: "smtp", scheme: "tcp");
var smtp = mailpit.GetEndpoint("smtp");

var identityApi = builder.AddProject<Projects.IdentityService_Api>("identity-api")
    .WithReference(identityDb).WaitFor(identityDb)
    .WithReference(messaging).WaitFor(messaging)
    .WithEnvironment("Smtp__Host", smtp.Property(EndpointProperty.Host))
    .WithEnvironment("Smtp__Port", smtp.Property(EndpointProperty.Port))
    .WithEnvironment("Smtp__Security", "None")
    .WaitFor(mailpit);

// Authority musi być równe Jwt:Issuer w IdentityService - stamtąd Shop API pobiera klucze JWKS
builder.AddProject<Projects.ShopService_Api>("shop-api")
    .WithReference(shopDb).WaitFor(shopDb)
    .WithReference(messaging).WaitFor(messaging)
    .WithEnvironment("Jwt__Authority", identityApi.GetEndpoint("https"))
    .WaitFor(identityApi);

builder.Build().Run();

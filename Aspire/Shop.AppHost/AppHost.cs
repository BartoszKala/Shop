var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("shop-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent);

var identityDb = postgres.AddDatabase("IdentityDb", databaseName: "shop_identity");

var messaging = builder.AddRabbitMQ("messaging")
    .WithManagementPlugin()
    .WithDataVolume("shop-rabbitmq-data")
    .WithLifetime(ContainerLifetime.Persistent);

var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "latest")
    .WithHttpEndpoint(targetPort: 8025, name: "ui")
    .WithEndpoint(targetPort: 1025, name: "smtp", scheme: "tcp");
var smtp = mailpit.GetEndpoint("smtp");

builder.AddProject<Projects.IdentityService_Api>("identity-api")
    .WithReference(identityDb).WaitFor(identityDb)
    .WithReference(messaging).WaitFor(messaging)
    .WithEnvironment("Smtp__Host", smtp.Property(EndpointProperty.Host))
    .WithEnvironment("Smtp__Port", smtp.Property(EndpointProperty.Port))
    .WithEnvironment("Smtp__Security", "None")
    .WaitFor(mailpit);

builder.Build().Run();

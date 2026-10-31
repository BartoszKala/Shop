using IdentityService.Infrastructure.Email;
using IdentityService.Infrastructure.Outbox;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(IdentityService.IntegrationTests.Infrastructure.IdentityApiFactory))]

namespace IdentityService.IntegrationTests.Infrastructure;

public sealed class IdentityApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@shop.local";
    public const string AdminPassword = "Admin123!";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18").Build();
    private readonly string _signingKeyPath = Path.Combine(Path.GetTempPath(), $"identity-tests-{Guid.NewGuid():N}.pem");

    public FakeMailbox Mailbox { get; } = new();
    public FakeIntegrationEventSender IntegrationEvents { get; } = new();

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        File.Delete(_signingKeyPath);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:IdentityDb", _postgres.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Messaging:Enabled", "false");
        builder.UseSetting("PasswordPolicy:CheckBreachedPasswords", "false");
        builder.UseSetting("RateLimiting:AuthPermitLimit", "100000");
        builder.UseSetting("Outbox:PollingInterval", "00:00:00.100");
        builder.UseSetting("Jwt:SigningKey:Path", _signingKeyPath);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailDelivery>();
            services.AddSingleton<IEmailDelivery>(Mailbox);
            services.RemoveAll<IIntegrationEventSender>();
            services.AddSingleton<IIntegrationEventSender>(IntegrationEvents);
        });
    }

    public HttpClient CreateDevice(string userAgent = "IntegrationTests/1.0")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        return client;
    }
}

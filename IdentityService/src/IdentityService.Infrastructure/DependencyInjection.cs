using IdentityService.Application.Abstractions;
using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Email;
using IdentityService.Infrastructure.Identity;
using IdentityService.Infrastructure.Outbox;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Infrastructure.QrCodes;
using IdentityService.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace IdentityService.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseConnectionName = "IdentityDb";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SmtpOptions>().BindConfiguration(SmtpOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SigningKeyOptions>().BindConfiguration(SigningKeyOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<OutboxOptions>().BindConfiguration(OutboxOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<RetentionOptions>().BindConfiguration(RetentionOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<MessagingOptions>().BindConfiguration(MessagingOptions.SectionName);
        services.AddOptions<PasswordPolicyOptions>().BindConfiguration(PasswordPolicyOptions.SectionName);
        services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.SectionName);

        services.AddDbContext<AppIdentityDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString(DatabaseConnectionName)));

        services.AddDataProtection()
            .SetApplicationName("Shop.IdentityService")
            .PersistKeysToDbContext<AppIdentityDbContext>();

        AddIdentity(services);

        services.AddSingleton(sp => SigningKeyProvider.Load(
            sp.GetRequiredService<IOptions<SigningKeyOptions>>().Value,
            sp.GetRequiredService<IHostEnvironment>().ContentRootPath));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IConsentRepository, ConsentRepository>();
        services.AddScoped<ISecurityEventLog, SecurityEventLog>();
        services.AddScoped<IAdminUserQueries, AdminUserQueries>();
        services.AddSingleton<IAccessTokenGenerator, JwtAccessTokenGenerator>();
        services.AddSingleton<ITwoFactorLoginTokenProtector, TwoFactorLoginTokenProtector>();
        services.AddSingleton<IQrCodeGenerator, QrCodeGenerator>();

        AddOutbox(services);
        services.AddHostedService<RetentionCleanupService>();

        return services;
    }

    private static void AddIdentity(IServiceCollection services)
    {
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;

                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<AppIdentityDbContext>()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<PolishIdentityErrorDescriber>();

        services.AddHttpClient<PwnedPasswordValidator>(client =>
        {
            client.BaseAddress = new Uri("https://api.pwnedpasswords.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Shop.IdentityService");
        });
        services.AddScoped<IPasswordValidator<ApplicationUser>>(sp => sp.GetRequiredService<PwnedPasswordValidator>());

        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromHours(24));
    }

    private static void AddOutbox(IServiceCollection services)
    {
        services.AddScoped<IEmailService, OutboxEmailService>();
        services.AddScoped<IIntegrationEventPublisher, OutboxIntegrationEventPublisher>();
        services.AddTransient<IEmailDelivery, SmtpEmailSender>();

        services.AddSingleton<IIntegrationEventSender>(sp =>
            sp.GetRequiredService<IOptions<MessagingOptions>>().Value.Enabled
                ? ActivatorUtilities.CreateInstance<RabbitMqIntegrationEventSender>(sp, sp.GetRequiredService<IConnection>())
                : ActivatorUtilities.CreateInstance<LoggingIntegrationEventSender>(sp));

        services.AddHostedService<OutboxProcessor>();
    }
}

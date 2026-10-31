using IdentityService.Application.Options;
using IdentityService.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<FrontendOptions>().BindConfiguration(FrontendOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<TwoFactorOptions>().BindConfiguration(TwoFactorOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<LegalOptions>().BindConfiguration(LegalOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<TokenService>();
        services.AddScoped<UserProfileBuilder>();
        services.AddScoped<AuthService>();
        services.AddScoped<AccountService>();
        services.AddScoped<PrivacyService>();
        services.AddScoped<TwoFactorService>();
        services.AddScoped<AdminUserService>();
        services.AddScoped<AccountEmailSender>();

        return services;
    }
}

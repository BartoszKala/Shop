using System.Net;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using ShopService.Api;
using ShopService.Api.Infrastructure;
using ShopService.Api.Json;
using ShopService.Api.OpenApi;
using ShopService.Application;
using ShopService.Application.Abstractions;
using ShopService.Application.Common;
using ShopService.Infrastructure;
using ShopService.Infrastructure.Outbox;
using ShopService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.EnrichNpgsqlDbContext<ShopDbContext>();

if (builder.Configuration.GetValue(MessagingOptions.SectionName + ":Enabled", true))
{
    builder.AddRabbitMQClient("messaging");
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
    {
        options.KnownProxies.Add(IPAddress.Parse(proxy));
    }
});

// Tokeny wydaje IdentityService; klucze publiczne pobieramy z jego /.well-known/jwks.json (bez współdzielonego sekretu)
builder.Services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        options.Authority = jwtOptions.Value.Authority;
        options.Audience = jwtOptions.Value.Audience;
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = JwtClaimNames.Email;
        options.TokenValidationParameters.RoleClaimType = JwtClaimNames.Role;
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddProblemDetails();
builder.Services.AddControllers()
    .AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions));
builder.Services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddSchemaTransformer<StronglyTypedIdSchemaTransformer>();
});

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else
{
    app.UseExceptionHandler();
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.InitializeDatabaseAsync();
}

await app.RunAsync();

static void ConfigureJson(System.Text.Json.JsonSerializerOptions options)
{
    options.Converters.Add(new StronglyTypedIdJsonConverterFactory());
    options.Converters.Add(new JsonStringEnumConverter());
}

public partial class Program;

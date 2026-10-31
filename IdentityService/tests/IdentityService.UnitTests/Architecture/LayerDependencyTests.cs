using System.Reflection;
using NetArchTest.Rules;

namespace IdentityService.UnitTests.Architecture;

public sealed class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(IdentityService.Domain.UserId).Assembly;
    private static readonly Assembly Application = typeof(IdentityService.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(IdentityService.Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Domain_depends_on_no_other_layer_nor_frameworks()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "IdentityService.Application",
                "IdentityService.Infrastructure",
                "IdentityService.Api",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore.Http",
                "Microsoft.AspNetCore.Mvc")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_web_or_ef()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(
                "IdentityService.Infrastructure",
                "IdentityService.Api",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore.Http",
                "Microsoft.AspNetCore.Mvc",
                "RabbitMQ",
                "MailKit")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_api()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOn("IdentityService.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Infrastructure_implementations_are_not_public_unless_needed()
    {
        var result = Types.InAssembly(Infrastructure)
            .That().HaveNameEndingWith("Repository")
            .Should().NotBePublic()
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        "Violations: " + string.Join(", ", result.FailingTypeNames ?? []);
}

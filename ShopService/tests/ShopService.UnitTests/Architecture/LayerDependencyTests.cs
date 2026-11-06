using System.Reflection;
using NetArchTest.Rules;

namespace ShopService.UnitTests.Architecture;

public sealed class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(ShopService.Domain.Entities.Order).Assembly;
    private static readonly Assembly Application = typeof(ShopService.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(ShopService.Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Domain_depends_on_no_other_layer_nor_frameworks()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "ShopService.Application",
                "ShopService.Infrastructure",
                "ShopService.Api",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_web_or_ef()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(
                "ShopService.Infrastructure",
                "ShopService.Api",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_api()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOn("ShopService.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Repositories_are_not_public()
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

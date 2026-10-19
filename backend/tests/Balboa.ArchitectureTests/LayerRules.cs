using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Balboa.ArchitectureTests;

/// <summary>Dependency rules between the layers of a module.</summary>
public class LayerRules
{
    private const string _framework = @"^(Microsoft\.EntityFrameworkCore|Microsoft\.AspNetCore|Npgsql)(\..*)?$";
    private const string _infrastructure = @"^Balboa\.Modules\.[^.]+\.Infrastructure(\..*)?$";

    [Fact]
    public void Domain_DependsOnNeitherEntityFrameworkNorAspNetCore()
    {
        Layers.ShouldMatchSomething("Domain");

        IArchRule rule = Layers.Of("Domain").Should()
            .NotDependOnAnyTypesThat().ResideInNamespaceMatching(_framework)
            .WithoutRequiringPositiveResults();

        rule.Check(Code.Architecture);
    }

    [Fact]
    public void Domain_DependsOnNothingInsideBalboaExceptSharedKernelAndItself()
    {
        IArchRule rule = Layers.Of("Domain").Should()
            .NotDependOnAnyTypesThat().ResideInNamespaceMatching(
                @"^Balboa\.(BuildingBlocks|ServiceDefaults|Host)(\..*)?$|^Balboa\.Modules\.[^.]+\.(Contracts|Application|Infrastructure)(\..*)?$")
            .WithoutRequiringPositiveResults();

        rule.Check(Code.Architecture);
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructure()
    {
        Layers.ShouldMatchSomething("Application");

        IArchRule rule = Layers.Of("Application").Should()
            .NotDependOnAnyTypesThat().ResideInNamespaceMatching(_infrastructure)
            .WithoutRequiringPositiveResults();

        rule.Check(Code.Architecture);
    }

    [Fact]
    public void Application_DependsOnNeitherEntityFrameworkNorAspNetCore()
    {
        IArchRule rule = Layers.Of("Application").Should()
            .NotDependOnAnyTypesThat().ResideInNamespaceMatching(_framework)
            .WithoutRequiringPositiveResults();

        rule.Check(Code.Architecture);
    }

    [Fact]
    public void SharedKernel_DependsOnNothingElseInBalboa()
    {
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^Balboa\.SharedKernel(\..*)?$")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Balboa\.(?!SharedKernel)")
            .WithoutRequiringPositiveResults();

        rule.Check(Code.Architecture);
    }

    [Fact]
    public void Contracts_DependOnlyOnSharedKernelAndBuildingBlocksNotOnInternals()
    {
        Layers.ShouldMatchSomething("Contracts");

        IArchRule rule = Layers.Of("Contracts").Should()
            .NotDependOnAnyTypesThat().ResideInNamespaceMatching(
                @"^Balboa\.Modules\.[^.]+\.(Domain|Application|Infrastructure)(\..*)?$|^Balboa\.(Host|ServiceDefaults)(\..*)?$")
            .WithoutRequiringPositiveResults();

        rule.Check(Code.Architecture);
    }
}

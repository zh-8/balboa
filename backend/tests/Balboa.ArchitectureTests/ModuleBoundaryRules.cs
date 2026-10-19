using System.Reflection;
using System.Runtime.CompilerServices;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Balboa.ArchitectureTests;

/// <summary>Rules about how modules relate to each other and to the Host.</summary>
public class ModuleBoundaryRules
{
    [Fact]
    public void AtLeastOneModuleIsDiscovered() => Code.Modules.ShouldNotBeEmpty("the architecture tests found no Balboa.Modules.* assemblies to inspect");

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_DoesNotDependOnAnotherModulesNonContractsTypes(string module)
    {
        Layers.ShouldMatchSomething("(Domain|Application|Infrastructure)", module);

        // Any namespace Balboa.Modules.<other>.<Domain|Application|Infrastructure> is off limits; <other>.Contracts is fine.
        string otherModulesInternals =
            $@"^Balboa\.Modules\.(?!{module}\.)[^.]+\.(Domain|Application|Infrastructure)(\..*)?$";
        IArchRule rule = Types().That()
            .ResideInNamespaceMatching($@"^Balboa\.Modules\.{module}\..*$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(otherModulesInternals)
            .WithoutRequiringPositiveResults();

        rule.Check(Code.Architecture);
    }

    [Fact]
    public void Contracts_ContainOnlyRecordsAndInterfaces()
    {
        Layers.ShouldMatchSomething("Contracts");

        IArchRule rule = Types().That().ResideInNamespaceMatching(Layers.Pattern("Contracts"))
            .Should().FollowCustomCondition(
                t => t is Interface or Class { IsRecord: true },
                "be a record or an interface",
                "is neither a record nor an interface");

        rule.Check(Code.Architecture);
    }

    [Fact]
    public void Host_DoesNotDependOnModuleDomainApplicationOrContractsInternals()
    {
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^Balboa\.Host(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(
                @"^Balboa\.Modules\.[^.]+\.(Domain|Application)(\..*)?$")
            .WithoutRequiringPositiveResults();

        rule.Check(Code.Architecture);
    }

    [Fact]
    public void Host_TouchesInfrastructureOnlyThroughTheModuleEntryPointClass()
    {
        // Only classes named <Module>Module may be referenced; they are the home of AddXModule / MapXEndpoints.
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^Balboa\.Host(\..*)?$")
            .Should().NotDependOnAnyTypesThat()
            .Are(Types().That().ResideInNamespaceMatching(
                    @"^Balboa\.Modules\.[^.]+\.Infrastructure(\..*)?$")
                .And().DoNotHaveNameMatching(@"^[A-Za-z]+Module$"))
            .WithoutRequiringPositiveResults();

        rule.Check(Code.Architecture);
    }

    [Fact]
    public void Infrastructure_PublicTypesAreOnlyTheModuleEntryPointClass()
    {
        Layers.ShouldMatchSomething("Infrastructure");

        // EF's migration generator emits public classes; they are generated code that EF finds by reflection.
        IArchRule rule = Layers.Of("Infrastructure").And().DoNotResideInNamespaceMatching(@"\.Migrations$").And().ArePublic()
            .Should().HaveNameMatching(@"^[A-Za-z]+Module$")
            .AndShould().FollowCustomCondition(
                t => t is Class { IsAbstract: true, IsSealed: true },
                "be a static class",
                "is not a static class");

        rule.Check(Code.Architecture);
    }

    [Fact]
    public void Infrastructure_PublicMethodsAreOnlyAddXModuleAndMapXEndpoints()
    {
        IArchRule rule = MethodMembers().That()
            .AreDeclaredIn(Layers.ClassesOf("Infrastructure").And().HaveNameMatching(@"^[A-Za-z]+Module$"))
            .And().ArePublic()
            .Should().HaveNameMatching(@"^(Add[A-Za-z]+Module|Map[A-Za-z]+Endpoints)\(.*$");

        rule.Check(Code.Architecture);
    }

    [Fact]
    public void InternalsVisibleTo_IsGrantedOnlyToTheModulesOwnProjects()
    {
        var violations = new List<string>();
        foreach (System.Reflection.Assembly assembly in Code.Assemblies)
        {
            string name = assembly.GetName().Name!;
            string? module = Code.ModuleOf(name);
            foreach (InternalsVisibleToAttribute grant in assembly.GetCustomAttributes<InternalsVisibleToAttribute>())
            {
                string friend = grant.AssemblyName.Split(',')[0];
                bool allowed = module is not null && friend.StartsWith($"Balboa.Modules.{module}.", StringComparison.Ordinal);
                if (!allowed)
                {
                    violations.Add($"{name} grants internals to '{friend}'");
                }
            }
        }

        violations.ShouldBeEmpty("InternalsVisibleTo may only name the module's own sibling or test projects");
    }

    public static TheoryData<string> ModuleNames => Code.ModuleData();
}

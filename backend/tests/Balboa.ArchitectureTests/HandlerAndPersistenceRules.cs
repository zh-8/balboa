using System.Reflection;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using Balboa.BuildingBlocks.Cqrs;
using Balboa.BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Balboa.ArchitectureTests;

public partial class HandlerAndPersistenceRules
{
    private const string _handlerInterfaces =
        @"^Balboa\.BuildingBlocks\.(Cqrs\.(ICommandHandler|IQueryHandler|IDomainEventHandler)|Messaging\.IIntegrationEventHandler)[`<]";

    private static bool IsDbContext(Class type) => type.BaseClass?.FullName == "Microsoft.EntityFrameworkCore.DbContext";

    private static bool ImplementsHandlerInterface(Class type) =>
        type.ImplementedInterfaces.Any(i => MyRegex().IsMatch(i.FullName));

    [Fact]
    public void Handlers_AreInternalSealedClasses()
    {
        Layers.ShouldMatchSomething("Application");
        Layers.ClassesOf("Application").And().FollowCustomPredicate(ImplementsHandlerInterface, "implement a handler interface")
            .GetObjects(Code.Architecture).ShouldNotBeEmpty("no handlers found; the rule would pass vacuously");

        IArchRule rule = Layers.ClassesOf("Application").And()
            .FollowCustomPredicate(ImplementsHandlerInterface, "implement a handler interface")
            .Should().NotBePublic().AndShould().BeSealed();

        rule.Check(Code.Architecture);
    }

    [Fact]
    public void Handlers_DoNotLiveOutsideTheApplicationLayer()
    {
        IArchRule rule = Classes().That().FollowCustomPredicate(ImplementsHandlerInterface, "implement a handler interface")
            .And().ResideInNamespaceMatching(@"^Balboa\.Modules\.")
            .Should().ResideInNamespaceMatching(Layers.Pattern("Application"));

        rule.Check(Code.Architecture);
    }

    [Fact]
    public void DbContexts_AreInternalAndLiveInInfrastructure()
    {
        IArchRule rule = Classes().That().FollowCustomPredicate(IsDbContext, "derive from DbContext")
            .And().ResideInNamespaceMatching(@"^Balboa\.Modules\.")
            .Should().NotBePublic()
            .AndShould().ResideInNamespaceMatching(Layers.Pattern("Infrastructure"));

        rule.Check(Code.Architecture);
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void DbContext_MapsOnlyItsOwnModulesEntitiesInItsOwnSchema(string module)
    {
        Type[] contexts = [.. Code.Assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(DbContext).IsAssignableFrom(t)
                && t.Namespace is { } ns
                && ns.StartsWith($"Balboa.Modules.{module}.", StringComparison.Ordinal))];
        contexts.ShouldHaveSingleItem($"module '{module}' must have exactly one DbContext");

        using DbContext context = CreateContext(contexts[0]);
        string expectedSchema = module.ToLowerInvariant();
        var foreign = new List<string>();
        foreach (IEntityType entity in context.Model.GetEntityTypes())
        {
            Type clr = entity.ClrType;
            bool ownEntity = clr.Namespace?.StartsWith($"Balboa.Modules.{module}.", StringComparison.Ordinal) == true;
            bool sharedOutboxEntity = clr == typeof(OutboxMessage) || clr == typeof(InboxMessage);
            if (!ownEntity && !sharedOutboxEntity)
            {
                foreign.Add($"{clr.FullName} is not an entity of module '{module}'");
            }

            if (entity.GetSchema() != expectedSchema)
            {
                foreign.Add($"{clr.Name} maps to schema '{entity.GetSchema()}', expected '{expectedSchema}'");
            }

            foreach (IForeignKey fk in entity.GetForeignKeys().Where(k => k.PrincipalEntityType.GetSchema() != entity.GetSchema()))
            {
                foreign.Add($"{clr.Name} has a cross-schema foreign key to {fk.PrincipalEntityType.ClrType.Name}");
            }
        }

        foreign.ShouldBeEmpty();
        context.Model.GetDefaultSchema().ShouldBe(expectedSchema);
        // The history table location (schema + name) is configured with the provider options in AddXModule, so it is
        // asserted against the real database by the module's SchemaTests rather than here.
    }

    public static TheoryData<string> ModuleNames => Code.ModuleData();

    private static DbContext CreateContext(Type contextType)
    {
        // Building the EF model needs no database connection; a throwaway connection string is enough.
        Type builderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(contextType);
        var builder = (DbContextOptionsBuilder)Activator.CreateInstance(builderType)!;
        builder.UseNpgsql("Host=localhost;Database=architecture_tests");
        return (DbContext)Activator.CreateInstance(contextType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [builder.Options], null)!;
    }

    [System.Text.RegularExpressions.GeneratedRegex(_handlerInterfaces)]
    private static partial System.Text.RegularExpressions.Regex MyRegex();
}

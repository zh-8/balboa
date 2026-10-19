using System.Reflection;
using System.Text.RegularExpressions;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Fluent.Syntax.Elements.Types.Classes;
using ArchUnitNET.Loader;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Architecture = ArchUnitNET.Domain.Architecture;
using Assembly = System.Reflection.Assembly;

namespace Balboa.ArchitectureTests;

/// <summary>
/// The code under inspection: every production Balboa assembly found next to the test binaries. Because the Host
/// is referenced, a new module is picked up automatically once the Host references its Infrastructure project.
/// </summary>
internal static partial class Code
{
    private static readonly Regex _productionAssembly = ProductionAssemblyPattern();

    public static readonly Assembly[] Assemblies =
    [
        .. Directory.GetFiles(AppContext.BaseDirectory, "Balboa.*.dll")
            .Where(path => _productionAssembly.IsMatch(Path.GetFileName(path)))
            .Select(Assembly.LoadFrom),
    ];

    public static readonly Architecture Architecture = new ArchLoader().LoadAssemblies(Assemblies).Build();

    /// <summary>Module names derived from assembly names: Balboa.Modules.{Name}.{Layer}.</summary>
    public static IReadOnlyList<string> Modules { get; } =
    [
        .. Assemblies
            .Select(a => ModuleOf(a.GetName().Name!))
            .Where(m => m is not null)
            .Select(m => m!)
            .Distinct()
            .Order(),
    ];

    public static TheoryData<string> ModuleData() => [.. Modules];

    public static string? ModuleOf(string assemblyName)
    {
        Match match = ModuleAssemblyPattern().Match(assemblyName);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"^Balboa\.(SharedKernel|BuildingBlocks|ServiceDefaults|Host|Modules\..+)\.dll$")]
    private static partial Regex ProductionAssemblyPattern();

    [GeneratedRegex(@"^Balboa\.Modules\.([^.]+)\.")]
    private static partial Regex ModuleAssemblyPattern();
}

/// <summary>Namespace-based selectors. A project's namespace equals its assembly name by convention.</summary>
internal static class Layers
{
    public static string Pattern(string layer, string module = "[^.]+") =>
        $@"^Balboa\.Modules\.{module}\.{layer}(\..*)?$";

    public static GivenTypesConjunction Of(string layer, string module = "[^.]+") =>
        Types().That().ResideInNamespaceMatching(Pattern(layer, module));

    public static GivenClassesConjunction ClassesOf(string layer, string module = "[^.]+") =>
        Classes().That().ResideInNamespaceMatching(Pattern(layer, module));

    /// <summary>Guards against a rule passing vacuously because its selector matched nothing.</summary>
    public static void ShouldMatchSomething(string layer, string module = "[^.]+")
    {
        Code.Architecture.Types
            .Count(t => Regex.IsMatch(t.Namespace.FullName, Pattern(layer, module)))
            .ShouldBeGreaterThan(0, $"no types found in layer '{layer}' of module '{module}'; the rule would pass vacuously");
    }
}

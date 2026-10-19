using System.Text.RegularExpressions;
using Xunit.v3;

namespace Balboa.TestKit;

/// <summary>
/// Ties a test to the acceptance criterion it proves, e.g. <c>[Spec("AC-0001-02")]</c>. Surfaces as the xUnit
/// trait <c>Spec=AC-0001-02</c>; <c>make spec-check</c> verifies every criterion has at least one tagged test.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed partial class SpecAttribute : Attribute, ITraitAttribute
{
    public SpecAttribute(string criterionId)
    {
        if (!CriterionPattern().IsMatch(criterionId))
        {
            throw new ArgumentException(
                $"'{criterionId}' is not a valid acceptance criterion id. Expected AC-NNNN-NN, e.g. AC-0001-02.",
                nameof(criterionId));
        }

        CriterionId = criterionId;
    }

    public string CriterionId { get; }

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() => [new("Spec", CriterionId)];

    [GeneratedRegex(@"^AC-\d{4}-\d{2}$")]
    private static partial Regex CriterionPattern();
}

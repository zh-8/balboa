// PLACEHOLDER: this module exists only to demonstrate the Balboa module pattern (aggregate with invariants
// enforced in a Result-returning factory, a value object, a strongly-typed id and a domain event).
// It has no product meaning and will be deleted when the first real bounded context arrives.
using Balboa.SharedKernel;

namespace Balboa.Modules.Sample.Domain;

public readonly record struct WidgetId(Guid Value) : IStronglyTypedId<WidgetId>
{
    public static WidgetId New() => new(Guid.NewGuid());

    public static WidgetId From(Guid value) => new(value);
}

public static class WidgetErrors
{
    public static readonly Error InvalidSku = Error.Validation(
        "Widget.InvalidSku", "SKU must be 3-20 characters: letters, digits and hyphens only.");

    public static readonly Error InvalidName = Error.Validation(
        "Widget.InvalidName", $"Name is required and must be at most {WidgetCatalogItem.MaxNameLength} characters.");

    public static readonly Error AlreadyPublished = Error.Conflict(
        "Widget.AlreadyPublished", "The widget is already published.");

    public static readonly Error DuplicateSku = Error.Conflict(
        "Widget.DuplicateSku", "A widget with this SKU already exists.");

    public static readonly Error NotFound = Error.NotFound("Widget.NotFound", "The widget does not exist.");
}

/// <summary>Stock-keeping unit: normalised to upper case; 3-20 letters, digits or hyphens.</summary>
public sealed class Sku : ValueObject
{
    public const int MinLength = 3;
    public const int MaxLength = 20;

    private Sku(string value) => Value = value;

    public string Value { get; }

    public static Result<Sku> Create(string? value)
    {
        string normalised = (value ?? string.Empty).Trim().ToUpperInvariant();
        bool valid = normalised.Length is >= MinLength and <= MaxLength
            && normalised.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
        return valid ? new Sku(normalised) : WidgetErrors.InvalidSku;
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}

/// <summary>Raised when a widget is published. Stays inside the module; the Application layer maps it to an integration event.</summary>
public sealed record WidgetPublished(WidgetId WidgetId, string Sku) : DomainEvent;

public sealed class WidgetCatalogItem : AggregateRoot<WidgetId>
{
    public const int MaxNameLength = 100;

    private WidgetCatalogItem(WidgetId id, Sku sku, string name)
        : base(id)
    {
        Sku = sku;
        Name = name;
    }

    // For EF Core only.
    private WidgetCatalogItem()
    {
        Sku = default!;
        Name = string.Empty;
    }

    public Sku Sku { get; private set; }

    public string Name { get; private set; }

    public bool IsPublished { get; private set; }

    /// <summary>All invariants are enforced here; there is no way to build an invalid item.</summary>
    public static Result<WidgetCatalogItem> Create(string? sku, string? name)
    {
        Result<Sku> skuResult = Sku.Create(sku);
        if (skuResult.IsFailure)
        {
            return Result.Failure<WidgetCatalogItem>(skuResult.Error);
        }

        string trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length is 0 or > MaxNameLength)
        {
            return WidgetErrors.InvalidName;
        }

        return new WidgetCatalogItem(WidgetId.New(), skuResult.Value, trimmed);
    }

    public Result Publish()
    {
        if (IsPublished)
        {
            return Result.Failure(WidgetErrors.AlreadyPublished);
        }

        IsPublished = true;
        Raise(new WidgetPublished(Id, Sku.Value));
        return Result.Success();
    }
}

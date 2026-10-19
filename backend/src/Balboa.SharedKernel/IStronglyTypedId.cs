namespace Balboa.SharedKernel;

/// <summary>
/// Strongly-typed identifier pattern: declare <c>public readonly record struct WidgetId(Guid Value) : IStronglyTypedId&lt;WidgetId&gt;</c>
/// and implement <see cref="From"/>. Prevents mixing up ids of different aggregates.
/// </summary>
public interface IStronglyTypedId<TSelf>
    where TSelf : IStronglyTypedId<TSelf>
{
    public Guid Value { get; }

    public static abstract TSelf From(Guid value);
}

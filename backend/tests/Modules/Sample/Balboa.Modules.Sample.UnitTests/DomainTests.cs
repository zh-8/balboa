using Balboa.Modules.Sample.Domain;
using Balboa.SharedKernel;

namespace Balboa.Modules.Sample.UnitTests;

public class SkuTests
{
    [Spec("AC-0001-01")]
    [Theory]
    [InlineData("abc-123", "ABC-123")]
    [InlineData("  w1x  ", "W1X")]
    public void Create_ValidInput_ReturnsUpperCaseTrimmedSku(string input, string expected)
    {
        Result<Sku> result = Sku.Create(input);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(expected);
    }

    [Spec("AC-0001-02")]
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("AB")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")]
    [InlineData("ABC DEF")]
    [InlineData("ABC_DEF")]
    public void Create_InvalidInput_ReturnsValidationError(string? input)
    {
        Result<Sku> result = Sku.Create(input);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(WidgetErrors.InvalidSku);
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Equals_SameValueDifferentCase_AreEqual()
    {
        Sku.Create("abc-1").Value.ShouldBe(Sku.Create("ABC-1").Value);
        (Sku.Create("abc-1").Value == Sku.Create("ABC-2").Value).ShouldBeFalse();
    }
}

public class WidgetCatalogItemTests
{
    [Spec("AC-0001-01")]
    [Fact]
    public void Create_ValidSkuAndName_ReturnsUnpublishedItem()
    {
        Result<WidgetCatalogItem> result = WidgetCatalogItem.Create("wid-001", "  Brass Gizmo ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Sku.Value.ShouldBe("WID-001");
        result.Value.Name.ShouldBe("Brass Gizmo");
        result.Value.IsPublished.ShouldBeFalse();
        result.Value.Id.Value.ShouldNotBe(Guid.Empty);
        result.Value.DomainEvents.ShouldBeEmpty();
    }

    [Spec("AC-0001-02")]
    [Fact]
    public void Create_InvalidSku_ReturnsInvalidSkuError()
    {
        Result<WidgetCatalogItem> result = WidgetCatalogItem.Create("!!", "Gizmo");

        result.Error.ShouldBe(WidgetErrors.InvalidSku);
    }

    [Spec("AC-0001-02")]
    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Create_BlankName_ReturnsInvalidNameError(string? name) => WidgetCatalogItem.Create("WID-001", name).Error.ShouldBe(WidgetErrors.InvalidName);

    [Spec("AC-0001-02")]
    [Fact]
    public void Create_NameLongerThanMax_ReturnsInvalidNameError()
    {
        string name = new('x', WidgetCatalogItem.MaxNameLength + 1);

        WidgetCatalogItem.Create("WID-001", name).Error.ShouldBe(WidgetErrors.InvalidName);
    }

    [Spec("AC-0001-04")]
    [Fact]
    public void Publish_Unpublished_SetsFlagAndRaisesWidgetPublished()
    {
        WidgetCatalogItem item = WidgetCatalogItem.Create("WID-001", "Gizmo").Value;

        Result result = item.Publish();

        result.IsSuccess.ShouldBeTrue();
        item.IsPublished.ShouldBeTrue();
        WidgetPublished raised = item.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<WidgetPublished>();
        raised.WidgetId.ShouldBe(item.Id);
        raised.Sku.ShouldBe("WID-001");
    }

    [Spec("AC-0001-04")]
    [Fact]
    public void Publish_AlreadyPublished_ReturnsConflictAndRaisesNothingNew()
    {
        WidgetCatalogItem item = WidgetCatalogItem.Create("WID-001", "Gizmo").Value;
        item.Publish();
        item.ClearDomainEvents();

        Result result = item.Publish();

        result.Error.ShouldBe(WidgetErrors.AlreadyPublished);
        item.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Equals_SameId_AreEqualRegardlessOfState()
    {
        WidgetCatalogItem item = WidgetCatalogItem.Create("WID-001", "Gizmo").Value;

        item.Equals(item).ShouldBeTrue();
        item.Equals(WidgetCatalogItem.Create("WID-001", "Gizmo").Value).ShouldBeFalse();
    }
}

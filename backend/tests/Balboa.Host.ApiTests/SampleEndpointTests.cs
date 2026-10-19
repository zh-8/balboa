using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Balboa.Modules.Sample.Contracts;
using Balboa.TestKit;

namespace Balboa.Host.ApiTests;

[Trait("Category", "Integration")]
public class SampleEndpointTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Spec("AC-0001-01")]
    [Fact]
    public async Task PostWidget_ValidRequest_Returns201WithLocationAndWidgetIsReadable()
    {
        HttpResponseMessage created = await Client.PostAsJsonAsync(
            "/api/sample/widgets", new CreateWidgetRequest("api-001", "API Gizmo"), Ct);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        created.Headers.Location.ShouldNotBeNull();
        WidgetDto? widget = await Client.GetFromJsonAsync<WidgetDto>(created.Headers.Location, Ct);
        widget.ShouldNotBeNull();
        widget.Sku.ShouldBe("API-001");
        widget.Name.ShouldBe("API Gizmo");
        widget.IsPublished.ShouldBeFalse();
    }

    [Spec("AC-0001-02")]
    [Theory]
    [InlineData("x", "Name")]
    [InlineData("API-001", "")]
    public async Task PostWidget_InvalidInput_Returns400ProblemWithErrorCode(string sku, string name)
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/sample/widgets", new CreateWidgetRequest(sku, name), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        problem.RootElement.GetProperty("title").GetString().ShouldStartWith("Widget.Invalid");
    }

    [Spec("AC-0001-03")]
    [Fact]
    public async Task PostWidget_DuplicateSku_Returns409()
    {
        await Client.PostAsJsonAsync("/api/sample/widgets", new CreateWidgetRequest("dup-api", "One"), Ct);

        HttpResponseMessage second = await Client.PostAsJsonAsync(
            "/api/sample/widgets", new CreateWidgetRequest("DUP-API", "Two"), Ct);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Spec("AC-0001-04")]
    [Fact]
    public async Task PublishWidget_ThenPublishAgain_Returns204ThenConflict()
    {
        Guid id = await CreateAsync("pub-001");

        HttpResponseMessage first = await Client.PostAsync($"/api/sample/widgets/{id}/publish", null, Ct);
        HttpResponseMessage second = await Client.PostAsync($"/api/sample/widgets/{id}/publish", null, Ct);

        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Client.GetFromJsonAsync<WidgetDto>($"/api/sample/widgets/{id}", Ct))!.IsPublished.ShouldBeTrue();
    }

    [Spec("AC-0001-04")]
    [Fact]
    public async Task PublishWidget_UnknownId_Returns404()
    {
        HttpResponseMessage response = await Client.PostAsync($"/api/sample/widgets/{Guid.NewGuid()}/publish", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Spec("AC-0001-06")]
    [Fact]
    public async Task GetWidget_UnknownId_Returns404()
    {
        HttpResponseMessage response = await Client.GetAsync($"/api/sample/widgets/{Guid.NewGuid()}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Spec("AC-0001-06")]
    [Fact]
    public async Task GetWidgets_TwoCreated_ReturnsBoth()
    {
        await CreateAsync("lst-002");
        await CreateAsync("lst-001");

        List<WidgetDto>? widgets = await Client.GetFromJsonAsync<List<WidgetDto>>("/api/sample/widgets", Ct);

        widgets.ShouldNotBeNull();
        widgets.Select(w => w.Sku).ShouldBe(["LST-001", "LST-002"]);
    }

    private async Task<Guid> CreateAsync(string sku)
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/sample/widgets", new CreateWidgetRequest(sku, "Widget " + sku), Ct);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return body.RootElement.GetProperty("id").GetGuid();
    }
}

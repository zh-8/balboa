using Balboa.BuildingBlocks.Cqrs;
using Balboa.Modules.Sample.Application;
using Balboa.Modules.Sample.Contracts;
using Balboa.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Balboa.Modules.Sample.Infrastructure;

internal static class SampleEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/sample/widgets").WithTags("Sample");

        group.MapPost("/", async (
            CreateWidgetRequest request,
            ICommandHandler<CreateWidgetCommand, Guid> handler,
            CancellationToken ct) =>
        {
            Result<Guid> result = await handler.HandleAsync(new CreateWidgetCommand(request.Sku, request.Name), ct);
            return result.IsSuccess
                ? Results.Created($"/api/sample/widgets/{result.Value}", new { id = result.Value })
                : result.Error.ToProblem();
        });

        group.MapGet("/", async (ISampleQueries queries, CancellationToken ct) =>
        {
            Result<IReadOnlyList<WidgetDto>> result = await queries.ListWidgetsAsync(ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
        });

        group.MapGet("/{id:guid}", async (Guid id, ISampleQueries queries, CancellationToken ct) =>
        {
            Result<WidgetDto> result = await queries.GetWidgetAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
        });

        group.MapPost("/{id:guid}/publish", async (
            Guid id,
            ICommandHandler<PublishWidgetCommand> handler,
            CancellationToken ct) =>
        {
            Result result = await handler.HandleAsync(new PublishWidgetCommand(id), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
        });
    }

    private static IResult ToProblem(this Error error) => Results.Problem(
        title: error.Code,
        detail: error.Description,
        statusCode: error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        });
}

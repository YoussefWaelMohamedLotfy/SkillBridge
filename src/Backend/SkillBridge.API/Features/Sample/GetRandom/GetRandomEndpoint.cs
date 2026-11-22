using FastEndpoints;

namespace SkillBridge.API.Features.Sample.GetRandom;

public sealed class GetRandomEndpoint : EndpointWithoutRequest<int>
{
    public override void Configure()
    {
        Get("/Random");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        return Task.FromResult(Random.Shared.NextInt64());
    }
}

using Luna;

namespace Penumbra;

public sealed class PenumbraIntermediary(PenumbraLoader parent) : IIntermediaryPlugin<Penumbra>
{
    public PenumbraLoader Parent { get; } = parent;

    PluginLoader<Penumbra> IIntermediaryPlugin<Penumbra>.Parent
        => Parent;

    public Task<bool> ValidateAsync(CancellationToken cancel)
        => Task.FromResult(true);

    public Task OnValidationFailedAsync(CancellationToken cancel)
        => throw new Exception("INVALID!");

    public Task<PluginInitializationFailure.FailureHandling> OnPluginInitializationFailedAsync(PluginInitializationFailure arguments,
        CancellationToken cancel)
        => Task.FromResult(PluginInitializationFailure.FailureHandling.Rethrow);

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}

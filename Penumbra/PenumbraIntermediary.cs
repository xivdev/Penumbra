using Luna;
using Penumbra.Services;
using Penumbra.UI;

namespace Penumbra;

public sealed class PenumbraIntermediary(PenumbraLoader parent) : IIntermediaryPlugin<Penumbra>
{
    public  PenumbraLoader               Parent    { get; } = parent;
    public  ValidationService            Validator { get; } = parent.Services.GetService<ValidationService>();
    private PenumbraErrorWindow?         _window;
    public  PluginInitializationFailure? Failure { get; private set; }

    PluginLoader<Penumbra> IIntermediaryPlugin<Penumbra>.Parent
        => Parent;

    public Task<bool> ValidateAsync(CancellationToken cancel)
        => Task.FromResult(Validator.Validate() && false);

    public Task OnValidationFailedAsync(CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        _window                                       =  new PenumbraErrorWindow(this, Parent.Services.GetService<ImSharpDalamudContext>(), Parent.Services.GetService<PenumbraSupportInfo>());
        Parent.PluginInterface.UiBuilder.OpenMainUi   += OpenErrorWindow;
        Parent.PluginInterface.UiBuilder.OpenConfigUi += OpenErrorWindow;
        return Task.CompletedTask;
    }

    public async Task<PluginInitializationFailure.FailureHandling> OnPluginInitializationFailedAsync(PluginInitializationFailure arguments,
        CancellationToken cancel)
    {
        Failure = arguments;
        await OnValidationFailedAsync(cancel).ConfigureAwait(false);
        return PluginInitializationFailure.FailureHandling.Handled;
    }

    public ValueTask DisposeAsync()
    {
        Parent.PluginInterface.UiBuilder.OpenMainUi   -= OpenErrorWindow;
        Parent.PluginInterface.UiBuilder.OpenConfigUi -= OpenErrorWindow;
        _window?.Dispose();
        _window = null;
        return ValueTask.CompletedTask;
    }

    private void OpenErrorWindow()
        => _window?.IsOpen = true;
}

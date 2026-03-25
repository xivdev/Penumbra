using Dalamud.Interface;
using Dalamud.Plugin.Services;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.Services;
using Penumbra.String.Classes;

namespace Penumbra.Api;

public class DalamudSubstitutionProvider : IDisposable, Luna.IApiService
{
    private readonly ITextureSubstitutionProvider _substitution;
    private readonly IUiBuilder                   _uiBuilder;
    private readonly ActiveCollectionData         _activeCollectionData;
    private readonly Configuration                _config;
    private readonly CommunicatorService          _communicator;

    public bool Enabled
        => _config.UseDalamudUiTextureRedirection;

    public DalamudSubstitutionProvider(ITextureSubstitutionProvider substitution, ActiveCollectionData activeCollectionData,
        Configuration config, CommunicatorService communicator, IUiBuilder ui)
    {
        _substitution         = substitution;
        _uiBuilder            = ui;
        _activeCollectionData = activeCollectionData;
        _config               = config;
        _communicator         = communicator;
        if (Enabled)
            Subscribe();
    }

    public void Set(bool value)
    {
        if (value)
            Enable();
        else
            Disable();
    }

    public void ResetSubstitutions(IEnumerable<Utf8GamePath> paths)
    {
        if (!_uiBuilder.UiPrepared)
            return;

        var transformed = paths
            .Where(p => (p.Path.StartsWith("ui/"u8) || p.Path.StartsWith("common/font/"u8)) && p.Path.EndsWith(".tex"u8))
            .Select(p => p.ToString());
        _substitution.InvalidatePaths(transformed);
    }

    public void Enable()
    {
        if (Enabled)
            return;

        _config.UseDalamudUiTextureRedirection = true;
        _config.Save();
        Subscribe();
    }

    public void Disable()
    {
        if (!Enabled)
            return;

        Unsubscribe();
        _config.UseDalamudUiTextureRedirection = false;
        _config.Save();
    }

    public void Dispose()
        => Unsubscribe();

    private void OnCollectionChange(in CollectionChange.Arguments arguments)
    {
        if (arguments.Type is not CollectionType.Interface)
            return;

        var enumerable = arguments.OldCollection?.ResolvedFiles.Keys ?? Array.Empty<Utf8GamePath>().AsEnumerable();
        enumerable = enumerable.Concat(arguments.NewCollection?.ResolvedFiles.Keys ?? Array.Empty<Utf8GamePath>().AsEnumerable());
        ResetSubstitutions(enumerable);
    }

    private void OnResolvedFileChange(in ResolvedFileChanged.Arguments arguments)
    {
        if (_activeCollectionData.Interface != arguments.Collection)
            return;

        switch (arguments.Type)
        {
            case ResolvedFileChanged.Type.Added:
            case ResolvedFileChanged.Type.Removed:
            case ResolvedFileChanged.Type.Replaced:
                ResetSubstitutions([arguments.GamePath]);
                break;
            case ResolvedFileChanged.Type.FullRecomputeStart:
            case ResolvedFileChanged.Type.FullRecomputeFinished:
                ResetSubstitutions(arguments.Collection.ResolvedFiles.Keys);
                break;
        }
    }

    private void OnEnabledChange(in EnabledChanged.Arguments arguments)
    {
        if (arguments.Enabled)
            OnCollectionChange(new CollectionChange.Arguments(CollectionType.Interface, null, _activeCollectionData.Interface, string.Empty));
        else
            OnCollectionChange(new CollectionChange.Arguments(CollectionType.Interface, _activeCollectionData.Interface, null, string.Empty));
    }

    private void Substitute(string path, ref string? replacementPath)
    {
        // Do not replace when not enabled.
        if (!_config.EnableMods)
            return;

        // Let other plugins prioritize replacement paths.
        if (replacementPath != null)
            return;

        // Only replace interface textures.
        if (!path.StartsWith("ui/") && !path.StartsWith("common/font/"))
            return;

        try
        {
            if (!Utf8GamePath.FromString(path, out var utf8Path))
                return;

            var resolved = _activeCollectionData.Interface.ResolvePath(utf8Path);
            replacementPath = resolved?.FullName;
        }
        catch
        {
            // ignored
        }
    }

    private void Subscribe()
    {
        _substitution.InterceptTexDataLoad += Substitute;
        _communicator.CollectionChange.Subscribe(OnCollectionChange, CollectionChange.Priority.DalamudSubstitutionProvider);
        _communicator.ResolvedFileChanged.Subscribe(OnResolvedFileChange, ResolvedFileChanged.Priority.DalamudSubstitutionProvider);
        _communicator.EnabledChanged.Subscribe(OnEnabledChange, EnabledChanged.Priority.DalamudSubstitutionProvider);
        OnCollectionChange(new CollectionChange.Arguments(CollectionType.Interface, null, _activeCollectionData.Interface, string.Empty));
    }

    private void Unsubscribe()
    {
        _substitution.InterceptTexDataLoad -= Substitute;
        _communicator.CollectionChange.Unsubscribe(OnCollectionChange);
        _communicator.ResolvedFileChanged.Unsubscribe(OnResolvedFileChange);
        _communicator.EnabledChanged.Unsubscribe(OnEnabledChange);
        OnCollectionChange(new CollectionChange.Arguments(CollectionType.Interface, _activeCollectionData.Interface, null, string.Empty));
    }
}

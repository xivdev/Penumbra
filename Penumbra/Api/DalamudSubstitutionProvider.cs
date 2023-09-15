using Dalamud.Plugin.Services;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Services;
using Penumbra.String.Classes;

namespace Penumbra.Api;

public class DalamudSubstitutionProvider : IDisposable
{
    private readonly ITextureSubstitutionProvider _substitution;
    private readonly ActiveCollectionData         _activeCollectionData;
    private readonly Configuration                _config;
    private readonly CommunicatorService          _communicator;

    public bool Enabled
        => _config.UseDalamudUiTextureRedirection;

    public DalamudSubstitutionProvider(ITextureSubstitutionProvider substitution, ActiveCollectionData activeCollectionData,
        Configuration config, CommunicatorService communicator)
    {
        _substitution         = substitution;
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
        // TODO fix
        //var transformed = paths
        //    .Where(p => (p.Path.StartsWith("ui/"u8) || p.Path.StartsWith("common/font/"u8)) && p.Path.EndsWith(".tex"u8))
        //    .Select(p => p.ToString());
        //_substitution.InvalidatePaths(transformed);
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

    private void OnCollectionChange(CollectionType type, ModCollection? oldCollection, ModCollection? newCollection, string _)
    {
        if (type is not CollectionType.Interface)
            return;

        var enumerable = oldCollection?.ResolvedFiles.Keys ?? Array.Empty<Utf8GamePath>().AsEnumerable();
        enumerable = enumerable.Concat(newCollection?.ResolvedFiles.Keys ?? Array.Empty<Utf8GamePath>().AsEnumerable());
        ResetSubstitutions(enumerable);
    }

    private void OnResolvedFileChange(ModCollection collection, ResolvedFileChanged.Type type, Utf8GamePath key, FullPath _1, FullPath _2,
        IMod? _3)
    {
        if (_activeCollectionData.Interface != collection)
            return;

        switch (type)
        {
            case ResolvedFileChanged.Type.Added:
            case ResolvedFileChanged.Type.Removed:
            case ResolvedFileChanged.Type.Replaced:
                ResetSubstitutions(new[]
                {
                    key,
                });
                break;
            case ResolvedFileChanged.Type.FullRecomputeStart:
            case ResolvedFileChanged.Type.FullRecomputeFinished:
                ResetSubstitutions(collection.ResolvedFiles.Keys);
                break;
        }
    }

    private void OnEnabledChange(bool state)
    {
        if (state)
            OnCollectionChange(CollectionType.Interface, null, _activeCollectionData.Interface, string.Empty);
        else
            OnCollectionChange(CollectionType.Interface, _activeCollectionData.Interface, null, string.Empty);
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
            if (!Utf8GamePath.FromString(path, out var utf8Path, true))
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
        OnCollectionChange(CollectionType.Interface, null, _activeCollectionData.Interface, string.Empty);
    }

    private void Unsubscribe()
    {
        _substitution.InterceptTexDataLoad -= Substitute;
        _communicator.CollectionChange.Unsubscribe(OnCollectionChange);
        _communicator.ResolvedFileChanged.Unsubscribe(OnResolvedFileChange);
        _communicator.EnabledChanged.Unsubscribe(OnEnabledChange);
        OnCollectionChange(CollectionType.Interface, _activeCollectionData.Interface, null, string.Empty);
    }
}

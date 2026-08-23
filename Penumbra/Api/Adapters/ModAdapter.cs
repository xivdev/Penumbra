using Luna;
using Luna.Generators;
using Penumbra.Api.Wrappers;
using Penumbra.Mods;

namespace Penumbra.Api;

public sealed partial class ModAdapter(ModManagerAdapter parent, Mod mod)
    : IpcObjectManager.BasicAdapter(parent.Parent, parent.Owner, nameof(ModAdapter)), IpcObjectManager.IBasicAdapter
{
    [AdapterMethod(ModWrapper.Method.Version)]
    public override (int Major, int Minor) Version
        => (1, 0);

    private Mod _mod = mod;

    [AdapterMethod(ModWrapper.Method.ModPath)]
    private string ModPath
        => _mod.ModPath.FullName;

    [AdapterMethod(ModWrapper.Method.Index)]
    private int Index
        => _mod.Index;

    [AdapterMethod(ModWrapper.Method.Name)]
    private string Name
        => _mod.Name;

    [AdapterMethod(ModWrapper.Method.Identifier)]
    private string Identifier
        => _mod.Identifier;

    [AdapterMethod(ModWrapper.Method.Author)]
    private string Author
        => _mod.Author;

    [AdapterMethod(ModWrapper.Method.Description)]
    private string Description
        => _mod.Description;

    [AdapterMethod(ModWrapper.Method.ModVersion)]
    private string ModVersion
        => _mod.Version;

    [AdapterMethod(ModWrapper.Method.Website)]
    private string Website
        => _mod.Website;

    [AdapterMethod(ModWrapper.Method.Image)]
    private string Image
        => _mod.Image;

    [AdapterMethod(ModWrapper.Method.SortName)]
    private string? SortName
        => _mod.Path.SortName;

    [AdapterMethod(ModWrapper.Method.Folder)]
    private string Folder
        => _mod.Path.Folder;

    [AdapterMethod(ModWrapper.Method.FullPath)]
    private string FullPath
        => _mod.Path.CurrentPath;

    [AdapterMethod(ModWrapper.Method.Favorite)]
    private bool Favorite
        => _mod.Favorite;

    [AdapterMethod(ModWrapper.Method.ImportDate)]
    private DateTimeOffset ImportDate
        => DateTimeOffset.FromUnixTimeMilliseconds(_mod.ImportDate);

    [AdapterMethod(ModWrapper.Method.LastConfigEdit)]
    private DateTimeOffset LastConfigEdit
        => DateTimeOffset.FromUnixTimeMilliseconds(_mod.LastConfigEdit);

    [AdapterMethod(ModWrapper.Method.LocalTags)]
    private IReadOnlyList<string> LocalTags
        => _mod.LocalTags;

    [AdapterMethod(ModWrapper.Method.ModTags)]
    private IReadOnlyList<string> ModTags
        => _mod.ModTags;

    [AdapterMethod(ModWrapper.Method.RequiredFeatures)]
    private ulong RequiredFeatures
        => (ulong)_mod.RequiredFeatures;

    protected override void DisposeInternal()
        => _mod = null!;
}

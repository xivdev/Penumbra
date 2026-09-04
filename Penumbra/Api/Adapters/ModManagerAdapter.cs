using Dalamud.Plugin.Ipc;
using Luna;
using Luna.Generators;
using Penumbra.Api.Wrappers;
using Penumbra.Mods;
using Penumbra.Mods.Manager;

namespace Penumbra.Api;

public sealed class ModManagerAdapterFactory(IpcObjectManager ipcManager, ModManager mods) : IAdapterFactory, IApiService
{
    public readonly ModManager       Mods = mods;
    public          IpcObjectManager IpcManager { get; } = ipcManager;

    public IpcObjectManager.IBasicAdapter CreateAdapter(CallerPlugin owner, object? data)
        => new ModManagerAdapter(this, owner);
}

public sealed partial class ModManagerAdapter(ModManagerAdapterFactory parent, CallerPlugin owner)
    : IpcObjectManager.BasicAdapter(parent, owner, nameof(ModManagerAdapter)), IAdapterFactory, IpcObjectManager.IBasicAdapter
{
    [AdapterMethod(ModManagerWrapper.Method.Version, AlwaysAlive = true)]
    public override (int Major, int Minor) Version
        => (1, 0);

    [AdapterMethod(ModManagerWrapper.Method.Alive, AlwaysAlive = true)]
    public override bool Alive
        => base.Parent is not null;

    [AdapterMethod(ModManagerWrapper.Method.DisposedEvent, AlwaysAlive = true)]
    public event Action? Disposed;

    protected override void InvokeDisposed()
        => Disposed?.Invoke();

    public IpcObjectManager IpcManager
        => Parent.IpcManager;

    public new ModManagerAdapterFactory Parent
        => (ModManagerAdapterFactory)base.Parent!;

    [AdapterMethod(ModManagerWrapper.Method.Count)]
    private int Count
        => Parent.Mods.Count;

    [AdapterMethod(ModManagerWrapper.Method.NameByIndex)]
    private ModIdentifier NameByIndex(int modIndex)
    {
        if (modIndex < 0 || modIndex >= Parent.Mods.Count)
            return (string.Empty, string.Empty);

        var mod = Parent.Mods[modIndex];
        return (mod.Identifier, mod.Name);
    }

    [AdapterMethod(ModManagerWrapper.Method.IndexByName)]
    private int IndexByName(ModIdentifier mod)
        => Parent.Mods.TryGetMod(mod.Identifier, mod.Name, out var m) ? m.Index : -1;

    [AdapterMethod(ModManagerWrapper.Method.GetByIndex, DisposeOnFailure = true)]
    private IIdDataShareAdapter? GetByIndex(int modIndex)
        => CreateMod(modIndex < 0 || modIndex >= Parent.Mods.Count ? null : Parent.Mods[modIndex]);

    [AdapterMethod(ModManagerWrapper.Method.GetByName, DisposeOnFailure = true)]
    private IIdDataShareAdapter? GetByName(ModIdentifier identifier)
        => CreateMod(Parent.Mods.TryGetMod(identifier.Identifier, identifier.Name, out var mod) ? mod : null);

    [AdapterMethod(ModManagerWrapper.Method.EnumerateNames)]
    private IEnumerable<ModIdentifier> EnumerateNames()
        => Parent.Mods.Select(m => (m.Identifier, m.Name));

    [AdapterMethod(ModManagerWrapper.Method.ContainsChangedItem)]
    private bool ContainsChangedItem(int modIndex, string changedItem)
    {
        if (modIndex < 0 || modIndex >= Parent.Mods.Count)
            return false;

        var mod = Parent.Mods[modIndex];
        return mod.ChangedItems.ContainsKey(changedItem);
    }

    [AdapterMethod(ModManagerWrapper.Method.ModDirectory)]
    private DirectoryInfo? ModDirectory
        => parent.Mods.Valid ? parent.Mods.BasePath : null;

    [return: NotNullIfNotNull(nameof(mod))]
    private IIdDataShareAdapter? CreateMod(Mod? mod, [CallerMemberName] string? callerName = null)
        => this.Create(Owner, mod, callerName);

    public IpcObjectManager.IBasicAdapter? CreateAdapter(CallerPlugin owner, object? mod)
        => mod is not Mod m ? null : new ModAdapter(this, m);
}

using Dalamud.Plugin;
using ImSharp;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;

namespace Penumbra.Api.IpcTester;

public class ModsIpcTester : Luna.IUiService, IDisposable
{
    private readonly IDalamudPluginInterface _pi;

    private string                      _modDirectory   = string.Empty;
    private string                      _modName        = string.Empty;
    private string                      _pathInput      = string.Empty;
    private string                      _newInstallPath = string.Empty;
    private PenumbraApiEc               _lastReloadEc;
    private PenumbraApiEc               _lastAddEc;
    private PenumbraApiEc               _lastDeleteEc;
    private PenumbraApiEc               _lastSetPathEc;
    private PenumbraApiEc               _lastInstallEc;
    private Dictionary<string, string>  _mods         = [];
    private Dictionary<string, object?> _changedItems = [];

    public readonly EventSubscriber<string>         DeleteSubscriber;
    public readonly EventSubscriber<string>         AddSubscriber;
    public readonly EventSubscriber<string, string> MoveSubscriber;

    private DateTimeOffset _lastDeletedModTime = DateTimeOffset.UnixEpoch;
    private string         _lastDeletedMod     = string.Empty;
    private DateTimeOffset _lastAddedModTime   = DateTimeOffset.UnixEpoch;
    private string         _lastAddedMod       = string.Empty;
    private DateTimeOffset _lastMovedModTime   = DateTimeOffset.UnixEpoch;
    private string         _lastMovedModFrom   = string.Empty;
    private string         _lastMovedModTo     = string.Empty;

    public ModsIpcTester(IDalamudPluginInterface pi)
    {
        _pi = pi;
        DeleteSubscriber = ModDeleted.Subscriber(pi, s =>
        {
            _lastDeletedModTime = DateTimeOffset.UtcNow;
            _lastDeletedMod     = s;
        });
        AddSubscriber = ModAdded.Subscriber(pi, s =>
        {
            _lastAddedModTime = DateTimeOffset.UtcNow;
            _lastAddedMod     = s;
        });
        MoveSubscriber = ModMoved.Subscriber(pi, (s1, s2) =>
        {
            _lastMovedModTime = DateTimeOffset.UtcNow;
            _lastMovedModFrom = s1;
            _lastMovedModTo   = s2;
        });
        DeleteSubscriber.Disable();
        AddSubscriber.Disable();
        MoveSubscriber.Disable();
    }

    public void Dispose()
    {
        DeleteSubscriber.Dispose();
        DeleteSubscriber.Disable();
        AddSubscriber.Dispose();
        AddSubscriber.Disable();
        MoveSubscriber.Dispose();
        MoveSubscriber.Disable();
    }

    public void Draw()
    {
        using var _ = Im.Tree.Node("Mods"u8);
        if (!_)
            return;

        Im.Input.Text("##install"u8, ref _newInstallPath, "Install File Path..."u8);
        Im.Input.Text("##modDir"u8, ref _modDirectory, "Mod Directory Name..."u8);
        Im.Input.Text("##modName"u8, ref _modName, "Mod Name..."u8);
        Im.Input.Text("##path"u8, ref _pathInput, "New Path..."u8);
        using var table = Im.Table.Begin(StringU8.Empty, 3, TableFlags.SizingFixedFit);
        if (!table)
            return;

        IpcTester.DrawIntro(GetModList.Label, "Mods"u8);
        DrawModsPopup();
        if (Im.Button("Get##Mods"u8))
        {
            _mods = new GetModList(_pi).Invoke();
            Im.Popup.Open("Mods"u8);
        }

        IpcTester.DrawIntro(ReloadMod.Label, "Reload Mod"u8);
        if (Im.Button("Reload"u8))
            _lastReloadEc = new ReloadMod(_pi).Invoke(_modDirectory, _modName);

        Im.Line.Same();
        Im.Text($"{_lastReloadEc}");

        IpcTester.DrawIntro(InstallMod.Label, "Install Mod"u8);
        if (Im.Button("Install"u8))
            _lastInstallEc = new InstallMod(_pi).Invoke(_newInstallPath);

        Im.Line.Same();
        Im.Text($"{_lastInstallEc}");

        IpcTester.DrawIntro(AddMod.Label, "Add Mod"u8);
        if (Im.Button("Add"u8))
            _lastAddEc = new AddMod(_pi).Invoke(_modDirectory);

        Im.Line.Same();
        Im.Text($"{_lastAddEc}");

        IpcTester.DrawIntro(DeleteMod.Label, "Delete Mod"u8);
        if (Im.Button("Delete"u8))
            _lastDeleteEc = new DeleteMod(_pi).Invoke(_modDirectory, _modName);

        Im.Line.Same();
        Im.Text(_lastDeleteEc.ToString());

        IpcTester.DrawIntro(GetChangedItems.Label, "Get Changed Items"u8);
        DrawChangedItemsPopup();
        if (Im.Button("Get##ChangedItems"u8))
        {
            _changedItems = new GetChangedItems(_pi).Invoke(_modDirectory, _modName);
            Im.Popup.Open("ChangedItems"u8);
        }

        IpcTester.DrawIntro(GetModPath.Label, "Current Path"u8);
        var (ec, path, def, nameDef) = new GetModPath(_pi).Invoke(_modDirectory, _modName);
        Im.Text($"{path} ({(def ? "Custom" : "Default")} Path, {(nameDef ? "Custom" : "Default")} Name) [{ec}]");

        IpcTester.DrawIntro(SetModPath.Label, "Set Path"u8);
        if (Im.Button("Set"u8))
            _lastSetPathEc = new SetModPath(_pi).Invoke(_modDirectory, _pathInput, _modName);

        Im.Line.Same();
        Im.Text($"{_lastSetPathEc}");

        IpcTester.DrawIntro(ModDeleted.Label, "Last Mod Deleted"u8);
        if (_lastDeletedModTime > DateTimeOffset.UnixEpoch)
            Im.Text($"{_lastDeletedMod} at {_lastDeletedModTime}");

        IpcTester.DrawIntro(ModAdded.Label, "Last Mod Added"u8);
        if (_lastAddedModTime > DateTimeOffset.UnixEpoch)
            Im.Text($"{_lastAddedMod} at {_lastAddedModTime}");

        IpcTester.DrawIntro(ModMoved.Label, "Last Mod Moved");
        if (_lastMovedModTime > DateTimeOffset.UnixEpoch)
            Im.Text($"{_lastMovedModFrom} -> {_lastMovedModTo} at {_lastMovedModTime}");
    }

    private void DrawModsPopup()
    {
        Im.Window.SetNextSize(ImEx.ScaledVector(500, 500));
        using var p = Im.Popup.Begin("Mods"u8);
        if (!p)
            return;

        foreach (var (modDir, modName) in _mods)
            Im.Text($"{modDir}: {modName}");

        if (Im.Button("Close"u8, -Vector2.UnitX) || !Im.Window.Focused())
            Im.Popup.CloseCurrent();
    }

    private void DrawChangedItemsPopup()
    {
        Im.Window.SetNextSize(ImEx.ScaledVector(500, 500));
        using var p = Im.Popup.Begin("ChangedItems"u8);
        if (!p)
            return;

        foreach (var (name, data) in _changedItems)
            Im.Text($"{name}: {data}");

        if (Im.Button("Close"u8, -Vector2.UnitX) || !Im.Window.Focused())
            Im.Popup.CloseCurrent();
    }
}

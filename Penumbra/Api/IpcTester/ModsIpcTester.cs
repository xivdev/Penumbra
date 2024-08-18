using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;

namespace Penumbra.Api.IpcTester;

public class ModsIpcTester : IUiService, IDisposable
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
        using var _ = ImRaii.TreeNode("Mods");
        if (!_)
            return;

        ImGui.InputTextWithHint("##install", "Install File Path...",  ref _newInstallPath, 100);
        ImGui.InputTextWithHint("##modDir",  "Mod Directory Name...", ref _modDirectory,   100);
        ImGui.InputTextWithHint("##modName", "Mod Name...",           ref _modName,        100);
        ImGui.InputTextWithHint("##path",    "New Path...",           ref _pathInput,      100);
        using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        IpcTester.DrawIntro(GetModList.Label, "Mods");
        DrawModsPopup();
        if (ImGui.Button("Get##Mods"))
        {
            _mods = new GetModList(_pi).Invoke();
            ImGui.OpenPopup("Mods");
        }

        IpcTester.DrawIntro(ReloadMod.Label, "Reload Mod");
        if (ImGui.Button("Reload"))
            _lastReloadEc = new ReloadMod(_pi).Invoke(_modDirectory, _modName);

        ImGui.SameLine();
        ImGui.TextUnformatted(_lastReloadEc.ToString());

        IpcTester.DrawIntro(InstallMod.Label, "Install Mod");
        if (ImGui.Button("Install"))
            _lastInstallEc = new InstallMod(_pi).Invoke(_newInstallPath);

        ImGui.SameLine();
        ImGui.TextUnformatted(_lastInstallEc.ToString());

        IpcTester.DrawIntro(AddMod.Label, "Add Mod");
        if (ImGui.Button("Add"))
            _lastAddEc = new AddMod(_pi).Invoke(_modDirectory);

        ImGui.SameLine();
        ImGui.TextUnformatted(_lastAddEc.ToString());

        IpcTester.DrawIntro(DeleteMod.Label, "Delete Mod");
        if (ImGui.Button("Delete"))
            _lastDeleteEc = new DeleteMod(_pi).Invoke(_modDirectory, _modName);

        ImGui.SameLine();
        ImGui.TextUnformatted(_lastDeleteEc.ToString());

        IpcTester.DrawIntro(GetChangedItems.Label, "Get Changed Items");
        DrawChangedItemsPopup();
        if (ImUtf8.Button("Get##ChangedItems"u8))
        {
            _changedItems = new GetChangedItems(_pi).Invoke(_modDirectory, _modName);
            ImUtf8.OpenPopup("ChangedItems"u8);
        }

        IpcTester.DrawIntro(GetModPath.Label, "Current Path");
        var (ec, path, def, nameDef) = new GetModPath(_pi).Invoke(_modDirectory, _modName);
        ImGui.TextUnformatted($"{path} ({(def ? "Custom" : "Default")} Path, {(nameDef ? "Custom" : "Default")} Name) [{ec}]");

        IpcTester.DrawIntro(SetModPath.Label, "Set Path");
        if (ImGui.Button("Set"))
            _lastSetPathEc = new SetModPath(_pi).Invoke(_modDirectory, _pathInput, _modName);

        ImGui.SameLine();
        ImGui.TextUnformatted(_lastSetPathEc.ToString());

        IpcTester.DrawIntro(ModDeleted.Label, "Last Mod Deleted");
        if (_lastDeletedModTime > DateTimeOffset.UnixEpoch)
            ImGui.TextUnformatted($"{_lastDeletedMod} at {_lastDeletedModTime}");

        IpcTester.DrawIntro(ModAdded.Label, "Last Mod Added");
        if (_lastAddedModTime > DateTimeOffset.UnixEpoch)
            ImGui.TextUnformatted($"{_lastAddedMod} at {_lastAddedModTime}");

        IpcTester.DrawIntro(ModMoved.Label, "Last Mod Moved");
        if (_lastMovedModTime > DateTimeOffset.UnixEpoch)
            ImGui.TextUnformatted($"{_lastMovedModFrom} -> {_lastMovedModTo} at {_lastMovedModTime}");
    }

    private void DrawModsPopup()
    {
        ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(500, 500));
        using var p = ImRaii.Popup("Mods");
        if (!p)
            return;

        foreach (var (modDir, modName) in _mods)
            ImGui.TextUnformatted($"{modDir}: {modName}");

        if (ImGui.Button("Close", -Vector2.UnitX) || !ImGui.IsWindowFocused())
            ImGui.CloseCurrentPopup();
    }

    private void DrawChangedItemsPopup()
    {
        ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(500, 500));
        using var p = ImUtf8.Popup("ChangedItems"u8);
        if (!p)
            return;

        foreach (var (name, data) in _changedItems)
            ImUtf8.Text($"{name}: {data}");

        if (ImUtf8.Button("Close"u8, -Vector2.UnitX) || !ImGui.IsWindowFocused())
            ImGui.CloseCurrentPopup();
    }
}

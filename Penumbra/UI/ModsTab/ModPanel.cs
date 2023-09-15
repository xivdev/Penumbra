using Dalamud.Interface;
using Dalamud.Plugin;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Mods;
using Penumbra.UI.AdvancedWindow;

namespace Penumbra.UI.ModsTab;

public class ModPanel : IDisposable
{
    private readonly ModFileSystemSelector _selector;
    private readonly ModEditWindow         _editWindow;
    private readonly ModPanelHeader        _header;
    private readonly ModPanelTabBar        _tabs;

    public ModPanel(DalamudPluginInterface pi, ModFileSystemSelector selector, ModEditWindow editWindow, ModPanelTabBar tabs)
    {
        _selector                  =  selector;
        _editWindow                =  editWindow;
        _tabs                      =  tabs;
        _header                    =  new ModPanelHeader(pi);
        _selector.SelectionChanged += OnSelectionChange;
    }

    public void Draw()
    {
        if (!_valid)
        {
            DrawMultiSelection();
            return;
        }

        _header.Draw();
        _tabs.Draw(_mod);
    }

    public void Dispose()
    {
        _selector.SelectionChanged -= OnSelectionChange;
        _header.Dispose();
    }

    private void DrawMultiSelection()
    {
        if (_selector.SelectedPaths.Count == 0)
            return;

        var sizeType             = ImGui.GetFrameHeight();
        var availableSizePercent = (ImGui.GetContentRegionAvail().X - sizeType - 4 * ImGui.GetStyle().CellPadding.X) / 100;
        var sizeMods             = availableSizePercent * 35;
        var sizeFolders          = availableSizePercent * 65;

        ImGui.NewLine();
        ImGui.TextUnformatted("Currently Selected Objects");
        ImGui.Separator();
        using var table = ImRaii.Table("mods", 3, ImGuiTableFlags.RowBg);
        ImGui.TableSetupColumn("type", ImGuiTableColumnFlags.WidthFixed, sizeType);
        ImGui.TableSetupColumn("mod",  ImGuiTableColumnFlags.WidthFixed, sizeMods);
        ImGui.TableSetupColumn("path", ImGuiTableColumnFlags.WidthFixed, sizeFolders);

        var i = 0;
        foreach (var (fullName, path) in _selector.SelectedPaths.Select(p => (p.FullName(), p))
                     .OrderBy(p => p.Item1, StringComparer.OrdinalIgnoreCase))
        {
            using var id = ImRaii.PushId(i++);
            ImGui.TableNextColumn();
            var icon = (path is ModFileSystem.Leaf ? FontAwesomeIcon.FileCircleMinus : FontAwesomeIcon.FolderMinus).ToIconString();
            if (ImGuiUtil.DrawDisabledButton(icon, new Vector2(sizeType), "Remove from selection.", false, true))
                _selector.RemovePathFromMultiselection(path);

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(path is ModFileSystem.Leaf l ? l.Value.Name : string.Empty);

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(fullName);
        }
    }


    private bool _valid;
    private Mod  _mod = null!;

    private void OnSelectionChange(Mod? old, Mod? mod, in ModFileSystemSelector.ModState _)
    {
        if (mod == null || _selector.Selected == null)
        {
            _editWindow.IsOpen = false;
            _valid             = false;
        }
        else
        {
            if (_editWindow.IsOpen)
                _editWindow.ChangeMod(mod);
            _valid = true;
            _mod   = mod;
            _header.UpdateModData(_mod);
            _tabs.Settings.Reset();
            _tabs.Edit.Reset();
        }
    }
}

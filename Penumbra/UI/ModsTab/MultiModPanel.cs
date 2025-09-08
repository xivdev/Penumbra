using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using OtterGui.Extensions;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.Mods;
using Penumbra.Mods.Manager;

namespace Penumbra.UI.ModsTab;

public class MultiModPanel(ModFileSystemSelector selector, ModDataEditor editor, PredefinedTagManager tagManager) : Luna.IUiService
{
    public void Draw()
    {
        if (selector.SelectedPaths.Count == 0)
            return;

        ImGui.NewLine();
        var treeNodePos = ImGui.GetCursorPos();
        var numLeaves   = DrawModList();
        DrawCounts(treeNodePos, numLeaves);
        DrawMultiTagger();
    }

    private void DrawCounts(Vector2 treeNodePos, int numLeaves)
    {
        var startPos   = ImGui.GetCursorPos();
        var numFolders = selector.SelectedPaths.Count - numLeaves;
        var text = (numLeaves, numFolders) switch
        {
            (0, 0)   => string.Empty, // should not happen
            (> 0, 0) => $"{numLeaves} Mods",
            (0, > 0) => $"{numFolders} Folders",
            _        => $"{numLeaves} Mods, {numFolders} Folders",
        };
        ImGui.SetCursorPos(treeNodePos);
        ImUtf8.TextRightAligned(text);
        ImGui.SetCursorPos(startPos);
    }

    private int DrawModList()
    {
        using var tree = ImUtf8.TreeNode("Currently Selected Objects###Selected"u8,
            ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.NoTreePushOnOpen);
        ImGui.Separator();


        if (!tree)
            return selector.SelectedPaths.Count(l => l is ModFileSystem.Leaf);

        var sizeType             = new Vector2(ImGui.GetFrameHeight());
        var availableSizePercent = (ImGui.GetContentRegionAvail().X - sizeType.X - 4 * ImGui.GetStyle().CellPadding.X) / 100;
        var sizeMods             = availableSizePercent * 35;
        var sizeFolders          = availableSizePercent * 65;

        var leaves = 0;
        using (var table = ImUtf8.Table("mods"u8, 3, ImGuiTableFlags.RowBg))
        {
            if (!table)
                return selector.SelectedPaths.Count(l => l is ModFileSystem.Leaf);

            ImUtf8.TableSetupColumn("type"u8, ImGuiTableColumnFlags.WidthFixed, sizeType.X);
            ImUtf8.TableSetupColumn("mod"u8,  ImGuiTableColumnFlags.WidthFixed, sizeMods);
            ImUtf8.TableSetupColumn("path"u8, ImGuiTableColumnFlags.WidthFixed, sizeFolders);

            var i = 0;
            foreach (var (fullName, path) in selector.SelectedPaths.Select(p => (p.FullName(), p))
                         .OrderBy(p => p.Item1, StringComparer.OrdinalIgnoreCase))
            {
                using var id = ImRaii.PushId(i++);
                var (icon, text) = path is ModFileSystem.Leaf l
                    ? (FontAwesomeIcon.FileCircleMinus, l.Value.Name.Text)
                    : (FontAwesomeIcon.FolderMinus, string.Empty);
                ImGui.TableNextColumn();
                if (ImUtf8.IconButton(icon, "Remove from selection."u8, sizeType))
                    selector.RemovePathFromMultiSelection(path);

                ImUtf8.DrawFrameColumn(text);
                ImUtf8.DrawFrameColumn(fullName);
                if (path is ModFileSystem.Leaf)
                    ++leaves;
            }
        }

        ImGui.Separator();
        return leaves;
    }

    private          string           _tag        = string.Empty;
    private readonly List<Mod>        _addMods    = [];
    private readonly List<(Mod, int)> _removeMods = [];

    private void DrawMultiTagger()
    {
        var width = ImGuiHelpers.ScaledVector2(150, 0);
        ImUtf8.TextFrameAligned("Multi Tagger:"u8);
        ImGui.SameLine();

        var predefinedTagsEnabled = tagManager.Enabled;
        var inputWidth = predefinedTagsEnabled
            ? ImGui.GetContentRegionAvail().X - 2 * width.X - 3 * ImGui.GetStyle().ItemInnerSpacing.X - ImGui.GetFrameHeight()
            : ImGui.GetContentRegionAvail().X - 2 * (width.X + ImGui.GetStyle().ItemInnerSpacing.X);
        ImGui.SetNextItemWidth(inputWidth);
        ImUtf8.InputText("##tag"u8, ref _tag, "Local Tag Name..."u8);

        UpdateTagCache();
        var label = _addMods.Count > 0
            ? $"Add to {_addMods.Count} Mods"
            : "Add";
        var tooltip = _addMods.Count == 0
            ? _tag.Length == 0
                ? "No tag specified."
                : $"All mods selected already contain the tag \"{_tag}\", either locally or as mod data."
            : $"Add the tag \"{_tag}\" to {_addMods.Count} mods as a local tag:\n\n\t{string.Join("\n\t", _addMods.Select(m => m.Name.Text))}";
        ImUtf8.SameLineInner();
        if (ImUtf8.ButtonEx(label, tooltip, width, _addMods.Count == 0))
            foreach (var mod in _addMods)
                editor.ChangeLocalTag(mod, mod.LocalTags.Count, _tag);

        label = _removeMods.Count > 0
            ? $"Remove from {_removeMods.Count} Mods"
            : "Remove";
        tooltip = _removeMods.Count == 0
            ? _tag.Length == 0
                ? "No tag specified."
                : $"No selected mod contains the tag \"{_tag}\" locally."
            : $"Remove the local tag \"{_tag}\" from {_removeMods.Count} mods:\n\n\t{string.Join("\n\t", _removeMods.Select(m => m.Item1.Name.Text))}";
        ImUtf8.SameLineInner();
        if (ImUtf8.ButtonEx(label, tooltip, width, _removeMods.Count == 0))
            foreach (var (mod, index) in _removeMods)
                editor.ChangeLocalTag(mod, index, string.Empty);
        
        if (predefinedTagsEnabled)
        {
            ImUtf8.SameLineInner();
            tagManager.DrawToggleButton();
            tagManager.DrawListMulti(selector.SelectedPaths.OfType<ModFileSystem.Leaf>().Select(l => l.Value));
        }

        ImGui.Separator();
    }

    private void UpdateTagCache()
    {
        _addMods.Clear();
        _removeMods.Clear();
        if (_tag.Length == 0)
            return;

        foreach (var leaf in selector.SelectedPaths.OfType<ModFileSystem.Leaf>())
        {
            var index = leaf.Value.LocalTags.IndexOf(_tag);
            if (index >= 0)
                _removeMods.Add((leaf.Value, index));
            else if (!leaf.Value.ModTags.Contains(_tag))
                _addMods.Add(leaf.Value);
        }
    }
}

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using ImSharp;
using Luna;
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

        Im.Line.New();
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
        Im.Separator();


        if (!tree)
            return selector.SelectedPaths.Count(l => l is ModFileSystem.Leaf);

        var sizeType             = new Vector2(Im.Style.FrameHeight);
        var availableSizePercent = (Im.ContentRegion.Available.X - sizeType.X - 4 * Im.Style.CellPadding.X) / 100;
        var sizeMods             = availableSizePercent * 35;
        var sizeFolders          = availableSizePercent * 65;

        var leaves = 0;
        using (var table = Im.Table.Begin("mods"u8, 3, TableFlags.RowBackground))
        {
            if (!table)
                return selector.SelectedPaths.Count(l => l is ModFileSystem.Leaf);

            table.SetupColumn("type"u8, TableColumnFlags.WidthFixed, sizeType.X);
            table.SetupColumn("mod"u8,  TableColumnFlags.WidthFixed, sizeMods);
            table.SetupColumn("path"u8, TableColumnFlags.WidthFixed, sizeFolders);

            var i = 0;
            foreach (var (fullName, path) in selector.SelectedPaths.Select(p => (p.FullName(), p))
                         .OrderBy(p => p.Item1, StringComparer.OrdinalIgnoreCase))
            {
                using var id = ImRaii.PushId(i++);
                var (icon, text) = path is ModFileSystem.Leaf l
                    ? (FontAwesomeIcon.FileCircleMinus, l.Value.Name)
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

        Im.Separator();
        return leaves;
    }

    private          string           _tag        = string.Empty;
    private readonly List<Mod>        _addMods    = [];
    private readonly List<(Mod, int)> _removeMods = [];

    private void DrawMultiTagger()
    {
        var width = ImEx.ScaledVector(150, 0);
        ImUtf8.TextFrameAligned("Multi Tagger:"u8);
        Im.Line.Same();

        var predefinedTagsEnabled = tagManager.Enabled;
        var inputWidth = predefinedTagsEnabled
            ? Im.ContentRegion.Available.X - 2 * width.X - 3 * Im.Style.ItemInnerSpacing.X - Im.Style.FrameHeight
            : Im.ContentRegion.Available.X - 2 * (width.X + Im.Style.ItemInnerSpacing.X);
        Im.Item.SetNextWidth(inputWidth);
        ImUtf8.InputText("##tag"u8, ref _tag, "Local Tag Name..."u8);

        UpdateTagCache();
        var label = _addMods.Count > 0
            ? $"Add to {_addMods.Count} Mods"
            : "Add";
        var tooltip = _addMods.Count == 0
            ? _tag.Length == 0
                ? "No tag specified."
                : $"All mods selected already contain the tag \"{_tag}\", either locally or as mod data."
            : $"Add the tag \"{_tag}\" to {_addMods.Count} mods as a local tag:\n\n\t{string.Join("\n\t", _addMods.Select(m => m.Name))}";
        Im.Line.SameInner();
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
            : $"Remove the local tag \"{_tag}\" from {_removeMods.Count} mods:\n\n\t{string.Join("\n\t", _removeMods.Select(m => m.Item1.Name))}";
        Im.Line.SameInner();
        if (ImUtf8.ButtonEx(label, tooltip, width, _removeMods.Count == 0))
            foreach (var (mod, index) in _removeMods)
                editor.ChangeLocalTag(mod, index, string.Empty);
        
        if (predefinedTagsEnabled)
        {
            Im.Line.SameInner();
            tagManager.DrawToggleButton();
            tagManager.DrawListMulti(selector.SelectedPaths.OfType<ModFileSystem.Leaf>().Select(l => l.Value));
        }

        Im.Separator();
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

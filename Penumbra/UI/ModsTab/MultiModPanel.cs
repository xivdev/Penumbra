using Dalamud.Interface;
using ImSharp;
using Luna;
using Penumbra.Mods;
using Penumbra.Mods.Manager;

namespace Penumbra.UI.ModsTab;

public class MultiModPanel(ModFileSystem fileSystem, ModDataEditor editor, PredefinedTagManager tagManager) : IUiService
{
    public void Draw()
    {
        if (fileSystem.Selection.OrderedNodes.Count is 0)
            return;

        Im.Line.New();
        var treeNodePos = Im.Cursor.Position;
        DrawModList();
        DrawCounts(treeNodePos);
        DrawMultiTagger();
    }

    private void DrawCounts(Vector2 treeNodePos)
    {
        var startPos   = Im.Cursor.Position;
        var numLeaves  = fileSystem.Selection.DataNodes.Count;
        var numFolders = fileSystem.Selection.Folders.Count;
        Utf8StringHandler<TextStringHandlerBuffer> text = (numLeaves, numFolders) switch
        {
            (0, 0)   => StringU8.Empty, // should not happen
            (> 0, 0) => $"{numLeaves} Mods",
            (0, > 0) => $"{numFolders} Folders",
            _        => $"{numLeaves} Mods, {numFolders} Folders",
        };
        Im.Cursor.Position = treeNodePos;
        ImEx.TextRightAligned(ref text);
        Im.Cursor.Position = startPos;
    }

    private void DrawModList()
    {
        using var tree = Im.Tree.Node("Currently Selected Objects###Selected"u8,
            TreeNodeFlags.DefaultOpen | TreeNodeFlags.NoTreePushOnOpen);
        Im.Separator();

        if (!tree)
            return;

        var sizeType             = new Vector2(Im.Style.FrameHeight);
        var availableSizePercent = (Im.ContentRegion.Available.X - sizeType.X - 4 * Im.Style.CellPadding.X) / 100;
        var sizeMods             = availableSizePercent * 35;
        var sizeFolders          = availableSizePercent * 65;

        using (var table = Im.Table.Begin("mods"u8, 3, TableFlags.RowBackground))
        {
            if (!table)
                return;

            table.SetupColumn("type"u8, TableColumnFlags.WidthFixed, sizeType.X);
            table.SetupColumn("mod"u8,  TableColumnFlags.WidthFixed, sizeMods);
            table.SetupColumn("path"u8, TableColumnFlags.WidthFixed, sizeFolders);

            var i = 0;
            foreach (var node in fileSystem.Selection.OrderedNodes.OrderBy(p => p.FullPath, StringComparer.OrdinalIgnoreCase))
            {
                using var id = Im.Id.Push(i++);
                var (icon, text) = node is IFileSystemData<Mod> l
                    ? (LunaStyle.RemoveFileIcon, l.Value.Name)
                    : (LunaStyle.RemoveFolderIcon, string.Empty);
                table.NextColumn();
                if (ImEx.Icon.Button(icon, "Remove from selection."u8, false, sizeType))
                    fileSystem.Selection.RemoveFromSelection(node);

                table.DrawFrameColumn(text);
                table.DrawFrameColumn(node.FullPath);
            }
        }

        Im.Separator();
    }

    private          string           _tag        = string.Empty;
    private readonly List<Mod>        _addMods    = [];
    private readonly List<(Mod, int)> _removeMods = [];

    private void DrawMultiTagger()
    {
        var width = ImEx.ScaledVector(150, 0);
        ImEx.TextFrameAligned("Multi Tagger:"u8);
        Im.Line.Same();

        var predefinedTagsEnabled = tagManager.Enabled;
        var inputWidth = predefinedTagsEnabled
            ? Im.ContentRegion.Available.X - 2 * width.X - 3 * Im.Style.ItemInnerSpacing.X - Im.Style.FrameHeight
            : Im.ContentRegion.Available.X - 2 * (width.X + Im.Style.ItemInnerSpacing.X);
        Im.Item.SetNextWidth(inputWidth);
        Im.Input.Text("##tag"u8, ref _tag, "Local Tag Name..."u8);

        UpdateTagCache();
        Utf8StringHandler<LabelStringHandlerBuffer> label = _addMods.Count > 0
            ? $"Add to {_addMods.Count} Mods"
            : "Add";
        Utf8StringHandler<TextStringHandlerBuffer> tooltip = _addMods.Count is 0
            ? _tag.Length is 0
                ? "No tag specified."
                : $"All mods selected already contain the tag \"{_tag}\", either locally or as mod data."
            : $"Add the tag \"{_tag}\" to {_addMods.Count} mods as a local tag:\n\n\t{string.Join("\n\t", _addMods.Select(m => m.Name))}";
        Im.Line.SameInner();
        if (ImEx.Button(label, width, tooltip, _addMods.Count is 0))
            foreach (var mod in _addMods)
                editor.ChangeLocalTag(mod, mod.LocalTags.Count, _tag);

        label = _removeMods.Count > 0
            ? $"Remove from {_removeMods.Count} Mods"
            : "Remove";
        tooltip = _removeMods.Count is 0
            ? _tag.Length is 0
                ? "No tag specified."
                : $"No selected mod contains the tag \"{_tag}\" locally."
            : $"Remove the local tag \"{_tag}\" from {_removeMods.Count} mods:\n\n\t{string.Join("\n\t", _removeMods.Select(m => m.Item1.Name))}";
        Im.Line.SameInner();
        if (ImEx.Button(label, width, tooltip, _removeMods.Count is 0))
            foreach (var (mod, index) in _removeMods)
                editor.ChangeLocalTag(mod, index, string.Empty);

        if (predefinedTagsEnabled)
        {
            Im.Line.SameInner();
            tagManager.DrawToggleButton();
            tagManager.DrawListMulti(fileSystem.Selection.DataNodes.Select(l => (Mod)l.Value));
        }

        Im.Separator();
    }

    private void UpdateTagCache()
    {
        _addMods.Clear();
        _removeMods.Clear();
        if (_tag.Length is 0)
            return;

        foreach (var leaf in fileSystem.Selection.DataNodes)
        {
            var mod   = (Mod)leaf.Value;
            var index = mod.LocalTags.IndexOf(_tag);
            if (index >= 0)
                _removeMods.Add((mod, index));
            else if (!mod.ModTags.Contains(_tag))
                _addMods.Add(mod);
        }
    }
}

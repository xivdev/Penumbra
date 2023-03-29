using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using Penumbra.Interop.ResourceTree;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public class ResourceTreeViewer
{
    private readonly Configuration                 _config;
    private readonly ResourceTreeFactory           _treeFactory;
    private readonly int                           _actionCapacity;
    private readonly Action                        _onRefresh;
    private readonly Action<ResourceNode, Vector2> _drawActions;
    private readonly HashSet<ResourceNode>         _unfolded;

    private Task<ResourceTree[]>?      _task;

    public ResourceTreeViewer(Configuration config, ResourceTreeFactory treeFactory, int actionCapacity, Action onRefresh,
        Action<ResourceNode, Vector2> drawActions)
    {
        _config         = config;
        _treeFactory    = treeFactory;
        _actionCapacity = actionCapacity;
        _onRefresh      = onRefresh;
        _drawActions    = drawActions;
        _unfolded       = new HashSet<ResourceNode>();
    }

    public void Draw()
    {
        if (ImGui.Button("Refresh Character List") || _task == null)
            _task = RefreshCharacterList();

        using var child = ImRaii.Child("##Data");
        if (!child)
            return;

        var textColorNonPlayer = ImGui.GetColorU32(ImGuiCol.Text);
        var textColorPlayer    = (textColorNonPlayer & 0xFF000000u) | ((textColorNonPlayer & 0x00FEFEFE) >> 1) | 0x8000u; // Half green
        if (!_task.IsCompleted)
        {
            ImGui.NewLine();
            ImGui.TextUnformatted("Calculating character list...");
        }
        else if (_task.Exception != null)
        {
            ImGui.NewLine();
            using var color = ImRaii.PushColor(ImGuiCol.Text, Colors.RegexWarningBorder);
            ImGui.TextUnformatted($"Error during calculation of character list:\n\n{_task.Exception}");
        }
        else if (_task.IsCompletedSuccessfully)
        {
            foreach (var (tree, index) in _task.Result.WithIndex())
            {
                using (var c = ImRaii.PushColor(ImGuiCol.Text, tree.PlayerRelated ? textColorPlayer : textColorNonPlayer))
                {
                    if (!ImGui.CollapsingHeader($"{tree.Name}##{index}", index == 0 ? ImGuiTreeNodeFlags.DefaultOpen : 0))
                        continue;
                }

                using var id = ImRaii.PushId(index);

                ImGui.Text($"Collection: {tree.CollectionName}");

                using var table = ImRaii.Table("##ResourceTree", _actionCapacity > 0 ? 4 : 3,
                    ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg);
                if (!table)
                    continue;

                ImGui.TableSetupColumn(string.Empty,  ImGuiTableColumnFlags.WidthStretch, 0.2f);
                ImGui.TableSetupColumn("Game Path",   ImGuiTableColumnFlags.WidthStretch, 0.3f);
                ImGui.TableSetupColumn("Actual Path", ImGuiTableColumnFlags.WidthStretch, 0.5f);
                if (_actionCapacity > 0)
                    ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed,
                        (_actionCapacity - 1) * 3 * ImGuiHelpers.GlobalScale + _actionCapacity * ImGui.GetFrameHeight());
                ImGui.TableHeadersRow();

                DrawNodes(tree.Nodes, 0);
            }
        }
    }

    private Task<ResourceTree[]> RefreshCharacterList()
        => Task.Run(() =>
        {
            try
            {
                return _treeFactory.FromObjectTable();
            }
            finally
            {
                _unfolded.Clear();
                _onRefresh();
            }
        });

    private void DrawNodes(IEnumerable<ResourceNode> resourceNodes, int level)
    {
        var debugMode   = _config.DebugMode;
        var frameHeight = ImGui.GetFrameHeight();
        var cellHeight  = _actionCapacity > 0 ? frameHeight : 0.0f;
        foreach (var (resourceNode, index) in resourceNodes.WithIndex())
        {
            if (resourceNode.Internal && !debugMode)
                continue;

            using var id = ImRaii.PushId(index);
            ImGui.TableNextColumn();
            var unfolded = _unfolded.Contains(resourceNode);
            using (var indent = ImRaii.PushIndent(level))
            {
                ImGui.TableHeader((resourceNode.Children.Count > 0 ? unfolded ? "[-] " : "[+] " : string.Empty) + resourceNode.Name);
                if (ImGui.IsItemClicked() && resourceNode.Children.Count > 0)
                {
                    if (unfolded)
                        _unfolded.Remove(resourceNode);
                    else
                        _unfolded.Add(resourceNode);
                    unfolded = !unfolded;
                }

                if (debugMode)
                    ImGuiUtil.HoverTooltip(
                        $"Resource Type: {resourceNode.Type}\nSource Address: 0x{resourceNode.SourceAddress:X16}");
            }

            ImGui.TableNextColumn();
            var hasGamePaths = resourceNode.PossibleGamePaths.Length > 0;
            ImGui.Selectable(resourceNode.PossibleGamePaths.Length switch
            {
                0 => "(none)",
                1 => resourceNode.GamePath.ToString(),
                _ => "(multiple)",
            }, false, hasGamePaths ? 0 : ImGuiSelectableFlags.Disabled, new Vector2(ImGui.GetContentRegionAvail().X, cellHeight));
            if (hasGamePaths)
            {
                var allPaths = string.Join('\n', resourceNode.PossibleGamePaths);
                if (ImGui.IsItemClicked())
                    ImGui.SetClipboardText(allPaths);
                ImGuiUtil.HoverTooltip($"{allPaths}\n\nClick to copy to clipboard.");
            }

            ImGui.TableNextColumn();
            if (resourceNode.FullPath.FullName.Length > 0)
            {
                ImGui.Selectable(resourceNode.FullPath.ToString(), false, 0, new Vector2(ImGui.GetContentRegionAvail().X, cellHeight));
                if (ImGui.IsItemClicked())
                    ImGui.SetClipboardText(resourceNode.FullPath.ToString());
                ImGuiUtil.HoverTooltip($"{resourceNode.FullPath}\n\nClick to copy to clipboard.");
            }
            else
            {
                ImGui.Selectable("(unavailable)", false, ImGuiSelectableFlags.Disabled,
                    new Vector2(ImGui.GetContentRegionAvail().X, cellHeight));
                ImGuiUtil.HoverTooltip("The actual path to this file is unavailable.\nIt may be managed by another plug-in.");
            }

            if (_actionCapacity > 0)
            {
                ImGui.TableNextColumn();
                using var spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing,
                    ImGui.GetStyle().ItemSpacing with { X = 3 * ImGuiHelpers.GlobalScale });
                _drawActions(resourceNode, new Vector2(frameHeight));
            }

            if (unfolded)
                DrawNodes(resourceNode.Children, level + 1);
        }
    }
}

using Dalamud.Interface;
using Dalamud.Interface.Utility;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using OtterGui.Text;
using Penumbra.Api.Enums;
using Penumbra.Interop.ResourceTree;
using Penumbra.Services;
using Penumbra.UI.Classes;
using Penumbra.String;

namespace Penumbra.UI.AdvancedWindow;

public class ResourceTreeViewer(
    Configuration config,
    ResourceTreeFactory treeFactory,
    ChangedItemDrawer changedItemDrawer,
    IncognitoService incognito,
    int actionCapacity,
    Action onRefresh,
    Action<ResourceNode, Vector2> drawActions,
    CommunicatorService communicator)
{
    private const ResourceTreeFactory.Flags ResourceTreeFactoryFlags =
        ResourceTreeFactory.Flags.RedactExternalPaths | ResourceTreeFactory.Flags.WithUiData | ResourceTreeFactory.Flags.WithOwnership;

    private readonly HashSet<nint>       _unfolded     = [];

    private readonly Dictionary<nint, NodeVisibility> _filterCache = [];

    private TreeCategory        _categoryFilter = AllCategories;
    private ChangedItemIconFlag _typeFilter     = ChangedItemFlagExtensions.AllFlags;
    private string              _nameFilter     = string.Empty;
    private string              _nodeFilter     = string.Empty;

    private Task<ResourceTree[]>? _task;

    public void Draw()
    {
        DrawControls();
        _task ??= RefreshCharacterList();

        using var child = ImRaii.Child("##Data");
        if (!child)
            return;

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
            var debugMode = config.DebugMode;
            foreach (var (tree, index) in _task.Result.WithIndex())
            {
                var category = Classify(tree);
                if (!_categoryFilter.HasFlag(category) || !tree.Name.Contains(_nameFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                using (var c = ImRaii.PushColor(ImGuiCol.Text, CategoryColor(category).Value()))
                {
                    var isOpen = ImGui.CollapsingHeader($"{(incognito.IncognitoMode ? tree.AnonymizedName : tree.Name)}###{index}",
                        index == 0 ? ImGuiTreeNodeFlags.DefaultOpen : 0);
                    if (debugMode)
                    {
                        using var _ = ImRaii.PushFont(UiBuilder.MonoFont);
                        ImGuiUtil.HoverTooltip(
                            $"Object Index:        {tree.GameObjectIndex}\nObject Address:      0x{tree.GameObjectAddress:X16}\nDraw Object Address: 0x{tree.DrawObjectAddress:X16}");
                    }

                    if (!isOpen)
                        continue;
                }

                using var id = ImRaii.PushId(index);

                ImGui.TextUnformatted($"Collection: {(incognito.IncognitoMode ? tree.AnonymizedCollectionName : tree.CollectionName)}");

                using var table = ImRaii.Table("##ResourceTree", actionCapacity > 0 ? 4 : 3,
                    ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg);
                if (!table)
                    continue;

                ImGui.TableSetupColumn(string.Empty,  ImGuiTableColumnFlags.WidthStretch, 0.2f);
                ImGui.TableSetupColumn("Game Path",   ImGuiTableColumnFlags.WidthStretch, 0.3f);
                ImGui.TableSetupColumn("Actual Path", ImGuiTableColumnFlags.WidthStretch, 0.5f);
                if (actionCapacity > 0)
                    ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed,
                        (actionCapacity - 1) * 3 * ImGuiHelpers.GlobalScale + actionCapacity * ImGui.GetFrameHeight());
                ImGui.TableHeadersRow();

                DrawNodes(tree.Nodes, 0, unchecked(tree.DrawObjectAddress * 31), 0);
            }
        }
    }

    private void DrawControls()
    {
        var yOffset = (ChangedItemDrawer.TypeFilterIconSize.Y - ImGui.GetFrameHeight()) / 2f;
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + yOffset);

        if (ImGui.Button("Refresh Character List"))
            _task = RefreshCharacterList();

        var checkSpacing = ImGui.GetStyle().ItemInnerSpacing.X;
        var checkPadding = 10 * ImGuiHelpers.GlobalScale + ImGui.GetStyle().ItemSpacing.X;
        ImGui.SameLine(0, checkPadding);

        using (var id = ImRaii.PushId("TreeCategoryFilter"))
        {
            var categoryFilter = (uint)_categoryFilter;
            foreach (var category in Enum.GetValues<TreeCategory>())
            {
                using var c = ImRaii.PushColor(ImGuiCol.CheckMark, CategoryColor(category).Value());
                ImGui.CheckboxFlags($"##{category}", ref categoryFilter, (uint)category);
                ImGuiUtil.HoverTooltip(CategoryFilterDescription(category));
                ImGui.SameLine(0.0f, checkSpacing);
            }

            _categoryFilter = (TreeCategory)categoryFilter;
        }

        ImGui.SameLine(0, checkPadding);

        var filterChanged = false;
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - yOffset);
        using (ImRaii.Child("##typeFilter", new Vector2(ImGui.GetContentRegionAvail().X, ChangedItemDrawer.TypeFilterIconSize.Y)))
        {
            filterChanged |= changedItemDrawer.DrawTypeFilter(ref _typeFilter);
        }

        var fieldWidth = (ImGui.GetContentRegionAvail().X - checkSpacing * 2.0f - ImGui.GetFrameHeightWithSpacing()) / 2.0f;
        ImGui.SetNextItemWidth(fieldWidth);
        filterChanged |= ImGui.InputTextWithHint("##TreeNameFilter", "Filter by Character/Entity Name...", ref _nameFilter, 128);
        ImGui.SameLine(0, checkSpacing);
        ImGui.SetNextItemWidth(fieldWidth);
        filterChanged |= ImGui.InputTextWithHint("##NodeFilter", "Filter by Item/Part Name or Path...", ref _nodeFilter, 128);
        ImGui.SameLine(0, checkSpacing);
        incognito.DrawToggle(ImGui.GetFrameHeightWithSpacing());

        if (filterChanged)
            _filterCache.Clear();
    }

    private Task<ResourceTree[]> RefreshCharacterList()
        => Task.Run(() =>
        {
            try
            {
                return treeFactory.FromObjectTable(ResourceTreeFactoryFlags)
                    .Select(entry => entry.ResourceTree)
                    .ToArray();
            }
            finally
            {
                _filterCache.Clear();
                _unfolded.Clear();
                onRefresh();
            }
        });

    private void DrawNodes(IEnumerable<ResourceNode> resourceNodes, int level, nint pathHash,
        ChangedItemIconFlag parentFilterIconFlag)
    {
        var debugMode   = config.DebugMode;
        var frameHeight = ImGui.GetFrameHeight();
        var cellHeight  = actionCapacity > 0 ? frameHeight : 0.0f;

        foreach (var (resourceNode, index) in resourceNodes.WithIndex())
        {
            var nodePathHash = unchecked(pathHash + resourceNode.ResourceHandle);

            var visibility = GetNodeVisibility(nodePathHash, resourceNode, parentFilterIconFlag);
            if (visibility == NodeVisibility.Hidden)
                continue;

            using var mutedColor = ImRaii.PushColor(ImGuiCol.Text, ImGuiUtil.HalfTransparentText(), resourceNode.Internal);

            var filterIcon = resourceNode.IconFlag != 0 ? resourceNode.IconFlag : parentFilterIconFlag;

            using var id = ImRaii.PushId(index);
            ImGui.TableNextColumn();
            var unfolded = _unfolded.Contains(nodePathHash);
            using (var indent = ImRaii.PushIndent(level))
            {
                var hasVisibleChildren = resourceNode.Children.Any(child
                    => GetNodeVisibility(unchecked(nodePathHash * 31 + child.ResourceHandle), child, filterIcon) != NodeVisibility.Hidden);
                var unfoldable = hasVisibleChildren && visibility != NodeVisibility.DescendentsOnly;
                if (unfoldable)
                {
                    using var font   = ImRaii.PushFont(UiBuilder.IconFont);
                    var       icon   = (unfolded ? FontAwesomeIcon.CaretDown : FontAwesomeIcon.CaretRight).ToIconString();
                    var       offset = (ImGui.GetFrameHeight() - ImGui.CalcTextSize(icon).X) / 2;
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
                    ImGui.TextUnformatted(icon);
                    ImGui.SameLine(0f, offset + ImGui.GetStyle().ItemInnerSpacing.X);
                }
                else
                {
                    if (hasVisibleChildren && !unfolded)
                    {
                        _unfolded.Add(nodePathHash);
                        unfolded = true;
                    }

                    ImGui.Dummy(new Vector2(ImGui.GetFrameHeight()));
                    ImGui.SameLine(0f, ImGui.GetStyle().ItemInnerSpacing.X);
                }

                changedItemDrawer.DrawCategoryIcon(resourceNode.IconFlag);
                ImGui.SameLine(0f, ImGui.GetStyle().ItemInnerSpacing.X);
                ImGui.TableHeader(resourceNode.Name);
                if (ImGui.IsItemClicked() && unfoldable)
                {
                    if (unfolded)
                        _unfolded.Remove(nodePathHash);
                    else
                        _unfolded.Add(nodePathHash);
                    unfolded = !unfolded;
                }

                if (debugMode)
                {
                    using var _ = ImRaii.PushFont(UiBuilder.MonoFont);
                    ImGuiUtil.HoverTooltip(
                        $"Resource Type:   {resourceNode.Type}\nObject Address:  0x{resourceNode.ObjectAddress:X16}\nResource Handle: 0x{resourceNode.ResourceHandle:X16}\nLength:          0x{resourceNode.Length:X16}");
                }
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
                var hasMod = resourceNode.Mod.TryGetTarget(out var mod);
                if (resourceNode is { ModName: not null, ModRelativePath: not null })
                {
                    var       modName = $"[{(hasMod ? mod!.Name : resourceNode.ModName)}]";
                    var       textPos = ImGui.GetCursorPosX() + ImUtf8.CalcTextSize(modName).X + ImGui.GetStyle().ItemInnerSpacing.X;
                    using var group   = ImUtf8.Group();
                    using (var color = ImRaii.PushColor(ImGuiCol.Text, (hasMod ? ColorId.NewMod : ColorId.DisabledMod).Value()))
                    {
                        ImUtf8.Selectable(modName, false, ImGuiSelectableFlags.AllowItemOverlap, new Vector2(ImGui.GetContentRegionAvail().X, cellHeight));
                    }

                    ImGui.SameLine();
                    ImGui.SetCursorPosX(textPos);
                    ImUtf8.Text(resourceNode.ModRelativePath);
                }
                else
                {
                    ImGui.Selectable(resourceNode.FullPath.ToPath(), false, ImGuiSelectableFlags.AllowItemOverlap, new Vector2(ImGui.GetContentRegionAvail().X, cellHeight));
                }

                if (ImGui.IsItemClicked())
                    ImGui.SetClipboardText(resourceNode.FullPath.ToPath());
                if (hasMod && ImGui.IsItemClicked(ImGuiMouseButton.Right) && ImGui.GetIO().KeyCtrl)
                    communicator.SelectTab.Invoke(TabType.Mods, mod);

                ImGuiUtil.HoverTooltip(
                    $"{resourceNode.FullPath.ToPath()}\n\nClick to copy to clipboard.{(hasMod ? "\nControl + Right-Click to jump to mod." : string.Empty)}{GetAdditionalDataSuffix(resourceNode.AdditionalData)}");
            }
            else
            {
                ImUtf8.Selectable(GetPathStatusLabel(resourceNode.FullPathStatus), false, ImGuiSelectableFlags.Disabled,
                    new Vector2(ImGui.GetContentRegionAvail().X, cellHeight));
                ImGuiUtil.HoverTooltip(
                    $"{GetPathStatusDescription(resourceNode.FullPathStatus)}{GetAdditionalDataSuffix(resourceNode.AdditionalData)}");
            }

            mutedColor.Dispose();

            if (actionCapacity > 0)
            {
                ImGui.TableNextColumn();
                using var spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing,
                    ImGui.GetStyle().ItemSpacing with { X = 3 * ImGuiHelpers.GlobalScale });
                drawActions(resourceNode, new Vector2(frameHeight));
            }

            if (unfolded)
                DrawNodes(resourceNode.Children, level + 1, unchecked(nodePathHash * 31), filterIcon);
        }

        return;

        string GetAdditionalDataSuffix(CiByteString data)
            => !debugMode || data.IsEmpty ? string.Empty : $"\n\nAdditional Data: {data}";

        NodeVisibility GetNodeVisibility(nint nodePathHash, ResourceNode node, ChangedItemIconFlag parentFilterIcon)
        {
            if (!_filterCache.TryGetValue(nodePathHash, out var visibility))
            {
                visibility = CalculateNodeVisibility(nodePathHash, node, parentFilterIcon);
                _filterCache.Add(nodePathHash, visibility);
            }

            return visibility;
        }

        NodeVisibility CalculateNodeVisibility(nint nodePathHash, ResourceNode node, ChangedItemIconFlag parentFilterIcon)
        {
            if (node.Internal && !debugMode)
                return NodeVisibility.Hidden;

            var filterIcon = node.IconFlag != 0 ? node.IconFlag : parentFilterIcon;
            if (MatchesFilter(node, filterIcon))
                return NodeVisibility.Visible;

            foreach (var child in node.Children)
            {
                if (GetNodeVisibility(unchecked(nodePathHash * 31 + child.ResourceHandle), child, filterIcon) != NodeVisibility.Hidden)
                    return NodeVisibility.DescendentsOnly;
            }

            return NodeVisibility.Hidden;
        }

        bool MatchesFilter(ResourceNode node, ChangedItemIconFlag filterIcon)
        {
            if (!_typeFilter.HasFlag(filterIcon))
                return false;

            if (_nodeFilter.Length == 0)
                return true;

            return node.Name != null && node.Name.Contains(_nodeFilter, StringComparison.OrdinalIgnoreCase)
             || node.FullPath.FullName.Contains(_nodeFilter, StringComparison.OrdinalIgnoreCase)
             || node.FullPath.InternalName.ToString().Contains(_nodeFilter, StringComparison.OrdinalIgnoreCase)
             || Array.Exists(node.PossibleGamePaths, path => path.Path.ToString().Contains(_nodeFilter, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static ReadOnlySpan<byte> GetPathStatusLabel(ResourceNode.PathStatus status)
        => status switch
        {
            ResourceNode.PathStatus.External    => "(managed by external tools)"u8,
            ResourceNode.PathStatus.NonExistent => "(not found)"u8,
            _                                   => "(unavailable)"u8,
        };

    private static string GetPathStatusDescription(ResourceNode.PathStatus status)
        => status switch
        {
            ResourceNode.PathStatus.External    => "The actual path to this file is unavailable, because it is managed by external tools.",
            ResourceNode.PathStatus.NonExistent => "The actual path to this file is unavailable, because it seems to have been moved or deleted since it was loaded.",
            _                                   => "The actual path to this file is unavailable.",
        };

    [Flags]
    private enum TreeCategory : uint
    {
        LocalPlayer  = 1,
        Player       = 2,
        Networked    = 4,
        NonNetworked = 8,
    }

    private const TreeCategory AllCategories = (TreeCategory)(((uint)TreeCategory.NonNetworked << 1) - 1);

    private static TreeCategory Classify(ResourceTree tree)
        => tree.LocalPlayerRelated ? TreeCategory.LocalPlayer :
            tree.PlayerRelated     ? TreeCategory.Player :
            tree.Networked         ? TreeCategory.Networked :
                                     TreeCategory.NonNetworked;

    private static ColorId CategoryColor(TreeCategory category)
        => category switch
        {
            TreeCategory.LocalPlayer  => ColorId.ResTreeLocalPlayer,
            TreeCategory.Player       => ColorId.ResTreePlayer,
            TreeCategory.Networked    => ColorId.ResTreeNetworked,
            TreeCategory.NonNetworked => ColorId.ResTreeNonNetworked,
            _                         => throw new ArgumentException(),
        };

    private static string CategoryFilterDescription(TreeCategory category)
        => category switch
        {
            TreeCategory.LocalPlayer  => "Show you and what you own (mount, minion, accessory, pets and so on).",
            TreeCategory.Player       => "Show other players and what they own.",
            TreeCategory.Networked    => "Show non-player entities handled by the game server.",
            TreeCategory.NonNetworked => "Show non-player entities handled locally.",
            _                         => throw new ArgumentException(),
        };

    [Flags]
    private enum NodeVisibility : uint
    {
        Hidden          = 0,
        Visible         = 1,
        DescendentsOnly = 2,
    }
}

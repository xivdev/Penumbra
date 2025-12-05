using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using ImSharp;
using Lumina.Data;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.GameData.Files;
using Penumbra.GameData.Structs;
using Penumbra.Interop.ResourceTree;
using Penumbra.Services;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public class ResourceTreeViewer(
    Configuration config,
    ResourceTreeFactory treeFactory,
    ChangedItemDrawer changedItemDrawer,
    IncognitoService incognito,
    int actionCapacity,
    Action onRefresh,
    Action<ResourceNode, IWritable?, Vector2> drawActions,
    CommunicatorService communicator,
    PcpService pcpService,
    IDataManager gameData,
    FileDialogService fileDialog,
    FileCompactor compactor)
{
    private const ResourceTreeFactory.Flags ResourceTreeFactoryFlags =
        ResourceTreeFactory.Flags.WithUiData | ResourceTreeFactory.Flags.WithOwnership;

    private readonly HashSet<nint> _unfolded = [];

    private readonly Dictionary<nint, NodeVisibility> _filterCache   = [];
    private readonly Dictionary<FullPath, IWritable?> _writableCache = [];

    private TreeCategory        _categoryFilter = AllCategories;
    private ChangedItemIconFlag _typeFilter     = ChangedItemFlagExtensions.AllFlags;
    private string              _nameFilter     = string.Empty;
    private string              _nodeFilter     = string.Empty;
    private string              _note           = string.Empty;

    private Task<ResourceTree[]>? _task;

    public void Draw()
    {
        DrawModifiedGameFilesWarning();
        DrawControls();
        _task ??= RefreshCharacterList();

        using var child = Im.Child.Begin("##Data"u8);
        if (!child)
            return;

        if (!_task.IsCompleted)
        {
            Im.Line.New();
            Im.Text("Calculating character list..."u8);
        }
        else if (_task.Exception != null)
        {
            Im.Line.New();
            Im.Text($"Error during calculation of character list:\n\n{_task.Exception}", Colors.RegexWarningBorder);
        }
        else if (_task.IsCompletedSuccessfully)
        {
            var debugMode = config.DebugMode;
            foreach (var (index, tree) in _task.Result.Index())
            {
                var category = Classify(tree);
                if (!_categoryFilter.HasFlag(category) || !tree.Name.Contains(_nameFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                using (ImGuiColor.Text.Push(CategoryColor(category).Value()))
                {
                    var isOpen = Im.Tree.Header($"{(incognito.IncognitoMode ? tree.AnonymizedName : tree.Name)}###{index}",
                        index == 0 ? TreeNodeFlags.DefaultOpen : 0);
                    if (debugMode)
                    {
                        using var _ = Im.Font.PushMono();
                        Im.Tooltip.OnHover(
                            $"Object Index:        {tree.GameObjectIndex}\nObject Address:      0x{tree.GameObjectAddress:X16}\nDraw Object Address: 0x{tree.DrawObjectAddress:X16}");
                    }

                    if (!isOpen)
                        continue;
                }

                using var id = Im.Id.Push(index);

                ImEx.TextFrameAligned($"Collection: {(incognito.IncognitoMode ? tree.AnonymizedCollectionName : tree.CollectionName)}");
                Im.Line.Same();
                if (ImEx.Button("Export Character Pack"u8,
                        "Note that this recomputes the current data of the actor if it still exists, and does not use the cached data."u8))
                {
                    pcpService.CreatePcp((ObjectIndex)tree.GameObjectIndex, null, _note).ContinueWith(t =>
                    {
                        var (success, text) = t.Result;

                        if (success)
                            Penumbra.Messager.NotificationMessage($"Created {text}.", NotificationType.Success, false);
                        else
                            Penumbra.Messager.NotificationMessage(text, NotificationType.Error, false);
                    });
                    _note = string.Empty;
                }

                Im.Line.SameInner();
                if (ImEx.Button("Export To..."u8,
                        "Note that this recomputes the current data of the actor if it still exists, and does not use the cached data."u8))
                    fileDialog.OpenSavePicker("Export PCP...",
                        $"Penumbra Mod Packs{{.pcp,.pmp}},{config.PcpSettings.PcpExtension},Any File{{.*}}",
                        PcpService.ModName(tree.Name, _note, DateTime.Now),
                        config.PcpSettings.PcpExtension,
                        (selected, path) =>
                        {
                            if (!selected)
                                return;

                            pcpService.CreatePcp((ObjectIndex)tree.GameObjectIndex, path, _note).ContinueWith(t =>
                            {
                                var (success, text) = t.Result;

                                if (success)
                                    Penumbra.Messager.NotificationMessage($"Created {text}.", NotificationType.Success, false);
                                else
                                    Penumbra.Messager.NotificationMessage(text, NotificationType.Error, false);
                            });
                            _note = string.Empty;
                        }, config.ExportDirectory, false);
                Im.Line.SameInner();
                Im.Item.SetNextWidth(Im.ContentRegion.Available.X);
                Im.Input.Text("##note"u8, ref _note, "Export note..."u8);


                using var table = Im.Table.Begin("##ResourceTree"u8, 4,
                    TableFlags.SizingFixedFit | TableFlags.RowBackground);
                if (!table)
                    continue;

                table.SetupColumn(StringU8.Empty,  TableColumnFlags.WidthStretch, 0.2f);
                table.SetupColumn("Game Path"u8,   TableColumnFlags.WidthStretch, 0.3f);
                table.SetupColumn("Actual Path"u8, TableColumnFlags.WidthStretch, 0.5f);
                table.SetupColumn(StringU8.Empty, TableColumnFlags.WidthFixed,
                    actionCapacity * 3 * Im.Style.GlobalScale + (actionCapacity + 1) * Im.Style.FrameHeight);
                table.HeaderRow();

                DrawNodes(table, tree.Nodes, 0, unchecked(tree.DrawObjectAddress * 31), 0);
            }
        }
    }

    private void DrawModifiedGameFilesWarning()
    {
        if (!gameData.HasModifiedGameDataFiles)
            return;

        using var style = ImGuiColor.Text.Push(ImGuiColors.DalamudOrange);

        Im.TextWrapped(
            "Dalamud is reporting your FFXIV installation has modified game files. Any mods installed through TexTools will produce this message."u8);
        Im.TextWrapped("Penumbra and some other plugins assume your FFXIV installation is unmodified in order to work."u8);
        Im.TextWrapped(
            "Data displayed here may be inaccurate because of this, which, in turn, can break functionality relying on it, such as Character Pack exports/imports, or mod synchronization functions provided by other plugins."u8);
        Im.TextWrapped(
            "Exit the game, open XIVLauncher, click the arrow next to Log In and select \"repair game files\" to resolve this issue. Afterwards, do not install any mods with TexTools. Your plugin configurations will remain, as will mods enabled in Penumbra."u8);

        Im.Separator();
    }

    private void DrawControls()
    {
        var yOffset = (ChangedItemDrawer.TypeFilterIconSize.Y - Im.Style.FrameHeight) / 2f;
        Im.Cursor.Y += yOffset;

        if (Im.Button("Refresh Character List"u8))
            _task = RefreshCharacterList();

        var checkSpacing = Im.Style.ItemInnerSpacing.X;
        var checkPadding = 10 * Im.Style.GlobalScale + Im.Style.ItemSpacing.X;
        Im.Line.Same(0, checkPadding);

        using (Im.Id.Push("TreeCategoryFilter"u8))
        {
            foreach (var category in Enum.GetValues<TreeCategory>())
            {
                using var c = ImGuiColor.CheckMark.Push(CategoryColor(category).Value());
                Im.Checkbox($"##{category}", ref _categoryFilter, category);
                Im.Tooltip.OnHover(CategoryFilterDescription(category));
                Im.Line.Same(0.0f, checkSpacing);
            }
        }

        Im.Line.Same(0, checkPadding);

        var filterChanged = false;
        Im.Cursor.Y -= yOffset;
        using (Im.Child.Begin("##typeFilter"u8, new Vector2(Im.ContentRegion.Available.X, ChangedItemDrawer.TypeFilterIconSize.Y)))
        {
            filterChanged |= changedItemDrawer.DrawTypeFilter(ref _typeFilter);
        }

        var fieldWidth = (Im.ContentRegion.Available.X - checkSpacing * 2.0f - Im.Style.FrameHeightWithSpacing) / 2.0f;
        Im.Item.SetNextWidth(fieldWidth);
        filterChanged |= Im.Input.Text("##TreeNameFilter"u8, ref _nameFilter, "Filter by Character/Entity Name..."u8);
        Im.Line.Same(0, checkSpacing);
        Im.Item.SetNextWidth(fieldWidth);
        filterChanged |= Im.Input.Text("##NodeFilter"u8, ref _nodeFilter, "Filter by Item/Part Name or Path..."u8);
        Im.Line.Same(0, checkSpacing);
        incognito.DrawToggle(Im.Style.FrameHeightWithSpacing);

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
                _writableCache.Clear();
                _unfolded.Clear();
                onRefresh();
            }
        });

    private void DrawNodes(in Im.TableDisposable table, IEnumerable<ResourceNode> resourceNodes, int level, nint pathHash,
        ChangedItemIconFlag parentFilterIconFlag)
    {
        var debugMode   = config.DebugMode;
        var frameHeight = Im.Style.FrameHeight;

        foreach (var (index, resourceNode) in resourceNodes.Index())
        {
            var nodePathHash = unchecked(pathHash + resourceNode.ResourceHandle);

            var visibility = GetNodeVisibility(nodePathHash, resourceNode, parentFilterIconFlag);
            if (visibility == NodeVisibility.Hidden)
                continue;

            using var mutedColor = ImGuiColor.Text.Push(Im.Style[ImGuiColor.Text].WithAlpha(0.5f), resourceNode.Internal);

            var filterIcon = resourceNode.IconFlag != 0 ? resourceNode.IconFlag : parentFilterIconFlag;

            using var id = Im.Id.Push(index);
            table.NextColumn();
            var unfolded = _unfolded.Contains(nodePathHash);
            using (Im.Indent(level))
            {
                var hasVisibleChildren = resourceNode.Children.Any(child
                    => GetNodeVisibility(unchecked(nodePathHash * 31 + child.ResourceHandle), child, filterIcon) != NodeVisibility.Hidden);
                var unfoldable = hasVisibleChildren && visibility != NodeVisibility.DescendentsOnly;
                if (unfoldable)
                {
                    var icon   = unfolded ? LunaStyle.CollapseUpIcon : LunaStyle.ExpandDownIcon;
                    var offset = (Im.Style.FrameHeight - ImEx.Icon.CalculateSize(icon).X) / 2;
                    Im.Cursor.X += offset;
                    ImEx.Icon.Draw(icon);
                    Im.Line.Same(0f, offset + Im.Style.ItemInnerSpacing.X);
                }
                else
                {
                    if (hasVisibleChildren && !unfolded)
                    {
                        _unfolded.Add(nodePathHash);
                        unfolded = true;
                    }

                    Im.FrameDummy();
                    Im.Line.SameInner();
                }

                changedItemDrawer.DrawCategoryIcon(resourceNode.IconFlag);
                Im.Line.SameInner();
                table.Header(resourceNode.Name!);
                if (unfoldable && Im.Item.Clicked())
                {
                    if (unfolded)
                        _unfolded.Remove(nodePathHash);
                    else
                        _unfolded.Add(nodePathHash);
                    unfolded = !unfolded;
                }

                if (debugMode)
                {
                    using var _ = Im.Font.PushMono();
                    Im.Tooltip.OnHover(
                        $"Resource Type:   {resourceNode.Type}\nObject Address:  0x{resourceNode.ObjectAddress:X16}\nResource Handle: 0x{resourceNode.ResourceHandle:X16}\nLength:          0x{resourceNode.Length:X16}");
                }
            }

            table.NextColumn();
            var hasGamePaths = resourceNode.PossibleGamePaths.Length > 0;
            Im.Selectable(resourceNode.PossibleGamePaths.Length switch
            {
                0 => "(none)"u8,
                1 => $"{resourceNode.GamePath}",
                _ => "(multiple)"u8,
            }, false, hasGamePaths ? 0 : SelectableFlags.Disabled, Im.ContentRegion.Available with { Y = frameHeight });
            if (hasGamePaths && Im.Item.Hovered())
            {
                var allPaths = StringU8.Join((byte)'\n', resourceNode.PossibleGamePaths.AsEnumerable());
                if (Im.Item.Clicked())
                    Im.Clipboard.Set(allPaths);
                using var tt = Im.Tooltip.Begin();
                Im.Text(allPaths);
                Im.Text("\n\nClick to copy to clipboard."u8);
            }

            table.NextColumn();
            if (resourceNode.FullPath.FullName.Length > 0)
            {
                var hasMod = resourceNode.Mod.TryGetTarget(out var mod);
                if (resourceNode is { ModName: not null, ModRelativePath: not null })
                {
                    var       modName = $"[{(hasMod ? mod!.Name : resourceNode.ModName)}]";
                    var       textPos = Im.Cursor.X + Im.Font.CalculateSize(modName).X + Im.Style.ItemInnerSpacing.X;
                    using var group   = Im.Group();
                    using (ImGuiColor.Text.Push((hasMod ? ColorId.NewMod : ColorId.DisabledMod).Value()))
                    {
                        Im.Selectable(modName, false, SelectableFlags.AllowOverlap, Im.ContentRegion.Available with { Y = frameHeight });
                    }

                    Im.Line.Same();
                    Im.Cursor.X = textPos;
                    Im.Text(resourceNode.ModRelativePath);
                }
                else if (resourceNode.FullPath.IsRooted)
                {
                    var path                   = resourceNode.FullPath.FullName;
                    var lastDirectorySeparator = path.LastIndexOf('\\');
                    var secondLastDirectorySeparator = lastDirectorySeparator > 0
                        ? path.LastIndexOf('\\', lastDirectorySeparator - 1)
                        : -1;
                    if (secondLastDirectorySeparator >= 0)
                        path = $"…{path.AsSpan(secondLastDirectorySeparator)}";
                    Im.Selectable(path, false, SelectableFlags.AllowOverlap, Im.ContentRegion.Available with { Y = frameHeight });
                }
                else
                {
                    Im.Selectable(resourceNode.FullPath.ToPath(), false, SelectableFlags.AllowOverlap,
                        Im.ContentRegion.Available with { Y = frameHeight });
                }

                if (Im.Item.Clicked())
                    Im.Clipboard.Set(resourceNode.FullPath.ToPath());
                if (hasMod && Im.Item.RightClicked() && Im.Io.KeyControl)
                    communicator.SelectTab.Invoke(new SelectTab.Arguments(TabType.Mods, mod));

                Im.Tooltip.OnHover(
                    $"{resourceNode.FullPath.ToPath()}\n\nClick to copy to clipboard.{(hasMod ? "\nControl + Right-Click to jump to mod." : string.Empty)}{GetAdditionalDataSuffix(resourceNode.AdditionalData)}");
            }
            else
            {
                Im.Selectable(GetPathStatusLabel(resourceNode.FullPathStatus), false, SelectableFlags.Disabled,
                    Im.ContentRegion.Available with { Y = frameHeight });
                Im.Tooltip.OnHover(
                    $"{GetPathStatusDescription(resourceNode.FullPathStatus)}{GetAdditionalDataSuffix(resourceNode.AdditionalData)}");
            }

            mutedColor.Pop();

            table.NextColumn();
            using var spacing = ImStyleDouble.ItemSpacing.PushX(3 * Im.Style.GlobalScale);
            DrawActions(resourceNode, new Vector2(frameHeight));

            if (unfolded)
                DrawNodes(table, resourceNode.Children, level + 1, unchecked(nodePathHash * 31), filterIcon);
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

        void DrawActions(ResourceNode resourceNode, Vector2 buttonSize)
        {
            if (!_writableCache.TryGetValue(resourceNode.FullPath, out var writable))
            {
                var path = resourceNode.FullPath.ToPath();
                if (resourceNode.FullPath.IsRooted)
                {
                    writable = new RawFileWritable(path);
                }
                else
                {
                    var file = gameData.GetFile(path);
                    writable = file is null ? null : new RawGameFileWritable(file);
                }

                _writableCache.Add(resourceNode.FullPath, writable);
            }

            if (ImEx.Icon.Button(LunaStyle.SaveIcon, "Export this file."u8, resourceNode.FullPath.FullName.Length is 0 || writable is null, buttonSize))
            {
                var fullPathStr = resourceNode.FullPath.FullName;
                var ext = resourceNode.PossibleGamePaths.Length == 1
                    ? Path.GetExtension(resourceNode.GamePath.ToString())
                    : Path.GetExtension(fullPathStr);
                fileDialog.OpenSavePicker($"Export {Path.GetFileName(fullPathStr)} to...", ext, Path.GetFileNameWithoutExtension(fullPathStr),
                    ext,
                    (success, name) =>
                    {
                        if (!success)
                            return;

                        try
                        {
                            compactor.WriteAllBytes(name, writable!.Write());
                        }
                        catch (Exception e)
                        {
                            Penumbra.Log.Error($"Could not export {fullPathStr}:\n{e}");
                        }
                    }, null, false);
            }

            drawActions(resourceNode, writable, new Vector2(frameHeight));
        }
    }

    private static ReadOnlySpan<byte> GetPathStatusLabel(ResourceNode.PathStatus status)
        => status switch
        {
            ResourceNode.PathStatus.External    => "(managed by external tools)"u8,
            ResourceNode.PathStatus.NonExistent => "(not found)"u8,
            _                                   => "(unavailable)"u8,
        };

    private static ReadOnlySpan<byte> GetPathStatusDescription(ResourceNode.PathStatus status)
        => status switch
        {
            ResourceNode.PathStatus.External => "The actual path to this file is unavailable, because it is managed by external tools."u8,
            ResourceNode.PathStatus.NonExistent =>
                "The actual path to this file is unavailable, because it seems to have been moved or deleted since it was loaded."u8,
            _ => "The actual path to this file is unavailable."u8,
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

    private static ReadOnlySpan<byte> CategoryFilterDescription(TreeCategory category)
        => category switch
        {
            TreeCategory.LocalPlayer  => "Show you and what you own (mount, minion, accessory, pets and so on)."u8,
            TreeCategory.Player       => "Show other players and what they own."u8,
            TreeCategory.Networked    => "Show non-player entities handled by the game server."u8,
            TreeCategory.NonNetworked => "Show non-player entities handled locally."u8,
            _                         => throw new ArgumentException(),
        };

    [Flags]
    private enum NodeVisibility : uint
    {
        Hidden          = 0,
        Visible         = 1,
        DescendentsOnly = 2,
    }

    private record RawFileWritable(string Path) : IWritable
    {
        public bool Valid
            => true;

        public byte[] Write()
            => File.ReadAllBytes(Path);
    }

    private record RawGameFileWritable(FileResource FileResource) : IWritable
    {
        public bool Valid
            => true;

        public byte[] Write()
            => FileResource.Data;
    }
}

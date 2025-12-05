using FFXIVClientStructs.FFXIV.Client.System.Resource;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.STD;
using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Interop.Hooks.ResourceLoading;

namespace Penumbra.UI.Tabs;

public sealed class ResourceTab(Configuration config, ResourceManagerService resourceManager)
    : ITab<TabType>
{
    public TabType Identifier
        => TabType.ResourceManager;

    public ReadOnlySpan<byte> Label
        => "Resource Manager"u8;

    public bool IsVisible
        => config.DebugMode;

    public readonly TextFilter Filter = new();

    /// <summary> Draw a tab to iterate over the main resource maps and see what resources are currently loaded. </summary>
    public unsafe void DrawContent()
    {
        // Filter for resources containing the input string.
        Filter.DrawFilter("##ResourceFilter"u8, Im.ContentRegion.Available);
        using var child = Im.Child.Begin("##ResourceManagerTab"u8, Im.ContentRegion.Available);
        if (!child)
            return;

        resourceManager.IterateGraphs(DrawCategoryContainer);

        Im.Line.New();
        using var table = Im.Table.Begin("##t"u8, 2, TableFlags.SizingFixedFit);
        if (!table)
            return;

        table.DrawColumn("Static Address:"u8);
        table.NextColumn();
        Penumbra.Dynamis.DrawPointer(resourceManager.ResourceManagerAddress);
        table.DrawColumn("Actual Address:"u8);
        table.NextColumn();
        Penumbra.Dynamis.DrawPointer(resourceManager.ResourceManager);
    }

    private float  _hashColumnWidth;
    private float  _pathColumnWidth;
    private float  _refsColumnWidth;

    /// <summary> Draw a single resource map. </summary>
    private unsafe void DrawResourceMap(ResourceCategory category, uint ext,
        StdMap<uint, FFXIVClientStructs.Interop.Pointer<ResourceHandle>>* map)
    {
        if (map == null)
            return;

        var       label = GetNodeLabel((uint)category, ext, map->Count);
        using var tree  = Im.Tree.Node(label);
        if (!tree || map->Count == 0)
            return;

        using var table = Im.Table.Begin("##table"u8, 4, TableFlags.SizingFixedFit | TableFlags.RowBackground);
        if (!table)
            return;

        table.SetupColumn("Hash"u8, TableColumnFlags.WidthFixed, _hashColumnWidth);
        table.SetupColumn("Ptr"u8,  TableColumnFlags.WidthFixed, _hashColumnWidth);
        table.SetupColumn("Path"u8, TableColumnFlags.WidthFixed, _pathColumnWidth);
        table.SetupColumn("Refs"u8, TableColumnFlags.WidthFixed, _refsColumnWidth);
        Im.Table.HeaderRow();

        resourceManager.IterateResourceMap(map, (hash, r) =>
        {
            // Filter unwanted names.
            if (Filter.Text.Length > 0 && Filter.WouldBeVisible(r->FileName.ToString(), -1))
                return;

            Im.Table.DrawColumn($"0x{hash:X8}");
            Im.Table.NextColumn();
            Penumbra.Dynamis.DrawPointer(r);
            var resource = (Interop.Structs.ResourceHandle*)r;
            Im.Table.DrawColumn(resource->FileName().Span);
            if (Im.Item.Clicked())
                Im.Clipboard.Set(resource->FileName().Span);
            else if (Im.Item.RightClicked())
            {
                var data = resource->CsHandle.GetData();
                if (data != null)
                {
                    var length = (int)resource->CsHandle.GetLength();
                    Im.Clipboard.Set(StringU8.Join((byte) ' ',
                        new ReadOnlySpan<byte>(data, length).ToArray().Select(b => b.ToString("X2"))));
                }
            }

            Im.Tooltip.OnHover("Click to copy the file name to clipboard.\nRight-Click to copy byte-wise file data to clipboard, if any."u8);

            Im.Table.DrawColumn($"{r->RefCount}");
        });
    }

    /// <summary> Draw a full category for the resource manager. </summary>
    private unsafe void DrawCategoryContainer(ResourceCategory category,
        StdMap<uint, FFXIVClientStructs.Interop.Pointer<StdMap<uint, FFXIVClientStructs.Interop.Pointer<ResourceHandle>>>>* map, int idx)
    {
        if (map is null)
            return;

        using var tree = Im.Tree.Node($"({(uint)category:D2}) {category} (Ex {idx}) - {map->Count}###{(uint)category}_{idx}");
        if (!tree)
            return;

        SetTableWidths();
        resourceManager.IterateExtMap(map, (ext, m) => DrawResourceMap(category, ext, m));
    }

    /// <summary> Obtain a label for an extension node. </summary>
    private static Utf8StringHandler<LabelStringHandlerBuffer> GetNodeLabel(uint label, uint type, int count)
    {
        var (lowest, mid1, mid2, highest) = BitFunctions.SplitBytes(type);
        return highest is 0
            ? $"({type:X8}) {(char)mid2}{(char)mid1}{(char)lowest} - {count}###{label}{type}"
            : $"({type:X8}) {(char)highest}{(char)mid2}{(char)mid1}{(char)lowest} - {count}###{label}{type}";
    }

    /// <summary> Set the widths for a resource table. </summary>
    private void SetTableWidths()
    {
        _hashColumnWidth = 100 * Im.Style.GlobalScale;
        _pathColumnWidth = Im.Window.MaximumContentRegion.X - Im.Window.MinimumContentRegion.X - 300 * Im.Style.GlobalScale;
        _refsColumnWidth = 30 * Im.Style.GlobalScale;
    }
}

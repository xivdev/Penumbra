using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.STD;
using ImSharp;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Interop.Hooks.ResourceLoading;
using Penumbra.String.Classes;

namespace Penumbra.UI.Tabs;

public class ResourceTab(Configuration config, ResourceManagerService resourceManager, ISigScanner sigScanner)
    : ITab, Luna.IUiService
{
    public ReadOnlySpan<byte> Label
        => "Resource Manager"u8;

    public bool IsVisible
        => config.DebugMode;

    /// <summary> Draw a tab to iterate over the main resource maps and see what resources are currently loaded. </summary>
    public void DrawContent()
    {
        // Filter for resources containing the input string.
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##resourceFilter", "Filter...", ref _resourceManagerFilter, Utf8GamePath.MaxGamePathLength);

        using var child = ImRaii.Child("##ResourceManagerTab", -Vector2.One);
        if (!child)
            return;

        unsafe
        {
            resourceManager.IterateGraphs(DrawCategoryContainer);
        }

        Im.Line.New();
        unsafe
        {
            ImGui.TextUnformatted(
                $"Static Address: 0x{(ulong)resourceManager.ResourceManagerAddress:X} (+0x{(ulong)resourceManager.ResourceManagerAddress - (ulong)sigScanner.Module.BaseAddress:X})");
            ImGui.TextUnformatted($"Actual Address: 0x{(ulong)resourceManager.ResourceManager:X}");
        }
    }

    private float  _hashColumnWidth;
    private float  _pathColumnWidth;
    private float  _refsColumnWidth;
    private string _resourceManagerFilter = string.Empty;

    /// <summary> Draw a single resource map. </summary>
    private unsafe void DrawResourceMap(ResourceCategory category, uint ext, StdMap<uint, FFXIVClientStructs.Interop.Pointer<ResourceHandle>>* map)
    {
        if (map == null)
            return;

        var       label = GetNodeLabel((uint)category, ext, map->Count);
        using var tree  = ImRaii.TreeNode(label);
        if (!tree || map->Count == 0)
            return;

        using var table = ImRaii.Table("##table", 4, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg);
        if (!table)
            return;

        ImGui.TableSetupColumn("Hash", ImGuiTableColumnFlags.WidthFixed, _hashColumnWidth);
        ImGui.TableSetupColumn("Ptr",  ImGuiTableColumnFlags.WidthFixed, _hashColumnWidth);
        ImGui.TableSetupColumn("Path", ImGuiTableColumnFlags.WidthFixed, _pathColumnWidth);
        ImGui.TableSetupColumn("Refs", ImGuiTableColumnFlags.WidthFixed, _refsColumnWidth);
        ImGui.TableHeadersRow();

        resourceManager.IterateResourceMap(map, (hash, r) =>
        {
            // Filter unwanted names.
            if (_resourceManagerFilter.Length != 0
             && !r->FileName.ToString().Contains(_resourceManagerFilter, StringComparison.OrdinalIgnoreCase))
                return;

            var address = $"0x{(ulong)r:X}";
            ImGuiUtil.DrawTableColumn($"0x{hash:X8}");
            ImGui.TableNextColumn();
            ImGuiUtil.CopyOnClickSelectable(address);

            var resource = (Interop.Structs.ResourceHandle*)r;
            ImGui.TableNextColumn();
            Im.Text(resource->FileName().Span);
            if (ImGui.IsItemClicked())
            {
                var data = resource->CsHandle.GetData();
                if (data != null)
                {
                    var length = (int)resource->CsHandle.GetLength();
                    ImGui.SetClipboardText(string.Join(" ",
                        new ReadOnlySpan<byte>(data, length).ToArray().Select(b => b.ToString("X2"))));
                }
            }

            ImGuiUtil.HoverTooltip("Click to copy byte-wise file data to clipboard, if any.");

            ImGuiUtil.DrawTableColumn(r->RefCount.ToString());
        });
    }

    /// <summary> Draw a full category for the resource manager. </summary>
    private unsafe void DrawCategoryContainer(ResourceCategory category,
        StdMap<uint, FFXIVClientStructs.Interop.Pointer<StdMap<uint, FFXIVClientStructs.Interop.Pointer<ResourceHandle>>>>* map, int idx)
    {
        if (map == null)
            return;

        using var tree = ImRaii.TreeNode($"({(uint)category:D2}) {category} (Ex {idx}) - {map->Count}###{(uint)category}_{idx}");
        if (tree)
        {
            SetTableWidths();
            resourceManager.IterateExtMap(map, (ext, m) => DrawResourceMap(category, ext, m));
        }
    }

    /// <summary> Obtain a label for an extension node. </summary>
    private static string GetNodeLabel(uint label, uint type, int count)
    {
        var (lowest, mid1, mid2, highest) = Functions.SplitBytes(type);
        return highest == 0
            ? $"({type:X8}) {(char)mid2}{(char)mid1}{(char)lowest} - {count}###{label}{type}"
            : $"({type:X8}) {(char)highest}{(char)mid2}{(char)mid1}{(char)lowest} - {count}###{label}{type}";
    }

    /// <summary> Set the widths for a resource table. </summary>
    private void SetTableWidths()
    {
        _hashColumnWidth = 100 * Im.Style.GlobalScale;
        _pathColumnWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X - 300 * Im.Style.GlobalScale;
        _refsColumnWidth = 30 * Im.Style.GlobalScale;
    }
}

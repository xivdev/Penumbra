using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using ImSharp;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using OtterGui.Widgets;
using Penumbra.Collections;
using Penumbra.Collections.Cache;
using Penumbra.Collections.Manager;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.Tabs;

public class EffectiveTab(CollectionManager collectionManager, CollectionSelectHeader collectionHeader)
    : ITab, Luna.IUiService
{
    public ReadOnlySpan<byte> Label
        => "Effective Changes"u8;

    public void DrawContent()
    {
        SetupEffectiveSizes();
        collectionHeader.Draw(true);
        DrawFilters();
        using var child = ImRaii.Child("##EffectiveChangesTab", Im.ContentRegion.Available, false);
        if (!child)
            return;

        var       height = Im.Style.TextHeightWithSpacing + 2 * ImGui.GetStyle().CellPadding.Y;
        var       skips  = ImGuiClip.GetNecessarySkips(height);
        using var table  = ImRaii.Table("##EffectiveChangesTable", 3, ImGuiTableFlags.RowBg);
        if (!table)
            return;

        ImGui.TableSetupColumn("##gamePath", ImGuiTableColumnFlags.WidthFixed, _effectiveLeftTextLength);
        ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, _effectiveArrowLength);
        ImGui.TableSetupColumn("##file",     ImGuiTableColumnFlags.WidthFixed, _effectiveRightTextLength);

        DrawEffectiveRows(collectionManager.Active.Current, skips, height,
            _effectiveFilePathFilter.Length > 0 || _effectiveGamePathFilter.Length > 0);
    }

    // Sizes
    private float _effectiveLeftTextLength;
    private float _effectiveRightTextLength;
    private float _effectiveUnscaledArrowLength;
    private float _effectiveArrowLength;

    // Filters
    private string _effectiveGamePathFilter = string.Empty;
    private string _effectiveFilePathFilter = string.Empty;

    /// <summary> Setup table sizes. </summary>
    private void SetupEffectiveSizes()
    {
        if (_effectiveUnscaledArrowLength == 0)
        {
            using var font = ImRaii.PushFont(UiBuilder.IconFont);
            _effectiveUnscaledArrowLength =
                ImGui.CalcTextSize(FontAwesomeIcon.LongArrowAltLeft.ToIconString()).X / Im.Style.GlobalScale;
        }

        _effectiveArrowLength     = _effectiveUnscaledArrowLength * Im.Style.GlobalScale;
        _effectiveLeftTextLength  = 450 * Im.Style.GlobalScale;
        _effectiveRightTextLength = ImGui.GetWindowSize().X - _effectiveArrowLength - _effectiveLeftTextLength;
    }

    /// <summary> Draw the header line for filters. </summary>
    private void DrawFilters()
    {
        var tmp = _effectiveGamePathFilter;
        ImGui.SetNextItemWidth(_effectiveLeftTextLength);
        if (ImGui.InputTextWithHint("##gamePathFilter", "Filter game path...", ref tmp, 256))
            _effectiveGamePathFilter = tmp;

        ImGui.SameLine(_effectiveArrowLength + _effectiveLeftTextLength + 3 * ImGui.GetStyle().ItemSpacing.X);
        ImGui.SetNextItemWidth(-1);
        tmp = _effectiveFilePathFilter;
        if (ImGui.InputTextWithHint("##fileFilter", "Filter file path...", ref tmp, 256))
            _effectiveFilePathFilter = tmp;
    }

    /// <summary> Draw all rows for one collection respecting filters and using clipping. </summary>
    private void DrawEffectiveRows(ModCollection active, int skips, float height, bool hasFilters)
    {
        // We can use the known counts if no filters are active.
        var stop = hasFilters
            ? ImGuiClip.FilteredClippedDraw(active.ResolvedFiles, skips, CheckFilters, DrawLine)
            : ImGuiClip.ClippedDraw(active.ResolvedFiles, skips, DrawLine, active.ResolvedFiles.Count);

        var m = active.MetaCache;
        // If no meta manipulations are active, we can just draw the end dummy.
        if (m is { Count: > 0 })
        {
            // Filters mean we can not use the known counts.
            if (hasFilters)
            {
                var it2 = m.IdentifierSources.Select(p => (p.Item1.ToString(), p.Item2.Name));
                if (stop >= 0)
                {
                    ImGuiClip.DrawEndDummy(stop + it2.Count(CheckFilters), height);
                }
                else
                {
                    stop = ImGuiClip.FilteredClippedDraw(it2, skips, CheckFilters, DrawLine, ~stop);
                    ImGuiClip.DrawEndDummy(stop, height);
                }
            }
            else
            {
                if (stop >= 0)
                {
                    ImGuiClip.DrawEndDummy(stop + m.Count, height);
                }
                else
                {
                    stop = ImGuiClip.ClippedDraw(m.IdentifierSources, skips, DrawLine, m.Count, ~stop);
                    ImGuiClip.DrawEndDummy(stop, height);
                }
            }
        }
        else
        {
            ImGuiClip.DrawEndDummy(stop, height);
        }
    }

    /// <summary> Draw a line for a game path and its redirected file. </summary>
    private static void DrawLine(KeyValuePair<Utf8GamePath, ModPath> pair)
    {
        var (path, name) = pair;
        ImGui.TableNextColumn();
        ImUtf8.CopyOnClickSelectable(path.Path.Span);

        ImGui.TableNextColumn();
        ImGuiUtil.PrintIcon(FontAwesomeIcon.LongArrowAltLeft);
        ImGui.TableNextColumn();
        ImUtf8.CopyOnClickSelectable(name.Path.InternalName.Span);
        ImGuiUtil.HoverTooltip($"\nChanged by {name.Mod.Name}.");
    }

    /// <summary> Draw a line for a path and its name. </summary>
    private static void DrawLine((string, string) pair)
    {
        var (path, name) = pair;
        ImGui.TableNextColumn();
        ImGuiUtil.CopyOnClickSelectable(path);

        ImGui.TableNextColumn();
        ImGuiUtil.PrintIcon(FontAwesomeIcon.LongArrowAltLeft);
        ImGui.TableNextColumn();
        ImGuiUtil.CopyOnClickSelectable(name);
    }

    /// <summary> Draw a line for a unfiltered/unconverted manipulation and mod-index pair. </summary>
    private static void DrawLine((IMetaIdentifier, IMod) pair)
    {
        var (manipulation, mod) = pair;
        ImGui.TableNextColumn();
        ImGuiUtil.CopyOnClickSelectable(manipulation.ToString());

        ImGui.TableNextColumn();
        ImGuiUtil.PrintIcon(FontAwesomeIcon.LongArrowAltLeft);
        ImGui.TableNextColumn();
        ImGuiUtil.CopyOnClickSelectable(mod.Name);
    }

    /// <summary> Check filters for file replacements. </summary>
    private bool CheckFilters(KeyValuePair<Utf8GamePath, ModPath> kvp)
    {
        var (gamePath, fullPath) = kvp;
        if (_effectiveGamePathFilter.Length > 0 && !gamePath.ToString().Contains(_effectiveGamePathFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        return _effectiveFilePathFilter.Length == 0 || fullPath.Path.FullName.Contains(_effectiveFilePathFilter, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary> Check filters for meta manipulations. </summary>
    private bool CheckFilters((string, string) kvp)
    {
        var (name, path) = kvp;
        if (_effectiveGamePathFilter.Length > 0 && !name.Contains(_effectiveGamePathFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        return _effectiveFilePathFilter.Length == 0 || path.Contains(_effectiveFilePathFilter, StringComparison.OrdinalIgnoreCase);
    }
}

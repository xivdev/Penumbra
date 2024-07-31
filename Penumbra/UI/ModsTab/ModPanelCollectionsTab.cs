using Dalamud.Interface.Utility;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
using OtterGui.Widgets;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab;

public class ModPanelCollectionsTab(CollectionManager manager, ModFileSystemSelector selector) : ITab, IUiService
{
    private enum ModState
    {
        Enabled,
        Disabled,
        Unconfigured,
    }

    private readonly List<(ModCollection, ModCollection, uint, ModState)> _cache = [];

    public ReadOnlySpan<byte> Label
        => "Collections"u8;

    public void DrawContent()
    {
        var (direct, inherited) = CountUsage(selector.Selected!);
        ImGui.NewLine();
        if (direct == 1)
            ImUtf8.Text("This Mod is directly configured in 1 collection."u8);
        else if (direct == 0)
            ImUtf8.Text("This mod is entirely unused."u8, Colors.RegexWarningBorder);
        else
            ImUtf8.Text($"This Mod is directly configured in {direct} collections.");
        if (inherited > 0)
            ImUtf8.Text($"It is also implicitly used in {inherited} {(inherited == 1 ? "collection" : "collections")} through inheritance.");

        ImGui.NewLine();
        ImGui.Separator();
        ImGui.NewLine();
        using var table = ImUtf8.Table("##modCollections"u8, 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg);
        if (!table)
            return;

        var size           = ImUtf8.CalcTextSize(ToText(ModState.Unconfigured)).X + 20 * ImGuiHelpers.GlobalScale;
        var collectionSize = 200 * ImGuiHelpers.GlobalScale;
        ImGui.TableSetupColumn("Collection",     ImGuiTableColumnFlags.WidthFixed, collectionSize);
        ImGui.TableSetupColumn("State",          ImGuiTableColumnFlags.WidthFixed, size);
        ImGui.TableSetupColumn("Inherited From", ImGuiTableColumnFlags.WidthFixed, collectionSize);

        ImGui.TableHeadersRow();
        foreach (var ((collection, parent, color, state), idx) in _cache.WithIndex())
        {
            using var id = ImUtf8.PushId(idx);
            ImUtf8.DrawTableColumn(collection.Name);

            ImGui.TableNextColumn();
            ImUtf8.Text(ToText(state), color);

            using (var context = ImUtf8.PopupContextItem("Context"u8, ImGuiPopupFlags.MouseButtonRight))
            {
                if (context)
                {
                    ImUtf8.Text(collection.Name);
                    ImGui.Separator();
                    using (ImRaii.Disabled(state is ModState.Enabled && parent == collection))
                    {
                        if (ImUtf8.MenuItem("Enable"u8))
                        {
                            if (parent != collection)
                                manager.Editor.SetModInheritance(collection, selector.Selected!, false);
                            manager.Editor.SetModState(collection, selector.Selected!, true);
                        }
                    }

                    using (ImRaii.Disabled(state is ModState.Disabled && parent == collection))
                    {
                        if (ImUtf8.MenuItem("Disable"u8))
                        {
                            if (parent != collection)
                                manager.Editor.SetModInheritance(collection, selector.Selected!, false);
                            manager.Editor.SetModState(collection, selector.Selected!, false);
                        }
                    }

                    using (ImRaii.Disabled(parent != collection))
                    {
                        if (ImUtf8.MenuItem("Inherit"u8))
                            manager.Editor.SetModInheritance(collection, selector.Selected!, true);
                    }
                }
            }

            ImUtf8.DrawTableColumn(parent == collection ? string.Empty : parent.Name);
        }
    }

    private static ReadOnlySpan<byte> ToText(ModState state)
        => state switch
        {
            ModState.Unconfigured => "Unconfigured"u8,
            ModState.Enabled      => "Enabled"u8,
            ModState.Disabled     => "Disabled"u8,
            _                     => "Unknown"u8,
        };

    private (int Direct, int Inherited) CountUsage(Mod mod)
    {
        _cache.Clear();
        var undefined      = ColorId.UndefinedMod.Value();
        var enabled        = ColorId.EnabledMod.Value();
        var inherited      = ColorId.InheritedMod.Value();
        var disabled       = ColorId.DisabledMod.Value();
        var disInherited   = ColorId.InheritedDisabledMod.Value();
        var directCount    = 0;
        var inheritedCount = 0;
        foreach (var collection in manager.Storage)
        {
            var (settings, parent) = collection[mod.Index];
            var (color, text) = settings == null
                ? (undefined, ModState.Unconfigured)
                : settings.Enabled
                    ? (parent == collection ? enabled : inherited, ModState.Enabled)
                    : (parent == collection ? disabled : disInherited, ModState.Disabled);
            _cache.Add((collection, parent, color, text));

            if (color == enabled)
                ++directCount;
            else if (color == inherited)
                ++inheritedCount;
        }

        return (directCount, inheritedCount);
    }
}

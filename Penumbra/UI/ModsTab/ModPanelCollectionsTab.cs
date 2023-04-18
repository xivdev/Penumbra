using System;
using System.Collections.Generic;
using Dalamud.Interface;
using ImGuiNET;
using Lumina.Data.Parsing.Layer;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab;

public class ModPanelCollectionsTab : ITab
{
    private readonly Configuration         _config;
    private readonly ModFileSystemSelector _selector;
    private readonly CollectionStorage     _collections;

    private readonly List<(ModCollection, ModCollection, uint, string)> _cache = new();

    public ModPanelCollectionsTab(Configuration config, CollectionStorage storage, ModFileSystemSelector selector)
    {
        _config      = config;
        _collections = storage;
        _selector    = selector;
    }

    public ReadOnlySpan<byte> Label
        => "Collections"u8;

    public void DrawContent()
    {
        var (direct, inherited) = CountUsage(_selector.Selected!);
        ImGui.NewLine();
        if (direct == 1)
            ImGui.TextUnformatted("This Mod is directly configured in 1 collection.");
        else if (direct == 0)
            ImGuiUtil.TextColored(Colors.RegexWarningBorder, "This mod is entirely unused.");
        else
            ImGui.TextUnformatted($"This Mod is directly configured in {direct} collections.");
        if (inherited > 0)
        {
            ImGui.TextUnformatted($"It is also implicitly used in {inherited} {(inherited == 1 ? "collection" : "collections")} through inheritance.");
        }

        ImGui.NewLine();
        ImGui.Separator();
        ImGui.NewLine();
        using var table = ImRaii.Table("##modCollections", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg);
        if (!table)
            return;

        var size           = ImGui.CalcTextSize("Unconfigured").X + 20 * ImGuiHelpers.GlobalScale;
        var collectionSize = 200 * ImGuiHelpers.GlobalScale;
        ImGui.TableSetupColumn("Collection",     ImGuiTableColumnFlags.WidthFixed, collectionSize);
        ImGui.TableSetupColumn("State",          ImGuiTableColumnFlags.WidthFixed, size);
        ImGui.TableSetupColumn("Inherited From", ImGuiTableColumnFlags.WidthFixed, collectionSize);

        ImGui.TableHeadersRow();
        foreach (var (collection, parent, color, text) in _cache)
        {
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(collection.Name);

            ImGui.TableNextColumn();
            using (var c = ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted(text);
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(parent == collection ? string.Empty : parent.Name);
        }
    }

    private (int Direct, int Inherited) CountUsage(Mod mod)
    {
        _cache.Clear();
        var undefined    = ColorId.UndefinedMod.Value(_config);
        var enabled      = ColorId.EnabledMod.Value(_config);
        var inherited    = ColorId.InheritedMod.Value(_config);
        var disabled     = ColorId.DisabledMod.Value(_config);
        var disInherited = ColorId.InheritedDisabledMod.Value(_config);
        var directCount       = 0;
        var inheritedCount    = 0;
        foreach (var collection in _collections)
        {
            var (settings, parent) = collection[mod.Index];
            var (color, text) = settings == null
                ? (undefined, "Unconfigured")
                : settings.Enabled
                    ? (parent == collection ? enabled : inherited, "Enabled")
                    : (parent == collection ? disabled : disInherited, "Disabled");
            _cache.Add((collection, parent, color, text));

            if (color == enabled)
                ++directCount;
            else if (color == inherited)
                ++inheritedCount;
        }

        return (directCount, inheritedCount);
    }
}

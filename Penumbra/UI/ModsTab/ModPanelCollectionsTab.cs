using ImSharp;
using Luna;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab;

public class ModPanelCollectionsTab(CollectionManager manager, ModSelection selection) : ITab<ModPanelTab>
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

    public ModPanelTab Identifier
        => ModPanelTab.Collections;

    public void DrawContent()
    {
        using var id = Im.Id.Push(selection.ModName);
        var (direct, inherited) = CountUsage(selection.Mod!);
        Im.Line.New();
        switch (direct)
        {
            case 1:  Im.Text("This Mod is directly configured in 1 collection."u8); break;
            case 0:  Im.Text("This mod is entirely unused."u8, Colors.RegexWarningBorder); break;
            default: Im.Text($"This Mod is directly configured in {direct} collections."); break;
        }

        if (inherited > 0)
            Im.Text($"It is also implicitly used in {inherited} {(inherited == 1 ? "collection" : "collections")} through inheritance.");

        if (ImEx.Button("Inherit in All Collections"u8, default, StringU8.Empty, !LunaStyle.Modifier.Destructive))
            foreach (var collection in manager.Storage)
                manager.Editor.SetModInheritance(collection, selection.Mod!, true);
        LunaStyle.Modifier.Destructive.Tooltip("inherit");

        Im.Line.Same();
        if (ImEx.Button("Disable in All Collections"u8, default, StringU8.Empty, !LunaStyle.Modifier.Destructive))
            foreach (var collection in manager.Storage)
                manager.Editor.SetModState(collection, selection.Mod!, false);
        LunaStyle.Modifier.Destructive.Tooltip("disable");


        LunaStyle.DrawSeparator();
        using var table = Im.Table.Begin("##modCollections"u8, 3, TableFlags.SizingFixedFit | TableFlags.RowBackground);
        if (!table)
            return;

        var size           = Im.Font.CalculateSize(ModState.Unconfigured.StringU8).X + 20 * Im.Style.GlobalScale;
        var collectionSize = 200 * Im.Style.GlobalScale;
        table.SetupColumn("Collection"u8,     TableColumnFlags.WidthFixed, collectionSize);
        table.SetupColumn("State"u8,          TableColumnFlags.WidthFixed, size);
        table.SetupColumn("Inherited From"u8, TableColumnFlags.WidthFixed, collectionSize);
        table.HeaderRow();

        foreach (var (idx, (collection, parent, color, state)) in _cache.Index())
        {
            id.Push(idx);
            table.DrawColumn(collection.Identity.Name);

            table.NextColumn();
            Im.Text(state.StringU8, color);

            using (var context = Im.Popup.BeginContextItem("Context"u8))
            {
                if (context)
                {
                    Im.Text(collection.Identity.Name);
                    Im.Separator();
                    using (Im.Disabled(state is ModState.Enabled && parent == collection))
                    {
                        if (Im.Menu.Item("Enable"u8))
                        {
                            if (parent != collection)
                                manager.Editor.SetModInheritance(collection, selection.Mod!, false);
                            manager.Editor.SetModState(collection, selection.Mod!, true);
                        }
                    }

                    using (Im.Disabled(state is ModState.Disabled && parent == collection))
                    {
                        if (Im.Menu.Item("Disable"u8))
                        {
                            if (parent != collection)
                                manager.Editor.SetModInheritance(collection, selection.Mod!, false);
                            manager.Editor.SetModState(collection, selection.Mod!, false);
                        }
                    }

                    using (Im.Disabled(parent != collection))
                    {
                        if (Im.Menu.Item("Inherit"u8))
                            manager.Editor.SetModInheritance(collection, selection.Mod!, true);
                    }
                }
            }

            table.DrawColumn(parent == collection ? StringU8.Empty : parent.Identity.Name);
            id.Pop();
        }
    }

    private (int Direct, int Inherited) CountUsage(Mod mod)
    {
        _cache.Clear();
        var undefined      = ColorId.UndefinedMod.Value;
        var enabled        = ColorId.EnabledMod.Value;
        var inherited      = ColorId.InheritedMod.Value;
        var disabled       = ColorId.DisabledMod.Value;
        var disInherited   = ColorId.InheritedDisabledMod.Value;
        var directCount    = 0;
        var inheritedCount = 0;
        foreach (var collection in manager.Storage)
        {
            var (settings, parent) = collection.GetInheritedSettings(mod.Index);
            var (color, text) = settings is null
                ? (undefined, ModState.Unconfigured)
                : settings.Enabled
                    ? (parent == collection ? enabled : inherited, ModState.Enabled)
                    : (parent == collection ? disabled : disInherited, ModState.Disabled);
            _cache.Add((collection, parent, color.Color, text));

            if (color == enabled)
                ++directCount;
            else if (color == inherited)
                ++inheritedCount;
        }

        return (directCount, inheritedCount);
    }
}

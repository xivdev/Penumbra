using ImSharp;
using Luna;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Services;
using Penumbra.UI.ModsTab.Settings;

namespace Penumbra.UI.ModsTab.Groups;

public sealed class ModGroupDrawer(
    Configuration config,
    CollectionManager collectionManager,
    SingleGroupCombo combo,
    CommunicatorService communicator)
    : IUiService
{
    public readonly SingleGroupCombo      Combo = combo;
    public          bool                  Locked { get; private set; }
    private         bool                  _temporary;
    private         TemporaryModSettings? _tempSettings;
    private         ModSettingContext     _context;

    public void Draw(ModSettingsCache cache, Mod mod, ModSettings settings, TemporaryModSettings? tempSettings)
    {
        if (cache.VisiblePages.Count is 0)
        {
            communicator.PostSettingsPanelDraw.Invoke(new PostSettingsPanelDraw.Arguments(mod));
            return;
        }

        _context      = new ModSettingContext(mod, tempSettings ?? settings);
        _tempSettings = tempSettings;
        _temporary    = tempSettings is not null;
        Locked        = (tempSettings?.Lock ?? 0) > 0;

        if (cache.VisiblePages.Count > 1 && config.DisplayPages)
        {
            Im.Dummy(UiHelpers.DefaultSpace);
            using var tabBar = Im.TabBar.Begin("##pages"u8, TabBarFlags.FittingPolicyScroll);
            if (!tabBar)
                return;

            foreach (var page in cache.VisiblePages)
            {
                using var _       = Im.Id.Push(page.Id);
                using var tabItem = tabBar.Item(page.Name, TabItemFlags.NoPushId);
                if (!tabItem)
                    continue;

                using var child = Im.Child.Begin("##child"u8, false, WindowFlags.NoSavedSettings);
                if (!child)
                    continue;

                DrawPage(page);
            }
        }
        else
        {
            var page = cache.VisiblePages[0];
            DrawPage(page);
        }

        return;

        void DrawPage(ModSettingPage page)
        {
            Im.Dummy(UiHelpers.DefaultSpace);


            using (ImStyleDouble.ItemSpacing.Push(cache.ScaledSpacing))
            {
                using var clipper = new Im.ListClipper(page.Drawing.Count, Im.Style.FrameHeightWithSpacing);

                // The lines are unclipped for simplicity.
                // They still need to be drawn after the clipper initialized, because it moves the cursor.
                foreach (var line in page.VerticalLines)
                    line.Draw(cache);

                foreach (var drawNode in clipper.Iterate(page.Drawing))
                {
                    var cursor = Im.Cursor.Position;
                    drawNode.Draw(this, cache);
                }
            }

            Im.Table.NextColumn();
            UiHelpers.DefaultLineSpace();
            communicator.PostSettingsPanelDraw.Invoke(new PostSettingsPanelDraw.Arguments(mod));
        }
    }

    private ModCollection Current
        => collectionManager.Active.Current;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal Setting GetModSetting(IModGroup group)
    {
        if (_context.Settings.IsEmpty)
            return group.DefaultSettings;

        return _context.Settings.Settings[group.Index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal void SetModSetting(IModGroup group, Setting setting)
    {
        if (_temporary || config.DefaultTemporaryMode)
        {
            _tempSettings                        ??= new TemporaryModSettings(group.Mod, _context.Settings);
            _tempSettings!.ForceInherit          =   false;
            _tempSettings!.Settings[group.Index] =   setting;
            collectionManager.Editor.SetTemporarySettings(Current, group.Mod, _tempSettings);
        }
        else
        {
            collectionManager.Editor.SetModSetting(Current, group.Mod, group.Index, setting);
        }
    }
}

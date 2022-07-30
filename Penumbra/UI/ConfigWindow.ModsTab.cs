using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Collections;
using Penumbra.Mods;
using Penumbra.UI.Classes;
using System;
using System.Linq;
using System.Numerics;
using Dalamud.Logging;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private void DrawModsTab()
    {
        if( !Penumbra.ModManager.Valid )
        {
            return;
        }

        try
        {
            using var tab = ImRaii.TabItem( "Mods" );
            OpenTutorial( BasicTutorialSteps.Mods );
            if( !tab )
            {
                return;
            }

            _selector.Draw( GetModSelectorSize() );
            ImGui.SameLine();
            using var group = ImRaii.Group();
            DrawHeaderLine();

            using var child = ImRaii.Child( "##ModsTabMod", -Vector2.One, true, ImGuiWindowFlags.HorizontalScrollbar );
            if( child )
            {
                _modPanel.Draw( _selector );
            }
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Exception thrown during ModPanel Render:\n{e}" );
            PluginLog.Error( $"{Penumbra.ModManager.Count} Mods\n"
              + $"{Penumbra.CollectionManager.Current.AnonymizedName} Current Collection\n"
              + $"{Penumbra.CollectionManager.Current.Settings.Count} Settings\n"
              + $"{_selector.SortMode.Name} Sort Mode\n"
              + $"{_selector.SelectedLeaf?.Name ?? "NULL"} Selected Leaf\n"
              + $"{_selector.Selected?.Name     ?? "NULL"} Selected Mod\n"
              + $"{string.Join( ", ", Penumbra.CollectionManager.Current.Inheritance.Select(c => c.AnonymizedName) )} Inheritances\n"
              + $"{_selector.SelectedSettingCollection.AnonymizedName} Collection\n" );
        }
    }

    // Draw the header line that can quick switch between collections.
    private void DrawHeaderLine()
    {
        using var style      = ImRaii.PushStyle( ImGuiStyleVar.FrameRounding, 0 ).Push( ImGuiStyleVar.ItemSpacing, Vector2.Zero );
        var       buttonSize = new Vector2( ImGui.GetContentRegionAvail().X / 8f, 0 );

        using( var group = ImRaii.Group() )
        {
            DrawDefaultCollectionButton( 3 * buttonSize );
            ImGui.SameLine();
            DrawInheritedCollectionButton( 3 * buttonSize );
            ImGui.SameLine();
            DrawCollectionSelector( "##collectionSelector", 2 * buttonSize.X, CollectionType.Current, false, null );
        }

        OpenTutorial( BasicTutorialSteps.CollectionSelectors );

        if( !Penumbra.CollectionManager.CurrentCollectionInUse )
        {
            ImGuiUtil.DrawTextButton( "The currently selected collection is not used in any way.", -Vector2.UnitX, Colors.PressEnterWarningBg );
        }
    }

    private static void DrawDefaultCollectionButton( Vector2 width )
    {
        var name      = $"Default Collection ({Penumbra.CollectionManager.Default.Name})";
        var isCurrent = Penumbra.CollectionManager.Default == Penumbra.CollectionManager.Current;
        var isEmpty   = Penumbra.CollectionManager.Default == ModCollection.Empty;
        var tt = isCurrent ? "The current collection is already the configured default collection."
            : isEmpty      ? "The default collection is configured to be empty."
                             : "Set the current collection to the configured default collection.";
        if( ImGuiUtil.DrawDisabledButton( name, width, tt, isCurrent || isEmpty ) )
        {
            Penumbra.CollectionManager.SetCollection( Penumbra.CollectionManager.Default, CollectionType.Current );
        }
    }

    private void DrawInheritedCollectionButton( Vector2 width )
    {
        var noModSelected = _selector.Selected == null;
        var collection    = _selector.SelectedSettingCollection;
        var modInherited  = collection != Penumbra.CollectionManager.Current;
        var (name, tt) = ( noModSelected, modInherited ) switch
        {
            (true, _) => ( "Inherited Collection", "No mod selected." ),
            (false, true) => ( $"Inherited Collection ({collection.Name})",
                "Set the current collection to the collection the selected mod inherits its settings from." ),
            (false, false) => ( "Not Inherited", "The selected mod does not inherit its settings." ),
        };
        if( ImGuiUtil.DrawDisabledButton( name, width, tt, noModSelected || !modInherited ) )
        {
            Penumbra.CollectionManager.SetCollection( collection, CollectionType.Current );
        }
    }

    // Get the correct size for the mod selector based on current config.
    private static float GetModSelectorSize()
    {
        var absoluteSize = Math.Clamp( Penumbra.Config.ModSelectorAbsoluteSize, Configuration.Constants.MinAbsoluteSize,
            Math.Min( Configuration.Constants.MaxAbsoluteSize, ImGui.GetContentRegionAvail().X - 100 ) );
        var relativeSize = Penumbra.Config.ScaleModSelector
            ? Math.Clamp( Penumbra.Config.ModSelectorScaledSize, Configuration.Constants.MinScaledSize, Configuration.Constants.MaxScaledSize )
            : 0;
        return !Penumbra.Config.ScaleModSelector
            ? absoluteSize
            : Math.Max( absoluteSize, relativeSize * ImGui.GetContentRegionAvail().X / 100 );
    }

    // The basic setup for the mod panel.
    // Details are in other files.
    private partial class ModPanel : IDisposable
    {
        private readonly ConfigWindow _window;

        private bool               _valid;
        private ModFileSystem.Leaf _leaf = null!;
        private Mod                _mod  = null!;

        public ModPanel( ConfigWindow window )
            => _window = window;

        public void Dispose()
        {
            _nameFont.Dispose();
        }

        public void Draw( ModFileSystemSelector selector )
        {
            Init( selector );
            if( !_valid )
            {
                return;
            }

            DrawModHeader();
            DrawTabBar();
        }

        private void Init( ModFileSystemSelector selector )
        {
            _valid = selector.Selected != null;
            if( !_valid )
            {
                return;
            }

            _leaf = selector.SelectedLeaf!;
            _mod  = selector.Selected!;
            UpdateSettingsData( selector );
            UpdateModData();
        }

        public void OnSelectionChange( Mod? old, Mod? mod, in ModFileSystemSelector.ModState _ )
        {
            if( old == mod )
            {
                return;
            }

            if( mod == null )
            {
                _window.ModEditPopup.IsOpen = false;
            }
            else if( _window.ModEditPopup.IsOpen )
            {
                _window.ModEditPopup.ChangeMod( mod );
            }

            _currentPriority = null;
            MoveDirectory.Reset();
            OptionTable.Reset();
            Input.Reset();
            AddOptionGroup.Reset();
        }
    }
}
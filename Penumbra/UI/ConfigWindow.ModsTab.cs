using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Collections;
using Penumbra.Mods;
using Penumbra.UI.Classes;
using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using OtterGui.Widgets;
using Penumbra.Api.Enums;

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

            using var style = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing, Vector2.Zero );

            using( var child = ImRaii.Child( "##ModsTabMod", new Vector2( -1, Penumbra.Config.HideRedrawBar ? 0 : -ImGui.GetFrameHeight() ),
                      true, ImGuiWindowFlags.HorizontalScrollbar ) )
            {
                style.Pop();
                if( child )
                {
                    _modPanel.Draw( _selector );
                }

                style.Push( ImGuiStyleVar.ItemSpacing, Vector2.Zero );
            }

            style.Push( ImGuiStyleVar.FrameRounding, 0 );
            DrawRedrawLine();
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Exception thrown during ModPanel Render:\n{e}" );
            Penumbra.Log.Error( $"{Penumbra.ModManager.Count} Mods\n"
              + $"{Penumbra.CollectionManager.Current.AnonymizedName} Current Collection\n"
              + $"{Penumbra.CollectionManager.Current.Settings.Count} Settings\n"
              + $"{_selector.SortMode.Name} Sort Mode\n"
              + $"{_selector.SelectedLeaf?.Name ?? "NULL"} Selected Leaf\n"
              + $"{_selector.Selected?.Name     ?? "NULL"} Selected Mod\n"
              + $"{string.Join( ", ", Penumbra.CollectionManager.Current.Inheritance.Select( c => c.AnonymizedName ) )} Inheritances\n"
              + $"{_selector.SelectedSettingCollection.AnonymizedName} Collection\n" );
        }
    }

    private void DrawRedrawLine()
    {
        if( Penumbra.Config.HideRedrawBar )
        {
            SkipTutorial( BasicTutorialSteps.Redrawing );
            return;
        }

        var frameHeight = new Vector2( 0, ImGui.GetFrameHeight() );
        var frameColor  = ImGui.GetColorU32( ImGuiCol.FrameBg );
        using( var _ = ImRaii.Group() )
        {
            using( var font = ImRaii.PushFont( UiBuilder.IconFont ) )
            {
                ImGuiUtil.DrawTextButton( FontAwesomeIcon.InfoCircle.ToIconString(), frameHeight, frameColor );
                ImGui.SameLine();
            }

            ImGuiUtil.DrawTextButton( "Redraw:        ", frameHeight, frameColor );
        }

        var hovered = ImGui.IsItemHovered();
        OpenTutorial( BasicTutorialSteps.Redrawing );
        if( hovered )
        {
            ImGui.SetTooltip( $"The supported modifiers for '/penumbra redraw' are:\n{SupportedRedrawModifiers}" );
        }

        void DrawButton( Vector2 size, string label, string lower )
        {
            if( ImGui.Button( label, size ) )
            {
                if( lower.Length > 0 )
                {
                    _penumbra.ObjectReloader.RedrawObject( lower, RedrawType.Redraw );
                }
                else
                {
                    _penumbra.ObjectReloader.RedrawAll( RedrawType.Redraw );
                }
            }

            ImGuiUtil.HoverTooltip( lower.Length > 0 ? $"Execute '/penumbra redraw {lower}'." : $"Execute '/penumbra redraw'." );
        }

        using var disabled = ImRaii.Disabled( Dalamud.ClientState.LocalPlayer == null );
        ImGui.SameLine();
        var buttonWidth = frameHeight with { X = ImGui.GetContentRegionAvail().X / 4 };
        DrawButton( buttonWidth, "All", string.Empty );
        ImGui.SameLine();
        DrawButton( buttonWidth, "Self", "self" );
        ImGui.SameLine();
        DrawButton( buttonWidth, "Target", "target" );
        ImGui.SameLine();
        DrawButton( frameHeight with { X = ImGui.GetContentRegionAvail().X - 1 }, "Focus", "focus" );
    }

    // Draw the header line that can quick switch between collections.
    private void DrawHeaderLine()
    {
        using var style      = ImRaii.PushStyle( ImGuiStyleVar.FrameRounding, 0 ).Push( ImGuiStyleVar.ItemSpacing, Vector2.Zero );
        var       buttonSize = new Vector2( ImGui.GetContentRegionAvail().X / 8f, 0 );

        using( var _ = ImRaii.Group() )
        {
            DrawDefaultCollectionButton( 3 * buttonSize );
            ImGui.SameLine();
            DrawInheritedCollectionButton( 3 * buttonSize );
            ImGui.SameLine();
            DrawCollectionSelector( "##collectionSelector", 2 * buttonSize.X, CollectionType.Current, false );
        }

        OpenTutorial( BasicTutorialSteps.CollectionSelectors );

        if( !Penumbra.CollectionManager.CurrentCollectionInUse )
        {
            ImGuiUtil.DrawTextButton( "The currently selected collection is not used in any way.", -Vector2.UnitX, Colors.PressEnterWarningBg );
        }
    }

    private static void DrawDefaultCollectionButton( Vector2 width )
    {
        var name      = $"{DefaultCollection} ({Penumbra.CollectionManager.Default.Name})";
        var isCurrent = Penumbra.CollectionManager.Default == Penumbra.CollectionManager.Current;
        var isEmpty   = Penumbra.CollectionManager.Default == ModCollection.Empty;
        var tt = isCurrent ? $"The current collection is already the configured {DefaultCollection}."
            : isEmpty      ? $"The {DefaultCollection} is configured to be empty."
                             : $"Set the {SelectedCollection} to the configured {DefaultCollection}.";
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

        private          bool               _valid;
        private          ModFileSystem.Leaf _leaf      = null!;
        private          Mod                _mod       = null!;
        private readonly TagButtons         _localTags = new();

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
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using Newtonsoft.Json;
using Penumbra.Models;
using Penumbra.Mods;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class Selector
        {
            private const string LabelSelectorList   = "##availableModList";
            private const string LabelModFilter      = "##ModFilter";
            private const string TooltipModFilter    = "Filter mods for those containing the given substring.";
            private const string TooltipMoveDown     = "Move the selected mod down in priority";
            private const string TooltipMoveUp       = "Move the selected mod up in priority";
            private const string TooltipDelete       = "Delete the selected mod";
            private const string TooltipAdd          = "Add an empty mod";
            private const string DialogDeleteMod     = "PenumbraDeleteMod";
            private const string ButtonYesDelete     = "Yes, delete it";
            private const string ButtonNoDelete      = "No, keep it";
            private const float  SelectorPanelWidth  = 240f;
            private const uint   DisabledModColor    = 0xFF666666;
            private const uint   ConflictingModColor = 0xFFAAAAFF;

            private static readonly Vector2 SelectorButtonSizes = new( 60, 0 );
            private static readonly string  ArrowUpString       = FontAwesomeIcon.ArrowUp.ToIconString();
            private static readonly string  ArrowDownString     = FontAwesomeIcon.ArrowDown.ToIconString();

            private readonly SettingsInterface _base;
            private ModCollection Mods => Service< ModManager >.Get().Mods;

            private ModInfo  _mod;
            private int      _index;
            private int?     _deleteIndex;
            private string   _modFilter = "";
            private string[] _modNamesLower;


            public Selector( SettingsInterface ui )
            {
                _base = ui;
                ResetModNamesLower();
            }

            public void ResetModNamesLower()
            {
                _modNamesLower = Mods?.ModSettings?.Select( I => I.Mod.Meta.Name.ToLowerInvariant() ).ToArray() ?? new string[]{};
            }

            private void DrawPriorityChangeButton( string iconString, bool up, int unavailableWhen )
            {
                ImGui.PushFont( UiBuilder.IconFont );
                if( _index != unavailableWhen )
                {
                    if( ImGui.Button( iconString, SelectorButtonSizes ) )
                    {
                        SetSelection( _index );
                        Service< ModManager >.Get().ChangeModPriority( _mod, up );
                        _modNamesLower.Swap( _index, _index + ( up ? 1 : -1 ) );
                        _index += up ? 1 : -1;
                    }
                }
                else
                {
                    ImGui.PushStyleVar( ImGuiStyleVar.Alpha, 0.5f );
                    ImGui.Button( iconString, SelectorButtonSizes );
                    ImGui.PopStyleVar();
                }

                ImGui.PopFont();

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip(
                        _base._plugin.Configuration.InvertModListOrder ^ up ? TooltipMoveDown : TooltipMoveUp
                    );
                }
            }

            private void DrawModTrashButton()
            {
                ImGui.PushFont( UiBuilder.IconFont );

                if( ImGui.Button( FontAwesomeIcon.Trash.ToIconString(), SelectorButtonSizes ) )
                {
                    _deleteIndex = _index;
                }

                ImGui.PopFont();

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( TooltipDelete );
                }
            }

            private static void DrawModAddButton()
            {
                ImGui.PushFont( UiBuilder.IconFont );

                if( ImGui.Button( FontAwesomeIcon.Plus.ToIconString(), SelectorButtonSizes ) )
                {
                    // Do nothing. YEAH. #TODO.
                }

                ImGui.PopFont();

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( TooltipAdd );
                }
            }

            private void DrawModsSelectorFilter()
            {
                ImGui.SetNextItemWidth( SelectorButtonSizes.X * 4 );
                var tmp = _modFilter;
                if( ImGui.InputText( LabelModFilter, ref tmp, 256 ) )
                {
                    _modFilter = tmp.ToLowerInvariant();
                }

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( TooltipModFilter );
                }
            }

            private void DrawModsSelectorButtons()
            {
                // Selector controls
                ImGui.PushStyleVar( ImGuiStyleVar.WindowPadding, ZeroVector );
                ImGui.PushStyleVar( ImGuiStyleVar.FrameRounding, 0 );

                DrawPriorityChangeButton( ArrowUpString, false, 0 );
                ImGui.SameLine();
                DrawPriorityChangeButton( ArrowDownString, true, Mods?.ModSettings.Count - 1 ?? 0 );
                ImGui.SameLine();
                DrawModTrashButton();
                ImGui.SameLine();
                DrawModAddButton();

                ImGui.PopStyleVar( 3 );
            }

            private void DrawDeleteModal()
            {
                if( _deleteIndex == null )
                {
                    return;
                }

                ImGui.OpenPopup( DialogDeleteMod );

                var ret = ImGui.BeginPopupModal( DialogDeleteMod );
                if( !ret )
                {
                    return;
                }

                if( _mod?.Mod == null )
                {
                    ImGui.CloseCurrentPopup();
                    ImGui.EndPopup();
                    return;
                }

                ImGui.Text( "Are you sure you want to delete the following mod:" );
                // todo: why the fuck does this become null??????
                ImGui.Text( _mod?.Mod?.Meta?.Name );

                if( ImGui.Button( ButtonYesDelete ) )
                {
                    ImGui.CloseCurrentPopup();
                    Service< ModManager >.Get().DeleteMod( _mod.Mod );
                    ClearSelection();
                    _base.ReloadMods();
                }

                ImGui.SameLine();

                if( ImGui.Button( ButtonNoDelete ) )
                {
                    ImGui.CloseCurrentPopup();
                    _deleteIndex = null;
                }

                ImGui.EndPopup();
            }


            public void Draw()
            {
                if( Mods == null )
                {
                    return;
                }

                // Selector pane
                ImGui.BeginGroup();
                ImGui.PushStyleVar( ImGuiStyleVar.ItemSpacing, ZeroVector );

                DrawModsSelectorFilter();

                // Inlay selector list
                ImGui.BeginChild( LabelSelectorList, new Vector2( SelectorPanelWidth, -ImGui.GetFrameHeightWithSpacing() ), true );

                for( var modIndex = 0; modIndex < Mods.ModSettings.Count; modIndex++ )
                {
                    var settings = Mods.ModSettings[ modIndex ];
                    var modName  = settings.Mod.Meta.Name;
                    if( _modFilter.Length > 0 && !_modNamesLower[ modIndex ].Contains( _modFilter ) )
                    {
                        continue;
                    }

                    var changedColour = false;
                    if( !settings.Enabled )
                    {
                        ImGui.PushStyleColor( ImGuiCol.Text, DisabledModColor );
                        changedColour = true;
                    }
                    else if( settings.Mod.FileConflicts.Any() )
                    {
                        ImGui.PushStyleColor( ImGuiCol.Text, ConflictingModColor );
                        changedColour = true;
                    }

#if DEBUG
                    var selected = ImGui.Selectable(
                        $"id={modIndex} {modName}",
                        modIndex == _index
                    );
#else
                    var selected = ImGui.Selectable( modName, modIndex == _index );
#endif

                    if( changedColour )
                    {
                        ImGui.PopStyleColor();
                    }

                    if( selected )
                    {
                        SetSelection( modIndex, settings );
                    }
                }

                ImGui.EndChild();

                DrawModsSelectorButtons();
                ImGui.EndGroup();

                DrawDeleteModal();
            }

            public ModInfo Mod() => _mod;

            private void SetSelection( int idx, ModInfo info )
            {
                _mod = info;
                if( idx != _index )
                {
                    _base._menu.InstalledTab.ModPanel.Details.ResetState();
                }

                _index       = idx;
                _deleteIndex = null;
            }

            private void SetSelection( int idx )
            {
                if( idx >= ( Mods?.ModSettings?.Count ?? 0 ) )
                {
                    idx = -1;
                }

                if( idx < 0 )
                {
                    SetSelection( 0, null );
                }
                else
                {
                    SetSelection( idx, Mods.ModSettings[ idx ] );
                }
            }

            public void ClearSelection() => SetSelection( -1 );

            public void SelectModByName( string name )
            {
                for( var modIndex = 0; modIndex < Mods.ModSettings.Count; modIndex++ )
                {
                    var mod = Mods.ModSettings[ modIndex ];

                    if( mod.Mod.Meta.Name != name )
                    {
                        continue;
                    }

                    SetSelection( modIndex, mod );
                    return;
                }
            }

            private string GetCurrentModMetaFile()
                => _mod == null ? "" : Path.Combine( _mod.Mod.ModBasePath.FullName, "meta.json" );

            public void ReloadCurrentMod()
            {
                var metaPath = GetCurrentModMetaFile();
                if( metaPath.Length > 0 && File.Exists( metaPath ) )
                {
                    _mod.Mod.Meta = ModMeta.LoadFromFile( metaPath ) ?? _mod.Mod.Meta;
                    _base._menu.InstalledTab.ModPanel.Details.ResetState();
                }

                _mod.Mod.RefreshModFiles();
                Service< ModManager >.Get().CalculateEffectiveFileList();
                _base._menu.EffectiveTab.RebuildFileList( _base._plugin.Configuration.ShowAdvanced );
                ResetModNamesLower();
            }

            public string SaveCurrentMod()
            {
                var metaPath = GetCurrentModMetaFile();
                if( metaPath.Length > 0 )
                {
                    File.WriteAllText( metaPath, JsonConvert.SerializeObject( _mod.Mod.Meta, Formatting.Indented ) );
                }

                _base._menu.InstalledTab.ModPanel.Details.ResetState();
                return metaPath;
            }
        }
    }
}
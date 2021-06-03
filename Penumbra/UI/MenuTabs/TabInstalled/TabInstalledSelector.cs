using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Plugin;
using ImGuiNET;
using Newtonsoft.Json;
using Penumbra.Importer;
using Penumbra.Models;
using Penumbra.Mods;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class Selector
        {
            private const string LabelSelectorList  = "##availableModList";
            private const string LabelModFilter     = "##ModFilter";
            private const string LabelPriorityPopup = "Priority";
            private const string LabelAddModPopup   = "AddMod";
            private const string TooltipModFilter   = "Filter mods for those containing the given substring.";
            private const string TooltipMoveDown    = "Move the selected mod down in priority";
            private const string TooltipMoveUp      = "Move the selected mod up in priority";
            private const string TooltipDelete      = "Delete the selected mod";
            private const string TooltipAdd         = "Add an empty mod";
            private const string DialogDeleteMod    = "PenumbraDeleteMod";
            private const string ButtonYesDelete    = "Yes, delete it";
            private const string ButtonNoDelete     = "No, keep it";
            private const string DescPriorityPopup  = "New Priority:";

            private const float SelectorPanelWidth  = 240f;
            private const uint  DisabledModColor    = 0xFF666666;
            private const uint  ConflictingModColor = 0xFFAAAAFF;

            private static readonly Vector2 SelectorButtonSizes = new( 60, 0 );
            private static readonly string  ArrowUpString       = FontAwesomeIcon.ArrowUp.ToIconString();
            private static readonly string  ArrowDownString     = FontAwesomeIcon.ArrowDown.ToIconString();

            private readonly SettingsInterface _base;

            private static ModCollection? Mods
                => Service< ModManager >.Get().Mods;

            public ModInfo? Mod { get; private set; }
            private int       _index;
            private int?      _deleteIndex;
            private string    _modFilter = "";
            private string[]? _modNamesLower;


            public Selector( SettingsInterface ui )
            {
                _base = ui;
                ResetModNamesLower();
            }

            public void ResetModNamesLower()
            {
                _modNamesLower = Mods?.ModSettings?.Where( I => I.Mod != null )
                       .Select( I => I.Mod!.Meta.Name.ToLowerInvariant() ).ToArray()
                 ?? new string[] { };
            }

            private void DrawPriorityChangeButton( string iconString, bool up, int unavailableWhen )
            {
                ImGui.PushFont( UiBuilder.IconFont );
                if( _index != unavailableWhen )
                {
                    if( ImGui.Button( iconString, SelectorButtonSizes ) )
                    {
                        SetSelection( _index );
                        Service< ModManager >.Get().ChangeModPriority( Mod!, up );
                        _modNamesLower!.Swap( _index, _index + ( up ? 1 : -1 ) );
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
                        _base._plugin!.Configuration!.InvertModListOrder ^ up ? TooltipMoveDown : TooltipMoveUp
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

            private bool _keyboardFocus = true;

            private void DrawModAddButton()
            {
                if( ImGui.BeginPopupContextItem( LabelAddModPopup ) )
                {
                    if( _keyboardFocus )
                    {
                        ImGui.SetKeyboardFocusHere();
                        _keyboardFocus = false;
                    }

                    var newName = "";
                    if( ImGui.InputTextWithHint( "##AddMod", "New Mod Name...", ref newName, 64, ImGuiInputTextFlags.EnterReturnsTrue ) )
                    {
                        try
                        {
                            var newDir = TexToolsImport.CreateModFolder( new DirectoryInfo( _base._plugin.Configuration!.CurrentCollection ),
                                newName );
                            var modMeta = new ModMeta
                            {
                                Author      = "Unknown",
                                Name        = newName,
                                Description = string.Empty,
                            };
                            var metaPath = Path.Combine( newDir.FullName, "meta.json" );
                            File.WriteAllText( metaPath, JsonConvert.SerializeObject( modMeta, Formatting.Indented ) );
                            _base.ReloadMods();
                            SelectModByDir( newDir.Name );
                        }
                        catch( Exception e )
                        {
                            PluginLog.Error( $"Could not create directory for new Mod {newName}:\n{e}" );
                        }

                        ImGui.CloseCurrentPopup();
                    }

                    if( ImGui.IsKeyPressed( ImGui.GetKeyIndex( ImGuiKey.Escape ) ) )
                    {
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndPopup();
                }

                ImGui.PushFont( UiBuilder.IconFont );

                if( ImGui.Button( FontAwesomeIcon.Plus.ToIconString(), SelectorButtonSizes ) )
                {
                    _keyboardFocus = true;
                    ImGui.OpenPopup( LabelAddModPopup );
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
                if( ImGui.InputTextWithHint( LabelModFilter, "Filter Mods...", ref tmp, 256 ) )
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
                DrawPriorityChangeButton( ArrowDownString, true, Mods?.ModSettings?.Count - 1 ?? 0 );
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

                var _ = true;
                ImGui.SetNextWindowPos( ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, Vector2.One / 2 );
                var ret = ImGui.BeginPopupModal( DialogDeleteMod, ref _, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration );
                if( !ret )
                {
                    return;
                }

                if( Mod?.Mod == null )
                {
                    ImGui.CloseCurrentPopup();
                    ImGui.EndPopup();
                    return;
                }

                ImGui.Text( "Are you sure you want to delete the following mod:" );
                // todo: why the fuck does this become null??????
                ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );
                ImGui.TextColored( new Vector4( 0.7f, 0.1f, 0.1f, 1 ), Mod?.Mod?.Meta?.Name ?? "Unknown" );
                ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() ) / 2 );

                var buttonSize = new Vector2( 120, 0 );
                if( ImGui.Button( ButtonYesDelete, buttonSize ) )
                {
                    ImGui.CloseCurrentPopup();
                    Service< ModManager >.Get().DeleteMod( Mod?.Mod );
                    ClearSelection();
                    _base.ReloadMods();
                }

                ImGui.SameLine();

                if( ImGui.Button( ButtonNoDelete, buttonSize ) )
                {
                    ImGui.CloseCurrentPopup();
                    _deleteIndex = null;
                }

                ImGui.EndPopup();
            }

            private int _priorityPopupIdx = 0;

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

                if( Mods.ModSettings != null )
                {
                    for( var modIndex = 0; modIndex < Mods.ModSettings.Count; modIndex++ )
                    {
                        var settings = Mods.ModSettings[ modIndex ];
                        var modName  = settings.Mod.Meta.Name;
                        if( _modFilter.Length > 0 && !_modNamesLower![ modIndex ].Contains( _modFilter ) )
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
                        if( ImGui.IsItemClicked( ImGuiMouseButton.Right ) )
                        {
                            if( ImGui.IsPopupOpen( LabelPriorityPopup ) )
                            {
                                ImGui.CloseCurrentPopup();
                            }

                            _priorityPopupIdx = modIndex;
                            _keyboardFocus    = true;
                            ImGui.OpenPopup( LabelPriorityPopup );
                        }

                        ImGui.OpenPopupOnItemClick( LabelPriorityPopup, ImGuiPopupFlags.MouseButtonRight );

                        if( changedColour )
                        {
                            ImGui.PopStyleColor();
                        }

                        if( selected )
                        {
                            SetSelection( modIndex, settings );
                        }
                    }
                }

                ImGui.EndChild();

                DrawModsSelectorButtons();
                ImGui.EndGroup();

                DrawDeleteModal();
                DrawPriorityPopup();
            }

            private void DrawPriorityPopup()
            {
                if( !ImGui.BeginPopupContextItem( LabelPriorityPopup ) )
                {
                    return;
                }

                var size = ImGui.CalcTextSize( DescPriorityPopup ).X;
                //ImGui.Text( DescPriorityPopup );
                var newPriority = _priorityPopupIdx;

                if( _keyboardFocus )
                {
                    ImGui.SetKeyboardFocusHere( -1 );
                    _keyboardFocus = false;
                }

                ImGui.SetNextItemWidth( size );
                if( ImGui.InputInt( "New Priority", ref newPriority, 0, 0,
                        ImGuiInputTextFlags.EnterReturnsTrue )
                 && newPriority != _priorityPopupIdx )
                {
                    Service< ModManager >.Get().ChangeModPriority( Mods!.ModSettings![ _priorityPopupIdx ], newPriority );
                    ResetModNamesLower();
                    if( _priorityPopupIdx == _index )
                    {
                        _index = newPriority;
                        SetSelection( _index );
                    }

                    ImGui.CloseCurrentPopup();
                }

                if( ImGui.IsKeyPressed( ImGui.GetKeyIndex( ImGuiKey.Escape ) ) )
                {
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }


            private void SetSelection( int idx, ModInfo? info )
            {
                Mod = info;
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
                    SetSelection( idx, Mods!.ModSettings![ idx ] );
                }
            }

            public void ClearSelection()
                => SetSelection( -1 );

            public void SelectModByName( string name )
            {
                var idx = Mods?.ModSettings?.FindIndex( mod => mod.Mod.Meta.Name == name ) ?? -1;
                SetSelection( idx );
            }

            public void SelectModByDir( string name )
            {
                var idx = Mods?.ModSettings?.FindIndex( mod => mod.FolderName == name ) ?? -1;
                SetSelection( idx );
            }

            private string GetCurrentModMetaFile()
                => Mod == null ? "" : Path.Combine( Mod.Mod.ModBasePath.FullName, "meta.json" );

            public void ReloadCurrentMod()
            {
                var metaPath = GetCurrentModMetaFile();
                if( metaPath.Length > 0 && File.Exists( metaPath ) )
                {
                    Mod!.Mod.Meta = ModMeta.LoadFromFile( metaPath ) ?? Mod.Mod.Meta;
                    _base._menu.InstalledTab.ModPanel.Details.ResetState();
                }

                Mod!.Mod.RefreshModFiles();
                Service< ModManager >.Get().CalculateEffectiveFileList();
                ResetModNamesLower();
            }

            public string SaveCurrentMod()
            {
                if( Mod == null )
                {
                    return "";
                }

                var metaPath = GetCurrentModMetaFile();
                if( metaPath.Length > 0 )
                {
                    File.WriteAllText( metaPath, JsonConvert.SerializeObject( Mod.Mod.Meta, Formatting.Indented ) );
                }

                _base._menu.InstalledTab.ModPanel.Details.ResetState();
                return metaPath;
            }
        }
    }
}
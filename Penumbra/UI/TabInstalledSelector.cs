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
            private const    string  LabelSelectorList   = "##availableModList";
            private const    string  TooltipMoveDown     = "Move the selected mod down in priority";
            private const    string  TooltipMoveUp       = "Move the selected mod up in priority";
            private const    string  TooltipDelete       = "Delete the selected mod";
            private const    string  TooltipAdd          = "Add an empty mod";
            private const    string  DialogDeleteMod     = "PenumbraDeleteMod";
            private const    string  ButtonYesDelete     = "Yes, delete it";
            private const    string  ButtonNoDelete      = "No, keep it";
            private const    float   SelectorPanelWidth  = 240f;
            private const    uint    DisabledModColor    = 0xFF666666;
            private const    uint    ConflictingModColor = 0xFFAAAAFF;

            private static readonly Vector2 SelectorButtonSizes = new(60, 0);
            private static readonly string  ArrowUpString       = FontAwesomeIcon.ArrowUp.ToIconString();
            private static readonly string  ArrowDownString     = FontAwesomeIcon.ArrowDown.ToIconString();

            private readonly SettingsInterface _base;
            private ModCollection Mods{ get{ return _base._plugin.ModManager.Mods; } }

            private ModInfo _mod         = null;
            private int     _index       = 0;
            private int?    _deleteIndex = null;

            public Selector(SettingsInterface ui)
            {
                _base = ui;
            }

            private void DrawPriorityChangeButton(string iconString, bool up, int unavailableWhen)
            {
                ImGui.PushFont( UiBuilder.IconFont );
                if( _index != unavailableWhen )
                {
                    if( ImGui.Button( iconString, SelectorButtonSizes ) )
                    {
                        SetSelection(_index);
                        _base._plugin.ModManager.ChangeModPriority( _mod, up );
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
                    ImGui.SetTooltip( TooltipDelete );
            }

            private void DrawModAddButton()
            {
                ImGui.PushFont( UiBuilder.IconFont );

                if( ImGui.Button( FontAwesomeIcon.Plus.ToIconString(), SelectorButtonSizes ) )
                {
                    // Do nothing. YEAH. #TODO.
                }

                ImGui.PopFont();

                if( ImGui.IsItemHovered() )
                    ImGui.SetTooltip( TooltipAdd );
            }

            private void DrawModsSelectorButtons()
            {
                // Selector controls
                ImGui.PushStyleVar( ImGuiStyleVar.WindowPadding, ZeroVector );
                ImGui.PushStyleVar( ImGuiStyleVar.FrameRounding, 0 );

                DrawPriorityChangeButton(ArrowUpString,   false, 0);
                ImGui.SameLine();
                DrawPriorityChangeButton(ArrowDownString, true, Mods?.ModSettings.Count - 1 ?? 0);
                ImGui.SameLine();
                DrawModTrashButton();
                ImGui.SameLine();
                DrawModAddButton();

                ImGui.PopStyleVar( 3 );
            }

            void DrawDeleteModal()
            {
                if( _deleteIndex == null )
                    return;

                ImGui.OpenPopup( DialogDeleteMod );

                var ret = ImGui.BeginPopupModal( DialogDeleteMod );
                if( !ret )
                    return;

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
                    _base._plugin.ModManager.DeleteMod( _mod.Mod );
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
                if (Mods == null)
                    return;

                // Selector pane
                ImGui.BeginGroup();
                ImGui.PushStyleVar( ImGuiStyleVar.ItemSpacing, ZeroVector );

                // Inlay selector list
                ImGui.BeginChild( LabelSelectorList, new Vector2(SelectorPanelWidth, -ImGui.GetFrameHeightWithSpacing() ), true );

                for( var modIndex = 0; modIndex < Mods.ModSettings.Count; modIndex++ )
                {
                    var settings = Mods.ModSettings[ modIndex ];

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
                        $"id={modIndex} {settings.Mod.Meta.Name}",
                        modIndex == _index
                    );
#else
                    var selected = ImGui.Selectable( settings.Mod.Meta.Name, modIndex == _index );
#endif

                    if( changedColour )
                        ImGui.PopStyleColor();

                    if( selected )
                        SetSelection(modIndex, settings);
                }

                ImGui.EndChild();

                DrawModsSelectorButtons();
                ImGui.EndGroup();

                DrawDeleteModal();
            }

            public ModInfo Mod() => _mod;

            private void SetSelection(int idx, ModInfo info)
            {
                _mod         = info;
                if (idx != _index)
                    _base._menu._installedTab._modPanel._details.ResetState();
                _index       = idx;
                _deleteIndex = null;
            }

            public void SetSelection(int idx)
            {
                if (idx >= (Mods?.ModSettings?.Count ?? 0))
                    idx = -1;
                if (idx < 0)
                    SetSelection(0, null);
                else
                    SetSelection(idx, Mods.ModSettings[idx]);
            }

            public void ClearSelection() => SetSelection(-1);

            public void SelectModByName( string name )
            {
                for( var modIndex = 0; modIndex < Mods.ModSettings.Count; modIndex++ )
                {
                    var mod = Mods.ModSettings[ modIndex ];

                    if( mod.Mod.Meta.Name != name )
                        continue;

                    SetSelection(modIndex, mod);
                    return;
                }
            }

            private string GetCurrentModMetaFile()
            {
                if( _mod == null )
                    return "";
                return Path.Combine( _mod.Mod.ModBasePath.FullName, "meta.json" );
            }

            public void ReloadCurrentMod()
            {
                var metaPath = GetCurrentModMetaFile();
                if (metaPath.Length > 0 && File.Exists(metaPath))
                {
                    _mod.Mod.Meta = ModMeta.LoadFromFile(metaPath) ?? _mod.Mod.Meta;
                    _base._menu._installedTab._modPanel._details.ResetState();
                }
                _mod.Mod.RefreshModFiles();
                _base._plugin.ModManager.CalculateEffectiveFileList();
            }

            public string SaveCurrentMod()
            {
                var metaPath = GetCurrentModMetaFile();
                if (metaPath.Length > 0)
                    File.WriteAllText( metaPath, JsonConvert.SerializeObject( _mod.Mod.Meta, Formatting.Indented ) );
                _base._menu._installedTab._modPanel._details.ResetState();
                return metaPath;
            }
        }
    }
}
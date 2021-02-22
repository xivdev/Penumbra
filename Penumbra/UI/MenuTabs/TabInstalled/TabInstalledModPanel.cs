using System;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Plugin;
using ImGuiNET;
using Penumbra.Models;
using Penumbra.Mods;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class ModPanel
        {
            private const string LabelModPanel        = "selectedModInfo";
            private const string LabelEditName        = "##editName";
            private const string LabelEditVersion     = "##editVersion";
            private const string LabelEditAuthor      = "##editAuthor";
            private const string LabelEditWebsite     = "##editWebsite";
            private const string LabelModEnabled      = "Enabled";
            private const string LabelEditingEnabled  = "Enable Editing";
            private const string ButtonOpenWebsite    = "Open Website";
            private const string ButtonOpenModFolder  = "Open Mod Folder";
            private const string ButtonEditJson       = "Edit JSON";
            private const string ButtonReloadJson     = "Reload JSON";
            private const string ButtonDeduplicate    = "Deduplicate";
            private const string TooltipOpenModFolder = "Open the directory containing this mod in your default file explorer.";
            private const string TooltipEditJson      = "Open the JSON configuration file in your default application for .json.";
            private const string TooltipReloadJson    = "Reload the configuration of all mods.";

            private const string TooltipDeduplicate =
                "Try to find identical files and remove duplicate occurences to reduce the mods disk size.\n" +
                "Introduces an invisible single-option Group \"Duplicates\".";

            private const           float   HeaderLineDistance = 10f;
            private static readonly Vector4 GreyColor          = new( 1f, 1f, 1f, 0.66f );

            private readonly SettingsInterface _base;
            private readonly Selector          _selector;
            public readonly  PluginDetails     Details;

            private bool   _editMode;
            private string _currentWebsite;
            private bool   _validWebsite;

            public ModPanel( SettingsInterface ui, Selector s )
            {
                _base           = ui;
                _selector       = s;
                Details         = new PluginDetails( _base, _selector );
                _currentWebsite = Meta?.Website ?? "";
            }

            private ModInfo? Mod => _selector.Mod();
            private ModMeta? Meta => Mod?.Mod.Meta;

            private void DrawName()
            {
                var name = Meta!.Name;
                if( ImGuiCustom.InputOrText( _editMode, LabelEditName, ref name, 64 )
                    && name.Length > 0 && name != Meta.Name )
                {
                    Meta.Name = name;
                    _selector.SaveCurrentMod();
                }
            }

            private void DrawVersion()
            {
                if( _editMode )
                {
                    ImGui.BeginGroup();
                    ImGui.Text( "(Version " );

                    ImGui.PushStyleVar( ImGuiStyleVar.ItemSpacing, ZeroVector );
                    ImGui.SameLine();
                    var version = Meta!.Version;
                    if( ImGuiCustom.ResizingTextInput( LabelEditVersion, ref version, 16 )
                        && version != Meta.Version )
                    {
                        Meta.Version = version;
                        _selector.SaveCurrentMod();
                    }

                    ImGui.SameLine();
                    ImGui.Text( ")" );
                    ImGui.PopStyleVar();
                    ImGui.EndGroup();
                }
                else if( Meta!.Version.Length > 0 )
                {
                    ImGui.Text( $"(Version {Meta.Version})" );
                }
            }

            private void DrawAuthor()
            {
                ImGui.BeginGroup();
                ImGui.TextColored( GreyColor, "by" );

                ImGui.SameLine();
                var author = Meta!.Author;
                if( ImGuiCustom.InputOrText( _editMode, LabelEditAuthor, ref author, 64 )
                    && author != Meta.Author )
                {
                    Meta.Author = author;
                    _selector.SaveCurrentMod();
                }

                ImGui.EndGroup();
            }

            private void DrawWebsite()
            {
                ImGui.BeginGroup();
                if( _editMode )
                {
                    ImGui.TextColored( GreyColor, "from" );
                    ImGui.SameLine();
                    var website = Meta!.Website;
                    if( ImGuiCustom.ResizingTextInput( LabelEditWebsite, ref website, 512 )
                        && website != Meta.Website )
                    {
                        Meta.Website = website;
                        _selector.SaveCurrentMod();
                    }
                }
                else if( Meta!.Website.Length > 0 )
                {
                    if( _currentWebsite != Meta.Website )
                    {
                        _currentWebsite = Meta.Website;
                        _validWebsite = Uri.TryCreate( Meta.Website, UriKind.Absolute, out var uriResult )
                            && ( uriResult.Scheme == Uri.UriSchemeHttps || uriResult.Scheme == Uri.UriSchemeHttp );
                    }

                    if( _validWebsite )
                    {
                        if( ImGui.SmallButton( ButtonOpenWebsite ) )
                        {
                            try
                            {
                                var process = new ProcessStartInfo( Meta.Website )
                                {
                                    UseShellExecute = true
                                };
                                Process.Start( process );
                            }
                            catch( System.ComponentModel.Win32Exception )
                            {
                                // Do nothing.
                            }
                        }

                        if( ImGui.IsItemHovered() )
                        {
                            ImGui.SetTooltip( Meta.Website );
                        }
                    }
                    else
                    {
                        ImGui.TextColored( GreyColor, "from" );
                        ImGui.SameLine();
                        ImGui.Text( Meta.Website );
                    }
                }

                ImGui.EndGroup();
            }

            private void DrawHeaderLine()
            {
                DrawName();
                ImGui.SameLine();
                DrawVersion();
                ImGui.SameLine();
                DrawAuthor();
                ImGui.SameLine();
                DrawWebsite();
            }

            private void DrawEnabledMark()
            {
                var enabled = Mod!.Enabled;
                if( ImGui.Checkbox( LabelModEnabled, ref enabled ) )
                {
                    Mod.Enabled = enabled;
                    var modManager = Service< ModManager >.Get();
                    modManager.Mods!.Save();
                    modManager.CalculateEffectiveFileList();
                    _base._menu.EffectiveTab.RebuildFileList( _base._plugin!.Configuration!.ShowAdvanced );
                }
            }

            private void DrawEditableMark()
            {
                ImGui.Checkbox( LabelEditingEnabled, ref _editMode );
            }

            private void DrawOpenModFolderButton()
            {
                if( ImGui.Button( ButtonOpenModFolder ) )
                {
                    Process.Start( Mod!.Mod.ModBasePath.FullName );
                }

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( TooltipOpenModFolder );
                }
            }

            private void DrawEditJsonButton()
            {
                if( ImGui.Button( ButtonEditJson ) )
                {
                    Process.Start( _selector.SaveCurrentMod() );
                }

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( TooltipEditJson );
                }
            }

            private void DrawReloadJsonButton()
            {
                if( ImGui.Button( ButtonReloadJson ) )
                {
                    _selector.ReloadCurrentMod();
                }

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( TooltipReloadJson );
                }
            }

            private void DrawDeduplicateButton()
            {
                if( ImGui.Button( ButtonDeduplicate ) )
                {
                    new Deduplicator( Mod!.Mod.ModBasePath, Meta! ).Run();
                    _selector.SaveCurrentMod();
                    Mod.Mod.RefreshModFiles();
                    Service< ModManager >.Get().CalculateEffectiveFileList();
                    _base._menu.EffectiveTab.RebuildFileList( _base._plugin!.Configuration!.ShowAdvanced );
                }

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( TooltipDeduplicate );
                }
            }

            private void DrawEditLine()
            {
                DrawOpenModFolderButton();
                ImGui.SameLine();
                DrawEditJsonButton();
                ImGui.SameLine();
                DrawReloadJsonButton();
                ImGui.SameLine();
                DrawDeduplicateButton();
            }

            public void Draw()
            {
                if( Mod == null )
                {
                    return;
                }

                try
                {
                    var ret = ImGui.BeginChild( LabelModPanel, AutoFillSize, true );
                    if( !ret )
                    {
                        return;
                    }

                    DrawHeaderLine();

                    // Next line with fixed distance.
                    ImGuiCustom.VerticalDistance( HeaderLineDistance );

                    DrawEnabledMark();
                    if( _base._plugin!.Configuration!.ShowAdvanced )
                    {
                        ImGui.SameLine();
                        DrawEditableMark();
                    }

                    // Next line, if editable.
                    if( _editMode )
                    {
                        DrawEditLine();
                    }

                    Details.Draw( _editMode );

                    ImGui.EndChild();
                }
                catch( Exception ex )
                {
                    PluginLog.LogError( ex, "fuck" );
                }
            }
        }
    }
}
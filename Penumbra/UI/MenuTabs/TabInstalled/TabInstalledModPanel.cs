using System;
using System.Diagnostics;
using System.IO;
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
            private const string LabelModPanel          = "selectedModInfo";
            private const string LabelEditName          = "##editName";
            private const string LabelEditVersion       = "##editVersion";
            private const string LabelEditAuthor        = "##editAuthor";
            private const string LabelEditWebsite       = "##editWebsite";
            private const string LabelModEnabled        = "Enabled";
            private const string LabelEditingEnabled    = "Enable Editing";
            private const string ButtonOpenWebsite      = "Open Website";
            private const string ButtonOpenModFolder    = "Open Mod Folder";
            private const string ButtonRenameModFolder  = "Rename Mod Folder";
            private const string ButtonEditJson         = "Edit JSON";
            private const string ButtonReloadJson       = "Reload JSON";
            private const string ButtonDeduplicate      = "Deduplicate";
            private const string TooltipOpenModFolder   = "Open the directory containing this mod in your default file explorer.";
            private const string TooltipRenameModFolder = "Rename the directory containing this mod without opening another application.";
            private const string TooltipEditJson        = "Open the JSON configuration file in your default application for .json.";
            private const string TooltipReloadJson      = "Reload the configuration of all mods.";
            private const string PopupRenameFolder      = "Rename Folder";

            private const string TooltipDeduplicate =
                "Try to find identical files and remove duplicate occurences to reduce the mods disk size.\n"
              + "Introduces an invisible single-option Group \"Duplicates\".";

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

            private ModInfo? Mod
                => _selector.Mod;

            private ModMeta? Meta
                => Mod?.Mod.Meta;

            private void DrawName()
            {
                var name = Meta!.Name;
                if( ImGuiCustom.InputOrText( _editMode, LabelEditName, ref name, 64 )
                 && name.Length > 0
                 && name        != Meta.Name )
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
                                    UseShellExecute = true,
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

            private string _newName       = "";
            private bool   _keyboardFocus = true;

            private void DrawRenameModFolderButton()
            {
                var _ = true;
                _keyboardFocus |= !ImGui.IsPopupOpen( PopupRenameFolder );

                ImGui.SetNextWindowPos( ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2( 0.5f, 1f ) );
                if( ImGui.BeginPopupModal( PopupRenameFolder, ref _, ImGuiWindowFlags.AlwaysAutoResize ) )
                {
                    if( ImGui.IsKeyPressed( ImGui.GetKeyIndex( ImGuiKey.Escape ) ) )
                    {
                        ImGui.CloseCurrentPopup();
                    }

                    var newName = Mod!.FolderName;

                    if( _keyboardFocus )
                    {
                        PluginLog.Log( "Fuck you" );
                        ImGui.SetKeyboardFocusHere();
                        _keyboardFocus = false;
                    }

                    if( ImGui.InputText( "New Folder Name##RenameFolderInput", ref newName, 64, ImGuiInputTextFlags.EnterReturnsTrue ) )
                    {
                        _newName = newName.RemoveNonAsciiSymbols().RemoveInvalidPathSymbols();
                        if( _newName.Length == 0 )
                        {
                            ImGui.CloseCurrentPopup();
                        }
                        else if( !string.Equals( _newName, Mod!.FolderName, StringComparison.InvariantCultureIgnoreCase ) )
                        {
                            DirectoryInfo dir    = Mod!.Mod.ModBasePath;
                            DirectoryInfo newDir = new( Path.Combine( dir.Parent!.FullName, _newName ) );
                            if( newDir.Exists )
                            {
                                PluginLog.Error( "GOTT" );
                                ImGui.OpenPopup( "OverwriteDir" );
                            }
                            else
                            {
                                try
                                {
                                    dir.MoveTo( newDir.FullName );
                                }
                                catch( Exception e )
                                {
                                    PluginLog.Error( $"Error while renaming directory {dir.FullName} to {newDir.FullName}:\n{e}" );
                                }

                                Mod!.FolderName      = _newName;
                                Mod!.Mod.ModBasePath = newDir;
                                _selector.ReloadCurrentMod();
                                Service< ModManager >.Get()!.Mods!.Save();
                                ImGui.CloseCurrentPopup();
                            }
                        }
                    }

                    ImGui.TextColored( GreyColor,
                        "Please restrict yourself to ascii symbols that are valid in a windows path,\nother symbols will be replaced by underscores." );

                    var closeParent = false;
                    _ = true;
                    ImGui.SetNextWindowPos( ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, Vector2.One / 2 );
                    if( ImGui.BeginPopupModal( "OverwriteDir", ref _, ImGuiWindowFlags.AlwaysAutoResize ) )
                    {
                        DirectoryInfo dir    = Mod!.Mod.ModBasePath;
                        DirectoryInfo newDir = new( Path.Combine( dir.Parent!.FullName, _newName ) );
                        ImGui.Text(
                            $"The mod directory {newDir} already exists.\nDo you want to merge / overwrite both mods?\nThis may corrupt the resulting mod in irrecoverable ways." );
                        var buttonSize = new Vector2( 120, 0 );
                        if( ImGui.Button( "Yes", buttonSize ) )
                        {
                            try
                            {
                                foreach( var file in dir.EnumerateFiles( "*", SearchOption.AllDirectories ) )
                                {
                                    var target = new FileInfo( Path.Combine( newDir.FullName,
                                        file.FullName.Substring( dir.FullName.Length ) ) );
                                    if( target.Exists )
                                    {
                                        target.Delete();
                                    }

                                    target.Directory?.Create();
                                    file.MoveTo( target.FullName );
                                }

                                dir.Delete( true );

                                var mod = Service< ModManager >.Get()!.Mods!.ModSettings!
                                   .RemoveAll( m => m.FolderName == _newName );

                                Mod!.FolderName      = _newName;
                                Mod!.Mod.ModBasePath = newDir;
                                Service< ModManager >.Get()!.Mods!.Save();
                                _base.ReloadMods();
                                _selector.SelectModByDir( _newName );
                            }
                            catch( Exception e )
                            {
                                PluginLog.Error( $"Error while renaming directory {dir.FullName} to {newDir.FullName}:\n{e}" );
                            }

                            closeParent = true;
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.SameLine();

                        if( ImGui.Button( "Cancel", buttonSize ) )
                        {
                            PluginLog.Error( "FUCKFUCK" );
                            _keyboardFocus = true;
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.EndPopup();
                    }

                    if( closeParent )
                    {
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndPopup();
                }

                if( ImGui.Button( ButtonRenameModFolder ) )
                {
                    ImGui.OpenPopup( PopupRenameFolder );
                }

                if( ImGui.IsItemHovered() )
                {
                    ImGui.SetTooltip( TooltipRenameModFolder );
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
                DrawRenameModFolderButton();
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
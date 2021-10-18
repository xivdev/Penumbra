using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Logging;
using ImGuiNET;
using Penumbra.Mod;
using Penumbra.Mods;
using Penumbra.UI.Custom;
using Penumbra.Util;

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
            private const string LabelOverWriteDir      = "OverwriteDir";
            private const string ButtonOpenWebsite      = "Open Website";
            private const string ButtonOpenModFolder    = "Open Mod Folder";
            private const string ButtonRenameModFolder  = "Rename Mod Folder";
            private const string ButtonEditJson         = "Edit JSON";
            private const string ButtonReloadJson       = "Reload JSON";
            private const string ButtonDeduplicate      = "Deduplicate";
            private const string ButtonNormalize        = "Normalize";
            private const string TooltipOpenModFolder   = "Open the directory containing this mod in your default file explorer.";
            private const string TooltipRenameModFolder = "Rename the directory containing this mod without opening another application.";
            private const string TooltipEditJson        = "Open the JSON configuration file in your default application for .json.";
            private const string TooltipReloadJson      = "Reload the configuration of all mods.";
            private const string PopupRenameFolder      = "Rename Folder";

            private const string TooltipDeduplicate =
                "Try to find identical files and remove duplicate occurences to reduce the mods disk size.\n"
              + "Introduces an invisible single-option Group \"Duplicates\".\nExperimental - use at own risk!";

            private const string TooltipNormalize =
                "Try to reduce unnecessary options or subdirectories to default options if possible.\nExperimental - use at own risk!";

            private const           float   HeaderLineDistance = 10f;
            private static readonly Vector4 GreyColor          = new( 1f, 1f, 1f, 0.66f );

            private readonly SettingsInterface _base;
            private readonly Selector          _selector;
            private readonly ModManager        _modManager;
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
                _modManager     = Service< ModManager >.Get();
            }

            private Mod.Mod? Mod
                => _selector.Mod;

            private ModMeta? Meta
                => Mod?.Data.Meta;

            private void DrawName()
            {
                var name = Meta!.Name;
                if( ImGuiCustom.InputOrText( _editMode, LabelEditName, ref name, 64 ) && _modManager.RenameMod( name, Mod!.Data ) )
                {
                    _selector.SelectModOnUpdate( Mod.Data.BasePath.Name );
                    if( !_modManager.Config.ModSortOrder.ContainsKey( Mod!.Data.BasePath.Name ) )
                    {
                        Mod.Data.Rename( name );
                    }
                }
            }

            private void DrawVersion()
            {
                if( _editMode )
                {
                    ImGui.BeginGroup();
                    using var raii = ImGuiRaii.DeferredEnd( ImGui.EndGroup );
                    ImGui.Text( "(Version " );

                    using var style = ImGuiRaii.PushStyle( ImGuiStyleVar.ItemSpacing, ZeroVector );
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
                    _selector.Cache.TriggerFilterReset();
                }

                ImGui.EndGroup();
            }

            private void DrawWebsite()
            {
                ImGui.BeginGroup();
                using var raii = ImGuiRaii.DeferredEnd( ImGui.EndGroup );
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

                        ImGuiCustom.HoverTooltip( Meta.Website );
                    }
                    else
                    {
                        ImGui.TextColored( GreyColor, "from" );
                        ImGui.SameLine();
                        ImGui.Text( Meta.Website );
                    }
                }
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

            private void DrawPriority()
            {
                var priority = Mod!.Settings.Priority;
                ImGui.SetNextItemWidth( 50 * ImGuiHelpers.GlobalScale );
                if( ImGui.InputInt( "Priority", ref priority, 0 ) && priority != Mod!.Settings.Priority )
                {
                    Mod.Settings.Priority = priority;
                    _base.SaveCurrentCollection( Mod.Data.Resources.MetaManipulations.Count > 0 );
                    _selector.Cache.TriggerFilterReset();
                }

                ImGuiCustom.HoverTooltip(
                    "Higher priority mods take precedence over other mods in the case of file conflicts.\n"
                  + "In case of identical priority, the alphabetically first mod takes precedence." );
            }

            private void DrawEnabledMark()
            {
                var enabled = Mod!.Settings.Enabled;
                if( ImGui.Checkbox( LabelModEnabled, ref enabled ) )
                {
                    Mod.Settings.Enabled = enabled;
                    if( !enabled )
                    {
                        Mod.Cache.ClearConflicts();
                    }

                    _base.SaveCurrentCollection( Mod.Data.Resources.MetaManipulations.Count > 0 );
                    _selector.Cache.TriggerFilterReset();
                }
            }

            public static bool DrawSortOrder( ModData mod, ModManager manager, Selector selector )
            {
                var currentSortOrder = mod.SortOrder.FullPath;
                ImGui.SetNextItemWidth( 300 * ImGuiHelpers.GlobalScale );
                if( ImGui.InputText( "Sort Order", ref currentSortOrder, 256, ImGuiInputTextFlags.EnterReturnsTrue ) )
                {
                    manager.ChangeSortOrder( mod, currentSortOrder );
                    selector.SelectModOnUpdate( mod.BasePath.Name );
                    return true;
                }

                return false;
            }

            private void DrawEditableMark()
            {
                ImGui.Checkbox( LabelEditingEnabled, ref _editMode );
            }

            private void DrawOpenModFolderButton()
            {
                Mod!.Data.BasePath.Refresh();
                if( ImGui.Button( ButtonOpenModFolder ) && Mod.Data.BasePath.Exists )
                {
                    Process.Start( new ProcessStartInfo( Mod!.Data.BasePath.FullName ) { UseShellExecute = true } );
                }

                ImGuiCustom.HoverTooltip( TooltipOpenModFolder );
            }

            private string _newName       = "";
            private bool   _keyboardFocus = true;

            private void RenameModFolder( string newName )
            {
                _newName = newName.ReplaceBadXivSymbols();
                if( _newName.Length == 0 )
                {
                    PluginLog.Debug( "New Directory name {NewName} was empty after removing invalid symbols.", newName );
                    ImGui.CloseCurrentPopup();
                }
                else if( !string.Equals( _newName, Mod!.Data.BasePath.Name, StringComparison.InvariantCultureIgnoreCase ) )
                {
                    DirectoryInfo dir    = Mod!.Data.BasePath;
                    DirectoryInfo newDir = new( Path.Combine( dir.Parent!.FullName, _newName ) );

                    if( newDir.Exists )
                    {
                        ImGui.OpenPopup( LabelOverWriteDir );
                    }
                    else if( _modManager.RenameModFolder( Mod.Data, newDir ) )
                    {
                        _selector.ReloadCurrentMod();
                        ImGui.CloseCurrentPopup();
                    }
                }
            }

            private static bool MergeFolderInto( DirectoryInfo source, DirectoryInfo target )
            {
                try
                {
                    foreach( var file in source.EnumerateFiles( "*", SearchOption.AllDirectories ) )
                    {
                        var targetFile = new FileInfo( Path.Combine( target.FullName, file.FullName.Substring( source.FullName.Length + 1 ) ) );
                        if( targetFile.Exists )
                        {
                            targetFile.Delete();
                        }

                        targetFile.Directory?.Create();
                        file.MoveTo( targetFile.FullName );
                    }

                    source.Delete( true );
                    return true;
                }
                catch( Exception e )
                {
                    PluginLog.Error( $"Could not merge directory {source.FullName} into {target.FullName}:\n{e}" );
                }

                return false;
            }

            private bool OverwriteDirPopup()
            {
                var closeParent = false;
                var _           = true;
                if( !ImGui.BeginPopupModal( LabelOverWriteDir, ref _, ImGuiWindowFlags.AlwaysAutoResize ) )
                {
                    return closeParent;
                }

                using var raii = ImGuiRaii.DeferredEnd( ImGui.EndPopup );

                DirectoryInfo dir    = Mod!.Data.BasePath;
                DirectoryInfo newDir = new( Path.Combine( dir.Parent!.FullName, _newName ) );
                ImGui.Text(
                    $"The mod directory {newDir} already exists.\nDo you want to merge / overwrite both mods?\nThis may corrupt the resulting mod in irrecoverable ways." );
                var buttonSize = ImGuiHelpers.ScaledVector2( 120, 0 );
                if( ImGui.Button( "Yes", buttonSize ) )
                {
                    if( MergeFolderInto( dir, newDir ) )
                    {
                        Service< ModManager >.Get()!.RenameModFolder( Mod.Data, newDir, false );

                        _selector.SelectModOnUpdate( _newName );

                        closeParent = true;
                        ImGui.CloseCurrentPopup();
                    }
                }

                ImGui.SameLine();

                if( ImGui.Button( "Cancel", buttonSize ) )
                {
                    _keyboardFocus = true;
                    ImGui.CloseCurrentPopup();
                }

                return closeParent;
            }

            private void DrawRenameModFolderPopup()
            {
                var _ = true;
                _keyboardFocus |= !ImGui.IsPopupOpen( PopupRenameFolder );

                ImGui.SetNextWindowPos( ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2( 0.5f, 1f ) );
                if( !ImGui.BeginPopupModal( PopupRenameFolder, ref _, ImGuiWindowFlags.AlwaysAutoResize ) )
                {
                    return;
                }

                using var raii = ImGuiRaii.DeferredEnd( ImGui.EndPopup );

                if( ImGui.IsKeyPressed( ImGui.GetKeyIndex( ImGuiKey.Escape ) ) )
                {
                    ImGui.CloseCurrentPopup();
                }

                var newName = Mod!.Data.BasePath.Name;

                if( _keyboardFocus )
                {
                    ImGui.SetKeyboardFocusHere();
                    _keyboardFocus = false;
                }

                if( ImGui.InputText( "New Folder Name##RenameFolderInput", ref newName, 64, ImGuiInputTextFlags.EnterReturnsTrue ) )
                {
                    RenameModFolder( newName );
                }

                ImGui.TextColored( GreyColor,
                    "Please restrict yourself to ascii symbols that are valid in a windows path,\nother symbols will be replaced by underscores." );

                ImGui.SetNextWindowPos( ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, Vector2.One / 2 );


                if( OverwriteDirPopup() )
                {
                    ImGui.CloseCurrentPopup();
                }
            }

            private void DrawRenameModFolderButton()
            {
                DrawRenameModFolderPopup();
                if( ImGui.Button( ButtonRenameModFolder ) )
                {
                    ImGui.OpenPopup( PopupRenameFolder );
                }

                ImGuiCustom.HoverTooltip( TooltipRenameModFolder );
            }

            private void DrawEditJsonButton()
            {
                if( ImGui.Button( ButtonEditJson ) )
                {
                    _selector.SaveCurrentMod();
                    Process.Start( new ProcessStartInfo( Mod!.Data.MetaFile.FullName ) { UseShellExecute = true } );
                }

                ImGuiCustom.HoverTooltip( TooltipEditJson );
            }

            private void DrawReloadJsonButton()
            {
                if( ImGui.Button( ButtonReloadJson ) )
                {
                    _selector.ReloadCurrentMod( true, false );
                }

                ImGuiCustom.HoverTooltip( TooltipReloadJson );
            }

            private void DrawResetMetaButton()
            {
                if( ImGui.Button( "Recompute Metadata" ) )
                {
                    _selector.ReloadCurrentMod( true, true, true );
                }

                ImGuiCustom.HoverTooltip(
                    "Force a recomputation of the metadata_manipulations.json file from all .meta files in the folder.\n"
                  + "Also reloads the mod.\n"
                  + "Be aware that this removes all manually added metadata changes." );
            }

            private void DrawDeduplicateButton()
            {
                if( ImGui.Button( ButtonDeduplicate ) )
                {
                    ModCleanup.Deduplicate( Mod!.Data.BasePath, Meta! );
                    _selector.SaveCurrentMod();
                    _selector.ReloadCurrentMod( true, true, true );
                }

                ImGuiCustom.HoverTooltip( TooltipDeduplicate );
            }

            private void DrawNormalizeButton()
            {
                if( ImGui.Button( ButtonNormalize ) )
                {
                    ModCleanup.Normalize( Mod!.Data.BasePath, Meta! );
                    _selector.SaveCurrentMod();
                    _selector.ReloadCurrentMod( true, true, true );
                }

                ImGuiCustom.HoverTooltip( TooltipNormalize );
            }

            private void DrawAutoGenerateGroupsButton()
            {
                if( ImGui.Button( "Auto-Generate Groups" ) )
                {
                    ModCleanup.AutoGenerateGroups( Mod!.Data.BasePath, Meta! );
                    _selector.SaveCurrentMod();
                    _selector.ReloadCurrentMod( true, true );
                }

                ImGuiCustom.HoverTooltip( "Automatically generate single-select groups from all folders (clears existing groups):\n"
                  + "First subdirectory: Option Group\n"
                  + "Second subdirectory: Option Name\n"
                  + "Afterwards: Relative file paths.\n"
                  + "Experimental - Use at own risk!" );
            }

            private void DrawSplitButton()
            {
                if( ImGui.Button( "Split Mod" ) )
                {
                    ModCleanup.SplitMod( Mod!.Data );
                }

                ImGuiCustom.HoverTooltip(
                    "Split off all options of a mod into single mods that are placed in a collective folder.\n"
                  + "Does not remove or change the mod itself, just create (potentially inefficient) copies.\n"
                  + "Experimental - Use at own risk!" );
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

                DrawResetMetaButton();
                ImGui.SameLine();
                DrawDeduplicateButton();
                ImGui.SameLine();
                DrawNormalizeButton();
                ImGui.SameLine();
                DrawAutoGenerateGroupsButton();
                ImGui.SameLine();
                DrawSplitButton();

                DrawSortOrder( Mod!.Data, _modManager, _selector );
            }

            public void Draw()
            {
                try
                {
                    using var raii = ImGuiRaii.DeferredEnd( ImGui.EndChild );
                    var       ret  = ImGui.BeginChild( LabelModPanel, AutoFillSize, true );

                    if( !ret || Mod == null )
                    {
                        return;
                    }

                    DrawHeaderLine();

                    // Next line with fixed distance.
                    ImGuiCustom.VerticalDistance( HeaderLineDistance );

                    DrawEnabledMark();
                    ImGui.SameLine();
                    DrawPriority();
                    if( Penumbra.Config.ShowAdvanced )
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
                }
                catch( Exception ex )
                {
                    PluginLog.LogError( ex, "Oh no" );
                }
            }
        }
    }
}
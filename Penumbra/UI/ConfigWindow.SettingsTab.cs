using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Utility;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private partial class SettingsTab
    {
        public const     int          RootDirectoryMaxLength = 64;
        private readonly ConfigWindow _window;

        public SettingsTab( ConfigWindow window )
            => _window = window;

        public void Draw()
        {
            using var tab = ImRaii.TabItem( "Settings" );
            OpenTutorial( BasicTutorialSteps.Fin );
            OpenTutorial( BasicTutorialSteps.Faq1 );
            OpenTutorial( BasicTutorialSteps.Faq2 );
            OpenTutorial( BasicTutorialSteps.Faq3 );
            if( !tab )
            {
                return;
            }

            using var child = ImRaii.Child( "##SettingsTab", -Vector2.One, false );
            if( !child )
            {
                return;
            }

            DrawEnabledBox();
            Checkbox( "Lock Main Window", "Prevent the main window from being resized or moved.", Penumbra.Config.FixMainWindow, v =>
            {
                Penumbra.Config.FixMainWindow = v;
                _window.Flags = v
                    ? _window.Flags | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize
                    : _window.Flags & ~( ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize );
            } );

            ImGui.NewLine();
            DrawRootFolder();
            DrawRediscoverButton();
            ImGui.NewLine();

            DrawModSelectorSettings();
            DrawColorSettings();
            DrawAdvancedSettings();

            _dialogManager.Draw();
            DrawSupportButtons();
        }

        // Changing the base mod directory.
        private          string?           _newModDirectory;
        private readonly FileDialogManager _dialogManager = SetupFileManager();
        private          bool              _dialogOpen; // For toggling on/off.

        // Do not change the directory without explicitly pressing enter or this button.
        // Shows up only if the current input does not correspond to the current directory.
        private static bool DrawPressEnterWarning( string newName, string old, float width, bool saved )
        {
            using var color = ImRaii.PushColor( ImGuiCol.Button, Colors.PressEnterWarningBg );
            var       w     = new Vector2( width, 0 );
            var (text, valid) = CheckPath( newName, old );

            return ( ImGui.Button( text, w ) || saved ) && valid;
        }

        private static (string Text, bool Valid) CheckPath( string newName, string old )
        {
            static bool IsSubPathOf( string basePath, string subPath )
            {
                if( basePath.Length == 0 )
                {
                    return false;
                }

                var rel = Path.GetRelativePath( basePath, subPath );
                return rel == "." || !rel.StartsWith( '.' ) && !Path.IsPathRooted( rel );
            }

            if( newName.Length > RootDirectoryMaxLength )
            {
                return ( $"Path is too long. The maximum length is {RootDirectoryMaxLength}.", false );
            }

            if( Path.GetDirectoryName( newName ) == null )
            {
                return ( "Path is not allowed to be a drive root. Please add a directory.", false );
            }

            var symbol = '\0';
            if( newName.Any( c => ( symbol = c ) > ( char )0x7F ) )
            {
                return ( $"Path contains invalid symbol {symbol}. Only ASCII is allowed.", false );
            }

            var desktop = Environment.GetFolderPath( Environment.SpecialFolder.Desktop );
            if( IsSubPathOf( desktop, newName ) )
            {
                return ( "Path is not allowed to be on your Desktop.", false );
            }

            var programFiles    = Environment.GetFolderPath( Environment.SpecialFolder.ProgramFiles );
            var programFilesX86 = Environment.GetFolderPath( Environment.SpecialFolder.ProgramFilesX86 );
            if( IsSubPathOf( programFiles, newName ) || IsSubPathOf( programFilesX86, newName ) )
            {
                return ( "Path is not allowed to be in ProgramFiles.", false );
            }

            var dalamud = Dalamud.PluginInterface.ConfigDirectory.Parent!.Parent!;
            if( IsSubPathOf( dalamud.FullName, newName ) )
            {
                return ( "Path is not allowed to be inside your Dalamud directories.", false );
            }

            if( Functions.GetDownloadsFolder( out var downloads ) && IsSubPathOf( downloads, newName ) )
            {
                return ( "Path is not allowed to be inside your Downloads folder.", false );
            }

            var gameDir = Dalamud.GameData.GameData.DataPath.Parent!.Parent!.FullName;
            if( IsSubPathOf( gameDir, newName ) )
            {
                return ( "Path is not allowed to be inside your game folder.", false );
            }

            return ( $"Press Enter or Click Here to Save (Current Directory: {old})", true );
        }

        // Draw a directory picker button that toggles the directory picker.
        // Selecting a directory does behave the same as writing in the text input, i.e. needs to be saved.
        private void DrawDirectoryPickerButton()
        {
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Folder.ToIconString(), _window._iconButtonSize,
                   "Select a directory via dialog.", false, true ) )
            {
                if( _dialogOpen )
                {
                    _dialogManager.Reset();
                    _dialogOpen = false;
                }
                else
                {
                    _newModDirectory ??= Penumbra.Config.ModDirectory;
                    // Use the current input as start directory if it exists,
                    // otherwise the current mod directory, otherwise the current application directory.
                    var startDir = Directory.Exists( _newModDirectory )
                        ? _newModDirectory
                        : Directory.Exists( Penumbra.Config.ModDirectory )
                            ? Penumbra.Config.ModDirectory
                            : ".";

                    _dialogManager.OpenFolderDialog( "Choose Mod Directory", ( b, s ) =>
                    {
                        _newModDirectory = b ? s : _newModDirectory;
                        _dialogOpen      = false;
                    }, startDir );
                    _dialogOpen = true;
                }
            }
        }

        private static void DrawOpenDirectoryButton( int id, DirectoryInfo directory, bool condition )
        {
            using var _   = ImRaii.PushId( id );
            var       ret = ImGui.Button( "Open Directory" );
            ImGuiUtil.HoverTooltip( "Open this directory in your configured file explorer." );
            if( ret && condition && Directory.Exists( directory.FullName ) )
            {
                Process.Start( new ProcessStartInfo( directory.FullName )
                {
                    UseShellExecute = true,
                } );
            }
        }

        // Draw the text input for the mod directory,
        // as well as the directory picker button and the enter warning.
        private void DrawRootFolder()
        {
            if( _newModDirectory.IsNullOrEmpty() )
            {
                _newModDirectory = Penumbra.Config.ModDirectory;
            }

            var       spacing = 3 * ImGuiHelpers.GlobalScale;
            using var group   = ImRaii.Group();
            ImGui.SetNextItemWidth( _window._inputTextWidth.X - spacing - _window._iconButtonSize.X );
            var       save  = ImGui.InputText( "##rootDirectory", ref _newModDirectory, 64, ImGuiInputTextFlags.EnterReturnsTrue );
            using var style = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing, new Vector2( spacing, 0 ) );
            ImGui.SameLine();
            DrawDirectoryPickerButton();
            style.Pop();
            ImGui.SameLine();

            const string tt = "This is where Penumbra will store your extracted mod files.\n"
              + "TTMP files are not copied, just extracted.\n"
              + "This directory needs to be accessible and you need write access here.\n"
              + "It is recommended that this directory is placed on a fast hard drive, preferably an SSD.\n"
              + "It should also be placed near the root of a logical drive - the shorter the total path to this folder, the better.\n"
              + "Definitely do not place it in your Dalamud directory or any sub-directory thereof.";
            ImGuiComponents.HelpMarker( tt );
            OpenTutorial( BasicTutorialSteps.GeneralTooltips );
            ImGui.SameLine();
            ImGui.TextUnformatted( "Root Directory" );
            ImGuiUtil.HoverTooltip( tt );

            group.Dispose();
            OpenTutorial( BasicTutorialSteps.ModDirectory );
            ImGui.SameLine();
            var pos = ImGui.GetCursorPosX();
            ImGui.NewLine();

            if( Penumbra.Config.ModDirectory != _newModDirectory
            && _newModDirectory.Length       != 0
            && DrawPressEnterWarning( _newModDirectory, Penumbra.Config.ModDirectory, pos, save ) )
            {
                Penumbra.ModManager.DiscoverMods( _newModDirectory );
            }
        }

        private static void DrawRediscoverButton()
        {
            DrawOpenDirectoryButton( 0, Penumbra.ModManager.BasePath, Penumbra.ModManager.Valid );
            ImGui.SameLine();
            var tt = Penumbra.ModManager.Valid
                ? "Force Penumbra to completely re-scan your root directory as if it was restarted."
                : "The currently selected folder is not valid. Please select a different folder.";
            if( ImGuiUtil.DrawDisabledButton( "Rediscover Mods", Vector2.Zero, tt, !Penumbra.ModManager.Valid ) )
            {
                Penumbra.ModManager.DiscoverMods();
            }
        }

        private void DrawEnabledBox()
        {
            var enabled = Penumbra.Config.EnableMods;
            if( ImGui.Checkbox( "Enable Mods", ref enabled ) )
            {
                _window._penumbra.SetEnabled( enabled );
            }

            OpenTutorial( BasicTutorialSteps.EnableMods );
        }

        private static void DrawColorSettings()
        {
            if( !ImGui.CollapsingHeader( "Colors" ) )
            {
                return;
            }

            foreach( var color in Enum.GetValues< ColorId >() )
            {
                var (defaultColor, name, description) = color.Data();
                var currentColor = Penumbra.Config.Colors.TryGetValue( color, out var current ) ? current : defaultColor;
                if( Widget.ColorPicker( name, description, currentColor, c => Penumbra.Config.Colors[ color ] = c, defaultColor ) )
                {
                    Penumbra.Config.Save();
                }
            }

            ImGui.NewLine();
        }

        public static void DrawDiscordButton( float width )
        {
            const string address = @"https://discord.gg/kVva7DHV4r";
            using var    color   = ImRaii.PushColor( ImGuiCol.Button, Colors.DiscordColor );
            if( ImGui.Button( "Join Discord for Support", new Vector2( width, 0 ) ) )
            {
                try
                {
                    var process = new ProcessStartInfo( address )
                    {
                        UseShellExecute = true,
                    };
                    Process.Start( process );
                }
                catch
                {
                    // ignored
                }
            }

            ImGuiUtil.HoverTooltip( $"Open {address}" );
        }

        private const string SupportInfoButtonText = "Copy Support Info to Clipboard";

        public static void DrawSupportButton()
        {
            if( ImGui.Button( SupportInfoButtonText ) )
            {
                var text = Penumbra.GatherSupportInformation();
                ImGui.SetClipboardText( text );
            }
        }

        private static void DrawGuideButton( float width )
        {
            const string address = @"https://reniguide.info/";
            using var color = ImRaii.PushColor( ImGuiCol.Button, 0xFFCC648D )
               .Push( ImGuiCol.ButtonHovered, 0xFFB070B0 )
               .Push( ImGuiCol.ButtonActive, 0xFF9070E0 );
            if( ImGui.Button( "Beginner's Guides", new Vector2( width, 0 ) ) )
            {
                try
                {
                    var process = new ProcessStartInfo( address )
                    {
                        UseShellExecute = true,
                    };
                    Process.Start( process );
                }
                catch
                {
                    // ignored
                }
            }

            ImGuiUtil.HoverTooltip(
                $"Open {address}\nImage and text based guides for most functionality of Penumbra made by Serenity.\n"
              + "Not directly affiliated and potentially, but not usually out of date." );
        }

        private void DrawSupportButtons()
        {
            var width = ImGui.CalcTextSize( SupportInfoButtonText ).X + ImGui.GetStyle().FramePadding.X * 2;
            var xPos  = ImGui.GetWindowWidth()                        - width;
            if( ImGui.GetScrollMaxY() > 0 )
            {
                xPos -= ImGui.GetStyle().ScrollbarSize + ImGui.GetStyle().FramePadding.X;
            }

            ImGui.SetCursorPos( new Vector2( xPos, ImGui.GetFrameHeightWithSpacing() ) );
            DrawSupportButton();

            ImGui.SetCursorPos( new Vector2( xPos, 0 ) );
            DrawDiscordButton( width );

            ImGui.SetCursorPos( new Vector2( xPos, 2 * ImGui.GetFrameHeightWithSpacing() ) );
            DrawGuideButton( width );

            ImGui.SetCursorPos( new Vector2( xPos, 3 * ImGui.GetFrameHeightWithSpacing() ) );
            if( ImGui.Button( "Restart Tutorial", new Vector2( width, 0 ) ) )
            {
                Penumbra.Config.TutorialStep = 0;
                Penumbra.Config.Save();
            }

            ImGui.SetCursorPos( new Vector2( xPos, 4 * ImGui.GetFrameHeightWithSpacing() ) );
            if( ImGui.Button( "Show Changelogs", new Vector2( width, 0 ) ) )
            {
                _window._penumbra.ForceChangelogOpen();
            }
        }
    }
}
using Dalamud.Utility;
using ImSharp;
using Luna;
using Penumbra.Files;
using Penumbra.Interop;
using Penumbra.Mods.Manager;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public sealed class MainSettings(
    FilenameService fileNames,
    TutorialService tutorial,
    MainConfig config,
    FileDialogService fileDialog,
    ModManager modManager) : IUiService
{
    private const int RootDirectoryMaxLength = 64;

    /// <summary> Changing the base mod directory. </summary>
    private string? _newModDirectory;

    private string _lastCloudSyncTestedPath = string.Empty;
    private bool   _lastCloudSyncTestResult;

    public void DrawHeader()
    {
        DrawEnabledBox();
        Im.Line.New();
        Im.Line.New();

        DrawRootFolder();
        DrawDirectoryButtons();
        Im.Line.New();
        Im.Line.New();
    }

    public void DrawGeneralSettings()
    {
        KeySelector.DoubleModifier("Destructive Modifier"u8,
            "A modifier you need to hold while clicking buttons that perform particularly destructive and generally irrecoverable actions, like deletions."u8,
            UiHelpers.InputTextWidth.X, config.DestructiveModifier, v => config.DestructiveModifier = v);
        KeySelector.DoubleModifier("Misclick Modifier"u8,
            "A modifier you need to hold while clicking buttons that should not be toggled by accident, but are generally easily revertible, like the Incognito or Temporary Settings Mode toggles.."u8,
            UiHelpers.InputTextWidth.X, config.MisclickModifier, v => config.MisclickModifier = v);
        if (SettingsTab.Checkbox("Print Chat Command Success Messages to Chat"u8,
                "Chat Commands usually print messages on failure but also on success to confirm your action. You can disable this here."u8,
                config.PrintSuccessfulCommandsToChat))
            config.PrintSuccessfulCommandsToChat ^= true;

        if (SettingsTab.Checkbox("Use Temporary Settings Per Default"u8,
                "When you make any changes to your collection, apply them as temporary changes first and require a click to 'turn permanent' if you want to keep them.\n\nThis can also be changed directly in the Mods tab."u8,
                config.DefaultTemporaryMode))
            config.DefaultTemporaryMode ^= true;

        Im.Line.Spacing();
    }


    /// <summary> Draw the Enable Mods Checkbox.</summary>
    private void DrawEnabledBox()
    {
        if (Im.Checkbox("Enable Mods"u8, config.EnableMods))
            config.EnableMods ^= true;

        tutorial.OpenTutorial(BasicTutorialSteps.EnableMods);
    }

    /// <summary>
    /// Do not change the directory without explicitly pressing enter or this button.
    /// Shows up only if the current input does not correspond to the current directory.
    /// </summary>
    private bool DrawPressEnterWarning(string newName, string old, float width, bool saved, bool selected)
    {
        using var color = ImGuiColor.Button.Push(Colors.PressEnterWarningBg);
        var (text, valid) = CheckRootDirectoryPath(newName, old, selected);
        var w = new Vector2(Math.Max(width, Im.Font.CalculateButtonSize(text).X), 0);
        return (Im.Button(text, w) || saved) && valid;
    }

    /// <summary> Check a potential new root directory for validity and return the button text and whether it is valid. </summary>
    private (string Text, bool Valid) CheckRootDirectoryPath(string newName, string old, bool selected)
    {
        if (newName.Length > RootDirectoryMaxLength)
            return ($"Path is too long. The maximum length is {RootDirectoryMaxLength}.", false);

        if (Path.GetDirectoryName(newName).IsNullOrEmpty())
            return ("Path is not allowed to be a drive root. Please add a directory.", false);

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (IsSubPathOf(desktop, newName))
            return ("Path is not allowed to be on your Desktop.", false);

        var programFiles    = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (IsSubPathOf(programFiles, newName) || IsSubPathOf(programFilesX86, newName))
            return ("Path is not allowed to be in ProgramFiles.", false);

        var dalamud = Path.GetDirectoryName(Path.GetDirectoryName(fileNames.ConfigurationDirectory))!;
        if (IsSubPathOf(dalamud, newName))
            return ("Path is not allowed to be inside your Dalamud directories.", false);

        if (WindowsFunctions.GetDownloadsFolder(out var downloads) && IsSubPathOf(downloads, newName))
            return ("Path is not allowed to be inside your Downloads folder.", false);

        var gameDir = Path.GetDirectoryName(Path.GetDirectoryName(fileNames.GameDataDirectory))!;
        if (IsSubPathOf(gameDir, newName))
            return ("Path is not allowed to be inside your game folder.", false);

        if (_lastCloudSyncTestedPath != newName)
        {
            _lastCloudSyncTestResult = CloudApi.IsCloudSynced(newName);
            _lastCloudSyncTestedPath = newName;
        }

        if (_lastCloudSyncTestResult)
            return ("Path is not allowed to be cloud-synced.", false);

        return selected
            ? ($"Press Enter or Click Here to Save (Current Directory: {old})", true)
            : ($"Click Here to Save (Current Directory: {old})", true);

        static bool IsSubPathOf(string basePath, string subPath)
        {
            if (basePath.Length is 0)
                return false;

            var rel = Path.GetRelativePath(basePath, subPath);
            return rel == "." || !rel.StartsWith('.') && !Path.IsPathRooted(rel);
        }
    }

    /// <summary>
    /// Draw a directory picker button that toggles the directory picker.
    /// Selecting a directory does behave the same as writing in the text input, i.e. needs to be saved.
    /// </summary>
    private void DrawDirectoryPickerButton()
    {
        if (!ImEx.Icon.Button(LunaStyle.FolderIcon, "Select a directory via dialog."u8))
            return;

        _newModDirectory ??= config.ModDirectory;
        // Use the current input as start directory if it exists,
        // otherwise the current mod directory, otherwise the current application directory.
        var startDir = Directory.Exists(_newModDirectory)
            ? _newModDirectory
            : Directory.Exists(config.ModDirectory)
                ? config.ModDirectory
                : ".";

        fileDialog.OpenFolderPicker("Choose Mod Directory", (b, s) => _newModDirectory = b ? s : _newModDirectory, startDir, false);
    }

    /// <summary>
    /// Draw the text input for the mod directory,
    /// as well as the directory picker button and the enter warning.
    /// </summary>
    private void DrawRootFolder()
    {
        if (_newModDirectory.IsNullOrEmpty())
            _newModDirectory = config.ModDirectory;

        bool save, selected;
        using (Im.Group())
        {
            Im.Item.SetNextWidth(UiHelpers.InputTextMinusButtonInner);
            using (var color = ImStyleBorder.Frame.Push(Colors.RegexWarningBorder, Im.Style.GlobalScale, !modManager.Valid))
            {
                color.Push(ImGuiColor.TextDisabled, Colors.RegexWarningBorder, !modManager.Valid);
                save = Im.Input.Text("##rootDirectory"u8, ref _newModDirectory, "Enter Root Directory here (MANDATORY)..."u8,
                    InputTextFlags.EnterReturnsTrue, RootDirectoryMaxLength);
            }

            selected = Im.Item.Active;
            Im.Line.SameInner();
            DrawDirectoryPickerButton();

            var tt = "This is where Penumbra will store your extracted mod files.\n"u8
              + "TTMP files are not copied, just extracted.\n"u8
              + "This directory needs to be accessible and you need write access here.\n"u8
              + "It is recommended that this directory is placed on a fast hard drive, preferably an SSD.\n"u8
              + "It should also be placed near the root of a logical drive - the shorter the total path to this folder, the better.\n"u8
              + "Definitely do not place it in your Dalamud directory or any sub-directory thereof."u8;

            Im.Line.SameInner();
            LunaStyle.DrawAlignedHelpMarker(tt);
            tutorial.OpenTutorial(BasicTutorialSteps.GeneralTooltips);
            Im.Line.SameInner();
            Im.Text("Root Directory"u8);
            Im.Tooltip.OnHover(tt);
        }

        tutorial.OpenTutorial(BasicTutorialSteps.ModDirectory);
        Im.Line.Same();
        var pos = Im.Cursor.X;
        Im.Line.New();

        if (config.ModDirectory != _newModDirectory
         && _newModDirectory.Length is not 0
         && DrawPressEnterWarning(_newModDirectory, config.ModDirectory, pos, save, selected))
            modManager.DiscoverMods(_newModDirectory, out _newModDirectory);
    }

    /// <summary> Draw the Open Directory and Rediscovery buttons.</summary>
    private void DrawDirectoryButtons()
    {
        UiHelpers.DrawOpenDirectoryButton(0, modManager.BasePath, modManager.Valid);
        Im.Line.Same();
        var tt = modManager.Valid
            ? "Force Penumbra to completely re-scan your root directory as if it was restarted."u8
            : "The currently selected folder is not valid. Please select a different folder."u8;
        if (ImEx.Button("Rediscover Mods"u8, Vector2.Zero, tt, !modManager.Valid))
            modManager.DiscoverMods();
    }
}

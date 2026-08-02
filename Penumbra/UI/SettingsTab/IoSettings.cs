using ImSharp;
using Luna;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public sealed class IoSettings(IoConfig config, MainConfig main, FileDialogService fileDialog, PcpSettings pcp, PcpService pcpService)
    : IUiService
{
    public void Draw()
    {
        using var header = Im.Tree.HeaderId("Mod Import/Export"u8);
        if (!header)
            return;

        DrawImportSettings();
        DrawWatcherSettings();
        DrawExportSettings();
        DrawPcpSettings();
    }

    private void DrawWatcherSettings()
    {
        using var tree = Im.Tree.Node("Automatic Import"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        DrawFileWatcherPath();
        if (SettingsTab.Checkbox("Enable Directory Watcher"u8,
                "Enables a File Watcher that automatically listens for Mod files that enter a specified directory, causing Penumbra to open a popup to import these mods."u8,
                config.EnableDirectoryWatch))
            config.EnableDirectoryWatch ^= true;
        if (SettingsTab.Checkbox("Enable Archive Peeking"u8,
                "Enables the File Watcher to Peek inside .rar .zip and .7z archives, extracting mods inside and causing Penumbra to open a popup to import these mods."u8,
                config.EnableContainerPeeking))
            config.EnableContainerPeeking ^= true;
        if (SettingsTab.Checkbox("Enable Fully Automatic Import"u8,
                "Uses the File Watcher in order to skip the query popup and automatically import any new mods."u8,
                config.EnableAutomaticModImport))
            config.EnableAutomaticModImport ^= true;
        if (SettingsTab.Checkbox("Prevent Exported Mods From Being Automatically Reimported"u8,
                "If your Automatic Import Directory is the same as your Default Mod Export Directory, prevents mods and character packs you export from being reimported or showing a query popup."u8,
                config.PreventExportLoopback))
            config.PreventExportLoopback ^= true;
    }

    private void DrawImportSettings()
    {
        using var tree = Im.Tree.Node("Mod Import"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        if (SettingsTab.Checkbox("Replace Non-Standard Symbols On Import"u8,
                "Replace all non-ASCII symbols in mod and option names with underscores when importing mods."u8,
                config.ReplaceNonAsciiOnImport))
            config.ReplaceNonAsciiOnImport ^= true;

        if (SettingsTab.Checkbox("Always Open Import at Default Directory"u8,
                "Open the import window at the location specified here every time, forgetting your previous path."u8,
                config.AlwaysOpenDefaultImport))
            config.AlwaysOpenDefaultImport ^= true;
        DrawDefaultModImportFolder();
        DrawDefaultModImportPath();

        if (SettingsTab.Checkbox("Always Open Detailed Mod Import Popup"u8,
                "Always open the detailed modal popup at the center of the screen with information about the latest imports, instead of the Dalamud notification."u8,
                config.AlwaysShowDetailedModImport))
            config.AlwaysShowDetailedModImport ^= true;
        if (SettingsTab.Checkbox("Automatically Dismiss Reports of Successful Mod Imports"u8,
                "Makes report notifications automatically disappear after a few seconds if all the mods were successfully imported.\nReports that contain errors will still have to be manually dismissed."u8,
                config.AutoDismissModImportSuccessReports))
            config.AutoDismissModImportSuccessReports ^= true;
    }

    private void DrawExportSettings()
    {
        using var tree = Im.Tree.Node("Mod Export"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        DrawDefaultModAuthor();
        DrawDefaultModExportPath();
    }

    private void DrawPcpSettings()
    {
        using var tree = Im.Tree.Node("Penumbra Character Packs (PCP)"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        if (SettingsTab.Checkbox("Handle PCP Files"u8,
                "When encountering specific mods, usually but not necessarily denoted by a .pcp file ending, Penumbra will automatically try to create an associated collection and assign it to a specific character for this mod package. This can turn this behaviour off if unwanted."u8,
                !pcp.DisableHandling))
            pcp.DisableHandling ^= true;

        var active = LunaStyle.Modifier.Destructive.Active;
        Im.Line.Same();
        if (ImEx.Button("Delete all PCP Mods"u8, default, "Deletes all mods tagged with 'PCP' from the mod list."u8, !active))
            pcpService.CleanPcpMods();
        LunaStyle.Modifier.Destructive.TooltipLineBreak("delete"u8);

        Im.Line.Same();
        if (ImEx.Button("Delete all PCP Collections"u8, default,
                "Deletes all collections whose name starts with 'PCP/' from the collection list."u8, !active))
            pcpService.CleanPcpCollections();
        LunaStyle.Modifier.Destructive.TooltipLineBreak("delete"u8);

        if (SettingsTab.Checkbox("Allow Other Plugins Access to PCP Handling"u8,
                "When creating or importing PCP files, other plugins can add and interpret their own data to the character.json file."u8,
                pcp.AllowIpc))
            pcp.AllowIpc ^= true;

        if (SettingsTab.Checkbox("Create PCP Collections"u8,
                "When importing PCP files, create the associated collection."u8,
                pcp.CreateCollection))
            pcp.CreateCollection ^= true;

        if (SettingsTab.Checkbox("Assign PCP Collections"u8,
                "When importing PCP files and creating the associated collection, assign it to the associated character."u8,
                pcp.AssignCollection))
            pcp.AssignCollection ^= true;
        DrawPcpFolder();
        DrawPcpExtension();
    }

    /// <summary> Draw input for the default folder to sort put newly imported mods into. </summary>
    private void DrawDefaultModImportFolder()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Text("##importFolder"u8, config.DefaultImportFolder, out string newFolder))
            config.DefaultImportFolder = newFolder;

        LunaStyle.DrawAlignedHelpMarkerLabel("Default Mod Import Organizational Folder"u8,
            "Set the default Penumbra mod folder to place newly imported mods into.\nLeave blank to import into Root."u8);
    }

    /// <summary> Draw input for the default import path for a mod. </summary>
    private void DrawDefaultModImportPath()
    {
        using var id = Im.Id.Push("##dmi"u8);
        Im.Item.SetNextWidth(UiHelpers.InputTextMinusButtonInner);
        if (ImEx.InputOnDeactivation.Text(StringU8.Empty, config.DefaultModImportPath, out string newDirectory))
            config.DefaultModImportPath = newDirectory;

        Im.Line.SameInner();
        if (ImEx.Icon.Button(LunaStyle.FolderIcon, "Select a directory via dialog."u8))
        {
            var startDir = config.DefaultModImportPath.Length > 0 && Directory.Exists(config.DefaultModImportPath)
                ? config.DefaultModImportPath
                : Directory.Exists(main.ModDirectory)
                    ? main.ModDirectory
                    : null;

            fileDialog.OpenFolderPicker("Choose Default Import Directory", (b, s) =>
            {
                if (!b)
                    return;

                config.DefaultModImportPath = s;
                config.Save();
            }, startDir, false);
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("Default Mod Import Directory"u8,
            "Set the directory that gets opened when using the file picker to import mods for the first time."u8);
    }

    /// <summary> Draw input for the Automatic Mod import path. </summary>
    private void DrawFileWatcherPath()
    {
        using var id = Im.Id.Push("fw"u8);
        Im.Item.SetNextWidth(UiHelpers.InputTextMinusButtonInner);
        if (ImEx.InputOnDeactivation.Text(StringU8.Empty, config.WatchDirectory, out string newDirectory, maxLength: 256))
            config.WatchDirectory = newDirectory;

        Im.Line.SameInner();
        if (ImEx.Icon.Button(LunaStyle.FolderIcon, "Select a directory via dialog."u8))
        {
            var startDir = config.WatchDirectory.Length > 0 && Directory.Exists(config.WatchDirectory)
                ? config.WatchDirectory
                : Directory.Exists(main.ModDirectory)
                    ? main.ModDirectory
                    : null;
            fileDialog.OpenFolderPicker("Choose Automatic Import Directory", (b, s) =>
            {
                if (b)
                    config.WatchDirectory = s;
            }, startDir, false);
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("Automatic Import Directory"u8,
            "Choose the Directory the File Watcher listens to."u8);
    }

    /// <summary> Draw input for the default export/backup path for mods. </summary>
    private void DrawDefaultModExportPath()
    {
        using var id = Im.Id.Push("##dme"u8);
        Im.Item.SetNextWidth(UiHelpers.InputTextMinusButtonInner);
        if (ImEx.InputOnDeactivation.Text(StringU8.Empty, config.ExportDirectory, out string newDirectory))
            config.ExportDirectory = newDirectory;

        Im.Line.SameInner();
        if (ImEx.Icon.Button(LunaStyle.FolderIcon, "Select a directory via dialog."u8))
        {
            var startDir = config.ExportDirectory.Length > 0 && Directory.Exists(config.ExportDirectory)
                ? config.ExportDirectory
                : Directory.Exists(main.ModDirectory)
                    ? main.ModDirectory
                    : null;
            fileDialog.OpenFolderPicker("Choose Default Export Directory", (b, s) =>
            {
                if (b)
                    config.ExportDirectory = s;
            }, startDir, false);
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("Default Mod Export Directory"u8,
            "Set the directory mods get saved to when using the export function or loaded from when reimporting backups.\n"u8
          + "Keep this empty to use the root directory."u8);
    }

    /// <summary> Draw input for the default name to input as author into newly generated mods. </summary>
    private void DrawDefaultModAuthor()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Text("##author"u8, config.DefaultModAuthor, out string newAuthor))
        {
            config.DefaultModAuthor = newAuthor;
            config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("Default Mod Author"u8, "Set the default author stored for newly created mods."u8);
    }

    /// <summary> Draw input for the default folder to sort put newly imported mods into. </summary>
    private void DrawPcpFolder()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Text("##pcpFolder"u8, pcp.FolderName, out string newFolder))
            pcp.FolderName = newFolder;

        LunaStyle.DrawAlignedHelpMarkerLabel("Default PCP Organizational Folder"u8,
            "The folder any penumbra character packs are moved to on import.\nLeave blank to import into Root."u8);
    }

    private void DrawPcpExtension()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        if (ImEx.InputOnDeactivation.Text("##pcpExtension"u8, pcp.PcpExtension, out string newExtension))
            pcp.PcpExtension = newExtension;

        Im.Line.SameInner();
        if (ImEx.Button("Reset##pcpExtension"u8, Vector2.Zero, "Reset the extension to its default value of \".pcp\"."u8,
                pcp.PcpExtension is ".pcp"))
            pcp.PcpExtension = ".pcp";

        LunaStyle.DrawAlignedHelpMarkerLabel("PCP Extension"u8,
            "The extension used when exporting PCP files. Should generally be either \".pcp\" or \".pmp\"."u8);
    }
}

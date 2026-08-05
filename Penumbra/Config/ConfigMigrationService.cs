using System.Text.Json;
using Dalamud.Interface.ImGuiNotification;
using ImSharp;
using Luna;
using Microsoft.Extensions.Options;
using Penumbra.Api.Enums;
using Penumbra.Files;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.Classes;
using Penumbra.UI.ModsTab.Selector;

namespace Penumbra;

public class ConfigMigrationService(
    SaveService saveService,
    PenumbraMessager messages,
    Configuration config,
    BackupService backupService,
    LocalModDatabase localModDatabase) : IService
{
    public void MigrateOldConfigStyle()
    {
        // Do this on every migration from now on for a while
        // because it stayed alive for a bunch of people for some reason.
        DeleteMetaTmp();

        try
        {
            if (!File.Exists(saveService.FileNames.ConfigurationFile))
                return;

            backupService.CreateMigrationBackup("pre_settings_restructuring", saveService.FileNames.ConfigurationFile,
                saveService.FileNames.Migration.Ephemeral, saveService.FileNames.Migration.UiConfigFile,
                saveService.FileNames.Migration.FilterFile);
            MigrateConfiguration();
            MigrateEphemeral();
            MigrateUiConfig();
            MigrateFilters();
        }
        catch (Exception ex)
        {
            messages.NotificationMessage(ex, "Unknown failure during migration. Possibly restore from backups.");
        }
    }

    private void MigrateFilters()
    {
        if (ReadAndDelete(saveService.FileNames.Migration.FilterFile) is not { } json)
            return;

        config.Filters.MigrationLoad(json.RootElement);
        config.Filters.Save();
    }

    private void MigrateUiConfig()
    {
        if (ReadAndDelete(saveService.FileNames.Migration.UiConfigFile) is not { } json)
            return;

        config.Ephemeral.ModTabScale = TwoPanelWidth.ReadJson(json.RootElement, "ModTabScale"u8, config.Ephemeral.ModTabScale);
        config.Ephemeral.CollectionsTabScale =
            TwoPanelWidth.ReadJson(json.RootElement, "CollectionsTabScale"u8, config.Ephemeral.CollectionsTabScale);
        config.Ui.ModSettingMaximumExtendLabelWidth =
            json.RootElement.PropertyOrDefault("ModSettingMaximumExtendLabelWidth"u8, config.Ui.ModSettingMaximumExtendLabelWidth);
        config.Ui.ModSettingBorderScale =
            json.RootElement.PropertyOrDefault("ModSettingBorderScale"u8, config.Ui.ModSettingBorderScale);
        config.Ui.ModSettingComboAlignment =
            json.RootElement.PropertyOrDefault("ModSettingComboAlignment"u8, config.Ui.ModSettingComboAlignment);
        config.Ui.ModSettingItemSpacingFactor =
            json.RootElement.PropertyOrDefault("ModSettingItemSpacingFactor"u8, config.Ui.ModSettingItemSpacingFactor);
        config.Ui.ModSettingLabelAlignment =
            json.RootElement.PropertyOrDefault("ModSettingLabelAlignment"u8, config.Ui.ModSettingLabelAlignment);
        config.Ui.ModSettingLineScale =
            json.RootElement.PropertyOrDefault("ModSettingLineScale"u8, config.Ui.ModSettingLineScale);

        if (json.RootElement.TryReadObject("Colors"u8, out var colors))
        {
#pragma warning disable CA1869
            var options = new JsonSerializerOptions(JsonFunctions.SerializerOptions);
#pragma warning restore CA1869
            options.Converters.Add(new ColorDictionaryConverter<ColorId, ColorIdData>(messages, true, true, true));
            if (colors.Deserialize<ColorDictionary<ColorId, ColorIdData>>(options) is { } dict)
                dict.Apply(config.Ui.Colors, true);
        }

        config.Ephemeral.Save();
        config.Ui.Save();
    }

    private void MigrateEphemeral()
    {
        if (ReadAndDelete(saveService.FileNames.Migration.Ephemeral) is not { } json)
            return;

        config.Ephemeral.LoadedVersion   = json.RootElement.PropertyOrDefault("Version"u8,         -1);
        config.Ephemeral.LastSeenVersion = json.RootElement.PropertyOrDefault("LastSeenVersion"u8, config.Ephemeral.LastSeenVersion);
        config.Ephemeral.DebugSeparateWindow =
            json.RootElement.PropertyOrDefault("DebugSeparateWindow"u8, config.Ephemeral.DebugSeparateWindow);
        config.Ephemeral.TutorialStep    = json.RootElement.PropertyOrDefault("TutorialStep"u8, config.Ephemeral.TutorialStep);
        config.Ephemeral.CollectionPanel = json.RootElement.EnumOrDefault("CollectionPanel"u8, config.Ephemeral.CollectionPanel);
        config.Ephemeral.SelectedTab     = json.RootElement.EnumOrDefault("SelectedTab"u8,     config.Ephemeral.SelectedTab);
        config.Ephemeral.SelectedManagementTab =
            json.RootElement.EnumOrDefault("SelectedManagementTab"u8, config.Ephemeral.SelectedManagementTab);
        config.Ephemeral.SelectedModPanelTab = json.RootElement.EnumOrDefault("SelectedModPanelTab"u8, config.Ephemeral.SelectedModPanelTab);
        config.Ephemeral.ForceRedrawOnFileChange =
            json.RootElement.PropertyOrDefault("ForceRedrawOnFileChange"u8, config.Ephemeral.ForceRedrawOnFileChange);
        config.Ephemeral.IncognitoMode = json.RootElement.PropertyOrDefault("IncognitoMode"u8, config.Ephemeral.IncognitoMode);
        if (json.RootElement.TryReadArray("AdvancedEditingOpenForModPaths"u8, out var array))
            config.Ephemeral.AdvancedEditingOpenForModPaths = array.Deserialize<HashSet<string>>() ?? [];
        config.Ephemeral.Save();
    }

    private void MigrateConfiguration()
    {
        if (ReadAndDelete(saveService.FileNames.ConfigurationFile) is not { } json)
            return;

        config.Main.LoadedVersion = json.RootElement.PropertyOrDefault("Version"u8, -1);
        switch (config.Main.LoadedVersion)
        {
            case -1: return; // No main config version.
            case < 11:
                messages.NotificationMessage(
                    $"Configuration version {config.Main.LoadedVersion} is too old and incompatible, configuration reset to defaults.");
                return;
        }

        var version = config.Main.LoadedVersion;
        Version11To12(ref version);
        Version12To13(ref version);
        Version13To14(json.RootElement, ref version);
        Version14To15(json.RootElement, ref version);
        MigrateToNewFiles(json.RootElement, version);
    }

    private void MigrateToNewFiles(in JsonElement json, int version)
    {
        if (version is not 15)
            throw new Exception(
                $"Migration of configuration files failed since passed version of the migrated file is {version} instead of 15.");

        // Main
        config.Main.ChangeLogDisplayType = json.EnumOrDefault("ChangeLogDisplayType"u8, config.Main.ChangeLogDisplayType);
        config.Main.EnableMods           = json.PropertyOrDefault("EnableMods"u8,           config.Main.EnableMods);
        config.Main.ModDirectory         = json.PropertyOrDefault("ModDirectory"u8,         config.Main.ModDirectory);
        config.Main.DefaultTemporaryMode = json.PropertyOrDefault("DefaultTemporaryMode"u8, config.Main.DefaultTemporaryMode);
        config.Main.DestructiveModifier =
            json.TryGetProperty("DeleteModModifier"u8, out var d) && DoubleModifier.TryDeserialize(d, out var r, false)
                ? r
                : config.Main.DestructiveModifier;
        config.Main.MisclickModifier =
            json.TryGetProperty("IncognitoModifier"u8, out var i) && DoubleModifier.TryDeserialize(i, out var m, false)
                ? m
                : config.Main.MisclickModifier;
        config.Main.PrintSuccessfulCommandsToChat =
            json.PropertyOrDefault("PrintSuccessfulCommandsToChat"u8, config.Main.PrintSuccessfulCommandsToChat);

        // Io
        config.Io.ExportDirectory          = json.PropertyOrDefault("ExportDirectory"u8,          config.Io.ExportDirectory);
        config.Io.WatchDirectory           = json.PropertyOrDefault("WatchDirectory"u8,           config.Io.WatchDirectory);
        config.Io.ReplaceNonAsciiOnImport  = json.PropertyOrDefault("ReplaceNonAsciiOnImport"u8,  config.Io.ReplaceNonAsciiOnImport);
        config.Io.EnableDirectoryWatch     = json.PropertyOrDefault("EnableDirectoryWatch"u8,     config.Io.EnableDirectoryWatch);
        config.Io.EnableAutomaticModImport = json.PropertyOrDefault("EnableAutomaticModImport"u8, config.Io.EnableAutomaticModImport);
        config.Io.AutoDismissModImportSuccessReports =
            json.PropertyOrDefault("AutoDismissModImportSuccessReports"u8, config.Io.AutoDismissModImportSuccessReports);
        config.Io.AlwaysShowDetailedModImport = json.PropertyOrDefault("AlwaysShowDetailedModImport"u8, config.Io.AlwaysShowDetailedModImport);
        config.Io.PreventExportLoopback       = json.PropertyOrDefault("PreventExportLoopback"u8,       config.Io.PreventExportLoopback);
        config.Io.IncludeShpkInSwap           = json.PropertyOrDefault("IncludeShpkInSwap"u8,           config.Io.IncludeShpkInSwap);
        if (json.TryReadObject("PcpSettings"u8, out var pcpSettings))
        {
            config.Io.PcpCreateCollection = pcpSettings.PropertyOrDefault("CreateCollection"u8, config.Io.PcpCreateCollection);
            config.Io.PcpAssignCollection = pcpSettings.PropertyOrDefault("AssignCollection"u8, config.Io.PcpAssignCollection);
            config.Io.PcpAllowIpc         = pcpSettings.PropertyOrDefault("AllowIpc"u8,         config.Io.PcpAllowIpc);
            config.Io.DisablePcpHandling  = pcpSettings.PropertyOrDefault("DisableHandling"u8,  config.Io.DisablePcpHandling);
            config.Io.PcpFolderName       = pcpSettings.PropertyOrDefault("FolderName"u8,       config.Io.PcpFolderName);
            config.Io.PcpExtension        = pcpSettings.PropertyOrDefault("PcpExtension"u8,     config.Io.PcpExtension);
        }

        config.Io.DefaultImportFolder       = json.PropertyOrDefault("DefaultImportFolder"u8,       config.Io.DefaultImportFolder);
        config.Io.MigrateImportedModelsToV6 = json.PropertyOrDefault("MigrateImportedModelsToV6"u8, config.Io.MigrateImportedModelsToV6);
        config.Io.MigrateImportedMaterialsToLegacy =
            json.PropertyOrDefault("MigrateImportedMaterialsToLegacy"u8, config.Io.MigrateImportedMaterialsToLegacy);
        config.Io.DefaultModImportPath    = json.PropertyOrDefault("DefaultModImportPath"u8,    config.Io.DefaultModImportPath);
        config.Io.AlwaysOpenDefaultImport = json.PropertyOrDefault("AlwaysOpenDefaultImport"u8, config.Io.AlwaysOpenDefaultImport);
        config.Io.DefaultModAuthor        = json.PropertyOrDefault("DefaultModAuthor"u8,        config.Io.DefaultModAuthor);

        // Advanced
        config.Advanced.UseCrashHandler =
            json.TryReadProperty("UseCrashHandler"u8, out bool? value, true) ? value : config.Advanced.UseCrashHandler;
        config.Advanced.EnableCustomShapes = json.PropertyOrDefault("EnableCustomShapes"u8, config.Advanced.EnableCustomShapes);
        config.Advanced.MinimumSize = json.TryReadObject("MinimumSize"u8, out var minSize)
            ? new Vector2(minSize.PropertyOrDefault("X"u8, config.Advanced.MinimumSize.X),
                minSize.PropertyOrDefault("Y"u8,           config.Advanced.MinimumSize.Y))
            : config.Advanced.MinimumSize;
        config.Advanced.DebugMode               = json.PropertyOrDefault("DebugMode"u8,               config.Advanced.DebugMode);
        config.Advanced.AutoDeduplicateOnImport = json.PropertyOrDefault("AutoDeduplicateOnImport"u8, config.Advanced.AutoDeduplicateOnImport);
        config.Advanced.AutoReduplicateUiOnImport =
            json.PropertyOrDefault("AutoReduplicateUiOnImport"u8, config.Advanced.AutoReduplicateUiOnImport);
        config.Advanced.UseFileSystemCompression =
            json.PropertyOrDefault("UseFileSystemCompression"u8, config.Advanced.UseFileSystemCompression);
        config.Advanced.EnableHttpApi          = json.PropertyOrDefault("EnableHttpApi"u8,          config.Advanced.EnableHttpApi);
        config.Advanced.KeepDefaultMetaChanges = json.PropertyOrDefault("KeepDefaultMetaChanges"u8, config.Advanced.KeepDefaultMetaChanges);
        config.Advanced.HdrRenderTargets       = json.PropertyOrDefault("HdrRenderTargets"u8,       config.Advanced.HdrRenderTargets);
        config.Advanced.AuxiliaryDeviceMode    = json.EnumOrDefault("AuxiliaryDeviceMode"u8, config.Advanced.AuxiliaryDeviceMode);

        // Ui
        config.Ui.OpenWindowAtStart        = json.PropertyOrDefault("OpenWindowAtStart"u8,        config.Ui.OpenWindowAtStart);
        config.Ui.HideUiInGPose            = json.PropertyOrDefault("HideUiInGPose"u8,            config.Ui.HideUiInGPose);
        config.Ui.HideUiInCutscenes        = json.PropertyOrDefault("HideUiInCutscenes"u8,        config.Ui.HideUiInCutscenes);
        config.Ui.HideUiWhenUiHidden       = json.PropertyOrDefault("HideUiWhenUiHidden"u8,       config.Ui.HideUiWhenUiHidden);
        config.Ui.HideChangedItemFilters   = json.PropertyOrDefault("HideChangedItemFilters"u8,   config.Ui.HideChangedItemFilters);
        config.Ui.HidePrioritiesInSelector = json.PropertyOrDefault("HidePrioritiesInSelector"u8, config.Ui.HidePrioritiesInSelector);
        config.Ui.HideRedrawBar            = json.PropertyOrDefault("HideRedrawBar"u8,            config.Ui.HideRedrawBar);
        config.Ui.HideMachinistOffhandFromChangedItems =
            json.PropertyOrDefault("HideMachinistOffhandFromChangedItems"u8, config.Ui.HideMachinistOffhandFromChangedItems);
        config.Ui.RememberModFilters         = json.PropertyOrDefault("RememberModFilters"u8,         config.Ui.RememberModFilters);
        config.Ui.RememberCollectionFilters  = json.PropertyOrDefault("RememberCollectionFilters"u8,  config.Ui.RememberCollectionFilters);
        config.Ui.RememberOnScreenFilters    = json.PropertyOrDefault("RememberOnScreenFilters"u8,    config.Ui.RememberOnScreenFilters);
        config.Ui.RememberChangedItemFilters = json.PropertyOrDefault("RememberChangedItemFilters"u8, config.Ui.RememberChangedItemFilters);
        config.Ui.RememberEffectiveChangesFilters =
            json.PropertyOrDefault("RememberEffectiveChangesFilters"u8, config.Ui.RememberEffectiveChangesFilters);
        config.Ui.RememberResourceManagerFilters =
            json.PropertyOrDefault("RememberResourceManagerFilters"u8, config.Ui.RememberResourceManagerFilters);
        config.Ui.ShowRename         = json.EnumOrDefault("ShowRename"u8,         config.Ui.ShowRename);
        config.Ui.ChangedItemDisplay = json.EnumOrDefault("ChangedItemDisplay"u8, config.Ui.ChangedItemDisplay);
        config.Ui.SortMode =
            json.TryReadProperty("SortMode"u8, out string? mode, true)
         && ISortMode.Valid.TryGetValue(mode ?? config.Ui.SortMode.GetType().Name, out var s)
                ? s
                : config.Ui.SortMode;
        config.Ui.OpenFoldersByDefault = json.PropertyOrDefault("OpenFoldersByDefault"u8, config.Ui.OpenFoldersByDefault);
        config.Ui.SingleGroupRadioMax  = json.PropertyOrDefault("SingleGroupRadioMax"u8,  config.Ui.SingleGroupRadioMax);
        config.Ui.SetQuickMoveFolder(0, json.PropertyOrDefault("QuickMoveFolder1"u8, config.Ui.QuickMoveFolder(0)));
        config.Ui.SetQuickMoveFolder(1, json.PropertyOrDefault("QuickMoveFolder2"u8, config.Ui.QuickMoveFolder(1)));
        config.Ui.SetQuickMoveFolder(2, json.PropertyOrDefault("QuickMoveFolder3"u8, config.Ui.QuickMoveFolder(2)));

        // Behavior
        config.Behavior.UseDalamudUiTextureRedirection =
            json.PropertyOrDefault("UseDalamudUiTextureRedirection"u8, config.Behavior.UseDalamudUiTextureRedirection);
        config.Behavior.AutoSelectCollection = json.PropertyOrDefault("AutoSelectCollection"u8, config.Behavior.AutoSelectCollection);
        config.Behavior.ShowModsInLobby      = json.PropertyOrDefault("ShowModsInLobby"u8,      config.Behavior.ShowModsInLobby);
        config.Behavior.UseCharacterCollectionInMainWindow = json.PropertyOrDefault("UseCharacterCollectionInMainWindow"u8,
            config.Behavior.UseCharacterCollectionInMainWindow);
        config.Behavior.UseCharacterCollectionsInCards =
            json.PropertyOrDefault("UseCharacterCollectionsInCards"u8, config.Behavior.UseCharacterCollectionsInCards);
        config.Behavior.UseCharacterCollectionInInspect =
            json.PropertyOrDefault("UseCharacterCollectionInInspect"u8, config.Behavior.UseCharacterCollectionInInspect);
        config.Behavior.UseCharacterCollectionInTryOn =
            json.PropertyOrDefault("UseCharacterCollectionInTryOn"u8, config.Behavior.UseCharacterCollectionInTryOn);
        config.Behavior.UseOwnerNameForCharacterCollection = json.PropertyOrDefault("UseOwnerNameForCharacterCollection"u8,
            config.Behavior.UseOwnerNameForCharacterCollection);
        config.Behavior.UseNoModsInInspect  = json.PropertyOrDefault("UseNoModsInInspect"u8,  config.Behavior.UseNoModsInInspect);
        config.Behavior.UseOwnerForHostiles = json.PropertyOrDefault("UseOwnerForHostiles"u8, config.Behavior.UseOwnerForHostiles);

        // Editing
        config.Editing.DefaultEditWindowModPinned =
            json.PropertyOrDefault("DefaultEditWindowModPinned"u8, config.Editing.DefaultEditWindowModPinned);
        config.Editing.EditRawTileTransforms = json.PropertyOrDefault("EditRawTileTransforms"u8, config.Editing.EditRawTileTransforms);
        config.Editing.WholePairSelectorAlwaysHighlights =
            json.PropertyOrDefault("WholePairSelectorAlwaysHighlights"u8, config.Editing.WholePairSelectorAlwaysHighlights);
        config.Editing.AllDyeChannels = json.PropertyOrDefault("AllDyeChannels"u8, config.Editing.AllDyeChannels);
        config.Editing.PreferredEditorFactories = json.TryReadObject("PreferredEditorFactories"u8, out var factories)
            ? factories.Deserialize<Dictionary<ResourceType, string>>() ?? []
            : config.Editing.PreferredEditorFactories;
    }

    private void Version14To15(in JsonElement json, ref int version)
    {
        if (version is not 14)
            return;

        version = 15;

        if (!json.TryReadObject("Colors"u8, out var colorsString))
            return;

        var colors = new Dictionary<ColorId, uint>(colorsString.GetPropertyCount());
        foreach (var property in colorsString.EnumerateObject())
        {
            if (ColorId.Parse(property.Name, out var id)
             && property.Value.ValueKind is JsonValueKind.Number
             && property.Value.TryGetUInt32(out var color))
                colors.Add(id, color);
        }

        if (colors.Count <= 0)
            return;

        var migrate = new ColorDictionary<ColorId, ColorIdData>(colors, (id, value) => ColorIdData.OldDefault(id) == value);
        config.Ui.Colors.Apply(migrate, false);
        config.Ui.Save();
    }

    private void Version13To14(in JsonElement json, ref int version)
    {
        if (version is not 13)
            return;

        DebugUtilities.BackupJsonFiles(json.PropertyOrDefault("ModDirectory"u8, string.Empty));
        version = 14;
    }

    private void Version12To13(ref int version)
    {
        if (version is not 12)
            return;

        backupService.CreateMigrationBackup("pre_local_mod_db",
            saveService.FileNames.Migration.OldLocalDataFiles.Concat([
                saveService.FileNames.ConfigurationFile,
                saveService.FileNames.Migration.Ephemeral, saveService.FileNames.Migration.UiConfigFile,
                saveService.FileNames.Migration.FilterFile,
            ]));
        localModDatabase.Migrate();
        version = 13;
    }

    private void Version11To12(ref int version)
    {
        if (version is not 11)
            return;

        backupService.CreateMigrationBackup("pre_initial_json_update",
            saveService.FileNames.Migration.OldLocalDataFiles.Concat([
                saveService.FileNames.ConfigurationFile,
                saveService.FileNames.Migration.Ephemeral, saveService.FileNames.Migration.UiConfigFile,
                saveService.FileNames.Migration.FilterFile,
            ]));
        version = 12;
    }

    private JsonDocument? ReadAndDelete(string path)
    {
        try
        {
            var json = IJsonParsable.ReadJson<ParsableJsonDocument>(saveService, path, false);
            return json.Document;
        }
        catch (Exception ex)
        {
            messages.NotificationMessage(ex, $"Failed to read {Path.GetFileNameWithoutExtension(path)} for migration", NotificationType.Error);
            return null;
        }
        finally
        {
            saveService.DeleteWithBackup(path);
        }
    }

    private void DeleteMetaTmp()
    {
        var path = Path.Combine(config.Main.ModDirectory, "penumbrametatmp");
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, true);
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not delete the outdated penumbrametatmp folder:\n{e}");
        }
    }
}

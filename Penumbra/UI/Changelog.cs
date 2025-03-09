using OtterGui.Services;
using OtterGui.Widgets;

namespace Penumbra.UI;

public class PenumbraChangelog : IUiService
{
    public const int LastChangelogVersion = 0;

    private readonly Configuration _config;
    public readonly  Changelog     Changelog;

    public PenumbraChangelog(Configuration config)
    {
        _config   = config;
        Changelog = new Changelog("Penumbra Changelog", ConfigData, Save);

        Add5_7_0(Changelog);
        Add5_7_1(Changelog);
        Add5_8_0(Changelog);
        Add5_8_7(Changelog);
        Add5_9_0(Changelog);
        Add5_10_0(Changelog);
        Add5_11_0(Changelog);
        Add5_11_1(Changelog);
        Add6_0_0(Changelog);
        Add6_0_2(Changelog);
        Add6_0_5(Changelog);
        Add6_1_0(Changelog);
        Add6_1_1(Changelog);
        Add6_2_0(Changelog);
        Add6_3_0(Changelog);
        Add6_4_0(Changelog);
        Add6_5_0(Changelog);
        Add6_5_2(Changelog);
        Add6_6_0(Changelog);
        Add6_6_1(Changelog);
        Add7_0_0(Changelog);
        Add7_0_1(Changelog);
        Add7_0_4(Changelog);
        Add7_1_0(Changelog);
        Add7_1_2(Changelog);
        Add7_2_0(Changelog);
        Add7_3_0(Changelog);
        Add8_0_0(Changelog);
        Add8_1_1(Changelog);
        Add8_1_2(Changelog);
        Add8_2_0(Changelog);
        Add8_3_0(Changelog);
        Add1_0_0_0(Changelog);
        AddDummy(Changelog);
        AddDummy(Changelog);
        Add1_1_0_0(Changelog);
        Add1_1_1_0(Changelog);
        Add1_2_1_0(Changelog);
        Add1_3_0_0(Changelog);
        Add1_3_1_0(Changelog);
        Add1_3_2_0(Changelog);
        Add1_3_3_0(Changelog);
        Add1_3_4_0(Changelog);
        Add1_3_5_0(Changelog);
    }

    #region Changelogs

    private static void Add1_3_5_0(Changelog log)
        => log.NextVersion("Version 1.3.5.0")
            .RegisterImportant(
                "Redirections of unsupported file types like .atch will now produce warnings when they are enabled. Please update mods still containing them or request updates from their creators.")
            .RegisterEntry("You can now import .atch in the Meta section of advanced editing to add their non-default changes to the mod.")
            .RegisterHighlight("Added an option in settings and in the collection bar in the mod tab to always use temporary settings.")
            .RegisterEntry(
                "While this option is enabled, all changes you make in the current collection will be applied as temporary changes, and you have to use Turn Permanent to make them permanent.",
                1)
            .RegisterEntry(
                "This should be useful for trying out new mods without needing to reset their settings later, or for creating mod associations in Glamourer from them.",
                1)
            .RegisterEntry(
                "Added a context menu entry on the mod selector blank-space context menu to clear all temporary settings made manually.")
            .RegisterHighlight(
                "Resource Trees now consider some additional files like decals, and improved the quick-import behaviour for some files that should not generally be modded.")
            .RegisterHighlight("The Changed Item display for single mods has been heavily improved.")
            .RegisterEntry("Any changed item will now show how many individual edits are affecting it in the mod in its tooltip.", 1)
            .RegisterEntry("Equipment pieces are now grouped by their model id, reducing clutter.",                                1)
            .RegisterEntry(
                "The primary equipment piece displayed is the one with the most changes affecting it, but can be configured to a specific item by the mod creator and locally.",
                1)
            .RegisterEntry(
                "Preferred changed items stored in the mod will be shared when exporting the mod, and used as the default for local preferences, which will not be shared.",
                2)
            .RegisterEntry(
                "You can configure whether groups are automatically collapsed or expanded, or remove grouping entirely in the settings.", 1)
            .RegisterHighlight("Fixed support for model import/export with more than one UV.")
            .RegisterEntry("Added some IPC relating to changed items.")
            .RegisterEntry("Skeleton and Physics changes should now be identified in Changed Items.")
            .RegisterEntry("Item Swaps will now also correctly swap EQP entries of multi-slot pieces.")
            .RegisterEntry("Meta edit transmission through IPC should be a lot more efficient than before.")
            .RegisterEntry("Fixed an issue with incognito names in some cutscenes.")
            .RegisterEntry("Newly extracted mod folders will now try to rename themselves three times before being considered a failure.");

    private static void Add1_3_4_0(Changelog log)
        => log.NextVersion("Version 1.3.4.0")
            .RegisterHighlight(
                "Added HDR functionality to diffuse buffers. This allows more accurate representation of non-standard color values for e.g. skin or hair colors when used with advanced customizations in Glamourer.")
            .RegisterEntry(
                "This option requires Wait For Plugins On Load to be enabled in Dalamud and to be enabled on start to work. It is on by default but can be turned off.",
                1)
            .RegisterHighlight("Added a new option group type: Combining Groups.")
            .RegisterEntry(
                "A combining group behaves similarly to a multi group for the user, but instead of enabling the different options separately, it results in exactly one option per choice of settings.",
                1)
            .RegisterEntry(
                "Example: The user sees 2 checkboxes [+25%, +50%], but the 4 different selection states result in +0%, +25%, +50% or +75% if both are toggled on. Every choice of settings can be configured separately by the mod creator.",
                1)
            .RegisterEntry(
                "Added new functionality to better track copies of the player character in cutscenes if they get forced to specific clothing, like in the Margrat cutscene. Might improve tracking in wedding ceremonies, too, let me know.")
            .RegisterEntry("Added a display of the number of selected files and folders to the multi mod selection.")
            .RegisterEntry(
                "Added cleaning functionality to remove outdated or unused files or backups from the config and mod folders via manual action.")
            .RegisterEntry("Updated the Bone and Material limits in the Model Importer.")
            .RegisterEntry("Improved handling of IMC and Material files loaded asynchronously.")
            .RegisterEntry("Added IPC functionality to query temporary settings.")
            .RegisterEntry("Improved some mod setting IPC functions.")
            .RegisterEntry("Fixed some path detection issues in the OnScreen tab.")
            .RegisterEntry("Fixed some issues with temporary mod settings.")
            .RegisterEntry("Fixed issues with IPC calls before the game has finished loading.")
            .RegisterEntry("Fixed using the wrong dye channel in the material editor previews.")
            .RegisterEntry("Added some log warnings if outdated materials are loaded by the game.")
            .RegisterEntry("Added Schemas for some of the json files generated and read by Penumbra to the solution.");

    private static void Add1_3_3_0(Changelog log)
        => log.NextVersion("Version 1.3.3.0")
            .RegisterHighlight("Added Temporary Settings to collections.")
            .RegisterEntry(
                "Settings can be manually turned temporary (and turned back) while editing mod settings via right-click context on the mod or buttons in the settings panel.",
                1)
            .RegisterEntry(
                "This can be used to test mods or changes without saving those changes permanently or having to reinstate the old settings afterwards.",
                1)
            .RegisterEntry(
                "More importantly, this can be set via IPC by other plugins, allowing Glamourer to only set and reset temporary settings when applying Mod Associations.",
                1)
            .RegisterEntry(
                "As an extreme example, it would be possible to only enable the consistent mods for your character in the collection, and let Glamourer handle all outfit mods itself via temporary settings only.",
                1)
            .RegisterEntry(
                "This required some pretty big changes that were in testing for a while now, but nobody talked about it much so it may still have some bugs or usability issues. Let me know!",
                1)
            .RegisterHighlight(
                "Added an option to automatically select the collection assigned to the current character on login events. This is off by default.")
            .RegisterEntry(
                "Added partial copying of color tables in material editing via right-click context menu entries on the import buttons.")
            .RegisterHighlight(
                "Added handling for TMB files cached by the game that should resolve issues of leaky TMBs from animation and VFX mods.")
            .RegisterEntry(
                "The enabled checkbox, Priority and Inheriting buttons now stick at the top of the Mod Settings panel even when scrolling down for specific settings.")
            .RegisterEntry("When creating new mods with Item Swap, the attributed author of the resulting mod was improved.")
            .RegisterEntry("Fixed an issue with rings in the On-Screen tab and in the data sent over to other plugins via IPC.")
            .RegisterEntry(
                "Fixed some issues when writing material files that resulted in technically valid files that still caused some issues with the game for unknown reasons.")
            .RegisterEntry("Fixed some ImGui assertions.");

    private static void Add1_3_2_0(Changelog log)
        => log.NextVersion("Version 1.3.2.0")
            .RegisterHighlight("Added ATCH meta manipulations that allow the composite editing of attachment points across multiple mods.")
            .RegisterEntry("Those ATCH manipulations should be shared via Mare Synchronos.", 1)
            .RegisterEntry(
                "This is an early implementation and might be bug-prone. Let me know of any issues. It was in testing for quite a while without reports.",
                1)
            .RegisterEntry(
                "Added jumping to identified mods in the On-Screen tab via Control + Right-Click and improved their display slightly.")
            .RegisterEntry("Added some right-click context menu copy options in the File Redirections editor for paths.")
            .RegisterHighlight("Added the option to change a specific mod's settings via chat commands by using '/penumbra mod settings'.")
            .RegisterEntry("Fixed issues with the copy-pasting of meta manipulations.")
            .RegisterEntry("Fixed some other issues related to meta manipulations.")
            .RegisterEntry(
                "Updated available NPC names and fixed an issue with some supposedly invisible characters in names showing in ImGui.");


    private static void Add1_3_1_0(Changelog log)
        => log.NextVersion("Version 1.3.1.0")
            .RegisterEntry("Penumbra has been updated for Dalamud API 11 and patch 7.1.")
            .RegisterImportant(
                "There are some known issues with potential crashes using certain VFX/SFX mods, probably related to sound files.")
            .RegisterEntry(
                "If you encounter those issues, please report them in the discord and potentially disable the corresponding mods for the time being.",
                1)
            .RegisterImportant(
                "The modding of .atch files has been disabled. Outdated modded versions of these files cause crashes when loaded.")
            .RegisterEntry("A better way for modular modding of .atch files via meta changes will release to the testing branch soonish.", 1)
            .RegisterHighlight("Temporary collections (as created by Mare) will now always respect ownership.")
            .RegisterEntry(
                "This means that you can toggle this setting off if you do not want it, and Mare will still work for minions and mounts of other players.",
                1)
            .RegisterEntry(
                "The new physics and animation engine files (.kdb and .bnmb) should now be correctly redirected and respect EST changes.")
            .RegisterEntry("Fixed issues with EQP entries being labeled wrongly and global EQP not changing all required values for earrings.")
            .RegisterEntry("Fixed an issue with global EQP changes of a mod being reset upon reloading the mod.")
            .RegisterEntry("Fixed another issue with left rings and mare synchronization / the on-screen tab.")
            .RegisterEntry("Maybe fixed some issues with characters appearing in the login screen being misidentified.")
            .RegisterEntry("Some improvements for debug visualization have been made.");


    private static void Add1_3_0_0(Changelog log)
        => log.NextVersion("Version 1.3.0.0")
            .RegisterHighlight("The textures tab in the advanced editing window can now import and export .tga files.")
            .RegisterEntry("BC4 and BC6 textures can now also be imported.", 1)
            .RegisterHighlight("Added item swapping from and to the Glasses slot.")
            .RegisterEntry("Reworked quite a bit of things around face wear / bonus items. Please let me know if anything broke.", 1)
            .RegisterEntry("The import date of a mod is now shown in the Edit Mod tab, and can be reset via button.")
            .RegisterEntry("A button to open the file containing local mod data for a mod was also added.", 1)
            .RegisterHighlight(
                "IMC groups can now be configured to only apply the attribute flags for their entry, and take the other values from the default value.")
            .RegisterEntry("This allows keeping the material index of every IMC entry of a group, while setting the attributes.", 1)
            .RegisterHighlight("Model Import/Export was fixed and re-enabled (thanks ackwell and ramen).")
            .RegisterHighlight("Added a hack to allow bonus items (face wear, glasses) to have VFX.")
            .RegisterEntry("Also fixed the hack that allowed accessories to have VFX not working anymore.", 1)
            .RegisterHighlight("Added rudimentary options to edit PBD files in the advanced editing window.")
            .RegisterEntry("Preparing the advanced editing window for a mod now does not freeze the game until it is ready.")
            .RegisterEntry(
                "Meta Manipulations in the advanced editing window are now ordered and do not eat into performance as much when drawn.")
            .RegisterEntry("Added a button to the advanced editing window to remove all default-valued meta manipulations from a mod")
            .RegisterEntry(
                "Default-valued manipulations will now also be removed on import from archives and .pmps, not just .ttmps, if not configured otherwise.",
                1)
            .RegisterEntry("Checkbox-based mod filters are now tri-state checkboxes instead of two disjoint checkboxes.")
            .RegisterEntry("Paths from the resource logger can now be copied.")
            .RegisterEntry("Silenced some redundant error logs when updating mods via Heliosphere.")
            .RegisterEntry("Added 'Page' to imported mod data for TexTools interop. The value is not used in Penumbra, just persisted.")
            .RegisterEntry("Updated all external dependencies.")
            .RegisterEntry("Fixed issue with Demihuman IMC entries.")
            .RegisterEntry("Fixed some off-by-one errors on the mod import window.")
            .RegisterEntry("Fixed a race-condition concerning the first-time creation of mod-meta files.")
            .RegisterEntry("Fixed an issue with long mod titles in the merge mods tab.")
            .RegisterEntry("A bunch of other miscellaneous fixes.");


    private static void Add1_2_1_0(Changelog log)
        => log.NextVersion("Version 1.2.1.0")
            .RegisterHighlight("Penumbra is now released for Dawntrail!")
            .RegisterEntry("Mods themselves may have to be updated. TexTools provides options for this.",                              1)
            .RegisterEntry("For model files, Penumbra provides a rudimentary update function, but prefer using TexTools if possible.", 1)
            .RegisterEntry("Other files, like materials and textures, will have to go through TexTools for the moment.",               1)
            .RegisterEntry(
                "Some outdated mods can be identified by Penumbra and are prevented from loading entirely (specifically shaders, by Ny).", 1)
            .RegisterImportant("I am sorry that it took this long, but there was an immense amount of work to be done from the start.")
            .RegisterImportant(
                "Since Penumbra has been in Testing for quite a while, multitudes of bugs and issues cropped up that needed to be dealt with.",
                1)
            .RegisterEntry("There very well may still be a lot of issues, so please report any you find.", 1)
            .RegisterImportant("BUT, please make sure that those issues are not caused by outdated mods before reporting them.", 1)
            .RegisterEntry(
                "This changelog may seem rather short for the timespan, but I omitted hundreds of smaller fixes and the details of getting Penumbra to work in Dawntrail.",
                1)
            .RegisterHighlight("The Material Editing tab in the Advanced Editing Window has been heavily improved (by Ny).")
            .RegisterEntry(
                "Especially for Dawntrail materials using the new shaders, the window provides much more in-depth and user-friendly editing options.",
                1)
            .RegisterHighlight("Many advancements regarding modded shaders, and modding bone deformers have been made.")
            .RegisterHighlight("IMC groups now allow their options to toggle attributes off that are on in the default entry.")
            .RegisterImportant(
                "The 'Update Bibo' button was removed. The functionality is redundant since any mods that old need to be updated anyway.")
            .RegisterEntry("Clicking the button on modern mods generally caused more harm than benefit.", 1)
            .RegisterEntry(
                "If you somehow still need to mass-migrate materials in your models, the Material Reassignment tab in Advanced Editing is still available for this.",
                1)
            .RegisterEntry("The On-Screen tab was updated and improved and can now display modded actual paths in more useful form.")
            .RegisterImportant("Model Import/Export is temporarily disabled until Dawntrail-related changes can be made.")
            .RegisterHighlight("You can now change a mods state in any collection from its Collections tab via right-clicking the state.")
            .RegisterHighlight("Items changed in a mod now sort before other items in the Item Swap tab, and are highlighted.")
            .RegisterEntry("Path handling was improved in regards to case-sensitivity.")
            .RegisterEntry("Fixed an issue with negative search matching on folders with no matches")
            .RegisterEntry("Mod option groups on the same priority are now applied in reverse index order. (1.2.0.12)")
            .RegisterEntry("Fixed the display of missing files in the Advanced Editing Window's header. (1.2.0.8)")
            .RegisterEntry(
                "Fixed some, but not all soft-locks that occur when your character gets redrawn while fishing. Just do not do that. (1.2.0.7)")
            .RegisterEntry("Improved handling of invalid Offhand IMC files for certain jobs. (1.2.0.6)")
            .RegisterEntry("Added automatic reduplication for files in the UI category, as they cause crashes when not unique. (1.2.0.5)")
            .RegisterEntry("The mod import popup can now be closed by clicking outside of it, if it is finished. (1.2.0.5)")
            .RegisterEntry("Fixed an issue with Mod Normalization skipping the default option. (1.2.0.5)")
            .RegisterEntry("Improved the Support Info output. (1.1.1.5)")
            .RegisterEntry("Reworked the handling of Meta Manipulations entirely. (1.1.1.3)")
            .RegisterEntry("Added a configuration option to disable showing mods in the character lobby and at the aesthetician. (1.1.1.1)")
            .RegisterEntry("Fixed an issue with the AddMods API and the root directory. (1.1.1.2)")
            .RegisterEntry("Fixed an issue with the Mod Merger file lookup and casing. (1.1.1.2)")
            .RegisterEntry("Fixed an issue with file saving not happening when merging mods or swapping items in some cases. (1.1.1.2)");

    private static void Add1_1_1_0(Changelog log)
        => log.NextVersion("Version 1.1.1.0")
            .RegisterHighlight("Filtering for mods is now tokenized and can filter for multiple things at once, or exclude specific things.")
            .RegisterEntry("Hover over the filter to see the new available options in the tooltip.", 1)
            .RegisterEntry("Be aware that the tokenization changed the prior behavior slightly.",    1)
            .RegisterEntry("This is open to improvements, if you have any ideas, let me know!",      1)
            .RegisterHighlight("Added initial identification of characters in the login-screen by name.")
            .RegisterEntry(
                "Those characters can not be redrawn and re-use some things, so this may not always behave as expected, but should work in general. Let me know if you encounter edge cases!",
                1)
            .RegisterEntry("Added functionality for IMC groups to apply to all variants for a model instead of a specific one.")
            .RegisterEntry("Improved the resource tree view with filters and incognito mode. (by Ny)")
            .RegisterEntry("Added a tooltip to the global EQP condition.")
            .RegisterEntry("Fixed the new worlds not being identified correctly because Square Enix could not be bothered to turn them public.")
            .RegisterEntry("Fixed model import getting stuck when doing weight adjustments. (by ackwell)")
            .RegisterEntry("Fixed an issue with dye previews in the material editor not applying.")
            .RegisterEntry("Fixed an issue with collections not saving on renames.")
            .RegisterEntry("Fixed an issue parsing collections with settings set to negative values, which should now be set to 0.")
            .RegisterEntry("Fixed an issue with the accessory VFX addition.")
            .RegisterEntry("Fixed an issue with GMP animation type entries.")
            .RegisterEntry("Fixed another issue with the mod merger.")
            .RegisterEntry("Fixed an issue with IMC groups and IPC.")
            .RegisterEntry("Fixed some issues with the capitalization of the root directory.")
            .RegisterEntry("Fixed IMC attribute tooltips not appearing for disabled checkboxes.")
            .RegisterEntry("Added GetChangedItems IPC for single mods. (1.1.0.2)")
            .RegisterEntry("Fixed an issue with creating unnamed collections. (1.1.0.2)")
            .RegisterEntry("Fixed an issue with the mod merger. (1.1.0.2)")
            .RegisterEntry("Fixed the global EQP entry for rings checking for bracelets instead of rings. (1.1.0.2)")
            .RegisterEntry("Fixed an issue with newly created collections not being added to the collection list. (1.1.0.1)");

    private static void Add1_1_0_0(Changelog log)
        => log.NextVersion("Version 1.1.0.0")
            .RegisterImportant(
                "This update comes, again, with a lot of very heavy backend changes (collections and groups) and thus may introduce new issues.")
            .RegisterEntry("Updated to .net8 and XIV 6.58, using some new framework facilities to improve performance and stability.")
            .RegisterHighlight(
                "Added an experimental crash handler that is supposed to write a Penumbra.log file when the game crashes, containing Penumbra-specific information.")
            .RegisterEntry("This is disabled by default. It can be enabled in Advanced Settings.", 1)
            .RegisterHighlight("Collections now have associated GUIDs as identifiers instead of their names, so they can now be renamed.")
            .RegisterEntry("Migrating those collections may introduce issues, please let me know as soon as possible if you encounter any.", 1)
            .RegisterEntry("A permanent (non-rolling) backup should be created before the migration in case of any issues.",                 1)
            .RegisterHighlight(
                "Added predefined tags that can be setup in the Settings tab and can be more easily applied or removed from mods. (by DZD)")
            .RegisterHighlight(
                "A total rework of how options and groups are handled internally, and introduction of the first new group type, the IMC Group.")
            .RegisterEntry(
                "Mod Creators can add a IMC Group to their mod that controls a single IMC Manipulation, so they can provide options for the separate attributes for it.",
                1)
            .RegisterEntry(
                "This makes it a lot easier to have combined options: No need for 'A', 'B' and 'AB', you can just define 'A' and 'B' and skip their combinations",
                1)
            .RegisterHighlight("A new type of Meta Manipulation was added, 'Global EQP Manipulation'.")
            .RegisterEntry(
                "Global EQP Manipulations allow accessories to make other equipment pieces not hide them, e.g. whenever a character is wearing a specific Bracelet, neither body nor hand items will ever hide bracelets.",
                1)
            .RegisterEntry(
                "This can be used if something like a jacket or a stole is put onto an accessory to prevent it from being hidden in general.",
                1)
            .RegisterEntry(
                "The first empty option in a single-select option group imported from a TTMP will now keep its location instead of being moved to the first option.")
            .RegisterEntry("Further empty options are still removed.", 1)
            .RegisterHighlight(
                "Added a field to rename mods directly from the mod selector context menu, instead of moving them in the filesystem.")
            .RegisterEntry("You can choose which rename field (none, either one or both) to display in the settings.", 1)
            .RegisterEntry("Added the characterglass.shpk shader file to special shader treatment to fix issues when replacing it. (By Ny)")
            .RegisterEntry("Made it more obvious if a user has not set their root directory yet.")
            .RegisterEntry(
                "You can now paste your current clipboard text into the mod selector filter with a simple right-click as long as it is not focused.")
            .RegisterHighlight(
                "Added the option to display VFX for accessories if added via IMC edits, which the game does not do inherently (by Ocealot).")
            .RegisterEntry("Added support for reading and writing the new material and model file formats from the benchmark.")
            .RegisterEntry(
                "Added the option to hide Machinist Offhands from the Changed Items tabs (because any change to it changes ALL of them), which is on by default.")
            .RegisterEntry("Removed the auto-generated descriptions for newly created groups in Penumbra.")
            .RegisterEntry(
                "Made some improvements to the Advanced Editing window, for example a much better and more performant Hex Viewer for unstructured data was added.")
            .RegisterEntry("Various improvements to model import/export by ackwell (throughout all patches).")
            .RegisterEntry(
                "Hovering over meta manipulations in other options in the advanced editing window now shows a list of those options.")
            .RegisterEntry("Reworked the API and IPC structure heavily.")
            .RegisterImportant("This means some plugins interacting with Penumbra may not work correctly until they update.", 1)
            .RegisterEntry("Worked around the UI IPC possibly displacing all settings when the drawn additions became too big.")
            .RegisterEntry("Fixed an issue where reloading a mod did not ensure settings for that mod being correct afterwards.")
            .RegisterEntry("Fixed some issues with the file sizes of compressed files.")
            .RegisterEntry("Fixed an issue with merging and deduplicating mods.")
            .RegisterEntry("Fixed a crash when scanning for mods without access rights to the folder.")
            .RegisterEntry(
                "Made plugin conform to Dalamud requirements by adding a punchline and another button to open the menu from the installer.")
            .RegisterEntry("Added an option to automatically redraw the player character when saving files. (1.0.0.8)")
            .RegisterEntry("Fixed issue with manipulating mods not triggering some events.  (1.0.0.7)")
            .RegisterEntry("Fixed issue with temporary mods not triggering some events.  (1.0.0.6)")
            .RegisterEntry("Fixed issue when renaming mods while the advanced edit window is open. (1.0.0.6)")
            .RegisterEntry("Fixed issue with empty option groups. (1.0.0.5)")
            .RegisterEntry("Fixed issues with cutscene character identification. (1.0.0.4)")
            .RegisterEntry("Added locale environment information to support info. (1.0.0.4)")
            .RegisterEntry("Fixed an issue with copied mod settings in IPC missing unused settings. (1.0.0.3)");

    private static void Add1_0_0_0(Changelog log)
        => log.NextVersion("Version 1.0.0.0")
            .RegisterHighlight("Mods in the mod selector can now be filtered by changed item categories.")
            .RegisterHighlight("Model Editing options in the Advanced Editing Window have been greatly extended (by ackwell):")
            .RegisterEntry("Attributes and referenced materials can now be set per mesh.", 1)
            .RegisterEntry("Model files (.mdl) can now be exported to the well-established glTF format, which can be imported e.g. by Blender.",
                1)
            .RegisterEntry("glTF files can also be imported back to a .mdl file.", 1)
            .RegisterHighlight(
                "Model Export and Import are a work in progress and may encounter issues, not support all cases or produce wrong results, please let us know!",
                1)
            .RegisterEntry("The last selected mod and the open/close state of the Advanced Editing Window are now stored across launches.")
            .RegisterEntry("Footsteps of certain mounts will now be associated to collections correctly.")
            .RegisterEntry("Save-in-Place in the texture tab now requires the configurable modifier.")
            .RegisterEntry("Updated OtterTex to a newer version of DirectXTex.")
            .RegisterEntry("Fixed an issue with horizontal scrolling if a mod title was very long.")
            .RegisterEntry("Fixed an issue with the mod panels header not updating its data when the selected mod updates.")
            .RegisterEntry("Fixed some issues with EQDP files for invalid characters.")
            .RegisterEntry("Fixed an issue with the FileDialog being drawn twice in certain situations.")
            .RegisterEntry(
                "A lot of backend changes that should not have an effect on users, but may cause issues if something got messed up.");

    private static void Add8_3_0(Changelog log)
        => log.NextVersion("Version 0.8.3.0")
            .RegisterHighlight("Improved the UI for the On-Screen tabs with highlighting of used paths, filtering and more selections. (by Ny)")
            .RegisterEntry(
                "Added an option to replace non-ASCII symbols with underscores for folder paths on mod import since this causes problems on some WINE systems. This option is off by default.")
            .RegisterEntry(
                "Added support for the Changed Item Icons to load modded icons, but this depends on a not-yet-released Dalamud update.")
            .RegisterEntry(
                "Penumbra should no longer redraw characters while they are fishing, but wait for them to reel in, because that could cause soft-locks. This may cause other issues, but I have not found any.")
            .RegisterEntry(
                "Hopefully fixed a bug on mod import where files were being read while they were still saving, causing Penumbra to create wrong options.")
            .RegisterEntry("Fixed a few display issues.")
            .RegisterEntry("Added some IPC functionality for Xande. (by Asriel)");

    private static void Add8_2_0(Changelog log)
        => log.NextVersion("Version 0.8.2.0")
            .RegisterHighlight(
                "You can now redraw indoor furniture. This may not be entirely stable and might break some customizable decoration like wallpapered walls.")
            .RegisterEntry("The redraw bar has been slightly improved and disables currently unavailable redraw commands now.")
            .RegisterEntry("Redrawing players now also actively redraws any accessories they are using.")
            .RegisterEntry("Power-users can now redraw game objects by index via chat command.")
            .RegisterHighlight(
                "You can now filter for the special case 'None' for filters where that makes sense (like Tags or Changed Items).")
            .RegisterHighlight("When selecting multiple mods, you can now add or remove tags from them at once.")
            .RegisterEntry(
                "The dye template combo in advanced material editing now displays the currently selected dye as it would appear with the respective template.")
            .RegisterEntry("The On-Screen tab and associated functionality has been heavily improved by Ny.")
            .RegisterEntry("Fixed an issue with the changed item identification for left rings.")
            .RegisterEntry("Updated BNPC data.")
            .RegisterEntry(
                "Some configuration like the currently selected tab states are now stored in a separate file that is not backed up and saved less often.")
            .RegisterEntry("Added option to open the Penumbra main window at game start independently of Debug Mode.")
            .RegisterEntry("Fixed some tooltips in the advanced editing window. (0.8.1.8)")
            .RegisterEntry("Fixed clicking to linked changed items not working. (0.8.1.8)")
            .RegisterEntry("Support correct handling of offhand-parts for two-handed weapons for changed items. (0.8.1.7)")
            .RegisterEntry("Fixed renaming the mod directory not updating paths in the advanced window. (0.8.1.6)")
            .RegisterEntry("Fixed portraits not respecting your card settings. (0.8.1.6)")
            .RegisterEntry("Added ReverseResolvePlayerPathsAsync for IPC. (0.8.1.6)")
            .RegisterEntry("Expanded the tooltip for Wait for Plugins on Startup. (0.8.1.5)")
            .RegisterEntry("Disabled window sounds for some popup windows. (0.8.1.5)")
            .RegisterEntry("Added support for middle-clicking mods to enable/disable them. (0.8.1.5)");

    private static void Add8_1_2(Changelog log)
        => log.NextVersion("Version 0.8.1.2")
            .RegisterEntry("Fixed an issue keeping mods selected after their deletion.")
            .RegisterEntry("Maybe fixed an issue causing individual assignments to get lost on game start.");

    private static void Add8_1_1(Changelog log)
        => log.NextVersion("Version 0.8.1.1")
            .RegisterImportant(
                "Updated for 6.5 - Square Enix shuffled around a lot of things this update, so some things still might not work but have not been noticed yet. Please report any issues.")
            .RegisterEntry("Added support for chat commands to affect multiple individuals matching the supplied string at once.")
            .RegisterEntry(
                "Improved messaging: many warnings or errors appearing will stay a little longer and can now be looked at in a Messages tab (visible only if there have been any).")
            .RegisterEntry("Fixed an issue with leading or trailing spaces when renaming mods.");


    private static void Add8_0_0(Changelog log)
        => log.NextVersion("Version 0.8.0.0")
            .RegisterEntry(
                "Penumbra now uses Windows' transparent file system compression by default (on Windows systems). You can disable this functionality in the settings.")
            .RegisterImportant("You can retroactively compress your existing mods in the settings via the press of a button, too.", 1)
            .RegisterEntry(
                "In our tests, this not only was able to reduce storage space by 30-60%, it even decreased loading times since less I/O had to take place.",
                1)
            .RegisterEntry("Added emotes to changed item identification.")
            .RegisterEntry(
                "Added quick select buttons to switch to the current interface collection or the collection applying to the current player character in the mods tab, reworked their text and tooltips slightly.")
            .RegisterHighlight("Drag & Drop of multiple mods and folders at once is now supported by holding Control while clicking them.")
            .RegisterEntry("You can now disable conflicting mods from the Conflicts panel via Control + Right-click.")
            .RegisterEntry("Added checks for your deletion-modifiers for restoring mods from backups or deleting backups.")
            .RegisterEntry(
                "Penumbra now should automatically try to restore your custom sort order (mod folders) and your active collections from backups if they fail to load. No guarantees though.")
            .RegisterEntry("The resource watcher now displays a column providing load state information of resources.")
            .RegisterEntry(
                "Custom RSP scaling outside of the collection assigned to Base should now be respected for emotes that adjust your stance on height differences.")
            .RegisterEntry(
                "Mods that replace the skin shaders will not cause visual glitches like loss of head shadows or Free Company crest tattoos anymore (by Ny).")
            .RegisterEntry("The Material editor has been improved (by Ny):")
            .RegisterHighlight(
                "Live-Preview for materials yourself or entities owned by you are currently using, so you can see color set edits in real time.",
                1)
            .RegisterEntry(
                "Colors on the color table of a material can be highlighted on yourself or entities owned by you by hovering a button.", 1)
            .RegisterEntry("The color table has improved color accuracy.",                                                               1)
            .RegisterEntry("Materials with non-dyable color tables can be made dyable, and vice-versa.",                                 1)
            .RegisterEntry("The 'Advanced Shader Resources' section has been split apart into dedicated sections.",                      1)
            .RegisterEntry(
                "Addition and removal of shader keys, textures, constants and a color table has been automated following shader requirements and can not be done manually anymore.",
                1)
            .RegisterEntry(
                "Plain English names and tooltips can now be displayed instead of hexadecimal identifiers or code names by providing dev-kit files installed via certain mods.",
                1)
            .RegisterEntry("The Texture editor has been improved (by Ny):")
            .RegisterHighlight("The overlay texture can now be combined in several ways and automatically resized to match the input texture.",
                1)
            .RegisterEntry("New color manipulation options have been added.",                  1)
            .RegisterEntry("Modifications to the selected texture can now be saved in-place.", 1)
            .RegisterEntry("The On-Screen tab has been improved (by Ny):")
            .RegisterEntry("The character list will load more quickly.",                           1)
            .RegisterEntry("It is now able to deal with characters under transformation effects.", 1)
            .RegisterEntry(
                "The headers are now color-coded to distinguish between you and other players, and between NPCs that are handled locally or on the server. Colors are customizable.",
                1)
            .RegisterEntry("More file types will be recognized and shown.",                           1)
            .RegisterEntry("The actual paths for game files will be displayed and copied correctly.", 1)
            .RegisterEntry("The Shader editor has been improved (by Ny):")
            .RegisterEntry(
                "New sections 'Shader Resources' and 'Shader Selection' have been added, expanding on some data that was in 'Further Content' before.",
                1)
            .RegisterEntry("A fail-safe mode for shader decompilation on platforms that do not fully support it has been added.", 1)
            .RegisterEntry("Fixed invalid game paths generated for variants of customizations.")
            .RegisterEntry("Lots of minor improvements across the codebase.")
            .RegisterEntry("Some unnamed mounts were made available for actor identification. (0.7.3.2)");

    private static void Add7_3_0(Changelog log)
        => log.NextVersion("Version 0.7.3.0")
            .RegisterEntry(
                "Added the ability to drag and drop mod files from external sources (like a file explorer or browser) into Penumbras mod selector to import them.")
            .RegisterEntry("You can also drag and drop texture files into the textures tab of the Advanced Editing Window.", 1)
            .RegisterEntry(
                "Added a priority display to the mod selector using the currently selected collections priorities. This can be hidden in settings.")
            .RegisterEntry("Added IPC for texture conversion, improved texture handling backend and threading.")
            .RegisterEntry(
                "Added Dalamud Substitution so that other plugins can more easily use replaced icons from Penumbras Interface collection when using Dalamuds new Texture Provider.")
            .RegisterEntry("Added a filter to texture selection combos in the textures tab of the Advanced Editing Window.")
            .RegisterEntry(
                "Changed behaviour when failing to load group JSON files for mods - the pre-existing but failing files are now backed up before being deleted or overwritten.")
            .RegisterEntry("Further backend changes, mostly relating to the Glamourer rework.")
            .RegisterEntry("Fixed an issue with modded decals not loading correctly when used with the Glamourer rework.")
            .RegisterEntry("Fixed missing scaling with UI Scale for some combos.")
            .RegisterEntry("Updated the used version of SharpCompress to deal with Zip64 correctly.")
            .RegisterEntry("Added a toggle to not display the Changed Item categories in settings (0.7.2.2).")
            .RegisterEntry("Many backend changes relating to the Glamourer rework (0.7.2.2).")
            .RegisterEntry("Fixed an issue when multiple options in the same option group had the same label (0.7.2.2).")
            .RegisterEntry("Fixed an issue with a GPose condition breaking animation and vfx modding in GPose (0.7.2.1).")
            .RegisterEntry("Fixed some handling of decals (0.7.2.1).");

    private static void Add7_2_0(Changelog log)
        => log.NextVersion("Version 0.7.2.0")
            .RegisterEntry(
                "Added Changed Item Categories and icons that can filter for specific types of Changed Items, in the Changed Items Tab as well as in the Changed Items panel for specific mods..")
            .RegisterEntry(
                "Icons at the top can be clicked to filter, as well as right-clicked to open a context menu with the option to inverse-filter for them",
                1)
            .RegisterEntry("There is also an ALL button that can be toggled.", 1)
            .RegisterEntry(
                "Modded files in the Font category now resolve from the Interface assignment instead of the base assignment, despite not technically being in the UI category.")
            .RegisterEntry(
                "Timeline files will no longer be associated with specific characters in cutscenes, since there is no way to correctly do this, and it could cause crashes if IVCS-requiring animations were used on characters without IVCS.")
            .RegisterEntry("File deletion in the Advanced Editing Window now also checks for your configured deletion key combo.")
            .RegisterEntry(
                "The Texture tab in the Advanced Editing Window now has some quick convert buttons to just convert the selected texture to a different format in-place.")
            .RegisterEntry(
                "These buttons only appear if only one texture is selected on the left side, it is not otherwise manipulated, and the texture is a .tex file.",
                1)
            .RegisterEntry("The text part of the mod filter in the mod selector now also resets when right-clicking the drop-down arrow.")
            .RegisterEntry("The Dissolve Folder option in the mod selector context menu has been moved to the bottom.")
            .RegisterEntry("Somewhat improved IMC handling to prevent some issues.")
            .RegisterEntry(
                "Improved the handling of mod renames on mods with default-search names to correctly rename their search-name in (hopefully) all cases too.")
            .RegisterEntry("A lot of backend improvements and changes related to the pending Glamourer rework.")
            .RegisterEntry("Fixed an issue where the displayed active collection count in the support info was wrong.")
            .RegisterEntry(
                "Fixed an issue with created directories dealing badly with non-standard whitespace characters like half-width or non-breaking spaces.")
            .RegisterEntry("Fixed an issue with unknown animation and vfx edits not being recognized correctly.")
            .RegisterEntry("Fixed an issue where changing option descriptions to be empty was not working correctly.")
            .RegisterEntry("Fixed an issue with texture names in the resource tree of the On-Screen views.")
            .RegisterEntry("Fixed a bug where the game would crash when drawing folders in the mod selector that contained a '%' symbol.")
            .RegisterEntry("Fixed an issue with parallel algorithms obtaining the wrong number of available cores.")
            .RegisterEntry("Updated the available selection of Battle NPC names.")
            .RegisterEntry("A typo in the 0.7.1.2 Changlog has been fixed.")
            .RegisterEntry("Added the Sea of Stars as accepted repository. (0.7.1.4)")
            .RegisterEntry("Fixed an issue with collections sometimes not loading correctly, and IMC files not applying correctly. (0.7.1.3)");


    private static void Add7_1_2(Changelog log)
        => log.NextVersion("Version 0.7.1.2")
            .RegisterEntry(
                "Changed threaded handling of collection caches. Maybe this fixes the startup problems some people are experiencing.")
            .RegisterEntry(
                "This is just testing and may not be the solution, or may even make things worse. Sorry if I have to put out multiple small patches again to get this right.",
                1)
            .RegisterEntry("Fixed Penumbra failing to load if the main configuration file is corrupted.")
            .RegisterEntry("Some miscellaneous small bug fixes.")
            .RegisterEntry("Slight changes in behaviour for deduplicator/normalizer, mostly backend.")
            .RegisterEntry("A typo in the 0.7.1.0 Changelog has been fixed.")
            .RegisterEntry("Fixed left rings not being valid for IMC entries after validation. (7.1.1)")
            .RegisterEntry(
                "Relaxed the scaling restrictions for RSP scaling values to go from 0.01 to 512.0 instead of the prior upper limit of 8.0, in interface as well as validation, to better support the fetish community. (7.1.1)");

    private static void Add7_1_0(Changelog log)
        => log.NextVersion("Version 0.7.1.0")
            .RegisterEntry("Updated for patch 6.4 - there may be some oversights on edge cases, but I could not find any issues myself.")
            .RegisterImportant(
                "This update changed some Dragoon skills that were moving the player character before to not do that anymore. If you have any mods that applied to those skills, please make sure that they do not contain any redirections for .tmb files. If skills that should no longer move your character still do that for some reason, this is detectable by the server.",
                1)
            .RegisterEntry(
                "Added a Mod Merging tab in the Advanced Editing Window. This can help you merge multiple mods to one, or split off specific options from an existing mod into a new mod.")
            .RegisterEntry(
                "Added advanced options to configure the minimum allowed window size for the main window (to reduce it). This is not quite supported and may look bad, so only use it if you really need smaller windows.")
            .RegisterEntry("The last tab selected in the main window is now saved and re-used when relaunching Penumbra.")
            .RegisterEntry("Added a hook to correctly associate some sounds that are played while weapons are drawn.")
            .RegisterEntry("Added a hook to correctly associate sounds that are played while dismounting.")
            .RegisterEntry("A hook to associate weapon-associated VFX was expanded to work in more cases.")
            .RegisterEntry("TMB resources now use a collection prefix to prevent retained state in some cases.")
            .RegisterEntry("Improved startup times a bit.")
            .RegisterEntry("Right-Click context menus for collections are now also ordered by name.")
            .RegisterEntry("Advanced Editing tabs have been reordered and renamed slightly.")
            .RegisterEntry("Added some validation of metadata changes to prevent stalling on load of bad IMC edits.")
            .RegisterEntry("Fixed an issue where collections could lose their configured inheritances during startup in some cases.")
            .RegisterEntry("Fixed some bugs when mods were removed from collection caches.")
            .RegisterEntry("Fixed some bugs with IMC files not correctly reverting to default values in some cases.")
            .RegisterEntry("Fixed an issue with the mod import popup not appearing (0.7.0.10)")
            .RegisterEntry("Fixed an issue with the file selectors not always opening at the expected locations. (0.7.0.7)")
            .RegisterEntry("Fixed some cache handling issues. (0.7.0.5 - 0.7.0.10)")
            .RegisterEntry("Fixed an issue with multiple collection context menus appearing for some identifiers (0.7.0.5)")
            .RegisterEntry(
                "Fixed an issue where the Update Bibo button did only work if the Advanced Editing window was opened before. (0.7.0.5)");

    private static void Add7_0_4(Changelog log)
        => log.NextVersion("Version 0.7.0.4")
            .RegisterEntry("Added options to the bulktag slash command to check all/local/mod tags specifically.")
            .RegisterEntry("Possibly improved handling of the delayed loading of individual assignments.")
            .RegisterEntry("Fixed a bug that caused metadata edits to apply even though mods were disabled.")
            .RegisterEntry("Fixed a bug that prevented material reassignments from working.")
            .RegisterEntry("Reverted trimming of whitespace for relative paths to only trim the end, not the start. (0.7.0.3)")
            .RegisterEntry("Fixed a bug that caused an integer overflow on textures of high dimensions. (0.7.0.3)")
            .RegisterEntry("Fixed a bug that caused Penumbra to enter invalid state when deleting mods. (0.7.0.2)")
            .RegisterEntry("Added Notification on invalid collection names. (0.7.0.2)");

    private static void Add7_0_1(Changelog log)
        => log.NextVersion("Version 0.7.0.1")
            .RegisterEntry("Individual assignments can again be re-ordered by drag-and-dropping them.")
            .RegisterEntry("Relax the restriction of a maximum of 32 characters for collection names to 64 characters.")
            .RegisterEntry("Fixed a bug that showed the Your Character collection as redundant even if it was not.")
            .RegisterEntry("Fixed a bug that caused some required collection caches to not be built on startup and thus mods not to apply.")
            .RegisterEntry("Fixed a bug that showed the current collection as unused even if it was used.");

    private static void Add7_0_0(Changelog log)
        => log.NextVersion("Version 0.7.0.0")
            .RegisterImportant(
                "The entire backend was reworked (this is still in progress). While this does not come with a lot of functionality changes, basically every file and functionality was touched.")
            .RegisterEntry(
                "This may have (re-)introduced some bugs that have not yet been noticed despite a long testing period - there are not many users of the testing branch.",
                1)
            .RegisterEntry("If you encounter any - but especially breaking or lossy - bugs, please report them immediately.", 1)
            .RegisterEntry("This also fixed or improved numerous bugs and issues that will not be listed here.",              1)
            .RegisterEntry("GitHub currently reports 321 changed files with 34541 additions and 28464 deletions.",            1)
            .RegisterEntry("Added Notifications on many failures that previously only wrote to log.")
            .RegisterEntry("Reworked the Collections Tab to hopefully be much more intuitive. It should be self-explanatory now.")
            .RegisterEntry("The tutorial was adapted to the new window, if you are unsure, maybe try restarting it.", 1)
            .RegisterEntry(
                "You can now toggle an incognito mode in the collection window so it shows shortened names of collections and players.", 1)
            .RegisterEntry(
                "You can get an overview about the current usage of a selected collection and its active and unused mod settings in the Collection Details panel.",
                1)
            .RegisterEntry("The currently selected collection is now highlighted in green (default, configurable) in multiple places.", 1)
            .RegisterEntry(
                "Mods now have a 'Collections' panel in the Mod Panel containing an overview about usage of the mod in all collections.")
            .RegisterEntry("The 'Changed Items' and 'Effective Changes' tab now contain a collection selector.")
            .RegisterEntry("Added the On-Screen tab to find what files a specific character is actually using (by Ny).")
            .RegisterEntry("Added 3 Quick Move folders in the mod selector that can be setup in context menus for easier cleanup.")
            .RegisterEntry(
                "Added handling for certain animation files for mounts and fashion accessories to correctly associate them to players.")
            .RegisterEntry("The file selectors in the Advanced Mod Editing Window now use filterable combos.")
            .RegisterEntry(
                "The Advanced Mod Editing Window now shows the number of meta edits and file swaps in unselected options and highlights the option selector.")
            .RegisterEntry("Added API/IPC to start unpacking and installing mods from external tools (by Sebastina).")
            .RegisterEntry("Hidden files and folders are now ignored for unused files in Advanced Mod Editing (by myr)")
            .RegisterEntry("Paths in mods are now automatically trimmed of whitespace on loading.")
            .RegisterEntry("Fixed double 'by' in mod author display (by Caraxi).")
            .RegisterEntry("Fixed a crash when trying to obtain names from the game data.")
            .RegisterEntry("Fixed some issues with tutorial windows.")
            .RegisterEntry("Fixed some bugs in the Resource Logger.")
            .RegisterEntry("Fixed Button Sizing for collapsible groups and several related bugs.")
            .RegisterEntry("Fixed issue with mods with default settings other than 0.")
            .RegisterEntry("Fixed issue with commands not registering on startup. (0.6.6.5)")
            .RegisterEntry("Improved Startup Times and Time Tracking. (0.6.6.4)")
            .RegisterEntry("Add Item Swapping between different types of Accessories and Hats. (0.6.6.4)")
            .RegisterEntry("Fixed bugs with assignment of temporary collections and their deletion. (0.6.6.4)")
            .RegisterEntry("Fixed bugs with new file loading mechanism. (0.6.6.2, 0.6.6.3)")
            .RegisterEntry("Added API/IPC to open and close the main window and select specific tabs and mods. (0.6.6.2)");

    private static void Add6_6_1(Changelog log)
        => log.NextVersion("Version 0.6.6.1")
            .RegisterEntry("Added an option to make successful chat commands not print their success confirmations to chat.")
            .RegisterEntry("Fixed an issue with migration of old mods not working anymore (fixes Material UI problems).")
            .RegisterEntry("Fixed some issues with using the Assign Current Player and Assign Current Target buttons.");

    private static void Add6_6_0(Changelog log)
        => log.NextVersion("Version 0.6.6.0")
            .RegisterEntry(
                "Added new Collection Assignment Groups for Children NPC and Elderly NPC. Those take precedence before any non-individual assignments for any NPC using a child- or elderly model respectively.")
            .RegisterEntry(
                "Added an option to display Single Selection Groups as a group of radio buttons similar to Multi Selection Groups, when the number of available options is below the specified value. Default value is 2.")
            .RegisterEntry("Added a button in option groups to collapse the option list if it has more than 5 available options.")
            .RegisterEntry(
                "Penumbra now circumvents the games inability to read files at paths longer than 260 UTF16 characters and can also deal with generic unicode symbols in paths.")
            .RegisterEntry(
                "This means that Penumbra should no longer cause issues when files become too long or when there is a non-ASCII character in them.",
                1)
            .RegisterEntry(
                "Shorter paths are still better, so restrictions on the root directory have not been relaxed. Mod names should no longer replace non-ASCII symbols on import though.",
                1)
            .RegisterEntry(
                "Resource logging has been relegated to its own tab with better filtering. Please do not keep resource logging on arbitrarily or set a low record limit if you do, otherwise this eats a lot of performance and memory after a while.")
            .RegisterEntry(
                "Added a lot of facilities to edit the shader part of .mtrl files and .shpk files themselves in the Advanced Editing Tab (Thanks Ny and aers).")
            .RegisterEntry("Added splitting of Multi Selection Groups with too many options when importing .pmp files or adding mods via IPC.")
            .RegisterEntry("Discovery, Reloading and Unloading of a specified mod is now possible via HTTP API (Thanks Sebastina).")
            .RegisterEntry("Cleaned up the HTTP API somewhat, removed currently useless options.")
            .RegisterEntry("Fixed an issue when extracting some textures.")
            .RegisterEntry("Fixed an issue with mannequins inheriting individual assignments for the current player when using ownership.")
            .RegisterEntry(
                "Fixed an issue with the resolving of .phyb and .sklb files for Item Swaps of head or body items with an EST entry but no unique racial model.");

    private static void Add6_5_2(Changelog log)
        => log.NextVersion("Version 0.6.5.2")
            .RegisterEntry("Updated for game version 6.31 Hotfix.")
            .RegisterEntry(
                "Added option-specific descriptions for mods, instead of having just descriptions for groups of options. (Thanks Caraxi!)")
            .RegisterEntry("Those are now accurately parsed from TTMPs, too.", 1)
            .RegisterEntry("Improved launch times somewhat through parallelization of some tasks.")
            .RegisterEntry(
                "Added some performance tracking for start-up durations and for real time data to Release builds. They can be seen and enabled in the Debug tab when Debug Mode is enabled.")
            .RegisterEntry("Fixed an issue with IMC changes and Mare Synchronos interoperability.")
            .RegisterEntry("Fixed an issue with housing mannequins crashing the game when resource logging was enabled.")
            .RegisterEntry("Fixed an issue generating Mip Maps for texture import on Wine.");

    private static void Add6_5_0(Changelog log)
        => log.NextVersion("Version 0.6.5.0")
            .RegisterEntry("Fixed an issue with Item Swaps not using applied IMC changes in some cases.")
            .RegisterEntry("Improved error message on texture import when failing to create mip maps (slightly).")
            .RegisterEntry("Tried to fix duty party banner identification again, also for the recommendation window this time.")
            .RegisterEntry("Added batched IPC to improve Mare performance.");

    private static void Add6_4_0(Changelog log)
        => log.NextVersion("Version 0.6.4.0")
            .RegisterEntry("Fixed an issue with the identification of actors in the duty group portrait.")
            .RegisterEntry("Fixed some issues with wrongly cached actors and resources.")
            .RegisterEntry("Fixed animation handling after redraws (notably for PLD idle animations with a shield equipped).")
            .RegisterEntry("Fixed an issue with collection listing API skipping one collection.")
            .RegisterEntry(
                "Fixed an issue with BGM files being sometimes loaded from other collections than the base collection, causing crashes.")
            .RegisterEntry(
                "Also distinguished file resolving for different file categories (improving performance) and disabled resolving for script files entirely.",
                1)
            .RegisterEntry("Some miscellaneous backend changes due to the Glamourer rework.");

    private static void Add6_3_0(Changelog log)
        => log.NextVersion("Version 0.6.3.0")
            .RegisterEntry("Add an Assign Current Target button for individual assignments")
            .RegisterEntry("Try identifying all banner actors correctly for PvE duties, Crystalline Conflict and Mahjong.")
            .RegisterEntry("Please let me know if this does not work for anything except identical twins.", 1)
            .RegisterEntry("Add handling for the 3 new screen actors (now 8 total, for PvE dutie portraits).")
            .RegisterEntry("Update the Battle NPC name database for 6.3.")
            .RegisterEntry("Added API/IPC functions to obtain or set group or individual collections.")
            .RegisterEntry("Maybe fix a problem with textures sometimes not loading from their corresponding collection.")
            .RegisterEntry("Another try to fix a problem with the collection selectors breaking state.")
            .RegisterEntry("Fix a problem identifying companions.")
            .RegisterEntry("Fix a problem when deleting collections assigned to Groups.")
            .RegisterEntry(
                "Fix a problem when using the Assign Currently Played Character button and then logging onto a different character without restarting in between.")
            .RegisterEntry("Some miscellaneous backend changes.");

    private static void Add6_2_0(Changelog log)
        => log.NextVersion("Version 0.6.2.0")
            .RegisterEntry("Update Penumbra for .net7, Dalamud API 8 and patch 6.3.")
            .RegisterEntry("Add a Bulktag chat command to toggle all mods with specific tags. (by SoyaX)")
            .RegisterEntry("Add placeholder options for setting individual collections via chat command.")
            .RegisterEntry("Add toggles to swap left and/or right rings separately for ring item swap.")
            .RegisterEntry("Add handling for looping sound effects caused by animations in non-base collections.")
            .RegisterEntry("Add an option to not use any mods at all in the Inspect/Try-On window.")
            .RegisterEntry("Add handling for Mahjong actors.")
            .RegisterEntry("Improve hint text for File Swaps in Advanced Editing, also inverted file swap display order.")
            .RegisterEntry("Fix a problem where the collection selectors could get desynchronized after adding or deleting collections.")
            .RegisterEntry("Fix a problem that could cause setting state to get desynchronized.")
            .RegisterEntry("Fix an oversight where some special screen actors did not actually respect the settings made for them.")
            .RegisterEntry("Add collection and associated game object to Full Resource Logging.")
            .RegisterEntry("Add performance tracking for DEBUG-compiled versions (i.e. testing only).")
            .RegisterEntry("Add some information to .mdl display and fix not respecting padding when reading them. (0.6.1.3)")
            .RegisterEntry("Fix association of some vfx game objects. (0.6.1.3)")
            .RegisterEntry("Stop forcing AVFX files to load synchronously. (0.6.1.3)")
            .RegisterEntry("Fix an issue when incorporating deduplicated meta files. (0.6.1.2)");

    private static void Add6_1_1(Changelog log)
        => log.NextVersion("Version 0.6.1.1")
            .RegisterEntry(
                "Added a toggle to use all the effective changes from the entire currently selected collection for swaps, instead of the selected mod.")
            .RegisterEntry("Fix using equipment paths for accessory swaps and thus accessory swaps not working at all")
            .RegisterEntry("Fix issues with swaps with gender-locked gear where the models for the other gender do not exist.")
            .RegisterEntry("Fix swapping universal hairstyles for midlanders breaking them for other races.")
            .RegisterEntry("Add some actual error messages on failure to create item swaps.")
            .RegisterEntry("Fix warnings about more than one affected item appearing for single items.");

    private static void Add6_1_0(Changelog log)
        => log.NextVersion("Version 0.6.1.0 (Happy New Year! Edition)")
            .RegisterEntry("Add a prototype for Item Swapping.")
            .RegisterEntry("A new tab in Advanced Editing.",                                                                         1)
            .RegisterEntry("Swapping of Hair, Tail, Ears, Equipment and Accessories is supported. Weapons and Faces may be coming.", 1)
            .RegisterEntry("The manipulations currently in use by the selected mod with its currents settings (ignoring enabled state)"
              + " should be used when creating the swap, but you can also just swap unmodded things.", 1)
            .RegisterEntry("You can write a swap to a new mod, or to a new option in the currently selected mod.",                  1)
            .RegisterEntry("The swaps are not heavily tested yet, and may also be not perfectly efficient. Please leave feedback.", 1)
            .RegisterEntry("More detailed help or explanations will be added later.",                                               1)
            .RegisterEntry("Heavily improve Chat Commands. Use /penumbra help for more information.")
            .RegisterEntry("Penumbra now considers meta manipulations for Changed Items.")
            .RegisterEntry("Penumbra now tries to associate battle voices to specific actors, so that they work in collections.")
            .RegisterEntry(
                "Heavily improve .atex and .avfx handling, Penumbra can now associate VFX to specific actors far better, including ground effects.")
            .RegisterEntry("Improve some file handling for Mare-Interaction.")
            .RegisterEntry("Add Equipment Slots to Demihuman IMC Edits.")
            .RegisterEntry(
                "Add a toggle to keep metadata edits that apply the default value (and thus do not really change anything) on import from TexTools .meta files.")
            .RegisterEntry("Add an option to directly change the 'Wait For Plugins To Load'-Dalamud Option from Penumbra.")
            .RegisterEntry("Add API to copy mod settings from one mod to another.")
            .RegisterEntry("Fix a problem where creating individual collections did not trigger events.")
            .RegisterEntry("Add a Hack to support Anamnesis Redrawing better. (0.6.0.6)")
            .RegisterEntry("Fix another problem with the aesthetician. (0.6.0.6)")
            .RegisterEntry("Fix a problem with the export directory not being respected. (0.6.0.6)");

    private static void Add6_0_5(Changelog log)
        => log.NextVersion("Version 0.6.0.5")
            .RegisterEntry("Allow hyphen as last character in player and retainer names.")
            .RegisterEntry("Fix various bugs with ownership and GPose.")
            .RegisterEntry("Fix collection selectors not updating for new or deleted collections in some cases.")
            .RegisterEntry("Fix Chocobos not being recognized correctly.")
            .RegisterEntry("Fix some problems with UI actors.")
            .RegisterEntry("Fix problems with aesthetician again.");

    private static void Add6_0_2(Changelog log)
        => log.NextVersion("Version 0.6.0.2")
            .RegisterEntry("Let Bell Retainer collections apply to retainer-named mannequins.")
            .RegisterEntry("Added a few informations to a help marker for new individual assignments.")
            .RegisterEntry("Fix bug with Demi Human IMC paths.")
            .RegisterEntry("Fix Yourself collection not applying to UI actors.")
            .RegisterEntry("Fix Yourself collection not applying during aesthetician.");

    private static void Add6_0_0(Changelog log)
        => log.NextVersion("Version 0.6.0.0")
            .RegisterEntry("Revamped Individual Collections:")
            .RegisterEntry("You can now specify individual collections for players (by name) of specific worlds or any world.", 1)
            .RegisterEntry("You can also specify NPCs (by grouped name and type of NPC), and owned NPCs (by specifying an NPC and a Player).",
                1)
            .RegisterImportant(
                "Migration should move all current names that correspond to NPCs to the appropriate NPC group and all names that can be valid Player names to a Player of any world.",
                1)
            .RegisterImportant(
                "Please look through your Individual Collections to verify everything migrated correctly and corresponds to the game object you want. You might also want to change the 'Player (Any World)' collections to your specific homeworld.",
                1)
            .RegisterEntry("You can also manually sort your Individual Collections by drag and drop now.",                 1)
            .RegisterEntry("This new system is a pretty big rework, so please report any discrepancies or bugs you find.", 1)
            .RegisterEntry("These changes made the specific ownership settings for Retainers and for preferring named over ownership obsolete.",
                1)
            .RegisterEntry("General ownership can still be toggled and should apply in order of: Owned NPC > Owner (if enabled) > General NPC.",
                1)
            .RegisterEntry(
                "Added NPC Model Parsing, changes in NPC models should now display the names of the changed game objects for most NPCs.")
            .RegisterEntry("Changed Items now also display variant or subtype in addition to the model set ID where applicable.")
            .RegisterEntry("Collection selectors can now be filtered by name.")
            .RegisterEntry("Try to use Unicode normalization before replacing invalid path symbols on import for somewhat nicer paths.")
            .RegisterEntry("Improved interface for group settings (minimally).")
            .RegisterEntry("New Special or Individual Assignments now default to your current Base assignment instead of None.")
            .RegisterEntry("Improved Support Info somewhat.")
            .RegisterEntry("Added Dye Previews for in-game dyes and dyeing templates in Material Editing.")
            .RegisterEntry("Colorset Editing now allows for negative values in all cases.")
            .RegisterEntry("Added Export buttons to .mdl and .mtrl previews in Advanced Editing.")
            .RegisterEntry("File Selection in the .mdl and .mtrl tabs now shows one associated game path by default and all on hover.")
            .RegisterEntry(
                "Added the option to reduplicate and normalize a mod, restoring all duplicates and moving the files to appropriate folders. (Duplicates Tab in Advanced Editing)")
            .RegisterEntry(
                "Added an option to re-export metadata changes to TexTools-typed .meta and .rgsp files. (Meta-Manipulations Tab in Advanced Editing)")
            .RegisterEntry("Fixed several bugs with the incorporation of meta changes when not done during TTMP import.")
            .RegisterEntry("Fixed a bug with RSP changes on non-base collections not applying correctly in some cases.")
            .RegisterEntry("Fixed a bug when dragging options during mod edit.")
            .RegisterEntry("Fixed a bug where sometimes the valid folder check caused issues.")
            .RegisterEntry("Fixed a bug where collections with inheritances were newly saved on every load.")
            .RegisterEntry("Fixed a bug where the /penumbra enable/disable command displayed the wrong message (functionality unchanged).")
            .RegisterEntry("Mods without names or invalid mod folders are now warnings instead of errors.")
            .RegisterEntry("Added IPC events for mod deletion, addition or moves, and resolving based on game objects.")
            .RegisterEntry("Prevent a bug that allowed IPC to add Mods from outside the Penumbra root folder.")
            .RegisterEntry("A lot of big backend changes.");

    private static void Add5_11_1(Changelog log)
        => log.NextVersion("Version 0.5.11.1")
            .RegisterEntry(
                "The 0.5.11.0 Update exposed an issue in Penumbras file-saving scheme that rarely could cause some, most or even all of your mods to lose their group information.")
            .RegisterEntry(
                "If this has happened to you, you will need to reimport affected mods, or manually restore their groups. I am very sorry for that.",
                1)
            .RegisterEntry(
                "I believe the problem is fixed with 0.5.11.1, but I can not be sure since it would occur only rarely. For the same reason, a testing build would not help (as it also did not with 0.5.11.0 itself).",
                1)
            .RegisterImportant(
                "If you do encounter this or similar problems in 0.5.11.1, please immediately let me know in Discord so I can revert the update again.",
                1);

    private static void Add5_11_0(Changelog log)
        => log.NextVersion("Version 0.5.11.0")
            .RegisterEntry(
                "Added local data storage for mods in the plugin config folder. This information is not exported together with your mod, but not dependent on collections.")
            .RegisterEntry("Moved the import date from mod metadata to local data.",                   1)
            .RegisterEntry("Added Favorites. You can declare mods as favorites and filter for them.",  1)
            .RegisterEntry("Added Local Tags. You can apply custom Tags to mods and filter for them.", 1)
            .RegisterEntry(
                "Added Mod Tags. Mod Creators (and the Edit Mod tab) can set tags that are stored in the mod meta data and are thus exported.")
            .RegisterEntry("Add backface and transparency toggles to .mtrl editing, as well as a info section.")
            .RegisterEntry("Meta Manipulation editing now highlights if the selected ID is 0 or 1.")
            .RegisterEntry("Fixed a bug when manually adding EQP or EQDP entries to Mods.")
            .RegisterEntry("Updated some tooltips and hints.")
            .RegisterEntry("Improved handling of IMC exception problems.")
            .RegisterEntry("Fixed a bug with misidentification of equipment decals.")
            .RegisterEntry(
                "Character collections can now be set via chat command, too. (/penumbra collection character <collection name> | <character name>)")
            .RegisterEntry("Backend changes regarding API/IPC, consumers can but do not need to use the Penumbra.Api library as a submodule.")
            .RegisterEntry("Added API to delete mods and read and set their pseudo-filesystem paths.", 1)
            .RegisterEntry("Added API to check Penumbras enabled state and updates to it.",            1);

    private static void Add5_10_0(Changelog log)
        => log.NextVersion("Version 0.5.10.0")
            .RegisterEntry("Renamed backup functionality to export functionality.")
            .RegisterEntry("A default export directory can now optionally be specified.")
            .RegisterEntry("If left blank, exports will still be stored in your mod directory.", 1)
            .RegisterEntry("Existing exports corresponding to existing mods will be moved automatically if the export directory is changed.",
                1)
            .RegisterEntry("Added buttons to export and import all color set rows at once during material editing.")
            .RegisterEntry("Fixed texture import being case sensitive on the extension.")
            .RegisterEntry("Fixed special collection selector increasing in size on non-default UI styling.")
            .RegisterEntry("Fixed color set rows not importing the dye values during material editing.")
            .RegisterEntry("Other miscellaneous small fixes.");

    private static void Add5_9_0(Changelog log)
        => log.NextVersion("Version 0.5.9.0")
            .RegisterEntry("Special Collections are now split between male and female.")
            .RegisterEntry("Fix a bug where the Base and Interface Collection were set to None instead of Default on a fresh install.")
            .RegisterEntry("Fix a bug where cutscene actors were not properly reset and could be misidentified across multiple cutscenes.")
            .RegisterEntry("TexTools .meta and .rgsp files are now incorporated based on file- and game path extensions.");

    private static void Add5_8_7(Changelog log)
        => log.NextVersion("Version 0.5.8.7")
            .RegisterEntry("Fixed some problems with metadata reloading and reverting and IMC files. (5.8.1 to 5.8.7).")
            .RegisterImportant(
                "If you encounter any issues, please try completely restarting your game after updating (not just relogging), before reporting them.",
                1);

    private static void Add5_8_0(Changelog log)
        => log.NextVersion("Version 0.5.8.0")
            .RegisterEntry("Added choices what Change Logs are to be displayed. It is recommended to just keep showing all.")
            .RegisterEntry("Added an Interface Collection assignment.")
            .RegisterEntry("All your UI mods will have to be in the interface collection.",                                           1)
            .RegisterEntry("Files that are categorized as UI files by the game will only check for redirections in this collection.", 1)
            .RegisterImportant(
                "Migration should have set your currently assigned Base Collection to the Interface Collection, please verify that.", 1)
            .RegisterEntry("New API / IPC for the Interface Collection added.", 1)
            .RegisterImportant("API / IPC consumers should verify whether they need to change resolving to the new collection.", 1)
            .RegisterImportant(
                "If other plugins are not using your interface collection yet, you can just keep Interface and Base the same collection for the time being.")
            .RegisterEntry(
                "Mods can now have default settings for each option group, that are shown while the mod is unconfigured and taken as initial values when configured.")
            .RegisterEntry("Default values are set when importing .ttmps from their default values, and can be changed in the Edit Mod tab.",
                1)
            .RegisterEntry("Files that the game loads super early should now be replaceable correctly via base or interface collection.")
            .RegisterEntry(
                "The 1.0 neck tattoo file should now be replaceable, even in character collections. You can also replace the transparent texture used instead. (This was ugly.)")
            .RegisterEntry("Continued Work on the Texture Import/Export Tab:")
            .RegisterEntry("Should work with lot more texture types for .dds and .tex files, most notably BC7 compression.", 1)
            .RegisterEntry("Supports saving .tex and .dds files in multiple texture types and generating MipMaps for them.", 1)
            .RegisterEntry("Interface reworked a bit, gives more information and the overlay side can be collapsed.",        1)
            .RegisterImportant(
                "May contain bugs or missing safeguards. Generally let me know what's missing, ugly, buggy, not working or could be improved. Not really feasible for me to test it all.",
                1)
            .RegisterEntry(
                "Added buttons for redrawing self or all as well as a tooltip to describe redraw options and a tutorial step for it.")
            .RegisterEntry("Collection Selectors now display None at the top if available.")
            .RegisterEntry(
                "Adding mods via API/IPC will now cause them to incorporate and then delete TexTools .meta and .rgsp files automatically.")
            .RegisterEntry("Fixed an issue with Actor 201 using Your Character collections in cutscenes.")
            .RegisterEntry("Fixed issues with and improved mod option editing.")
            .RegisterEntry(
                "Fixed some issues with and improved file redirection editing - you are now informed if you can not add a game path (because it is invalid or already in use).")
            .RegisterEntry("Backend optimizations.")
            .RegisterEntry("Changed metadata change system again.", 1)
            .RegisterEntry("Improved logging efficiency.",          1);

    private static void Add5_7_1(Changelog log)
        => log.NextVersion("Version 0.5.7.1")
            .RegisterEntry("Fixed the Changelog window not considering UI Scale correctly.")
            .RegisterEntry("Reworked Changelog display slightly.");

    private static void Add5_7_0(Changelog log)
        => log.NextVersion("Version 0.5.7.0")
            .RegisterEntry("Added a Changelog!")
            .RegisterEntry("Files in the UI category will no longer be deduplicated for the moment.")
            .RegisterImportant("If you experience UI-related crashes, please re-import your UI mods.", 1)
            .RegisterEntry("This is a temporary fix against those not-yet fully understood crashes and may be reworked later.", 1)
            .RegisterImportant(
                "There is still a possibility of UI related mods crashing the game, we are still investigating - they behave very weirdly. If you continue to experience crashing, try disabling your UI mods.",
                1)
            .RegisterEntry(
                "On import, Penumbra will now show files with extensions '.ttmp', '.ttmp2' and '.pmp'. You can still select showing generic archive files.")
            .RegisterEntry(
                "Penumbra Mod Pack ('.pmp') files are meant to be renames of any of the archive types that could already be imported that contain the necessary Penumbra meta files.",
                1)
            .RegisterImportant(
                "If you distribute any mod as an archive specifically for Penumbra, you should change its extension to '.pmp'. Supported base archive types are ZIP, 7-Zip and RAR.",
                1)
            .RegisterEntry("Penumbra will now save mod backups with the file extension '.pmp'. They still are regular ZIP files.", 1)
            .RegisterEntry(
                "Existing backups in your current mod directory should be automatically renamed. If you manage multiple mod directories, you may need to migrate the other ones manually.",
                1)
            .RegisterEntry("Fixed assigned collections not working correctly on adventurer plates.")
            .RegisterEntry("Fixed a wrongly displayed folder line in some circumstances.")
            .RegisterEntry("Fixed crash after deleting mod options.")
            .RegisterEntry("Fixed Inspect Window collections not working correctly.")
            .RegisterEntry("Made identically named options selectable in mod configuration. Do not name your options identically.")
            .RegisterEntry("Added some additional functionality for Mare Synchronos.");

    #endregion

    private static void AddDummy(Changelog log)
        => log.NextVersion(string.Empty);

    private (int, ChangeLogDisplayType) ConfigData()
        => (_config.Ephemeral.LastSeenVersion, _config.ChangeLogDisplayType);

    private void Save(int version, ChangeLogDisplayType type)
    {
        if (_config.Ephemeral.LastSeenVersion != version)
        {
            _config.Ephemeral.LastSeenVersion = version;
            _config.Ephemeral.Save();
        }

        if (_config.ChangeLogDisplayType != type)
        {
            _config.ChangeLogDisplayType = type;
            _config.Save();
        }
    }
}

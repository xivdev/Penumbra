using Luna;

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
        Add1_3_6_0(Changelog);
        Add1_3_6_4(Changelog);
        Add1_4_0_0(Changelog);
        Add1_5_0_0(Changelog);
        Add1_5_1_0(Changelog);
    }

    #region Changelogs

    private static void Add1_5_1_0(Changelog log)
        => log.NextVersion("Version 1.5.1.0"u8)
            .RegisterHighlight("Added the option to export a characters current data as a .pcp modpack in the On-Screen tab."u8)
            .RegisterEntry("Other plugins can attach to this functionality and package and interpret their own data."u8, 1)
            .RegisterEntry(
                "When a .pcp modpack is installed, it can create and assign collections for the corresponding character it was created for."u8,
                1)
            .RegisterEntry(
                "This basically provides an easier way to manually synchronize other players, but does not contain any automation."u8, 1)
            .RegisterEntry(
                "The settings provide some fine control about what happens when a PCP is installed, as well as buttons to cleanup any PCP-created data."u8,
                1)
            .RegisterEntry("Added a warning message when the game's integrity is corrupted to the On-Screen tab."u8)
            .RegisterEntry("Added .kdb files to the On-Screen tab and associated functionality (thanks Ny!)."u8)
            .RegisterEntry("Updated the creation of temporary collections to require a passed identity."u8)
            .RegisterEntry(
                "Added the option to change the skin material suffix in models using the stockings shader by adding specific attributes (thanks Ny!)."u8)
            .RegisterEntry("Added predefined tag utility to the multi-mod selection."u8)
            .RegisterEntry("Fixed an issue with the automatic collection selection on character login when no mods are assigned."u8)
            .RegisterImportant(
                "Fixed issue with new deformer data that makes modded deformers not containing this data work implicitly. Updates are still recommended (1.5.0.5)."u8)
            .RegisterEntry("Fixed various issues after patch (1.5.0.1 - 1.5.0.4)."u8);

    private static void Add1_5_0_0(Changelog log)
        => log.NextVersion("Version 1.5.0.0"u8)
            .RegisterImportant(
                "Updated for game version 7.30 and Dalamud API13, which uses a new GUI backend. Some things may not work as expected. Please let me know any issues you encounter."u8)
            .RegisterEntry("Added support for exporting models using two vertex color schemes (thanks zeroeightysix!)."u8)
            .RegisterEntry(
                "Possibly improved the color accuracy of the basecolor texture created when exporting models (thanks zeroeightysix!)."u8)
            .RegisterEntry(
                "Disabled enabling transparency for materials that use the characterstockings shader due to crashes (thanks zeroeightysix!)."u8)
            .RegisterEntry("Fixed some issues with model i/o and invalid tangents (thanks PassiveModding!)"u8)
            .RegisterEntry("Changed the behavior for default directory names when using the mod normalizer with combining groups."u8)
            .RegisterEntry("Added jumping to specific mods to the HTTP API."u8)
            .RegisterEntry("Fixed an issue with character sound modding (1.4.0.6)."u8)
            .RegisterHighlight("Added support for IMC-toggle attributes to accessories beyond the first toggle (1.4.0.5)."u8)
            .RegisterEntry("Fixed up some slot-specific attributes and shapes in models when swapping items between slots (1.4.0.5)."u8)
            .RegisterEntry("Added handling for human skin materials to the OnScreen tab and similar functionality (thanks Ny!) (1.4.0.5)."u8)
            .RegisterEntry("The OS thread ID a resource was loaded from was added to the resource logger (1.4.0.5)."u8)
            .RegisterEntry(
                "A button linking to my (Ottermandias') Ko-Fi and Patreon was added in the settings tab. Feel free, but not pressured, to use it! :D "u8)
            .RegisterHighlight("Mod setting combos now support mouse-wheel scrolling with Control and have filters (1.4.0.4)."u8)
            .RegisterEntry("Using the middle mouse button to toggle designs now works correctly with temporary settings (1.4.0.4)."u8)
            .RegisterEntry("Updated some BNPC associations (1.4.0.3)."u8)
            .RegisterEntry("Fixed further issues with shapes and attributes (1.4.0.4)."u8)
            .RegisterEntry(
                "Penumbra now handles textures with MipMap offsets broken by TexTools on import and removes unnecessary MipMaps (1.4.0.3)."u8)
            .RegisterEntry("Updated the Mod Merger for the new group types (1.4.0.3)."u8)
            .RegisterEntry("Added querying Penumbra for supported features via IPC (1.4.0.3)."u8)
            .RegisterEntry("Shape names can now be edited in Penumbras model editor (1.4.0.2)."u8)
            .RegisterEntry("Attributes and Shapes can be fully toggled (1.4.0.2)."u8)
            .RegisterEntry("Fixed several issues with attributes and shapes (1.4.0.1)."u8);

    private static void Add1_4_0_0(Changelog log)
        => log.NextVersion("Version 1.4.0.0"u8)
            .RegisterHighlight("Added two types of new Meta Changes, SHP and ATR (Thanks Karou!)."u8)
            .RegisterEntry("Those allow mod creators to toggle custom shape keys and attributes for models on and off, respectively."u8, 1)
            .RegisterEntry("Custom shape keys need to have the format 'shpx_*' and custom attributes need 'atrx_*'."u8,                  1)
            .RegisterHighlight(
                "Shapes of the following formats will automatically be toggled on if both relevant slots contain the same shape key:"u8, 1)
            .RegisterEntry("'shpx_wa_*', for the waist seam between the body and leg slot,"u8,    2)
            .RegisterEntry("'shpx_wr_*', for the wrist seams between the body and hands slot,"u8, 2)
            .RegisterEntry("'shpx_an_*', for the ankle seams between the leg and feet slot."u8,   2)
            .RegisterEntry(
                "Custom shape key and attributes can be turned off in the advanced settings section for the moment, but this is not recommended."u8,
                1)
            .RegisterHighlight("The mod selector width is now draggable within certain restrictions that depend on the total window width."u8)
            .RegisterEntry("The current behavior may not be final, let me know if you have any comments."u8, 1)
            .RegisterEntry("Improved the naming of NPCs for identifiers by using Haselnussbombers new naming functionality (Thanks Hasel!)."u8)
            .RegisterEntry("Added global EQP entries to always hide Au Ra horns, Viera ears, or Miqo'te ears, respectively."u8)
            .RegisterEntry("This will leave holes in the heads of the respective race if not modded in some way."u8, 1)
            .RegisterEntry("Added a filter for mods that have temporary settings in the mod selector panel (Thanks Caraxi)."u8)
            .RegisterEntry("Made the checkbox for toggling Temporary Settings Mode in the mod tab more visible."u8)
            .RegisterEntry("Improved the option select combo in advanced editing."u8)
            .RegisterEntry("Fixed some issues with item identification for EST changes."u8)
            .RegisterEntry("Fixed the sizing of the mod panel being off by 1 pixel sometimes."u8)
            .RegisterEntry("Fixed an issue with redrawing while in GPose when other plugins broke some assumptions about the game state."u8)
            .RegisterEntry("Fixed a clipping issue within the Meta Manipulations tab in advanced editing."u8)
            .RegisterEntry("Fixed an issue with empty and temporary settings."u8)
            .RegisterHighlight(
                "In the Item Swap tab, items changed by this mod are now sorted and highlighted before items changed in the current collection before other items for the source, and inversely for the target. (1.3.6.8)"u8)
            .RegisterHighlight(
                "Default-valued meta edits should now be kept on import and only removed when the option to keep them is not set AND no other options in the mod edit the same entry. (1.3.6.8)"u8)
            .RegisterEntry("Added a right-click context menu on file redirections to copy the full file path. (1.3.6.8)"u8)
            .RegisterEntry(
                "Added a right-click context menu on the mod export button to open the backup directory in your file explorer. (1.3.6.8)"u8)
            .RegisterEntry("Fixed some issues when redrawing characters from other plugins. (1.3.6.8)"u8)
            .RegisterEntry(
                "Added a modifier key separate from the delete modifier key that is used for less important key-checks, specifically toggling incognito mode. (1.3.6.7)"u8)
            .RegisterEntry("Fixed some issues with the Material Editor (Thanks Ny). (1.3.6.6)"u8);

    private static void Add1_3_6_4(Changelog log)
        => log.NextVersion("Version 1.3.6.4"u8)
            .RegisterEntry("The material editor should be functional again."u8);

    private static void Add1_3_6_0(Changelog log)
        => log.NextVersion("Version 1.3.6.0"u8)
            .RegisterImportant("Updated Penumbra for update 7.20 and Dalamud API 12."u8)
            .RegisterEntry(
                "This is not thoroughly tested, but I decided to push to stable instead of testing because otherwise a lot of people would just go to testing just for early access again despite having no business doing so."u8,
                1)
            .RegisterEntry(
                "I also do not use most of the functionality of Penumbra myself, so I am unable to even encounter most issues myself."u8, 1)
            .RegisterEntry("If you encounter any issues, please report them quickly on the discord."u8,                                   1)
            .RegisterHighlight(
                "The texture editor now has encoding support for Block Compression 1, 4 and 5 and tooltips explaining when to use which format."u8)
            .RegisterEntry("It also is able to use GPU compression and thus has become much faster for BC7 in particular. (Thanks Ny!)"u8, 1)
            .RegisterEntry(
                "Added the option to import .atch files found in the particular mod via right-click context menu on the import drag & drop button."u8)
            .RegisterEntry("Added a chat command to clear temporary settings done manually in Penumbra."u8)
            .RegisterEntry(
                "The changed item star to select the preferred changed item is a bit more noticeable by default, and its color can be configured."u8)
            .RegisterEntry("Some minor fixes for computing changed items. (Thanks Anna!)"u8)
            .RegisterEntry("The EQP entry previously named Unknown 4 was renamed to 'Hide Glove Cuffs'."u8)
            .RegisterEntry("Fixed the changed item identification for EST changes."u8)
            .RegisterEntry("Fixed clipping issues in the changed items panel when no grouping was active."u8);


    private static void Add1_3_5_0(Changelog log)
        => log.NextVersion("Version 1.3.5.0"u8)
            .RegisterImportant(
                "Redirections of unsupported file types like .atch will now produce warnings when they are enabled. Please update mods still containing them or request updates from their creators."u8)
            .RegisterEntry("You can now import .atch in the Meta section of advanced editing to add their non-default changes to the mod."u8)
            .RegisterHighlight("Added an option in settings and in the collection bar in the mod tab to always use temporary settings."u8)
            .RegisterEntry(
                "While this option is enabled, all changes you make in the current collection will be applied as temporary changes, and you have to use Turn Permanent to make them permanent."u8,
                1)
            .RegisterEntry(
                "This should be useful for trying out new mods without needing to reset their settings later, or for creating mod associations in Glamourer from them."u8,
                1)
            .RegisterEntry(
                "Added a context menu entry on the mod selector blank-space context menu to clear all temporary settings made manually."u8)
            .RegisterHighlight(
                "Resource Trees now consider some additional files like decals, and improved the quick-import behaviour for some files that should not generally be modded."u8)
            .RegisterHighlight("The Changed Item display for single mods has been heavily improved."u8)
            .RegisterEntry("Any changed item will now show how many individual edits are affecting it in the mod in its tooltip."u8, 1)
            .RegisterEntry("Equipment pieces are now grouped by their model id, reducing clutter."u8,                                1)
            .RegisterEntry(
                "The primary equipment piece displayed is the one with the most changes affecting it, but can be configured to a specific item by the mod creator and locally."u8,
                1)
            .RegisterEntry(
                "Preferred changed items stored in the mod will be shared when exporting the mod, and used as the default for local preferences, which will not be shared."u8,
                2)
            .RegisterEntry(
                "You can configure whether groups are automatically collapsed or expanded, or remove grouping entirely in the settings."u8, 1)
            .RegisterHighlight("Fixed support for model import/export with more than one UV."u8)
            .RegisterEntry("Added some IPC relating to changed items."u8)
            .RegisterEntry("Skeleton and Physics changes should now be identified in Changed Items."u8)
            .RegisterEntry("Item Swaps will now also correctly swap EQP entries of multi-slot pieces."u8)
            .RegisterEntry("Meta edit transmission through IPC should be a lot more efficient than before."u8)
            .RegisterEntry("Fixed an issue with incognito names in some cutscenes."u8)
            .RegisterEntry("Newly extracted mod folders will now try to rename themselves three times before being considered a failure."u8);

    private static void Add1_3_4_0(Changelog log)
        => log.NextVersion("Version 1.3.4.0"u8)
            .RegisterHighlight(
                "Added HDR functionality to diffuse buffers. This allows more accurate representation of non-standard color values for e.g. skin or hair colors when used with advanced customizations in Glamourer."u8)
            .RegisterEntry(
                "This option requires Wait For Plugins On Load to be enabled in Dalamud and to be enabled on start to work. It is on by default but can be turned off."u8,
                1)
            .RegisterHighlight("Added a new option group type: Combining Groups."u8)
            .RegisterEntry(
                "A combining group behaves similarly to a multi group for the user, but instead of enabling the different options separately, it results in exactly one option per choice of settings."u8,
                1)
            .RegisterEntry(
                "Example: The user sees 2 checkboxes [+25%, +50%], but the 4 different selection states result in +0%, +25%, +50% or +75% if both are toggled on. Every choice of settings can be configured separately by the mod creator."u8,
                1)
            .RegisterEntry(
                "Added new functionality to better track copies of the player character in cutscenes if they get forced to specific clothing, like in the Margrat cutscene. Might improve tracking in wedding ceremonies, too, let me know."u8)
            .RegisterEntry("Added a display of the number of selected files and folders to the multi mod selection."u8)
            .RegisterEntry(
                "Added cleaning functionality to remove outdated or unused files or backups from the config and mod folders via manual action."u8)
            .RegisterEntry("Updated the Bone and Material limits in the Model Importer."u8)
            .RegisterEntry("Improved handling of IMC and Material files loaded asynchronously."u8)
            .RegisterEntry("Added IPC functionality to query temporary settings."u8)
            .RegisterEntry("Improved some mod setting IPC functions."u8)
            .RegisterEntry("Fixed some path detection issues in the OnScreen tab."u8)
            .RegisterEntry("Fixed some issues with temporary mod settings."u8)
            .RegisterEntry("Fixed issues with IPC calls before the game has finished loading."u8)
            .RegisterEntry("Fixed using the wrong dye channel in the material editor previews."u8)
            .RegisterEntry("Added some log warnings if outdated materials are loaded by the game."u8)
            .RegisterEntry("Added Schemas for some of the json files generated and read by Penumbra to the solution."u8);

    private static void Add1_3_3_0(Changelog log)
        => log.NextVersion("Version 1.3.3.0"u8)
            .RegisterHighlight("Added Temporary Settings to collections."u8)
            .RegisterEntry(
                "Settings can be manually turned temporary (and turned back) while editing mod settings via right-click context on the mod or buttons in the settings panel."u8,
                1)
            .RegisterEntry(
                "This can be used to test mods or changes without saving those changes permanently or having to reinstate the old settings afterwards."u8,
                1)
            .RegisterEntry(
                "More importantly, this can be set via IPC by other plugins, allowing Glamourer to only set and reset temporary settings when applying Mod Associations."u8,
                1)
            .RegisterEntry(
                "As an extreme example, it would be possible to only enable the consistent mods for your character in the collection, and let Glamourer handle all outfit mods itself via temporary settings only."u8,
                1)
            .RegisterEntry(
                "This required some pretty big changes that were in testing for a while now, but nobody talked about it much so it may still have some bugs or usability issues. Let me know!"u8,
                1)
            .RegisterHighlight(
                "Added an option to automatically select the collection assigned to the current character on login events. This is off by default."u8)
            .RegisterEntry(
                "Added partial copying of color tables in material editing via right-click context menu entries on the import buttons."u8)
            .RegisterHighlight(
                "Added handling for TMB files cached by the game that should resolve issues of leaky TMBs from animation and VFX mods."u8)
            .RegisterEntry(
                "The enabled checkbox, Priority and Inheriting buttons now stick at the top of the Mod Settings panel even when scrolling down for specific settings."u8)
            .RegisterEntry("When creating new mods with Item Swap, the attributed author of the resulting mod was improved."u8)
            .RegisterEntry("Fixed an issue with rings in the On-Screen tab and in the data sent over to other plugins via IPC."u8)
            .RegisterEntry(
                "Fixed some issues when writing material files that resulted in technically valid files that still caused some issues with the game for unknown reasons."u8)
            .RegisterEntry("Fixed some ImGui assertions."u8);

    private static void Add1_3_2_0(Changelog log)
        => log.NextVersion("Version 1.3.2.0"u8)
            .RegisterHighlight("Added ATCH meta manipulations that allow the composite editing of attachment points across multiple mods."u8)
            .RegisterEntry("Those ATCH manipulations should be shared via Mare Synchronos."u8, 1)
            .RegisterEntry(
                "This is an early implementation and might be bug-prone. Let me know of any issues. It was in testing for quite a while without reports."u8,
                1)
            .RegisterEntry(
                "Added jumping to identified mods in the On-Screen tab via Control + Right-Click and improved their display slightly."u8)
            .RegisterEntry("Added some right-click context menu copy options in the File Redirections editor for paths."u8)
            .RegisterHighlight("Added the option to change a specific mod's settings via chat commands by using '/penumbra mod settings'."u8)
            .RegisterEntry("Fixed issues with the copy-pasting of meta manipulations."u8)
            .RegisterEntry("Fixed some other issues related to meta manipulations."u8)
            .RegisterEntry(
                "Updated available NPC names and fixed an issue with some supposedly invisible characters in names showing in ImGui."u8);


    private static void Add1_3_1_0(Changelog log)
        => log.NextVersion("Version 1.3.1.0"u8)
            .RegisterEntry("Penumbra has been updated for Dalamud API 11 and patch 7.1."u8)
            .RegisterImportant(
                "There are some known issues with potential crashes using certain VFX/SFX mods, probably related to sound files."u8)
            .RegisterEntry(
                "If you encounter those issues, please report them in the discord and potentially disable the corresponding mods for the time being."u8,
                1)
            .RegisterImportant(
                "The modding of .atch files has been disabled. Outdated modded versions of these files cause crashes when loaded."u8)
            .RegisterEntry("A better way for modular modding of .atch files via meta changes will release to the testing branch soonish."u8, 1)
            .RegisterHighlight("Temporary collections (as created by Mare) will now always respect ownership."u8)
            .RegisterEntry(
                "This means that you can toggle this setting off if you do not want it, and Mare will still work for minions and mounts of other players."u8,
                1)
            .RegisterEntry(
                "The new physics and animation engine files (.kdb and .bnmb) should now be correctly redirected and respect EST changes."u8)
            .RegisterEntry(
                "Fixed issues with EQP entries being labeled wrongly and global EQP not changing all required values for earrings."u8)
            .RegisterEntry("Fixed an issue with global EQP changes of a mod being reset upon reloading the mod."u8)
            .RegisterEntry("Fixed another issue with left rings and mare synchronization / the on-screen tab."u8)
            .RegisterEntry("Maybe fixed some issues with characters appearing in the login screen being misidentified."u8)
            .RegisterEntry("Some improvements for debug visualization have been made."u8);


    private static void Add1_3_0_0(Changelog log)
        => log.NextVersion("Version 1.3.0.0"u8)
            .RegisterHighlight("The textures tab in the advanced editing window can now import and export .tga files."u8)
            .RegisterEntry("BC4 and BC6 textures can now also be imported."u8, 1)
            .RegisterHighlight("Added item swapping from and to the Glasses slot."u8)
            .RegisterEntry("Reworked quite a bit of things around face wear / bonus items. Please let me know if anything broke."u8, 1)
            .RegisterEntry("The import date of a mod is now shown in the Edit Mod tab, and can be reset via button."u8)
            .RegisterEntry("A button to open the file containing local mod data for a mod was also added."u8, 1)
            .RegisterHighlight(
                "IMC groups can now be configured to only apply the attribute flags for their entry, and take the other values from the default value."u8)
            .RegisterEntry("This allows keeping the material index of every IMC entry of a group, while setting the attributes."u8, 1)
            .RegisterHighlight("Model Import/Export was fixed and re-enabled (thanks ackwell and ramen)."u8)
            .RegisterHighlight("Added a hack to allow bonus items (face wear, glasses) to have VFX."u8)
            .RegisterEntry("Also fixed the hack that allowed accessories to have VFX not working anymore."u8, 1)
            .RegisterHighlight("Added rudimentary options to edit PBD files in the advanced editing window."u8)
            .RegisterEntry("Preparing the advanced editing window for a mod now does not freeze the game until it is ready."u8)
            .RegisterEntry(
                "Meta Manipulations in the advanced editing window are now ordered and do not eat into performance as much when drawn."u8)
            .RegisterEntry("Added a button to the advanced editing window to remove all default-valued meta manipulations from a mod"u8)
            .RegisterEntry(
                "Default-valued manipulations will now also be removed on import from archives and .pmps, not just .ttmps, if not configured otherwise."u8,
                1)
            .RegisterEntry("Checkbox-based mod filters are now tri-state checkboxes instead of two disjoint checkboxes."u8)
            .RegisterEntry("Paths from the resource logger can now be copied."u8)
            .RegisterEntry("Silenced some redundant error logs when updating mods via Heliosphere."u8)
            .RegisterEntry("Added 'Page' to imported mod data for TexTools interop. The value is not used in Penumbra, just persisted."u8)
            .RegisterEntry("Updated all external dependencies."u8)
            .RegisterEntry("Fixed issue with Demihuman IMC entries."u8)
            .RegisterEntry("Fixed some off-by-one errors on the mod import window."u8)
            .RegisterEntry("Fixed a race-condition concerning the first-time creation of mod-meta files."u8)
            .RegisterEntry("Fixed an issue with long mod titles in the merge mods tab."u8)
            .RegisterEntry("A bunch of other miscellaneous fixes."u8);


    private static void Add1_2_1_0(Changelog log)
        => log.NextVersion("Version 1.2.1.0"u8)
            .RegisterHighlight("Penumbra is now released for Dawntrail!"u8)
            .RegisterEntry("Mods themselves may have to be updated. TexTools provides options for this."u8,                              1)
            .RegisterEntry("For model files, Penumbra provides a rudimentary update function, but prefer using TexTools if possible."u8, 1)
            .RegisterEntry("Other files, like materials and textures, will have to go through TexTools for the moment."u8,               1)
            .RegisterEntry(
                "Some outdated mods can be identified by Penumbra and are prevented from loading entirely (specifically shaders, by Ny)."u8, 1)
            .RegisterImportant("I am sorry that it took this long, but there was an immense amount of work to be done from the start."u8)
            .RegisterImportant(
                "Since Penumbra has been in Testing for quite a while, multitudes of bugs and issues cropped up that needed to be dealt with."u8,
                1)
            .RegisterEntry("There very well may still be a lot of issues, so please report any you find."u8, 1)
            .RegisterImportant("BUT, please make sure that those issues are not caused by outdated mods before reporting them."u8, 1)
            .RegisterEntry(
                "This changelog may seem rather short for the timespan, but I omitted hundreds of smaller fixes and the details of getting Penumbra to work in Dawntrail."u8,
                1)
            .RegisterHighlight("The Material Editing tab in the Advanced Editing Window has been heavily improved (by Ny)."u8)
            .RegisterEntry(
                "Especially for Dawntrail materials using the new shaders, the window provides much more in-depth and user-friendly editing options."u8,
                1)
            .RegisterHighlight("Many advancements regarding modded shaders, and modding bone deformers have been made."u8)
            .RegisterHighlight("IMC groups now allow their options to toggle attributes off that are on in the default entry."u8)
            .RegisterImportant(
                "The 'Update Bibo' button was removed. The functionality is redundant since any mods that old need to be updated anyway."u8)
            .RegisterEntry("Clicking the button on modern mods generally caused more harm than benefit."u8, 1)
            .RegisterEntry(
                "If you somehow still need to mass-migrate materials in your models, the Material Reassignment tab in Advanced Editing is still available for this."u8,
                1)
            .RegisterEntry("The On-Screen tab was updated and improved and can now display modded actual paths in more useful form."u8)
            .RegisterImportant("Model Import/Export is temporarily disabled until Dawntrail-related changes can be made."u8)
            .RegisterHighlight("You can now change a mods state in any collection from its Collections tab via right-clicking the state."u8)
            .RegisterHighlight("Items changed in a mod now sort before other items in the Item Swap tab, and are highlighted."u8)
            .RegisterEntry("Path handling was improved in regards to case-sensitivity."u8)
            .RegisterEntry("Fixed an issue with negative search matching on folders with no matches"u8)
            .RegisterEntry("Mod option groups on the same priority are now applied in reverse index order. (1.2.0.12)"u8)
            .RegisterEntry("Fixed the display of missing files in the Advanced Editing Window's header. (1.2.0.8)"u8)
            .RegisterEntry(
                "Fixed some, but not all soft-locks that occur when your character gets redrawn while fishing. Just do not do that. (1.2.0.7)"u8)
            .RegisterEntry("Improved handling of invalid Offhand IMC files for certain jobs. (1.2.0.6)"u8)
            .RegisterEntry("Added automatic reduplication for files in the UI category, as they cause crashes when not unique. (1.2.0.5)"u8)
            .RegisterEntry("The mod import popup can now be closed by clicking outside of it, if it is finished. (1.2.0.5)"u8)
            .RegisterEntry("Fixed an issue with Mod Normalization skipping the default option. (1.2.0.5)"u8)
            .RegisterEntry("Improved the Support Info output. (1.1.1.5)"u8)
            .RegisterEntry("Reworked the handling of Meta Manipulations entirely. (1.1.1.3)"u8)
            .RegisterEntry("Added a configuration option to disable showing mods in the character lobby and at the aesthetician. (1.1.1.1)"u8)
            .RegisterEntry("Fixed an issue with the AddMods API and the root directory. (1.1.1.2)"u8)
            .RegisterEntry("Fixed an issue with the Mod Merger file lookup and casing. (1.1.1.2)"u8)
            .RegisterEntry("Fixed an issue with file saving not happening when merging mods or swapping items in some cases. (1.1.1.2)"u8);

    private static void Add1_1_1_0(Changelog log)
        => log.NextVersion("Version 1.1.1.0"u8)
            .RegisterHighlight("Filtering for mods is now tokenized and can filter for multiple things at once, or exclude specific things."u8)
            .RegisterEntry("Hover over the filter to see the new available options in the tooltip."u8, 1)
            .RegisterEntry("Be aware that the tokenization changed the prior behavior slightly."u8,    1)
            .RegisterEntry("This is open to improvements, if you have any ideas, let me know!"u8,      1)
            .RegisterHighlight("Added initial identification of characters in the login-screen by name."u8)
            .RegisterEntry(
                "Those characters can not be redrawn and re-use some things, so this may not always behave as expected, but should work in general. Let me know if you encounter edge cases!"u8,
                1)
            .RegisterEntry("Added functionality for IMC groups to apply to all variants for a model instead of a specific one."u8)
            .RegisterEntry("Improved the resource tree view with filters and incognito mode. (by Ny)"u8)
            .RegisterEntry("Added a tooltip to the global EQP condition."u8)
            .RegisterEntry(
                "Fixed the new worlds not being identified correctly because Square Enix could not be bothered to turn them public."u8)
            .RegisterEntry("Fixed model import getting stuck when doing weight adjustments. (by ackwell)"u8)
            .RegisterEntry("Fixed an issue with dye previews in the material editor not applying."u8)
            .RegisterEntry("Fixed an issue with collections not saving on renames."u8)
            .RegisterEntry("Fixed an issue parsing collections with settings set to negative values, which should now be set to 0."u8)
            .RegisterEntry("Fixed an issue with the accessory VFX addition."u8)
            .RegisterEntry("Fixed an issue with GMP animation type entries."u8)
            .RegisterEntry("Fixed another issue with the mod merger."u8)
            .RegisterEntry("Fixed an issue with IMC groups and IPC."u8)
            .RegisterEntry("Fixed some issues with the capitalization of the root directory."u8)
            .RegisterEntry("Fixed IMC attribute tooltips not appearing for disabled checkboxes."u8)
            .RegisterEntry("Added GetChangedItems IPC for single mods. (1.1.0.2)"u8)
            .RegisterEntry("Fixed an issue with creating unnamed collections. (1.1.0.2)"u8)
            .RegisterEntry("Fixed an issue with the mod merger. (1.1.0.2)"u8)
            .RegisterEntry("Fixed the global EQP entry for rings checking for bracelets instead of rings. (1.1.0.2)"u8)
            .RegisterEntry("Fixed an issue with newly created collections not being added to the collection list. (1.1.0.1)"u8);

    private static void Add1_1_0_0(Changelog log)
        => log.NextVersion("Version 1.1.0.0"u8)
            .RegisterImportant(
                "This update comes, again, with a lot of very heavy backend changes (collections and groups) and thus may introduce new issues."u8)
            .RegisterEntry("Updated to .net8 and XIV 6.58, using some new framework facilities to improve performance and stability."u8)
            .RegisterHighlight(
                "Added an experimental crash handler that is supposed to write a Penumbra.log file when the game crashes, containing Penumbra-specific information."u8)
            .RegisterEntry("This is disabled by default. It can be enabled in Advanced Settings."u8, 1)
            .RegisterHighlight("Collections now have associated GUIDs as identifiers instead of their names, so they can now be renamed."u8)
            .RegisterEntry("Migrating those collections may introduce issues, please let me know as soon as possible if you encounter any."u8,
                1)
            .RegisterEntry("A permanent (non-rolling) backup should be created before the migration in case of any issues."u8, 1)
            .RegisterHighlight(
                "Added predefined tags that can be setup in the Settings tab and can be more easily applied or removed from mods. (by DZD)"u8)
            .RegisterHighlight(
                "A total rework of how options and groups are handled internally, and introduction of the first new group type, the IMC Group."u8)
            .RegisterEntry(
                "Mod Creators can add a IMC Group to their mod that controls a single IMC Manipulation, so they can provide options for the separate attributes for it."u8,
                1)
            .RegisterEntry(
                "This makes it a lot easier to have combined options: No need for 'A', 'B' and 'AB', you can just define 'A' and 'B' and skip their combinations"u8,
                1)
            .RegisterHighlight("A new type of Meta Manipulation was added, 'Global EQP Manipulation'."u8)
            .RegisterEntry(
                "Global EQP Manipulations allow accessories to make other equipment pieces not hide them, e.g. whenever a character is wearing a specific Bracelet, neither body nor hand items will ever hide bracelets."u8,
                1)
            .RegisterEntry(
                "This can be used if something like a jacket or a stole is put onto an accessory to prevent it from being hidden in general."u8,
                1)
            .RegisterEntry(
                "The first empty option in a single-select option group imported from a TTMP will now keep its location instead of being moved to the first option."u8)
            .RegisterEntry("Further empty options are still removed."u8, 1)
            .RegisterHighlight(
                "Added a field to rename mods directly from the mod selector context menu, instead of moving them in the filesystem."u8)
            .RegisterEntry("You can choose which rename field (none, either one or both) to display in the settings."u8, 1)
            .RegisterEntry("Added the characterglass.shpk shader file to special shader treatment to fix issues when replacing it. (By Ny)"u8)
            .RegisterEntry("Made it more obvious if a user has not set their root directory yet."u8)
            .RegisterEntry(
                "You can now paste your current clipboard text into the mod selector filter with a simple right-click as long as it is not focused."u8)
            .RegisterHighlight(
                "Added the option to display VFX for accessories if added via IMC edits, which the game does not do inherently (by Ocealot)."u8)
            .RegisterEntry("Added support for reading and writing the new material and model file formats from the benchmark."u8)
            .RegisterEntry(
                "Added the option to hide Machinist Offhands from the Changed Items tabs (because any change to it changes ALL of them), which is on by default."u8)
            .RegisterEntry("Removed the auto-generated descriptions for newly created groups in Penumbra."u8)
            .RegisterEntry(
                "Made some improvements to the Advanced Editing window, for example a much better and more performant Hex Viewer for unstructured data was added."u8)
            .RegisterEntry("Various improvements to model import/export by ackwell (throughout all patches)."u8)
            .RegisterEntry(
                "Hovering over meta manipulations in other options in the advanced editing window now shows a list of those options."u8)
            .RegisterEntry("Reworked the API and IPC structure heavily."u8)
            .RegisterImportant("This means some plugins interacting with Penumbra may not work correctly until they update."u8, 1)
            .RegisterEntry("Worked around the UI IPC possibly displacing all settings when the drawn additions became too big."u8)
            .RegisterEntry("Fixed an issue where reloading a mod did not ensure settings for that mod being correct afterwards."u8)
            .RegisterEntry("Fixed some issues with the file sizes of compressed files."u8)
            .RegisterEntry("Fixed an issue with merging and deduplicating mods."u8)
            .RegisterEntry("Fixed a crash when scanning for mods without access rights to the folder."u8)
            .RegisterEntry(
                "Made plugin conform to Dalamud requirements by adding a punchline and another button to open the menu from the installer."u8)
            .RegisterEntry("Added an option to automatically redraw the player character when saving files. (1.0.0.8)"u8)
            .RegisterEntry("Fixed issue with manipulating mods not triggering some events.  (1.0.0.7)"u8)
            .RegisterEntry("Fixed issue with temporary mods not triggering some events.  (1.0.0.6)"u8)
            .RegisterEntry("Fixed issue when renaming mods while the advanced edit window is open. (1.0.0.6)"u8)
            .RegisterEntry("Fixed issue with empty option groups. (1.0.0.5)"u8)
            .RegisterEntry("Fixed issues with cutscene character identification. (1.0.0.4)"u8)
            .RegisterEntry("Added locale environment information to support info. (1.0.0.4)"u8)
            .RegisterEntry("Fixed an issue with copied mod settings in IPC missing unused settings. (1.0.0.3)"u8);

    private static void Add1_0_0_0(Changelog log)
        => log.NextVersion("Version 1.0.0.0"u8)
            .RegisterHighlight("Mods in the mod selector can now be filtered by changed item categories."u8)
            .RegisterHighlight("Model Editing options in the Advanced Editing Window have been greatly extended (by ackwell):"u8)
            .RegisterEntry("Attributes and referenced materials can now be set per mesh."u8, 1)
            .RegisterEntry(
                "Model files (.mdl) can now be exported to the well-established glTF format, which can be imported e.g. by Blender."u8,
                1)
            .RegisterEntry("glTF files can also be imported back to a .mdl file."u8, 1)
            .RegisterHighlight(
                "Model Export and Import are a work in progress and may encounter issues, not support all cases or produce wrong results, please let us know!"u8,
                1)
            .RegisterEntry("The last selected mod and the open/close state of the Advanced Editing Window are now stored across launches."u8)
            .RegisterEntry("Footsteps of certain mounts will now be associated to collections correctly."u8)
            .RegisterEntry("Save-in-Place in the texture tab now requires the configurable modifier."u8)
            .RegisterEntry("Updated OtterTex to a newer version of DirectXTex."u8)
            .RegisterEntry("Fixed an issue with horizontal scrolling if a mod title was very long."u8)
            .RegisterEntry("Fixed an issue with the mod panels header not updating its data when the selected mod updates."u8)
            .RegisterEntry("Fixed some issues with EQDP files for invalid characters."u8)
            .RegisterEntry("Fixed an issue with the FileDialog being drawn twice in certain situations."u8)
            .RegisterEntry(
                "A lot of backend changes that should not have an effect on users, but may cause issues if something got messed up."u8);

    private static void Add8_3_0(Changelog log)
        => log.NextVersion("Version 0.8.3.0"u8)
            .RegisterHighlight(
                "Improved the UI for the On-Screen tabs with highlighting of used paths, filtering and more selections. (by Ny)"u8)
            .RegisterEntry(
                "Added an option to replace non-ASCII symbols with underscores for folder paths on mod import since this causes problems on some WINE systems. This option is off by default."u8)
            .RegisterEntry(
                "Added support for the Changed Item Icons to load modded icons, but this depends on a not-yet-released Dalamud update."u8)
            .RegisterEntry(
                "Penumbra should no longer redraw characters while they are fishing, but wait for them to reel in, because that could cause soft-locks. This may cause other issues, but I have not found any."u8)
            .RegisterEntry(
                "Hopefully fixed a bug on mod import where files were being read while they were still saving, causing Penumbra to create wrong options."u8)
            .RegisterEntry("Fixed a few display issues."u8)
            .RegisterEntry("Added some IPC functionality for Xande. (by Asriel)"u8);

    private static void Add8_2_0(Changelog log)
        => log.NextVersion("Version 0.8.2.0"u8)
            .RegisterHighlight(
                "You can now redraw indoor furniture. This may not be entirely stable and might break some customizable decoration like wallpapered walls."u8)
            .RegisterEntry("The redraw bar has been slightly improved and disables currently unavailable redraw commands now."u8)
            .RegisterEntry("Redrawing players now also actively redraws any accessories they are using."u8)
            .RegisterEntry("Power-users can now redraw game objects by index via chat command."u8)
            .RegisterHighlight(
                "You can now filter for the special case 'None' for filters where that makes sense (like Tags or Changed Items)."u8)
            .RegisterHighlight("When selecting multiple mods, you can now add or remove tags from them at once."u8)
            .RegisterEntry(
                "The dye template combo in advanced material editing now displays the currently selected dye as it would appear with the respective template."u8)
            .RegisterEntry("The On-Screen tab and associated functionality has been heavily improved by Ny."u8)
            .RegisterEntry("Fixed an issue with the changed item identification for left rings."u8)
            .RegisterEntry("Updated BNPC data."u8)
            .RegisterEntry(
                "Some configuration like the currently selected tab states are now stored in a separate file that is not backed up and saved less often."u8)
            .RegisterEntry("Added option to open the Penumbra main window at game start independently of Debug Mode."u8)
            .RegisterEntry("Fixed some tooltips in the advanced editing window. (0.8.1.8)"u8)
            .RegisterEntry("Fixed clicking to linked changed items not working. (0.8.1.8)"u8)
            .RegisterEntry("Support correct handling of offhand-parts for two-handed weapons for changed items. (0.8.1.7)"u8)
            .RegisterEntry("Fixed renaming the mod directory not updating paths in the advanced window. (0.8.1.6)"u8)
            .RegisterEntry("Fixed portraits not respecting your card settings. (0.8.1.6)"u8)
            .RegisterEntry("Added ReverseResolvePlayerPathsAsync for IPC. (0.8.1.6)"u8)
            .RegisterEntry("Expanded the tooltip for Wait for Plugins on Startup. (0.8.1.5)"u8)
            .RegisterEntry("Disabled window sounds for some popup windows. (0.8.1.5)"u8)
            .RegisterEntry("Added support for middle-clicking mods to enable/disable them. (0.8.1.5)"u8);

    private static void Add8_1_2(Changelog log)
        => log.NextVersion("Version 0.8.1.2"u8)
            .RegisterEntry("Fixed an issue keeping mods selected after their deletion."u8)
            .RegisterEntry("Maybe fixed an issue causing individual assignments to get lost on game start."u8);

    private static void Add8_1_1(Changelog log)
        => log.NextVersion("Version 0.8.1.1"u8)
            .RegisterImportant(
                "Updated for 6.5 - Square Enix shuffled around a lot of things this update, so some things still might not work but have not been noticed yet. Please report any issues."u8)
            .RegisterEntry("Added support for chat commands to affect multiple individuals matching the supplied string at once."u8)
            .RegisterEntry(
                "Improved messaging: many warnings or errors appearing will stay a little longer and can now be looked at in a Messages tab (visible only if there have been any)."u8)
            .RegisterEntry("Fixed an issue with leading or trailing spaces when renaming mods."u8);


    private static void Add8_0_0(Changelog log)
        => log.NextVersion("Version 0.8.0.0"u8)
            .RegisterEntry(
                "Penumbra now uses Windows' transparent file system compression by default (on Windows systems). You can disable this functionality in the settings."u8)
            .RegisterImportant("You can retroactively compress your existing mods in the settings via the press of a button, too."u8, 1)
            .RegisterEntry(
                "In our tests, this not only was able to reduce storage space by 30-60%, it even decreased loading times since less I/O had to take place."u8,
                1)
            .RegisterEntry("Added emotes to changed item identification."u8)
            .RegisterEntry(
                "Added quick select buttons to switch to the current interface collection or the collection applying to the current player character in the mods tab, reworked their text and tooltips slightly."u8)
            .RegisterHighlight("Drag & Drop of multiple mods and folders at once is now supported by holding Control while clicking them."u8)
            .RegisterEntry("You can now disable conflicting mods from the Conflicts panel via Control + Right-click."u8)
            .RegisterEntry("Added checks for your deletion-modifiers for restoring mods from backups or deleting backups."u8)
            .RegisterEntry(
                "Penumbra now should automatically try to restore your custom sort order (mod folders) and your active collections from backups if they fail to load. No guarantees though."u8)
            .RegisterEntry("The resource watcher now displays a column providing load state information of resources."u8)
            .RegisterEntry(
                "Custom RSP scaling outside of the collection assigned to Base should now be respected for emotes that adjust your stance on height differences."u8)
            .RegisterEntry(
                "Mods that replace the skin shaders will not cause visual glitches like loss of head shadows or Free Company crest tattoos anymore (by Ny)."u8)
            .RegisterEntry("The Material editor has been improved (by Ny):"u8)
            .RegisterHighlight(
                "Live-Preview for materials yourself or entities owned by you are currently using, so you can see color set edits in real time."u8,
                1)
            .RegisterEntry(
                "Colors on the color table of a material can be highlighted on yourself or entities owned by you by hovering a button."u8, 1)
            .RegisterEntry("The color table has improved color accuracy."u8,                                                               1)
            .RegisterEntry("Materials with non-dyable color tables can be made dyable, and vice-versa."u8,                                 1)
            .RegisterEntry("The 'Advanced Shader Resources' section has been split apart into dedicated sections."u8,                      1)
            .RegisterEntry(
                "Addition and removal of shader keys, textures, constants and a color table has been automated following shader requirements and can not be done manually anymore."u8,
                1)
            .RegisterEntry(
                "Plain English names and tooltips can now be displayed instead of hexadecimal identifiers or code names by providing dev-kit files installed via certain mods."u8,
                1)
            .RegisterEntry("The Texture editor has been improved (by Ny):"u8)
            .RegisterHighlight(
                "The overlay texture can now be combined in several ways and automatically resized to match the input texture."u8,
                1)
            .RegisterEntry("New color manipulation options have been added."u8,                  1)
            .RegisterEntry("Modifications to the selected texture can now be saved in-place."u8, 1)
            .RegisterEntry("The On-Screen tab has been improved (by Ny):"u8)
            .RegisterEntry("The character list will load more quickly."u8,                           1)
            .RegisterEntry("It is now able to deal with characters under transformation effects."u8, 1)
            .RegisterEntry(
                "The headers are now color-coded to distinguish between you and other players, and between NPCs that are handled locally or on the server. Colors are customizable."u8,
                1)
            .RegisterEntry("More file types will be recognized and shown."u8,                           1)
            .RegisterEntry("The actual paths for game files will be displayed and copied correctly."u8, 1)
            .RegisterEntry("The Shader editor has been improved (by Ny):"u8)
            .RegisterEntry(
                "New sections 'Shader Resources' and 'Shader Selection' have been added, expanding on some data that was in 'Further Content' before."u8,
                1)
            .RegisterEntry("A fail-safe mode for shader decompilation on platforms that do not fully support it has been added."u8, 1)
            .RegisterEntry("Fixed invalid game paths generated for variants of customizations."u8)
            .RegisterEntry("Lots of minor improvements across the codebase."u8)
            .RegisterEntry("Some unnamed mounts were made available for actor identification. (0.7.3.2)"u8);

    private static void Add7_3_0(Changelog log)
        => log.NextVersion("Version 0.7.3.0"u8)
            .RegisterEntry(
                "Added the ability to drag and drop mod files from external sources (like a file explorer or browser) into Penumbras mod selector to import them."u8)
            .RegisterEntry("You can also drag and drop texture files into the textures tab of the Advanced Editing Window."u8, 1)
            .RegisterEntry(
                "Added a priority display to the mod selector using the currently selected collections priorities. This can be hidden in settings."u8)
            .RegisterEntry("Added IPC for texture conversion, improved texture handling backend and threading."u8)
            .RegisterEntry(
                "Added Dalamud Substitution so that other plugins can more easily use replaced icons from Penumbras Interface collection when using Dalamuds new Texture Provider."u8)
            .RegisterEntry("Added a filter to texture selection combos in the textures tab of the Advanced Editing Window."u8)
            .RegisterEntry(
                "Changed behaviour when failing to load group JSON files for mods - the pre-existing but failing files are now backed up before being deleted or overwritten."u8)
            .RegisterEntry("Further backend changes, mostly relating to the Glamourer rework."u8)
            .RegisterEntry("Fixed an issue with modded decals not loading correctly when used with the Glamourer rework."u8)
            .RegisterEntry("Fixed missing scaling with UI Scale for some combos."u8)
            .RegisterEntry("Updated the used version of SharpCompress to deal with Zip64 correctly."u8)
            .RegisterEntry("Added a toggle to not display the Changed Item categories in settings (0.7.2.2)."u8)
            .RegisterEntry("Many backend changes relating to the Glamourer rework (0.7.2.2)."u8)
            .RegisterEntry("Fixed an issue when multiple options in the same option group had the same label (0.7.2.2)."u8)
            .RegisterEntry("Fixed an issue with a GPose condition breaking animation and vfx modding in GPose (0.7.2.1)."u8)
            .RegisterEntry("Fixed some handling of decals (0.7.2.1)."u8);

    private static void Add7_2_0(Changelog log)
        => log.NextVersion("Version 0.7.2.0"u8)
            .RegisterEntry(
                "Added Changed Item Categories and icons that can filter for specific types of Changed Items, in the Changed Items Tab as well as in the Changed Items panel for specific mods.."u8)
            .RegisterEntry(
                "Icons at the top can be clicked to filter, as well as right-clicked to open a context menu with the option to inverse-filter for them"u8,
                1)
            .RegisterEntry("There is also an ALL button that can be toggled."u8, 1)
            .RegisterEntry(
                "Modded files in the Font category now resolve from the Interface assignment instead of the base assignment, despite not technically being in the UI category."u8)
            .RegisterEntry(
                "Timeline files will no longer be associated with specific characters in cutscenes, since there is no way to correctly do this, and it could cause crashes if IVCS-requiring animations were used on characters without IVCS."u8)
            .RegisterEntry("File deletion in the Advanced Editing Window now also checks for your configured deletion key combo."u8)
            .RegisterEntry(
                "The Texture tab in the Advanced Editing Window now has some quick convert buttons to just convert the selected texture to a different format in-place."u8)
            .RegisterEntry(
                "These buttons only appear if only one texture is selected on the left side, it is not otherwise manipulated, and the texture is a .tex file."u8,
                1)
            .RegisterEntry("The text part of the mod filter in the mod selector now also resets when right-clicking the drop-down arrow."u8)
            .RegisterEntry("The Dissolve Folder option in the mod selector context menu has been moved to the bottom."u8)
            .RegisterEntry("Somewhat improved IMC handling to prevent some issues."u8)
            .RegisterEntry(
                "Improved the handling of mod renames on mods with default-search names to correctly rename their search-name in (hopefully) all cases too."u8)
            .RegisterEntry("A lot of backend improvements and changes related to the pending Glamourer rework."u8)
            .RegisterEntry("Fixed an issue where the displayed active collection count in the support info was wrong."u8)
            .RegisterEntry(
                "Fixed an issue with created directories dealing badly with non-standard whitespace characters like half-width or non-breaking spaces."u8)
            .RegisterEntry("Fixed an issue with unknown animation and vfx edits not being recognized correctly."u8)
            .RegisterEntry("Fixed an issue where changing option descriptions to be empty was not working correctly."u8)
            .RegisterEntry("Fixed an issue with texture names in the resource tree of the On-Screen views."u8)
            .RegisterEntry("Fixed a bug where the game would crash when drawing folders in the mod selector that contained a '%' symbol."u8)
            .RegisterEntry("Fixed an issue with parallel algorithms obtaining the wrong number of available cores."u8)
            .RegisterEntry("Updated the available selection of Battle NPC names."u8)
            .RegisterEntry("A typo in the 0.7.1.2 Changlog has been fixed."u8)
            .RegisterEntry("Added the Sea of Stars as accepted repository. (0.7.1.4)"u8)
            .RegisterEntry(
                "Fixed an issue with collections sometimes not loading correctly, and IMC files not applying correctly. (0.7.1.3)"u8);


    private static void Add7_1_2(Changelog log)
        => log.NextVersion("Version 0.7.1.2"u8)
            .RegisterEntry(
                "Changed threaded handling of collection caches. Maybe this fixes the startup problems some people are experiencing."u8)
            .RegisterEntry(
                "This is just testing and may not be the solution, or may even make things worse. Sorry if I have to put out multiple small patches again to get this right."u8,
                1)
            .RegisterEntry("Fixed Penumbra failing to load if the main configuration file is corrupted."u8)
            .RegisterEntry("Some miscellaneous small bug fixes."u8)
            .RegisterEntry("Slight changes in behaviour for deduplicator/normalizer, mostly backend."u8)
            .RegisterEntry("A typo in the 0.7.1.0 Changelog has been fixed."u8)
            .RegisterEntry("Fixed left rings not being valid for IMC entries after validation. (7.1.1)"u8)
            .RegisterEntry(
                "Relaxed the scaling restrictions for RSP scaling values to go from 0.01 to 512.0 instead of the prior upper limit of 8.0, in interface as well as validation, to better support the fetish community. (7.1.1)"u8);

    private static void Add7_1_0(Changelog log)
        => log.NextVersion("Version 0.7.1.0"u8)
            .RegisterEntry("Updated for patch 6.4 - there may be some oversights on edge cases, but I could not find any issues myself."u8)
            .RegisterImportant(
                "This update changed some Dragoon skills that were moving the player character before to not do that anymore. If you have any mods that applied to those skills, please make sure that they do not contain any redirections for .tmb files. If skills that should no longer move your character still do that for some reason, this is detectable by the server."u8,
                1)
            .RegisterEntry(
                "Added a Mod Merging tab in the Advanced Editing Window. This can help you merge multiple mods to one, or split off specific options from an existing mod into a new mod."u8)
            .RegisterEntry(
                "Added advanced options to configure the minimum allowed window size for the main window (to reduce it). This is not quite supported and may look bad, so only use it if you really need smaller windows."u8)
            .RegisterEntry("The last tab selected in the main window is now saved and re-used when relaunching Penumbra."u8)
            .RegisterEntry("Added a hook to correctly associate some sounds that are played while weapons are drawn."u8)
            .RegisterEntry("Added a hook to correctly associate sounds that are played while dismounting."u8)
            .RegisterEntry("A hook to associate weapon-associated VFX was expanded to work in more cases."u8)
            .RegisterEntry("TMB resources now use a collection prefix to prevent retained state in some cases."u8)
            .RegisterEntry("Improved startup times a bit."u8)
            .RegisterEntry("Right-Click context menus for collections are now also ordered by name."u8)
            .RegisterEntry("Advanced Editing tabs have been reordered and renamed slightly."u8)
            .RegisterEntry("Added some validation of metadata changes to prevent stalling on load of bad IMC edits."u8)
            .RegisterEntry("Fixed an issue where collections could lose their configured inheritances during startup in some cases."u8)
            .RegisterEntry("Fixed some bugs when mods were removed from collection caches."u8)
            .RegisterEntry("Fixed some bugs with IMC files not correctly reverting to default values in some cases."u8)
            .RegisterEntry("Fixed an issue with the mod import popup not appearing (0.7.0.10)"u8)
            .RegisterEntry("Fixed an issue with the file selectors not always opening at the expected locations. (0.7.0.7)"u8)
            .RegisterEntry("Fixed some cache handling issues. (0.7.0.5 - 0.7.0.10)"u8)
            .RegisterEntry("Fixed an issue with multiple collection context menus appearing for some identifiers (0.7.0.5)"u8)
            .RegisterEntry(
                "Fixed an issue where the Update Bibo button did only work if the Advanced Editing window was opened before. (0.7.0.5)"u8);

    private static void Add7_0_4(Changelog log)
        => log.NextVersion("Version 0.7.0.4"u8)
            .RegisterEntry("Added options to the bulktag slash command to check all/local/mod tags specifically."u8)
            .RegisterEntry("Possibly improved handling of the delayed loading of individual assignments."u8)
            .RegisterEntry("Fixed a bug that caused metadata edits to apply even though mods were disabled."u8)
            .RegisterEntry("Fixed a bug that prevented material reassignments from working."u8)
            .RegisterEntry("Reverted trimming of whitespace for relative paths to only trim the end, not the start. (0.7.0.3)"u8)
            .RegisterEntry("Fixed a bug that caused an integer overflow on textures of high dimensions. (0.7.0.3)"u8)
            .RegisterEntry("Fixed a bug that caused Penumbra to enter invalid state when deleting mods. (0.7.0.2)"u8)
            .RegisterEntry("Added Notification on invalid collection names. (0.7.0.2)"u8);

    private static void Add7_0_1(Changelog log)
        => log.NextVersion("Version 0.7.0.1"u8)
            .RegisterEntry("Individual assignments can again be re-ordered by drag-and-dropping them."u8)
            .RegisterEntry("Relax the restriction of a maximum of 32 characters for collection names to 64 characters."u8)
            .RegisterEntry("Fixed a bug that showed the Your Character collection as redundant even if it was not."u8)
            .RegisterEntry("Fixed a bug that caused some required collection caches to not be built on startup and thus mods not to apply."u8)
            .RegisterEntry("Fixed a bug that showed the current collection as unused even if it was used."u8);

    private static void Add7_0_0(Changelog log)
        => log.NextVersion("Version 0.7.0.0"u8)
            .RegisterImportant(
                "The entire backend was reworked (this is still in progress). While this does not come with a lot of functionality changes, basically every file and functionality was touched."u8)
            .RegisterEntry(
                "This may have (re-)introduced some bugs that have not yet been noticed despite a long testing period - there are not many users of the testing branch."u8,
                1)
            .RegisterEntry("If you encounter any - but especially breaking or lossy - bugs, please report them immediately."u8, 1)
            .RegisterEntry("This also fixed or improved numerous bugs and issues that will not be listed here."u8,              1)
            .RegisterEntry("GitHub currently reports 321 changed files with 34541 additions and 28464 deletions."u8,            1)
            .RegisterEntry("Added Notifications on many failures that previously only wrote to log."u8)
            .RegisterEntry("Reworked the Collections Tab to hopefully be much more intuitive. It should be self-explanatory now."u8)
            .RegisterEntry("The tutorial was adapted to the new window, if you are unsure, maybe try restarting it."u8, 1)
            .RegisterEntry(
                "You can now toggle an incognito mode in the collection window so it shows shortened names of collections and players."u8, 1)
            .RegisterEntry(
                "You can get an overview about the current usage of a selected collection and its active and unused mod settings in the Collection Details panel."u8,
                1)
            .RegisterEntry("The currently selected collection is now highlighted in green (default, configurable) in multiple places."u8, 1)
            .RegisterEntry(
                "Mods now have a 'Collections' panel in the Mod Panel containing an overview about usage of the mod in all collections."u8)
            .RegisterEntry("The 'Changed Items' and 'Effective Changes' tab now contain a collection selector."u8)
            .RegisterEntry("Added the On-Screen tab to find what files a specific character is actually using (by Ny)."u8)
            .RegisterEntry("Added 3 Quick Move folders in the mod selector that can be setup in context menus for easier cleanup."u8)
            .RegisterEntry(
                "Added handling for certain animation files for mounts and fashion accessories to correctly associate them to players."u8)
            .RegisterEntry("The file selectors in the Advanced Mod Editing Window now use filterable combos."u8)
            .RegisterEntry(
                "The Advanced Mod Editing Window now shows the number of meta edits and file swaps in unselected options and highlights the option selector."u8)
            .RegisterEntry("Added API/IPC to start unpacking and installing mods from external tools (by Sebastina)."u8)
            .RegisterEntry("Hidden files and folders are now ignored for unused files in Advanced Mod Editing (by myr)"u8)
            .RegisterEntry("Paths in mods are now automatically trimmed of whitespace on loading."u8)
            .RegisterEntry("Fixed double 'by' in mod author display (by Caraxi)."u8)
            .RegisterEntry("Fixed a crash when trying to obtain names from the game data."u8)
            .RegisterEntry("Fixed some issues with tutorial windows."u8)
            .RegisterEntry("Fixed some bugs in the Resource Logger."u8)
            .RegisterEntry("Fixed Button Sizing for collapsible groups and several related bugs."u8)
            .RegisterEntry("Fixed issue with mods with default settings other than 0."u8)
            .RegisterEntry("Fixed issue with commands not registering on startup. (0.6.6.5)"u8)
            .RegisterEntry("Improved Startup Times and Time Tracking. (0.6.6.4)"u8)
            .RegisterEntry("Add Item Swapping between different types of Accessories and Hats. (0.6.6.4)"u8)
            .RegisterEntry("Fixed bugs with assignment of temporary collections and their deletion. (0.6.6.4)"u8)
            .RegisterEntry("Fixed bugs with new file loading mechanism. (0.6.6.2, 0.6.6.3)"u8)
            .RegisterEntry("Added API/IPC to open and close the main window and select specific tabs and mods. (0.6.6.2)"u8);

    private static void Add6_6_1(Changelog log)
        => log.NextVersion("Version 0.6.6.1"u8)
            .RegisterEntry("Added an option to make successful chat commands not print their success confirmations to chat."u8)
            .RegisterEntry("Fixed an issue with migration of old mods not working anymore (fixes Material UI problems)."u8)
            .RegisterEntry("Fixed some issues with using the Assign Current Player and Assign Current Target buttons."u8);

    private static void Add6_6_0(Changelog log)
        => log.NextVersion("Version 0.6.6.0"u8)
            .RegisterEntry(
                "Added new Collection Assignment Groups for Children NPC and Elderly NPC. Those take precedence before any non-individual assignments for any NPC using a child- or elderly model respectively."u8)
            .RegisterEntry(
                "Added an option to display Single Selection Groups as a group of radio buttons similar to Multi Selection Groups, when the number of available options is below the specified value. Default value is 2."u8)
            .RegisterEntry("Added a button in option groups to collapse the option list if it has more than 5 available options."u8)
            .RegisterEntry(
                "Penumbra now circumvents the games inability to read files at paths longer than 260 UTF16 characters and can also deal with generic unicode symbols in paths."u8)
            .RegisterEntry(
                "This means that Penumbra should no longer cause issues when files become too long or when there is a non-ASCII character in them."u8,
                1)
            .RegisterEntry(
                "Shorter paths are still better, so restrictions on the root directory have not been relaxed. Mod names should no longer replace non-ASCII symbols on import though."u8,
                1)
            .RegisterEntry(
                "Resource logging has been relegated to its own tab with better filtering. Please do not keep resource logging on arbitrarily or set a low record limit if you do, otherwise this eats a lot of performance and memory after a while."u8)
            .RegisterEntry(
                "Added a lot of facilities to edit the shader part of .mtrl files and .shpk files themselves in the Advanced Editing Tab (Thanks Ny and aers)."u8)
            .RegisterEntry(
                "Added splitting of Multi Selection Groups with too many options when importing .pmp files or adding mods via IPC."u8)
            .RegisterEntry("Discovery, Reloading and Unloading of a specified mod is now possible via HTTP API (Thanks Sebastina)."u8)
            .RegisterEntry("Cleaned up the HTTP API somewhat, removed currently useless options."u8)
            .RegisterEntry("Fixed an issue when extracting some textures."u8)
            .RegisterEntry("Fixed an issue with mannequins inheriting individual assignments for the current player when using ownership."u8)
            .RegisterEntry(
                "Fixed an issue with the resolving of .phyb and .sklb files for Item Swaps of head or body items with an EST entry but no unique racial model."u8);

    private static void Add6_5_2(Changelog log)
        => log.NextVersion("Version 0.6.5.2"u8)
            .RegisterEntry("Updated for game version 6.31 Hotfix."u8)
            .RegisterEntry(
                "Added option-specific descriptions for mods, instead of having just descriptions for groups of options. (Thanks Caraxi!)"u8)
            .RegisterEntry("Those are now accurately parsed from TTMPs, too."u8, 1)
            .RegisterEntry("Improved launch times somewhat through parallelization of some tasks."u8)
            .RegisterEntry(
                "Added some performance tracking for start-up durations and for real time data to Release builds. They can be seen and enabled in the Debug tab when Debug Mode is enabled."u8)
            .RegisterEntry("Fixed an issue with IMC changes and Mare Synchronos interoperability."u8)
            .RegisterEntry("Fixed an issue with housing mannequins crashing the game when resource logging was enabled."u8)
            .RegisterEntry("Fixed an issue generating Mip Maps for texture import on Wine."u8);

    private static void Add6_5_0(Changelog log)
        => log.NextVersion("Version 0.6.5.0"u8)
            .RegisterEntry("Fixed an issue with Item Swaps not using applied IMC changes in some cases."u8)
            .RegisterEntry("Improved error message on texture import when failing to create mip maps (slightly)."u8)
            .RegisterEntry("Tried to fix duty party banner identification again, also for the recommendation window this time."u8)
            .RegisterEntry("Added batched IPC to improve Mare performance."u8);

    private static void Add6_4_0(Changelog log)
        => log.NextVersion("Version 0.6.4.0"u8)
            .RegisterEntry("Fixed an issue with the identification of actors in the duty group portrait."u8)
            .RegisterEntry("Fixed some issues with wrongly cached actors and resources."u8)
            .RegisterEntry("Fixed animation handling after redraws (notably for PLD idle animations with a shield equipped)."u8)
            .RegisterEntry("Fixed an issue with collection listing API skipping one collection."u8)
            .RegisterEntry(
                "Fixed an issue with BGM files being sometimes loaded from other collections than the base collection, causing crashes."u8)
            .RegisterEntry(
                "Also distinguished file resolving for different file categories (improving performance) and disabled resolving for script files entirely."u8,
                1)
            .RegisterEntry("Some miscellaneous backend changes due to the Glamourer rework."u8);

    private static void Add6_3_0(Changelog log)
        => log.NextVersion("Version 0.6.3.0"u8)
            .RegisterEntry("Add an Assign Current Target button for individual assignments"u8)
            .RegisterEntry("Try identifying all banner actors correctly for PvE duties, Crystalline Conflict and Mahjong."u8)
            .RegisterEntry("Please let me know if this does not work for anything except identical twins."u8, 1)
            .RegisterEntry("Add handling for the 3 new screen actors (now 8 total, for PvE dutie portraits)."u8)
            .RegisterEntry("Update the Battle NPC name database for 6.3."u8)
            .RegisterEntry("Added API/IPC functions to obtain or set group or individual collections."u8)
            .RegisterEntry("Maybe fix a problem with textures sometimes not loading from their corresponding collection."u8)
            .RegisterEntry("Another try to fix a problem with the collection selectors breaking state."u8)
            .RegisterEntry("Fix a problem identifying companions."u8)
            .RegisterEntry("Fix a problem when deleting collections assigned to Groups."u8)
            .RegisterEntry(
                "Fix a problem when using the Assign Currently Played Character button and then logging onto a different character without restarting in between."u8)
            .RegisterEntry("Some miscellaneous backend changes."u8);

    private static void Add6_2_0(Changelog log)
        => log.NextVersion("Version 0.6.2.0"u8)
            .RegisterEntry("Update Penumbra for .net7, Dalamud API 8 and patch 6.3."u8)
            .RegisterEntry("Add a Bulktag chat command to toggle all mods with specific tags. (by SoyaX)"u8)
            .RegisterEntry("Add placeholder options for setting individual collections via chat command."u8)
            .RegisterEntry("Add toggles to swap left and/or right rings separately for ring item swap."u8)
            .RegisterEntry("Add handling for looping sound effects caused by animations in non-base collections."u8)
            .RegisterEntry("Add an option to not use any mods at all in the Inspect/Try-On window."u8)
            .RegisterEntry("Add handling for Mahjong actors."u8)
            .RegisterEntry("Improve hint text for File Swaps in Advanced Editing, also inverted file swap display order."u8)
            .RegisterEntry("Fix a problem where the collection selectors could get desynchronized after adding or deleting collections."u8)
            .RegisterEntry("Fix a problem that could cause setting state to get desynchronized."u8)
            .RegisterEntry("Fix an oversight where some special screen actors did not actually respect the settings made for them."u8)
            .RegisterEntry("Add collection and associated game object to Full Resource Logging."u8)
            .RegisterEntry("Add performance tracking for DEBUG-compiled versions (i.e. testing only)."u8)
            .RegisterEntry("Add some information to .mdl display and fix not respecting padding when reading them. (0.6.1.3)"u8)
            .RegisterEntry("Fix association of some vfx game objects. (0.6.1.3)"u8)
            .RegisterEntry("Stop forcing AVFX files to load synchronously. (0.6.1.3)"u8)
            .RegisterEntry("Fix an issue when incorporating deduplicated meta files. (0.6.1.2)"u8);

    private static void Add6_1_1(Changelog log)
        => log.NextVersion("Version 0.6.1.1"u8)
            .RegisterEntry(
                "Added a toggle to use all the effective changes from the entire currently selected collection for swaps, instead of the selected mod."u8)
            .RegisterEntry("Fix using equipment paths for accessory swaps and thus accessory swaps not working at all"u8)
            .RegisterEntry("Fix issues with swaps with gender-locked gear where the models for the other gender do not exist."u8)
            .RegisterEntry("Fix swapping universal hairstyles for midlanders breaking them for other races."u8)
            .RegisterEntry("Add some actual error messages on failure to create item swaps."u8)
            .RegisterEntry("Fix warnings about more than one affected item appearing for single items."u8);

    private static void Add6_1_0(Changelog log)
        => log.NextVersion("Version 0.6.1.0 (Happy New Year! Edition)"u8)
            .RegisterEntry("Add a prototype for Item Swapping."u8)
            .RegisterEntry("A new tab in Advanced Editing."u8,                                                                         1)
            .RegisterEntry("Swapping of Hair, Tail, Ears, Equipment and Accessories is supported. Weapons and Faces may be coming."u8, 1)
            .RegisterEntry("The manipulations currently in use by the selected mod with its currents settings (ignoring enabled state)"u8
              + " should be used when creating the swap, but you can also just swap unmodded things."u8, 1)
            .RegisterEntry("You can write a swap to a new mod, or to a new option in the currently selected mod."u8,                  1)
            .RegisterEntry("The swaps are not heavily tested yet, and may also be not perfectly efficient. Please leave feedback."u8, 1)
            .RegisterEntry("More detailed help or explanations will be added later."u8,                                               1)
            .RegisterEntry("Heavily improve Chat Commands. Use /penumbra help for more information."u8)
            .RegisterEntry("Penumbra now considers meta manipulations for Changed Items."u8)
            .RegisterEntry("Penumbra now tries to associate battle voices to specific actors, so that they work in collections."u8)
            .RegisterEntry(
                "Heavily improve .atex and .avfx handling, Penumbra can now associate VFX to specific actors far better, including ground effects."u8)
            .RegisterEntry("Improve some file handling for Mare-Interaction."u8)
            .RegisterEntry("Add Equipment Slots to Demihuman IMC Edits."u8)
            .RegisterEntry(
                "Add a toggle to keep metadata edits that apply the default value (and thus do not really change anything) on import from TexTools .meta files."u8)
            .RegisterEntry("Add an option to directly change the 'Wait For Plugins To Load'-Dalamud Option from Penumbra."u8)
            .RegisterEntry("Add API to copy mod settings from one mod to another."u8)
            .RegisterEntry("Fix a problem where creating individual collections did not trigger events."u8)
            .RegisterEntry("Add a Hack to support Anamnesis Redrawing better. (0.6.0.6)"u8)
            .RegisterEntry("Fix another problem with the aesthetician. (0.6.0.6)"u8)
            .RegisterEntry("Fix a problem with the export directory not being respected. (0.6.0.6)"u8);

    private static void Add6_0_5(Changelog log)
        => log.NextVersion("Version 0.6.0.5"u8)
            .RegisterEntry("Allow hyphen as last character in player and retainer names."u8)
            .RegisterEntry("Fix various bugs with ownership and GPose."u8)
            .RegisterEntry("Fix collection selectors not updating for new or deleted collections in some cases."u8)
            .RegisterEntry("Fix Chocobos not being recognized correctly."u8)
            .RegisterEntry("Fix some problems with UI actors."u8)
            .RegisterEntry("Fix problems with aesthetician again."u8);

    private static void Add6_0_2(Changelog log)
        => log.NextVersion("Version 0.6.0.2"u8)
            .RegisterEntry("Let Bell Retainer collections apply to retainer-named mannequins."u8)
            .RegisterEntry("Added a few informations to a help marker for new individual assignments."u8)
            .RegisterEntry("Fix bug with Demi Human IMC paths."u8)
            .RegisterEntry("Fix Yourself collection not applying to UI actors."u8)
            .RegisterEntry("Fix Yourself collection not applying during aesthetician."u8);

    private static void Add6_0_0(Changelog log)
        => log.NextVersion("Version 0.6.0.0"u8)
            .RegisterEntry("Revamped Individual Collections:"u8)
            .RegisterEntry("You can now specify individual collections for players (by name) of specific worlds or any world."u8, 1)
            .RegisterEntry("You can also specify NPCs (by grouped name and type of NPC), and owned NPCs (by specifying an NPC and a Player)."u8,
                1)
            .RegisterImportant(
                "Migration should move all current names that correspond to NPCs to the appropriate NPC group and all names that can be valid Player names to a Player of any world."u8,
                1)
            .RegisterImportant(
                "Please look through your Individual Collections to verify everything migrated correctly and corresponds to the game object you want. You might also want to change the 'Player (Any World)' collections to your specific homeworld."u8,
                1)
            .RegisterEntry("You can also manually sort your Individual Collections by drag and drop now."u8,                 1)
            .RegisterEntry("This new system is a pretty big rework, so please report any discrepancies or bugs you find."u8, 1)
            .RegisterEntry(
                "These changes made the specific ownership settings for Retainers and for preferring named over ownership obsolete."u8,
                1)
            .RegisterEntry(
                "General ownership can still be toggled and should apply in order of: Owned NPC > Owner (if enabled) > General NPC."u8,
                1)
            .RegisterEntry(
                "Added NPC Model Parsing, changes in NPC models should now display the names of the changed game objects for most NPCs."u8)
            .RegisterEntry("Changed Items now also display variant or subtype in addition to the model set ID where applicable."u8)
            .RegisterEntry("Collection selectors can now be filtered by name."u8)
            .RegisterEntry("Try to use Unicode normalization before replacing invalid path symbols on import for somewhat nicer paths."u8)
            .RegisterEntry("Improved interface for group settings (minimally)."u8)
            .RegisterEntry("New Special or Individual Assignments now default to your current Base assignment instead of None."u8)
            .RegisterEntry("Improved Support Info somewhat."u8)
            .RegisterEntry("Added Dye Previews for in-game dyes and dyeing templates in Material Editing."u8)
            .RegisterEntry("Colorset Editing now allows for negative values in all cases."u8)
            .RegisterEntry("Added Export buttons to .mdl and .mtrl previews in Advanced Editing."u8)
            .RegisterEntry("File Selection in the .mdl and .mtrl tabs now shows one associated game path by default and all on hover."u8)
            .RegisterEntry(
                "Added the option to reduplicate and normalize a mod, restoring all duplicates and moving the files to appropriate folders. (Duplicates Tab in Advanced Editing)"u8)
            .RegisterEntry(
                "Added an option to re-export metadata changes to TexTools-typed .meta and .rgsp files. (Meta-Manipulations Tab in Advanced Editing)"u8)
            .RegisterEntry("Fixed several bugs with the incorporation of meta changes when not done during TTMP import."u8)
            .RegisterEntry("Fixed a bug with RSP changes on non-base collections not applying correctly in some cases."u8)
            .RegisterEntry("Fixed a bug when dragging options during mod edit."u8)
            .RegisterEntry("Fixed a bug where sometimes the valid folder check caused issues."u8)
            .RegisterEntry("Fixed a bug where collections with inheritances were newly saved on every load."u8)
            .RegisterEntry("Fixed a bug where the /penumbra enable/disable command displayed the wrong message (functionality unchanged)."u8)
            .RegisterEntry("Mods without names or invalid mod folders are now warnings instead of errors."u8)
            .RegisterEntry("Added IPC events for mod deletion, addition or moves, and resolving based on game objects."u8)
            .RegisterEntry("Prevent a bug that allowed IPC to add Mods from outside the Penumbra root folder."u8)
            .RegisterEntry("A lot of big backend changes."u8);

    private static void Add5_11_1(Changelog log)
        => log.NextVersion("Version 0.5.11.1"u8)
            .RegisterEntry(
                "The 0.5.11.0 Update exposed an issue in Penumbras file-saving scheme that rarely could cause some, most or even all of your mods to lose their group information."u8)
            .RegisterEntry(
                "If this has happened to you, you will need to reimport affected mods, or manually restore their groups. I am very sorry for that."u8,
                1)
            .RegisterEntry(
                "I believe the problem is fixed with 0.5.11.1, but I can not be sure since it would occur only rarely. For the same reason, a testing build would not help (as it also did not with 0.5.11.0 itself)."u8,
                1)
            .RegisterImportant(
                "If you do encounter this or similar problems in 0.5.11.1, please immediately let me know in Discord so I can revert the update again."u8,
                1);

    private static void Add5_11_0(Changelog log)
        => log.NextVersion("Version 0.5.11.0"u8)
            .RegisterEntry(
                "Added local data storage for mods in the plugin config folder. This information is not exported together with your mod, but not dependent on collections."u8)
            .RegisterEntry("Moved the import date from mod metadata to local data."u8,                   1)
            .RegisterEntry("Added Favorites. You can declare mods as favorites and filter for them."u8,  1)
            .RegisterEntry("Added Local Tags. You can apply custom Tags to mods and filter for them."u8, 1)
            .RegisterEntry(
                "Added Mod Tags. Mod Creators (and the Edit Mod tab) can set tags that are stored in the mod meta data and are thus exported."u8)
            .RegisterEntry("Add backface and transparency toggles to .mtrl editing, as well as a info section."u8)
            .RegisterEntry("Meta Manipulation editing now highlights if the selected ID is 0 or 1."u8)
            .RegisterEntry("Fixed a bug when manually adding EQP or EQDP entries to Mods."u8)
            .RegisterEntry("Updated some tooltips and hints."u8)
            .RegisterEntry("Improved handling of IMC exception problems."u8)
            .RegisterEntry("Fixed a bug with misidentification of equipment decals."u8)
            .RegisterEntry(
                "Character collections can now be set via chat command, too. (/penumbra collection character <collection name> | <character name>)"u8)
            .RegisterEntry("Backend changes regarding API/IPC, consumers can but do not need to use the Penumbra.Api library as a submodule."u8)
            .RegisterEntry("Added API to delete mods and read and set their pseudo-filesystem paths."u8, 1)
            .RegisterEntry("Added API to check Penumbras enabled state and updates to it."u8,            1);

    private static void Add5_10_0(Changelog log)
        => log.NextVersion("Version 0.5.10.0"u8)
            .RegisterEntry("Renamed backup functionality to export functionality."u8)
            .RegisterEntry("A default export directory can now optionally be specified."u8)
            .RegisterEntry("If left blank, exports will still be stored in your mod directory."u8, 1)
            .RegisterEntry("Existing exports corresponding to existing mods will be moved automatically if the export directory is changed."u8,
                1)
            .RegisterEntry("Added buttons to export and import all color set rows at once during material editing."u8)
            .RegisterEntry("Fixed texture import being case sensitive on the extension."u8)
            .RegisterEntry("Fixed special collection selector increasing in size on non-default UI styling."u8)
            .RegisterEntry("Fixed color set rows not importing the dye values during material editing."u8)
            .RegisterEntry("Other miscellaneous small fixes."u8);

    private static void Add5_9_0(Changelog log)
        => log.NextVersion("Version 0.5.9.0"u8)
            .RegisterEntry("Special Collections are now split between male and female."u8)
            .RegisterEntry("Fix a bug where the Base and Interface Collection were set to None instead of Default on a fresh install."u8)
            .RegisterEntry("Fix a bug where cutscene actors were not properly reset and could be misidentified across multiple cutscenes."u8)
            .RegisterEntry("TexTools .meta and .rgsp files are now incorporated based on file- and game path extensions."u8);

    private static void Add5_8_7(Changelog log)
        => log.NextVersion("Version 0.5.8.7"u8)
            .RegisterEntry("Fixed some problems with metadata reloading and reverting and IMC files. (5.8.1 to 5.8.7)."u8)
            .RegisterImportant(
                "If you encounter any issues, please try completely restarting your game after updating (not just relogging), before reporting them."u8,
                1);

    private static void Add5_8_0(Changelog log)
        => log.NextVersion("Version 0.5.8.0"u8)
            .RegisterEntry("Added choices what Change Logs are to be displayed. It is recommended to just keep showing all."u8)
            .RegisterEntry("Added an Interface Collection assignment."u8)
            .RegisterEntry("All your UI mods will have to be in the interface collection."u8,                                           1)
            .RegisterEntry("Files that are categorized as UI files by the game will only check for redirections in this collection."u8, 1)
            .RegisterImportant(
                "Migration should have set your currently assigned Base Collection to the Interface Collection, please verify that."u8, 1)
            .RegisterEntry("New API / IPC for the Interface Collection added."u8, 1)
            .RegisterImportant("API / IPC consumers should verify whether they need to change resolving to the new collection."u8, 1)
            .RegisterImportant(
                "If other plugins are not using your interface collection yet, you can just keep Interface and Base the same collection for the time being."u8)
            .RegisterEntry(
                "Mods can now have default settings for each option group, that are shown while the mod is unconfigured and taken as initial values when configured."u8)
            .RegisterEntry("Default values are set when importing .ttmps from their default values, and can be changed in the Edit Mod tab."u8,
                1)
            .RegisterEntry("Files that the game loads super early should now be replaceable correctly via base or interface collection."u8)
            .RegisterEntry(
                "The 1.0 neck tattoo file should now be replaceable, even in character collections. You can also replace the transparent texture used instead. (This was ugly.)"u8)
            .RegisterEntry("Continued Work on the Texture Import/Export Tab:"u8)
            .RegisterEntry("Should work with lot more texture types for .dds and .tex files, most notably BC7 compression."u8, 1)
            .RegisterEntry("Supports saving .tex and .dds files in multiple texture types and generating MipMaps for them."u8, 1)
            .RegisterEntry("Interface reworked a bit, gives more information and the overlay side can be collapsed."u8,        1)
            .RegisterImportant(
                "May contain bugs or missing safeguards. Generally let me know what's missing, ugly, buggy, not working or could be improved. Not really feasible for me to test it all."u8,
                1)
            .RegisterEntry(
                "Added buttons for redrawing self or all as well as a tooltip to describe redraw options and a tutorial step for it."u8)
            .RegisterEntry("Collection Selectors now display None at the top if available."u8)
            .RegisterEntry(
                "Adding mods via API/IPC will now cause them to incorporate and then delete TexTools .meta and .rgsp files automatically."u8)
            .RegisterEntry("Fixed an issue with Actor 201 using Your Character collections in cutscenes."u8)
            .RegisterEntry("Fixed issues with and improved mod option editing."u8)
            .RegisterEntry(
                "Fixed some issues with and improved file redirection editing - you are now informed if you can not add a game path (because it is invalid or already in use)."u8)
            .RegisterEntry("Backend optimizations."u8)
            .RegisterEntry("Changed metadata change system again."u8, 1)
            .RegisterEntry("Improved logging efficiency."u8,          1);

    private static void Add5_7_1(Changelog log)
        => log.NextVersion("Version 0.5.7.1"u8)
            .RegisterEntry("Fixed the Changelog window not considering UI Scale correctly."u8)
            .RegisterEntry("Reworked Changelog display slightly."u8);

    private static void Add5_7_0(Changelog log)
        => log.NextVersion("Version 0.5.7.0"u8)
            .RegisterEntry("Added a Changelog!"u8)
            .RegisterEntry("Files in the UI category will no longer be deduplicated for the moment."u8)
            .RegisterImportant("If you experience UI-related crashes, please re-import your UI mods."u8, 1)
            .RegisterEntry("This is a temporary fix against those not-yet fully understood crashes and may be reworked later."u8, 1)
            .RegisterImportant(
                "There is still a possibility of UI related mods crashing the game, we are still investigating - they behave very weirdly. If you continue to experience crashing, try disabling your UI mods."u8,
                1)
            .RegisterEntry(
                "On import, Penumbra will now show files with extensions '.ttmp', '.ttmp2' and '.pmp'. You can still select showing generic archive files."u8)
            .RegisterEntry(
                "Penumbra Mod Pack ('.pmp') files are meant to be renames of any of the archive types that could already be imported that contain the necessary Penumbra meta files."u8,
                1)
            .RegisterImportant(
                "If you distribute any mod as an archive specifically for Penumbra, you should change its extension to '.pmp'. Supported base archive types are ZIP, 7-Zip and RAR."u8,
                1)
            .RegisterEntry("Penumbra will now save mod backups with the file extension '.pmp'. They still are regular ZIP files."u8, 1)
            .RegisterEntry(
                "Existing backups in your current mod directory should be automatically renamed. If you manage multiple mod directories, you may need to migrate the other ones manually."u8,
                1)
            .RegisterEntry("Fixed assigned collections not working correctly on adventurer plates."u8)
            .RegisterEntry("Fixed a wrongly displayed folder line in some circumstances."u8)
            .RegisterEntry("Fixed crash after deleting mod options."u8)
            .RegisterEntry("Fixed Inspect Window collections not working correctly."u8)
            .RegisterEntry("Made identically named options selectable in mod configuration. Do not name your options identically."u8)
            .RegisterEntry("Added some additional functionality for Mare Synchronos."u8);

    #endregion

    private static void AddDummy(Changelog log)
        => log.NextVersion(""u8);

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

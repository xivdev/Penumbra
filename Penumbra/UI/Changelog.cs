using OtterGui.Widgets;

namespace Penumbra.UI;

public class PenumbraChangelog
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
    }

    #region Changelogs

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
            .RegisterHighlight(
                "Migration should move all current names that correspond to NPCs to the appropriate NPC group and all names that can be valid Player names to a Player of any world.",
                1)
            .RegisterHighlight(
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
            .RegisterHighlight(
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
            .RegisterHighlight(
                "If you encounter any issues, please try completely restarting your game after updating (not just relogging), before reporting them.",
                1);

    private static void Add5_8_0(Changelog log)
        => log.NextVersion("Version 0.5.8.0")
            .RegisterEntry("Added choices what Change Logs are to be displayed. It is recommended to just keep showing all.")
            .RegisterEntry("Added an Interface Collection assignment.")
            .RegisterEntry("All your UI mods will have to be in the interface collection.",                                           1)
            .RegisterEntry("Files that are categorized as UI files by the game will only check for redirections in this collection.", 1)
            .RegisterHighlight(
                "Migration should have set your currently assigned Base Collection to the Interface Collection, please verify that.", 1)
            .RegisterEntry("New API / IPC for the Interface Collection added.", 1)
            .RegisterHighlight("API / IPC consumers should verify whether they need to change resolving to the new collection.", 1)
            .RegisterHighlight(
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
            .RegisterHighlight(
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
            .RegisterHighlight("If you experience UI-related crashes, please re-import your UI mods.", 1)
            .RegisterEntry("This is a temporary fix against those not-yet fully understood crashes and may be reworked later.", 1)
            .RegisterHighlight(
                "There is still a possibility of UI related mods crashing the game, we are still investigating - they behave very weirdly. If you continue to experience crashing, try disabling your UI mods.",
                1)
            .RegisterEntry(
                "On import, Penumbra will now show files with extensions '.ttmp', '.ttmp2' and '.pmp'. You can still select showing generic archive files.")
            .RegisterEntry(
                "Penumbra Mod Pack ('.pmp') files are meant to be renames of any of the archive types that could already be imported that contain the necessary Penumbra meta files.",
                1)
            .RegisterHighlight(
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

    private (int, ChangeLogDisplayType) ConfigData()
        => (_config.LastSeenVersion, _config.ChangeLogDisplayType);

    private void Save(int version, ChangeLogDisplayType type)
    {
        _config.LastSeenVersion      = version;
        _config.ChangeLogDisplayType = type;
        _config.Save();
    }
}

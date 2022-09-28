using OtterGui.Widgets;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    public const int LastChangelogVersion = 0;

    public static Changelog CreateChangelog()
    {
        var ret = new Changelog( "Penumbra Changelog", () => ( Penumbra.Config.LastSeenVersion, Penumbra.Config.ChangeLogDisplayType ),
            ( version, type ) =>
            {
                Penumbra.Config.LastSeenVersion      = version;
                Penumbra.Config.ChangeLogDisplayType = type;
                Penumbra.Config.Save();
            } );

        Add5_7_0( ret );
        Add5_7_1( ret );
        Add5_8_0( ret );
        Add5_8_7( ret );
        Add5_9_0( ret );

        return ret;
    }

    private static void Add5_9_0( Changelog log )
        => log.NextVersion( "Version 0.5.9.0" )
           .RegisterEntry( "Special Collections are now split between male and female." )
           .RegisterEntry( "Fix a bug where the Base and Interface Collection were set to None instead of Default on a fresh install." )
           .RegisterEntry( "Fix a bug where cutscene actors were not properly reset and could be misidentified across multiple cutscenes." )
           .RegisterEntry( "TexTools .meta and .rgsp files are now incorporated based on file- and game path extensions." );

    private static void Add5_8_7( Changelog log )
        => log.NextVersion( "Version 0.5.8.7" )
           .RegisterEntry( "Fixed some problems with metadata reloading and reverting and IMC files. (5.8.1 to 5.8.7)." )
           .RegisterHighlight(
                "If you encounter any issues, please try completely restarting your game after updating (not just relogging), before reporting them.",
                1 );

    private static void Add5_8_0( Changelog log )
        => log.NextVersion( "Version 0.5.8.0" )
           .RegisterEntry( "Added choices what Change Logs are to be displayed. It is recommended to just keep showing all." )
           .RegisterEntry( "Added an Interface Collection assignment." )
           .RegisterEntry( "All your UI mods will have to be in the interface collection.", 1 )
           .RegisterEntry( "Files that are categorized as UI files by the game will only check for redirections in this collection.", 1 )
           .RegisterHighlight(
                "Migration should have set your currently assigned Base Collection to the Interface Collection, please verify that.", 1 )
           .RegisterEntry( "New API / IPC for the Interface Collection added.", 1 )
           .RegisterHighlight( "API / IPC consumers should verify whether they need to change resolving to the new collection.", 1 )
           .RegisterHighlight(
                "If other plugins are not using your interface collection yet, you can just keep Interface and Base the same collection for the time being." )
           .RegisterEntry(
                "Mods can now have default settings for each option group, that are shown while the mod is unconfigured and taken as initial values when configured." )
           .RegisterEntry( "Default values are set when importing .ttmps from their default values, and can be changed in the Edit Mod tab.",
                1 )
           .RegisterEntry( "Files that the game loads super early should now be replaceable correctly via base or interface collection." )
           .RegisterEntry(
                "The 1.0 neck tattoo file should now be replaceable, even in character collections. You can also replace the transparent texture used instead. (This was ugly.)" )
           .RegisterEntry( "Continued Work on the Texture Import/Export Tab:" )
           .RegisterEntry( "Should work with lot more texture types for .dds and .tex files, most notably BC7 compression.", 1 )
           .RegisterEntry( "Supports saving .tex and .dds files in multiple texture types and generating MipMaps for them.", 1 )
           .RegisterEntry( "Interface reworked a bit, gives more information and the overlay side can be collapsed.", 1 )
           .RegisterHighlight(
                "May contain bugs or missing safeguards. Generally let me know what's missing, ugly, buggy, not working or could be improved. Not really feasible for me to test it all.",
                1 )
           .RegisterEntry(
                "Added buttons for redrawing self or all as well as a tooltip to describe redraw options and a tutorial step for it." )
           .RegisterEntry( "Collection Selectors now display None at the top if available." )
           .RegisterEntry(
                "Adding mods via API/IPC will now cause them to incorporate and then delete TexTools .meta and .rgsp files automatically." )
           .RegisterEntry( "Fixed an issue with Actor 201 using Your Character collections in cutscenes." )
           .RegisterEntry( "Fixed issues with and improved mod option editing." )
           .RegisterEntry(
                "Fixed some issues with and improved file redirection editing - you are now informed if you can not add a game path (because it is invalid or already in use)." )
           .RegisterEntry( "Backend optimizations." )
           .RegisterEntry( "Changed metadata change system again.", 1 )
           .RegisterEntry( "Improved logging efficiency.", 1 );

    private static void Add5_7_1( Changelog log )
        => log.NextVersion( "Version 0.5.7.1" )
           .RegisterEntry( "Fixed the Changelog window not considering UI Scale correctly." )
           .RegisterEntry( "Reworked Changelog display slightly." );

    private static void Add5_7_0( Changelog log )
        => log.NextVersion( "Version 0.5.7.0" )
           .RegisterEntry( "Added a Changelog!" )
           .RegisterEntry( "Files in the UI category will no longer be deduplicated for the moment." )
           .RegisterHighlight( "If you experience UI-related crashes, please re-import your UI mods.", 1 )
           .RegisterEntry( "This is a temporary fix against those not-yet fully understood crashes and may be reworked later.", 1 )
           .RegisterHighlight(
                "There is still a possibility of UI related mods crashing the game, we are still investigating - they behave very weirdly. If you continue to experience crashing, try disabling your UI mods.",
                1 )
           .RegisterEntry(
                "On import, Penumbra will now show files with extensions '.ttmp', '.ttmp2' and '.pmp'. You can still select showing generic archive files." )
           .RegisterEntry(
                "Penumbra Mod Pack ('.pmp') files are meant to be renames of any of the archive types that could already be imported that contain the necessary Penumbra meta files.",
                1 )
           .RegisterHighlight(
                "If you distribute any mod as an archive specifically for Penumbra, you should change its extension to '.pmp'. Supported base archive types are ZIP, 7-Zip and RAR.",
                1 )
           .RegisterEntry( "Penumbra will now save mod backups with the file extension '.pmp'. They still are regular ZIP files.", 1 )
           .RegisterEntry(
                "Existing backups in your current mod directory should be automatically renamed. If you manage multiple mod directories, you may need to migrate the other ones manually.",
                1 )
           .RegisterEntry( "Fixed assigned collections not working correctly on adventurer plates." )
           .RegisterEntry( "Fixed a wrongly displayed folder line in some circumstances." )
           .RegisterEntry( "Fixed crash after deleting mod options." )
           .RegisterEntry( "Fixed Inspect Window collections not working correctly." )
           .RegisterEntry( "Made identically named options selectable in mod configuration. Do not name your options identically." )
           .RegisterEntry( "Added some additional functionality for Mare Synchronos." );
}
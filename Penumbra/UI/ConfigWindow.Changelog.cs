using OtterGui.Widgets;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    public const int LastChangelogVersion = 0;

    public static Changelog CreateChangelog()
    {
        var ret = new Changelog( "Penumbra Changelog", () => Penumbra.Config.LastSeenVersion, version =>
        {
            Penumbra.Config.LastSeenVersion = version;
            Penumbra.Config.Save();
        } );

        Add5_7_0( ret );
        Add5_7_1( ret );

        return ret;
    }

    private static void Add5_8_0( Changelog log )
        => log.NextVersion( "Version 0.5.8.0" )
           .RegisterEntry( "Fixed an issue with Actor 201 using Your Character collections in cutscenes." )
           .RegisterEntry( "Fixed issues with and improved mod option editing." )
           .RegisterEntry( "Backend optimizations." );

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
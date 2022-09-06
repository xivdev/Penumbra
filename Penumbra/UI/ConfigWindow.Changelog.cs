using Lumina.Excel.GeneratedSheets;
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

        return ret;
    }

    private static void Add5_7_0( Changelog log )
        => log.NextVersion( "Version 0.5.7.0" )
           .RegisterEntry( "Added a Changelog!" )
           .RegisterEntry( "Files in the UI category will no longer be deduplicated for the moment." )
           .RegisterHighlight( "If you experience UI-related crashes, please re-import your UI mods.", 1 )
           .RegisterEntry( "This is a temporary fix against those not-yet fully understood crashes and may be reworked later.", 1 )
           .RegisterEntry( "Fixed assigned collections not working correctly on adventurer plates." )
           .RegisterEntry( "Added some additional functionality for Mare Synchronos." );
}
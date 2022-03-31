using System.IO;

namespace Penumbra.Mods;

public sealed partial class Mod2
{
    public sealed partial class Manager
    {
        public static string ModFileSystemFile
            => Path.Combine( Dalamud.PluginInterface.GetPluginConfigDirectory(), "sort_order.json" );
    }
}
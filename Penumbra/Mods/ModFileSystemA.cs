using System.IO;
using OtterGui.Filesystem;

namespace Penumbra.Mods;

public sealed class ModFileSystemA : FileSystem< Mod >
{
    public void Save()
        => SaveToFile( new FileInfo( Mod.Manager.SortOrderFile ), SaveMod, true );

    public static ModFileSystemA Load()
    {
        var x = new ModFileSystemA();
        if( x.Load( new FileInfo( Mod.Manager.SortOrderFile ), Penumbra.ModManager.Mods, ModToIdentifier, ModToName ) )
        {
            x.Save();
        }

        x.Changed += ( _1, _2, _3, _4 ) => x.Save();

        return x;
    }

    private static string ModToIdentifier( Mod mod )
        => mod.BasePath.Name;

    private static string ModToName( Mod mod )
        => mod.Meta.Name.Text;

    private static (string, bool) SaveMod( Mod mod, string fullPath )
    {
        if( fullPath == ModToName( mod ) )
        {
            return ( string.Empty, false );
        }

        return ( ModToIdentifier( mod ), true );
    }
}
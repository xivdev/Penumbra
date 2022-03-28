using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Logging;

namespace Penumbra.Mods;

public partial class ModManagerNew
{
    private readonly List< Mod > _mods = new();

    public IReadOnlyList< Mod > Mods
        => _mods;

    public void DiscoverMods()
    {
        //_mods.Clear();
        //
        //if( CheckValidity() )
        //{
        //    foreach( var modFolder in BasePath.EnumerateDirectories() )
        //    {
        //        var mod = ModData.LoadMod( StructuredMods, modFolder );
        //        if( mod == null )
        //        {
        //            continue;
        //        }
        //
        //        Mods.Add( modFolder.Name, mod );
        //    }
        //
        //    SetModStructure();
        //}
        //
        //Collections.RecreateCaches();
    }
}

public partial class ModManagerNew
{
    public DirectoryInfo BasePath { get; private set; } = null!;
    public bool Valid { get; private set; }

    public event Action< DirectoryInfo >? BasePathChanged;

    public ModManagerNew()
    {
        InitBaseDirectory( Penumbra.Config.ModDirectory );
    }

    public bool CheckValidity()
    {
        if( Valid )
        {
            Valid = Directory.Exists( BasePath.FullName );
        }

        return Valid;
    }

    private static (DirectoryInfo, bool) CreateDirectory( string path )
    {
        var newDir = new DirectoryInfo( path );
        if( !newDir.Exists )
        {
            try
            {
                Directory.CreateDirectory( newDir.FullName );
                newDir.Refresh();
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Could not create specified mod directory {newDir.FullName}:\n{e}" );
                return ( newDir, false );
            }
        }

        return ( newDir, true );
    }

    private void InitBaseDirectory( string path )
    {
        if( path.Length == 0 )
        {
            Valid    = false;
            BasePath = new DirectoryInfo( "." );
            return;
        }

        ( BasePath, Valid ) = CreateDirectory( path );

        if( Penumbra.Config.ModDirectory != BasePath.FullName )
        {
            Penumbra.Config.ModDirectory = BasePath.FullName;
            Penumbra.Config.Save();
        }
    }

    private void ChangeBaseDirectory( string path )
    {
        if( string.Equals( path, Penumbra.Config.ModDirectory, StringComparison.InvariantCultureIgnoreCase ) )
        {
            return;
        }

        InitBaseDirectory( path );
        BasePathChanged?.Invoke( BasePath );
    }
}
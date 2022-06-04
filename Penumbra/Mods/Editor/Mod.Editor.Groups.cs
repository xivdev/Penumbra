using System.Collections.Generic;
using Penumbra.GameData.ByteString;

namespace Penumbra.Mods;

public partial class Mod
{
    public partial class Editor
    {
        public void Normalize()
        {}

        public void AutoGenerateGroups()
        {
            //ClearEmptySubDirectories( _mod.BasePath );
            //for( var i = _mod.Groups.Count - 1; i >= 0; --i )
            //{
            //    if (_mod.Groups.)
            //    Penumbra.ModManager.DeleteModGroup( _mod, i );
            //}
            //Penumbra.ModManager.OptionSetFiles( _mod, -1, 0, new Dictionary< Utf8GamePath, FullPath >() );
            //
            //foreach( var groupDir in _mod.BasePath.EnumerateDirectories() )
            //{
            //    var groupName = groupDir.Name;
            //    foreach( var optionDir in groupDir.EnumerateDirectories() )
            //    { }
            //}

            //var group = new OptionGroup
            //    {
            //        GroupName     = groupDir.Name,
            //        SelectionType = SelectType.Single,
            //        Options       = new List< Option >(),
            //    };
            //
            //    foreach( var optionDir in groupDir.EnumerateDirectories() )
            //    {
            //        var option = new Option
            //        {
            //            OptionDesc  = string.Empty,
            //            OptionName  = optionDir.Name,
            //            OptionFiles = new Dictionary< Utf8RelPath, HashSet< Utf8GamePath > >(),
            //        };
            //        foreach( var file in optionDir.EnumerateFiles( "*.*", SearchOption.AllDirectories ) )
            //        {
            //            if( Utf8RelPath.FromFile( file, baseDir, out var rel )
            //            && Utf8GamePath.FromFile( file, optionDir, out var game ) )
            //            {
            //                option.OptionFiles[ rel ] = new HashSet< Utf8GamePath > { game };
            //            }
            //        }
            //
            //        if( option.OptionFiles.Count > 0 )
            //        {
            //            group.Options.Add( option );
            //        }
            //    }
            //
            //    if( group.Options.Count > 0 )
            //    {
            //        meta.Groups.Add( groupDir.Name, group );
            //    }
            //}
            //
            //var idx = Penumbra.ModManager.Mods.IndexOf( m => m.Meta == meta );
            //foreach( var collection in Penumbra.CollectionManager )
            //{
            //    collection.Settings[ idx ]?.FixInvalidSettings( meta );
            //}
        }
    }
}
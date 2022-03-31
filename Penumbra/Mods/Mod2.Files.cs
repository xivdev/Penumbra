using System.Collections.Generic;
using System.IO;
using System.Linq;
using Penumbra.GameData.ByteString;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Mods;

public partial class Mod2
{
    public IReadOnlyDictionary< Utf8GamePath, FullPath > RemainingFiles
        => _remainingFiles;

    public IReadOnlyList< IModGroup > Options
        => _options;

    public bool HasOptions { get; private set; } = false;

    private void SetHasOptions()
    {
        HasOptions = _options.Any( o
            => o is MultiModGroup m && m.PrioritizedOptions.Count > 0 || o is SingleModGroup s && s.OptionData.Count > 1 );
    }

    private readonly Dictionary< Utf8GamePath, FullPath > _remainingFiles = new();
    private readonly List< IModGroup >                    _options        = new();

    public IEnumerable< (Utf8GamePath, FullPath) > AllFiles
        => _remainingFiles.Concat( _options.SelectMany( o => o ).SelectMany( o => o.Files.Concat( o.FileSwaps ) ) )
           .Select( kvp => ( kvp.Key, kvp.Value ) );

    public IEnumerable< MetaManipulation > AllManipulations
        => _options.SelectMany( o => o ).SelectMany( o => o.Manipulations );

    private void ReloadFiles()
    {
        // _remainingFiles.Clear();
        // _options.Clear();
        // HasOptions = false;
        // if( !Directory.Exists( BasePath.FullName ) )
        //     return;
    }
}
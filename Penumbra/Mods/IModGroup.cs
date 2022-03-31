using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Logging;
using Penumbra.Util;

namespace Penumbra.Mods;

public interface IModGroup : IEnumerable< ISubMod >
{
    public string Name { get; }
    public string Description { get; }
    public SelectType Type { get; }
    public int Priority { get; }

    public int OptionPriority( Index optionIdx );

    public ISubMod this[ Index idx ] { get; }

    public int Count { get; }

    public bool IsOption
        => Type switch
        {
            SelectType.Single => Count > 1,
            SelectType.Multi  => Count > 0,
            _                 => false,
        };

    public void Save( DirectoryInfo basePath );

    public string FileName( DirectoryInfo basePath )
        => Path.Combine( basePath.FullName, Name.RemoveInvalidPathSymbols() + ".json" );

    public void DeleteFile( DirectoryInfo basePath )
    {
        var file = FileName( basePath );
        if( !File.Exists( file ) )
        {
            return;
        }

        try
        {
            File.Delete( file );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not delete file {file}:\n{e}" );
            throw;
        }
    }
}
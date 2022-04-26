using System;
using System.Collections.Generic;
using Dalamud.Logging;
using Penumbra.GameData.ByteString;
using Penumbra.Mods;

namespace Penumbra.Api;

public enum RedirectResult
{
    Registered              = 0,
    Success                 = 0,
    IdenticalFileRegistered = 1,
    InvalidGamePath         = 2,
    OtherOwner              = 3,
    NotRegistered           = 4,
    NoPermission            = 5,
    FilteredGamePath        = 6,
    UnknownError            = 7,
}

public class SimpleRedirectManager
{
    internal readonly Dictionary< Utf8GamePath, (FullPath File, string Tag) > Replacements = new();
    public readonly   HashSet< string >                                       AllowedTags   = new();

    public void Apply( IDictionary< Utf8GamePath, FullPath > dict )
    {
        foreach( var (gamePath, (file, _)) in Replacements )
        {
            dict.TryAdd( gamePath, file );
        }
    }

    private RedirectResult? CheckPermission( string tag )
        => AllowedTags.Contains( tag ) ? null : RedirectResult.NoPermission;

    public RedirectResult IsRegistered( Utf8GamePath path, string tag )
        => CheckPermission( tag )
         ?? ( Replacements.TryGetValue( path, out var pair )
                ? pair.Tag == tag ? RedirectResult.Registered : RedirectResult.OtherOwner
                : RedirectResult.NotRegistered );

    public RedirectResult Register( Utf8GamePath path, FullPath file, string tag )
    {
        if( CheckPermission( tag ) != null )
        {
            return RedirectResult.NoPermission;
        }

        if( Mod.FilterFile( path ) )
        {
            return RedirectResult.FilteredGamePath;
        }

        try
        {
            if( Replacements.TryGetValue( path, out var pair ) )
            {
                if( file.Equals( pair.File ) )
                {
                    return RedirectResult.IdenticalFileRegistered;
                }

                if( tag != pair.Tag )
                {
                    return RedirectResult.OtherOwner;
                }
            }

            Replacements[ path ] = ( file, tag );
            return RedirectResult.Success;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"[{tag}] Unknown Error registering simple redirect {path} -> {file}:\n{e}" );
            return RedirectResult.UnknownError;
        }
    }

    public RedirectResult Unregister( Utf8GamePath path, string tag )
    {
        if( CheckPermission( tag ) != null )
        {
            return RedirectResult.NoPermission;
        }

        try
        {
            if( !Replacements.TryGetValue( path, out var pair ) )
            {
                return RedirectResult.NotRegistered;
            }

            if( tag != pair.Tag )
            {
                return RedirectResult.OtherOwner;
            }

            Replacements.Remove( path );
            return RedirectResult.Success;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"[{tag}] Unknown Error unregistering simple redirect {path}:\n{e}" );
            return RedirectResult.UnknownError;
        }
    }

    public RedirectResult Register( string path, string file, string tag )
        => Utf8GamePath.FromString( path, out var gamePath, true )
            ? Register( gamePath, new FullPath( file ), tag )
            : RedirectResult.InvalidGamePath;

    public RedirectResult Unregister( string path, string tag )
        => Utf8GamePath.FromString( path, out var gamePath, true )
            ? Unregister( gamePath, tag )
            : RedirectResult.InvalidGamePath;

    public RedirectResult IsRegistered( string path, string tag )
        => Utf8GamePath.FromString( path, out var gamePath, true )
            ? IsRegistered( gamePath, tag )
            : RedirectResult.InvalidGamePath;
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Dalamud.Logging;
using Penumbra.Mod;

namespace Penumbra.Util;

public static class ModelChanger
{
    private static int FindSubSequence( byte[] main, byte[] sub, int from = 0 )
    {
        if( sub.Length + from > main.Length )
        {
            return -1;
        }

        var length = main.Length - sub.Length;
        for( var i = from; i < length; ++i )
        {
            var span = main.AsSpan( i, sub.Length );
            if( span.SequenceEqual( sub ) )
            {
                return i;
            }
        }

        return -1;
    }

    private static bool ConvertString( string text, out byte[] data )
    {
        data = Encoding.UTF8.GetBytes( text );
        return data.Length == text.Length && !data.Any( b => b > 0b10000000 );
    }

    public static bool ValidStrings( string from, string to )
        => from.Length                         != 0
         && to.Length                          != 0
         && from.Length                        < 16
         && to.Length                          < 16
         && from                               != to
         && Encoding.UTF8.GetByteCount( from ) == from.Length
         && Encoding.UTF8.GetByteCount( to )   == to.Length;

    private static bool ConvertName( string name, out byte[] data )
    {
        if( name.Length != 0 )
        {
            return ConvertString( $"/mt_c0201b0001_{name}.mtrl", out data );
        }

        data = Array.Empty< byte >();
        return false;
    }

    private static int ReplaceEqualSequences( byte[] main, byte[] subLhs, byte[] subRhs )
    {
        if( subLhs.SequenceEqual( subRhs ) )
        {
            return 0;
        }

        var i            = 0;
        var replacements = 0;
        while( ( i = FindSubSequence( main, subLhs, i ) ) > 0 )
        {
            subRhs.CopyTo( main.AsSpan( i ) );
            i += subLhs.Length;
            ++replacements;
        }

        return replacements;
    }

    private static void ReplaceStringSize( byte[] main, int sizeDiff )
    {
        var stackSize           = BitConverter.ToUInt32( main, 4 );
        var runtimeBegin        = stackSize    + 0x44;
        var stringsLengthOffset = runtimeBegin + 4;
        var stringsLength       = BitConverter.ToUInt32( main, ( int )stringsLengthOffset );
        var newLength           = stringsLength + sizeDiff;
        Debug.Assert( newLength > 0 );
        BitConverter.TryWriteBytes( main.AsSpan( ( int )stringsLengthOffset ), ( uint )newLength );
    }

    private static int ReplaceSubSequences( ref byte[] main, byte[] subLhs, byte[] subRhs )
    {
        if( subLhs.Length == subRhs.Length )
        {
            return ReplaceEqualSequences( main, subLhs, subRhs );
        }

        var replacements = new List< int >( 4 );
        for( var i = FindSubSequence( main, subLhs ); i >= 0; i = FindSubSequence( main, subLhs, i + subLhs.Length ) )
        {
            replacements.Add( i );
        }

        var sizeDiff = ( subRhs.Length - subLhs.Length ) * replacements.Count;
        var ret      = new byte[main.Length + sizeDiff];

        var last        = 0;
        var totalLength = 0;
        foreach( var i in replacements )
        {
            var length = i - last;
            main.AsSpan( last, length ).CopyTo( ret.AsSpan( totalLength ) );
            totalLength += length;
            subRhs.CopyTo( ret.AsSpan( totalLength ) );
            totalLength += subRhs.Length;
            last        =  i + subLhs.Length;
        }

        main.AsSpan( last ).CopyTo( ret.AsSpan( totalLength ) );
        ReplaceStringSize( ret, sizeDiff );
        main = ret;
        return replacements.Count;
    }

    public static int ChangeMtrl( FullPath file, byte[] from, byte[] to )
    {
        if( !file.Exists )
        {
            return 0;
        }

        try
        {
            var text     = File.ReadAllBytes( file.FullName );
            var replaced = ReplaceSubSequences( ref text, from, to );
            if( replaced > 0 )
            {
                File.WriteAllBytes( file.FullName, text );
            }

            return replaced;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not write .mdl data for file {file.FullName}:\n{e}" );
            return -1;
        }
    }

    public static bool ChangeModMaterials( ModData mod, string from, string to )
    {
        if( ValidStrings( from, to ) && ConvertName( from, out var lhs ) && ConvertName( to, out var rhs ) )
        {
            return mod.Resources.ModFiles
               .Where( f => f.Extension.Equals( ".mdl", StringComparison.InvariantCultureIgnoreCase ) )
               .All( file => ChangeMtrl( file, lhs, rhs ) >= 0 );
        }

        PluginLog.Warning( $"{from} or {to} can not be valid material suffixes." );
        return false;
    }
}
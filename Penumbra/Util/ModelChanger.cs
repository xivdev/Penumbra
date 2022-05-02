using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Logging;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Files;
using Penumbra.Mods;

namespace Penumbra.Util;

public static class ModelChanger
{
    public const           string MaterialFormat = "/mt_c0201b0001_{0}.mtrl";
    public static readonly Regex  MaterialRegex  = new(@"/mt_c0201b0001_.*?\.mtrl", RegexOptions.Compiled);

    // Non-ASCII encoding can not be used.
    public static bool ValidStrings( string from, string to )
        =>  to.Length                          != 0
         && from.Length                        < 16
         && to.Length                          < 16
         && from                               != to
         && Encoding.UTF8.GetByteCount( from ) == from.Length
         && Encoding.UTF8.GetByteCount( to )   == to.Length;


    [Conditional( "FALSE" )]
    private static void WriteBackup( string name, byte[] text )
        => File.WriteAllBytes( name + ".bak", text );

    // Change material suffices for a single mdl file.
    public static int ChangeMtrl( FullPath file, string from, string to )
    {
        if( !file.Exists )
        {
            return 0;
        }

        try
        {
            var                  data    = File.ReadAllBytes( file.FullName );
            var                  mdlFile = new MdlFile( data );

            // If from is empty, match with any current material suffix,
            // otherwise check for exact matches with from.
            Func< string, bool > compare = MaterialRegex.IsMatch;
            if( from.Length > 0 )
            {
                from    = string.Format( MaterialFormat, from );
                compare = s => s == from;
            }

            to = string.Format( MaterialFormat, to );
            var replaced = 0;
            for( var i = 0; i < mdlFile.Materials.Length; ++i )
            {
                if( compare( mdlFile.Materials[ i ] ) )
                {
                    mdlFile.Materials[ i ] = to;
                    ++replaced;
                }
            }

            // Only rewrite the file if anything was changed.
            if( replaced > 0 )
            {
                WriteBackup( file.FullName, data );
                File.WriteAllBytes( file.FullName, mdlFile.Write() );
            }

            return replaced;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not write .mdl data for file {file.FullName}:\n{e}" );
            return -1;
        }
    }

    public static bool ChangeModMaterials( Mod mod, string from, string to )
    {
        if( ValidStrings( from, to ) )
        {
            return mod.AllFiles
               .Where( f => f.Extension.Equals( ".mdl", StringComparison.InvariantCultureIgnoreCase ) )
               .All( file => ChangeMtrl( file, from, to ) >= 0 );
        }

        PluginLog.Warning( $"{from} or {to} can not be valid material suffixes." );
        return false;
    }
}
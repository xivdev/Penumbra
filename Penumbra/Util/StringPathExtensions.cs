using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Penumbra.Util;

public static class StringPathExtensions
{
    private static readonly HashSet< char > Invalid = new(Path.GetInvalidFileNameChars());

    public static string ReplaceInvalidPathSymbols( this string s, string replacement = "_" )
    {
        StringBuilder sb = new(s.Length);
        foreach( var c in s )
        {
            if( Invalid.Contains( c ) )
            {
                sb.Append( replacement );
            }
            else
            {
                sb.Append( c );
            }
        }

        return sb.ToString();
    }

    public static string RemoveInvalidPathSymbols( this string s )
        => string.Concat( s.Split( Path.GetInvalidFileNameChars() ) );

    public static string ReplaceNonAsciiSymbols( this string s, string replacement = "_" )
    {
        StringBuilder sb = new(s.Length);
        foreach( var c in s )
        {
            if( c >= 128 )
            {
                sb.Append( replacement );
            }
            else
            {
                sb.Append( c );
            }
        }

        return sb.ToString();
    }

    public static string ReplaceBadXivSymbols( this string s, string replacement = "_" )
    {
        StringBuilder sb = new(s.Length);
        foreach( var c in s )
        {
            if( c >= 128 || Invalid.Contains( c ) )
            {
                sb.Append( replacement );
            }
            else
            {
                sb.Append( c );
            }
        }

        return sb.ToString();
    }
}
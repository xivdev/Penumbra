using System.IO;

namespace Penumbra
{
    public static class StringPathExtensions
    {
        private static char[] _invalid = Path.GetInvalidFileNameChars();
        public static string ReplaceInvalidPathSymbols( this string s, string replacement = "_" )
        {
            return string.Join( replacement, s.Split( _invalid ) );
        }

        public static string RemoveInvalidPathSymbols( this string s )
        {
            return string.Concat( s.Split( _invalid ) );
        }
    }
}
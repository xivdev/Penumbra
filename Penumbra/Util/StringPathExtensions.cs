using System.IO;
using System.Text;

namespace Penumbra.Util
{
    public static class StringPathExtensions
    {
        private static readonly char[] Invalid = Path.GetInvalidFileNameChars();

        public static string ReplaceInvalidPathSymbols( this string s, string replacement = "_" )
            => string.Join( replacement, s.Split( Invalid ) );

        public static string RemoveInvalidPathSymbols( this string s )
            => string.Concat( s.Split( Invalid ) );

        public static string RemoveNonAsciiSymbols( this string s, string replacement = "_" )
        {
            StringBuilder sb = new( s.Length );
            foreach( var c in s )
            {
                if( c < 128 )
                {
                    sb.Append( c );
                }
                else
                {
                    sb.Append( replacement );
                }
            }

            return sb.ToString();
        }
    }
}
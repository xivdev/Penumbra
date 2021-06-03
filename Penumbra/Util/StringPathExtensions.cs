using System.IO;
using System.Linq;
using System.Text;

namespace Penumbra
{
    public static class StringPathExtensions
    {
        private static readonly char[] _invalid = Path.GetInvalidFileNameChars();

        public static string ReplaceInvalidPathSymbols( this string s, string replacement = "_" )
            => string.Join( replacement, s.Split( _invalid ) );

        public static string RemoveInvalidPathSymbols( this string s )
            => string.Concat( s.Split( _invalid ) );

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
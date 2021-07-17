using System.IO;

namespace Penumbra.Util
{
    public static class MemoryStreamExtensions
    {
        public static void Write( this MemoryStream stream, byte[] data )
        {
            stream.Write( data, 0, data.Length );
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

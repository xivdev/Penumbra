using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Penumbra.GameData.Files;

public partial class MtrlFile
{
    public byte[] Write()
    {
        using var stream = new MemoryStream();
        using( var w = new BinaryWriter( stream ) )
        {
            const int materialHeaderSize = 4 + 2 + 2 + 2 + 2 + 1 + 1 + 1 + 1;

            w.BaseStream.Seek( materialHeaderSize, SeekOrigin.Begin );
            ushort cumulativeStringOffset = 0;
            foreach( var texture in Textures )
            {
                w.Write( cumulativeStringOffset );
                w.Write( texture.Flags );
                cumulativeStringOffset += ( ushort )( texture.Path.Length + 1 );
            }

            foreach( var set in UvSets )
            {
                w.Write( cumulativeStringOffset );
                w.Write( set.Index );
                cumulativeStringOffset += ( ushort )( set.Name.Length + 1 );
            }

            foreach( var set in ColorSets )
            {
                w.Write( cumulativeStringOffset );
                w.Write( set.Index );
                cumulativeStringOffset += ( ushort )( set.Name.Length + 1 );
            }

            foreach( var text in Textures.Select( t => t.Path )
                       .Concat( UvSets.Select( c => c.Name ) )
                       .Concat( ColorSets.Select( c => c.Name ) )
                       .Append( ShaderPackage.Name ) )
            {
                w.Write( Encoding.UTF8.GetBytes( text ) );
                w.Write( ( byte )'\0' );
            }

            w.Write( AdditionalData );
            var dataSetSize = 0;
            foreach( var row in ColorSets.Where( c => c.HasRows ).Select( c => c.Rows ) )
            {
                var span = row.AsBytes();
                w.Write( span );
                dataSetSize += span.Length;
            }

            foreach( var row in ColorDyeSets.Select( c => c.Rows ) )
            {
                var span = row.AsBytes();
                w.Write( span );
                dataSetSize += span.Length;
            }

            w.Write( ( ushort )( ShaderPackage.ShaderValues.Length * 4 ) );
            w.Write( ( ushort )ShaderPackage.ShaderKeys.Length );
            w.Write( ( ushort )ShaderPackage.Constants.Length );
            w.Write( ( ushort )ShaderPackage.Samplers.Length );
            w.Write( ShaderPackage.Flags );

            foreach( var key in ShaderPackage.ShaderKeys )
            {
                w.Write( key.Category );
                w.Write( key.Value );
            }

            foreach( var constant in ShaderPackage.Constants )
            {
                w.Write( constant.Id );
                w.Write( constant.Value );
            }

            foreach( var sampler in ShaderPackage.Samplers )
            {
                w.Write( sampler.SamplerId );
                w.Write( sampler.Flags );
                w.Write( sampler.TextureIndex );
                w.Write( ( ushort )0 );
                w.Write( ( byte )0 );
            }

            foreach( var value in ShaderPackage.ShaderValues )
            {
                w.Write( value );
            }

            WriteHeader( w, ( ushort )w.BaseStream.Position, dataSetSize, cumulativeStringOffset );
        }

        return stream.ToArray();
    }

    private void WriteHeader( BinaryWriter w, ushort fileSize, int dataSetSize, ushort shaderPackageNameOffset )
    {
        w.BaseStream.Seek( 0, SeekOrigin.Begin );
        w.Write( Version );
        w.Write( fileSize );
        w.Write( ( ushort )dataSetSize );
        w.Write( ( ushort )( shaderPackageNameOffset + ShaderPackage.Name.Length + 1 ) );
        w.Write( shaderPackageNameOffset );
        w.Write( ( byte )Textures.Length );
        w.Write( ( byte )UvSets.Length );
        w.Write( ( byte )ColorSets.Length );
        w.Write( ( byte )AdditionalData.Length );
    }
}
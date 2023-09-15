using System.Text;
using Penumbra.GameData;
using Penumbra.Import.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Import;

/// <summary>TexTools provices custom generated *.meta files for its modpacks, that contain changes to
///     - imc files
///     - eqp files
///     - gmp files
///     - est files
///     - eqdp files
/// made by the mod. The filename determines to what the changes are applied, and the binary file itself contains changes.
/// We parse every *.meta file in a mod and combine all actual changes that do not keep data on default values and that can be applied to the game in a .json.
/// TexTools may also generate files that contain non-existing changes, e.g. *.imc files for weapon offhands, which will be ignored.
/// TexTools also provides .rgsp files, that contain changes to the racial scaling parameters in the human.cmp file.</summary>
public partial class TexToolsMeta
{
    /// <summary> An empty TexToolsMeta. </summary>
    public static readonly TexToolsMeta Invalid = new(null!, string.Empty, 0);

    // The info class determines the files or table locations the changes need to apply to from the filename.
    public readonly  uint                     Version;
    public readonly  string                   FilePath;
    public readonly  List< MetaManipulation > MetaManipulations = new();
    private readonly bool                     _keepDefault      = false;

    private readonly MetaFileManager _metaFileManager;

    public TexToolsMeta( MetaFileManager metaFileManager, IGamePathParser parser, byte[] data, bool keepDefault )
    {
        _metaFileManager = metaFileManager;
        _keepDefault = keepDefault;
        try
        {
            using var reader = new BinaryReader( new MemoryStream( data ) );
            Version  = reader.ReadUInt32();
            FilePath = ReadNullTerminated( reader );
            var metaInfo    = new MetaFileInfo( parser, FilePath );
            var numHeaders  = reader.ReadUInt32();
            var headerSize  = reader.ReadUInt32();
            var headerStart = reader.ReadUInt32();
            reader.BaseStream.Seek( headerStart, SeekOrigin.Begin );

            List< (MetaManipulation.Type type, uint offset, int size) > entries = new();
            for( var i = 0; i < numHeaders; ++i )
            {
                var currentOffset = reader.BaseStream.Position;
                var type          = ( MetaManipulation.Type )reader.ReadUInt32();
                var offset        = reader.ReadUInt32();
                var size          = reader.ReadInt32();
                entries.Add( ( type, offset, size ) );
                reader.BaseStream.Seek( currentOffset + headerSize, SeekOrigin.Begin );
            }

            byte[]? ReadEntry( MetaManipulation.Type type )
            {
                var idx = entries.FindIndex( t => t.type == type );
                if( idx < 0 )
                {
                    return null;
                }

                reader.BaseStream.Seek( entries[ idx ].offset, SeekOrigin.Begin );
                return reader.ReadBytes( entries[ idx ].size );
            }

            DeserializeEqpEntry( metaInfo, ReadEntry( MetaManipulation.Type.Eqp ) );
            DeserializeGmpEntry( metaInfo, ReadEntry( MetaManipulation.Type.Gmp ) );
            DeserializeEqdpEntries( metaInfo, ReadEntry( MetaManipulation.Type.Eqdp ) );
            DeserializeEstEntries( metaInfo, ReadEntry( MetaManipulation.Type.Est ) );
            DeserializeImcEntries( metaInfo, ReadEntry( MetaManipulation.Type.Imc ) );
        }
        catch( Exception e )
        {
            FilePath = "";
            Penumbra.Log.Error( $"Error while parsing .meta file:\n{e}" );
        }
    }

    private TexToolsMeta( MetaFileManager metaFileManager, string filePath, uint version )
    {
        _metaFileManager = metaFileManager;
        FilePath         = filePath;
        Version          = version;
    }

    // Read a null terminated string from a binary reader.
    private static string ReadNullTerminated( BinaryReader reader )
    {
        var builder = new StringBuilder();
        for( var c = reader.ReadChar(); c != 0; c = reader.ReadChar() )
        {
            builder.Append( c );
        }

        return builder.ToString();
    }
}
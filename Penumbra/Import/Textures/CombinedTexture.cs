using System;
using System.IO;
using System.Numerics;
using Lumina.Data.Files;
using OtterTex;
using Penumbra.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using DalamudUtil = Dalamud.Utility.Util;
using Image = SixLabors.ImageSharp.Image;

namespace Penumbra.Import.Textures;

public partial class CombinedTexture : IDisposable
{
    public enum TextureSaveType
    {
        AsIs,
        Bitmap,
        BC3,
        BC7,
    }

    private enum Mode
    {
        Empty,
        LeftCopy,
        RightCopy,
        Custom,
    }

    private readonly Texture _left;
    private readonly Texture _right;

    private Texture? _current;
    private Mode     _mode = Mode.Empty;

    private readonly Texture _centerStorage = new();

    public bool IsLoaded
        => _mode != Mode.Empty;

    public Exception? SaveException { get; private set; } = null;

    public void Draw( Vector2 size )
    {
        if( _mode == Mode.Custom && !_centerStorage.IsLoaded )
        {
            var (width, height) = CombineImage();
            _centerStorage.TextureWrap =
                DalamudServices.PluginInterface.UiBuilder.LoadImageRaw( _centerStorage.RGBAPixels, width, height, 4 );
        }

        _current?.Draw( size );
    }


    public void SaveAsPng( string path )
    {
        if( !IsLoaded || _current == null )
        {
            return;
        }

        try
        {
            var image = Image.LoadPixelData< Rgba32 >( _current.RGBAPixels, _current.TextureWrap!.Width,
                _current.TextureWrap!.Height );
            image.Save( path, new PngEncoder() { CompressionLevel = PngCompressionLevel.NoCompression } );
            SaveException = null;
        }
        catch( Exception e )
        {
            SaveException = e;
        }
    }

    private void SaveAs( string path, TextureSaveType type, bool mipMaps, bool writeTex )
    {
        if( _current == null || _mode == Mode.Empty )
        {
            return;
        }

        try
        {
            if( _current.BaseImage is not ScratchImage s )
            {
                s = ScratchImage.FromRGBA( _current.RGBAPixels, _current.TextureWrap!.Width,
                    _current.TextureWrap!.Height, out var i ).ThrowIfError( i );
            }

            var tex = type switch
            {
                TextureSaveType.AsIs => _current.Type is Texture.FileType.Bitmap or Texture.FileType.Png ? CreateUncompressed( s, mipMaps ) : s,
                TextureSaveType.Bitmap => CreateUncompressed( s, mipMaps ),
                TextureSaveType.BC3 => CreateCompressed( s, mipMaps, false ),
                TextureSaveType.BC7 => CreateCompressed( s, mipMaps, true ),
                _ => throw new ArgumentOutOfRangeException( nameof( type ), type, null ),
            };

            if( !writeTex )
            {
                tex.SaveDDS( path );
            }
            else
            {
                SaveTex( path, tex );
            }

            SaveException = null;
        }
        catch( Exception e )
        {
            SaveException = e;
        }
    }

    private static void SaveTex( string path, ScratchImage input )
    {
        var header = input.ToTexHeader();
        if( header.Format == TexFile.TextureFormat.Unknown )
        {
            throw new Exception( $"Could not save tex file with format {input.Meta.Format}, not convertible to a valid .tex formats." );
        }

        using var stream = File.Open( path, File.Exists(path) ? FileMode.Truncate : FileMode.CreateNew);
        using var w      = new BinaryWriter( stream );
        header.Write( w );
        w.Write( input.Pixels );
    }

    private static ScratchImage AddMipMaps( ScratchImage input, bool mipMaps )
    {
        if( !mipMaps )
        {
            return input;
        }

        var numMips = Math.Min( 13, 1 + BitOperations.Log2( ( uint )Math.Max( input.Meta.Width, input.Meta.Height ) ) );
        var ec      = input.GenerateMipMaps( out var ret, numMips, ( DalamudUtil.IsLinux() ? FilterFlags.ForceNonWIC : 0 ) | FilterFlags.SeparateAlpha );
        if (ec != ErrorCode.Ok)
        {
            throw new Exception( $"Could not create the requested {numMips} mip maps, maybe retry with the top-right checkbox unchecked:\n{ec}" );
        }

        return ret;
    }

    private static ScratchImage CreateUncompressed( ScratchImage input, bool mipMaps )
    {
        if( input.Meta.Format == DXGIFormat.B8G8R8A8UNorm )
        {
            return AddMipMaps( input, mipMaps );
        }

        if( input.Meta.Format.IsCompressed() )
        {
            input = input.Decompress( DXGIFormat.B8G8R8A8UNorm );
        }
        else
        {
            input = input.Convert( DXGIFormat.B8G8R8A8UNorm );
        }

        return AddMipMaps( input, mipMaps );
    }

    private static ScratchImage CreateCompressed( ScratchImage input, bool mipMaps, bool bc7 )
    {
        var format = bc7 ? DXGIFormat.BC7UNorm : DXGIFormat.BC3UNorm;
        if( input.Meta.Format == format )
        {
            return input;
        }

        if( input.Meta.Format.IsCompressed() )
        {
            input = input.Decompress( DXGIFormat.B8G8R8A8UNorm );
        }

        input = AddMipMaps( input, mipMaps );

        return input.Compress( format, CompressFlags.BC7Quick | CompressFlags.Parallel );
    }

    public void SaveAsTex( string path, TextureSaveType type, bool mipMaps )
        => SaveAs( path, type, mipMaps, true );

    public void SaveAsDds( string path, TextureSaveType type, bool mipMaps )
        => SaveAs( path, type, mipMaps, false );


    public CombinedTexture( Texture left, Texture right )
    {
        _left         =  left;
        _right        =  right;
        _left.Loaded  += OnLoaded;
        _right.Loaded += OnLoaded;
        OnLoaded( false );
    }

    public void Dispose()
    {
        Clean();
        _left.Loaded  -= OnLoaded;
        _right.Loaded -= OnLoaded;
    }

    private void OnLoaded( bool _ )
        => Update();

    public void Update()
    {
        Clean();
        if( _left.IsLoaded )
        {
            if( _right.IsLoaded )
            {
                _current = _centerStorage;
                _mode    = Mode.Custom;
            }
            else if( !_invertLeft && _multiplierLeft.IsIdentity )
            {
                _mode    = Mode.LeftCopy;
                _current = _left;
            }
            else
            {
                _current = _centerStorage;
                _mode    = Mode.Custom;
            }
        }
        else if( _right.IsLoaded )
        {
            if( !_invertRight && _multiplierRight.IsIdentity )
            {
                _current = _right;
                _mode    = Mode.RightCopy;
            }
            else
            {
                _current = _centerStorage;
                _mode    = Mode.Custom;
            }
        }
    }

    private void Clean()
    {
        _centerStorage.Dispose();
        _current = null;
        _mode    = Mode.Empty;
    }
}
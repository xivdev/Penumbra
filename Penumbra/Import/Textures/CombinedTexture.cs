using System;
using System.Numerics;
using OtterTex;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace Penumbra.Import.Textures;

public partial class CombinedTexture : IDisposable
{
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

    public void Draw( Vector2 size )
    {
        if( _mode == Mode.Custom && !_centerStorage.IsLoaded )
        {
            var (width, height) = CombineImage();
            _centerStorage.TextureWrap =
                Dalamud.PluginInterface.UiBuilder.LoadImageRaw( _centerStorage.RGBAPixels, width, height, 4 );
        }

        _current?.Draw( size );
    }


    public void SaveAsPng( string path )
    {
        if( !IsLoaded  || _current == null )
        {
            return;
        }

        var image = Image.LoadPixelData< Rgba32 >( _current.RGBAPixels, _current.TextureWrap!.Width,
            _current.TextureWrap!.Height );
        image.Save( path, new PngEncoder() { CompressionLevel = PngCompressionLevel.NoCompression } );
    }

    public void SaveAsDDS( string path, DXGIFormat format, bool fast, float threshold = 0.5f )
    {
        if( _current == null )
            return;
        switch( _mode )
        {
            case Mode.Empty: return;
            case Mode.LeftCopy:
            case Mode.RightCopy:
                if( _centerStorage.BaseImage is ScratchImage s )
                {
                    if( format != s.Meta.Format )
                    {
                        s = s.Convert( format, threshold );
                    }

                    s.SaveDDS( path );
                }
                else
                {
                    var image = ScratchImage.FromRGBA( _current.RGBAPixels, _current.TextureWrap!.Width,
                        _current.TextureWrap!.Height, out var i ).ThrowIfError( i );
                    image.SaveDDS( path ).ThrowIfError();
                }

                break;
        }
    }

    //private void SaveAs( bool success, string path, int type )
    //{
    //    if( !success || _imageCenter == null || _wrapCenter == null )
    //    {
    //        return;
    //    }
    //
    //    try
    //    {
    //        switch( type )
    //        {
    //            case 0:
    //                var img = Image.LoadPixelData< Rgba32 >( _imageCenter, _wrapCenter.Width, _wrapCenter.Height );
    //                img.Save( path, new PngEncoder() { CompressionLevel = PngCompressionLevel.NoCompression } );
    //                break;
    //            case 1:
    //                if( TextureImporter.RgbaBytesToTex( _imageCenter, _wrapCenter.Width, _wrapCenter.Height, out var tex ) )
    //                {
    //                    File.WriteAllBytes( path, tex );
    //                }
    //
    //                break;
    //            case 2:
    //                //ScratchImage.LoadDDS( _imageCenter,  )
    //                //if( TextureImporter.RgbaBytesToDds( _imageCenter, _wrapCenter.Width, _wrapCenter.Height, out var dds ) )
    ////{
    //                //    File.WriteAllBytes( path, dds );
    ////}
    //
    //                break;
    //        }
    //    }
    //    catch( Exception e )
    //    {
    //        PluginLog.Error( $"Could not save image to {path}:\n{e}" );
    //    }

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
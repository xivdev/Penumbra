using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Utility;
using ImGuiNET;
using ImGuiScene;
using Lumina.Data.Files;
using OtterGui;
using OtterGui.Raii;
using OtterTex;
using Penumbra.GameData.ByteString;
using Penumbra.UI.Classes;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace Penumbra.Import.Textures;

public sealed class Texture : IDisposable
{
    public enum FileType
    {
        Unknown,
        Dds,
        Tex,
        Png,
        Bitmap,
    }

    // Path to the file we tried to load.
    public string Path = string.Empty;

    // If the load failed, an exception is stored.
    public Exception? LoadError = null;

    // The pixels of the main image in RGBA order.
    // Empty if LoadError != null or Path is empty.
    public byte[] RGBAPixels = Array.Empty< byte >();

    // The ImGui wrapper to load the image.
    // null if LoadError != null or Path is empty.
    public TextureWrap? TextureWrap = null;

    // The base image in whatever format it has.
    public object? BaseImage = null;

    // Original File Type.
    public FileType Type = FileType.Unknown;

    // Whether the file is successfully loaded and drawable.
    public bool IsLoaded
        => TextureWrap != null;

    public Texture()
    { }

    public void Draw( Vector2 size )
    {
        if( TextureWrap != null )
        {
            size = size.X < TextureWrap.Width
                ? size with { Y = TextureWrap.Height * size.X / TextureWrap.Width }
                : new Vector2( TextureWrap.Width, TextureWrap.Height );

            ImGui.Image( TextureWrap.ImGuiHandle, size );
            DrawData();
        }
        else if( LoadError != null )
        {
            ImGui.TextUnformatted( "Could not load file:" );
            ImGuiUtil.TextColored( Colors.RegexWarningBorder, LoadError.ToString() );
        }
    }

    public void DrawData()
    {
        using var table = ImRaii.Table( "##data", 2, ImGuiTableFlags.SizingFixedFit );
        ImGuiUtil.DrawTableColumn( "Width" );
        ImGuiUtil.DrawTableColumn( TextureWrap!.Width.ToString() );
        ImGuiUtil.DrawTableColumn( "Height" );
        ImGuiUtil.DrawTableColumn( TextureWrap!.Height.ToString() );
        ImGuiUtil.DrawTableColumn( "File Type" );
        ImGuiUtil.DrawTableColumn( Type.ToString() );
        ImGuiUtil.DrawTableColumn( "Bitmap Size" );
        ImGuiUtil.DrawTableColumn( $"{Functions.HumanReadableSize( RGBAPixels.Length )} ({RGBAPixels.Length} Bytes)" );
        switch( BaseImage )
        {
            case ScratchImage s:
                ImGuiUtil.DrawTableColumn( "Format" );
                ImGuiUtil.DrawTableColumn( s.Meta.Format.ToString() );
                ImGuiUtil.DrawTableColumn( "Mip Levels" );
                ImGuiUtil.DrawTableColumn( s.Meta.MipLevels.ToString() );
                ImGuiUtil.DrawTableColumn( "Data Size" );
                ImGuiUtil.DrawTableColumn( $"{Functions.HumanReadableSize( s.Pixels.Length )} ({s.Pixels.Length} Bytes)" );
                ImGuiUtil.DrawTableColumn( "Number of Images" );
                ImGuiUtil.DrawTableColumn( s.Images.Length.ToString() );
                break;
            case TexFile t:
                ImGuiUtil.DrawTableColumn( "Format" );
                ImGuiUtil.DrawTableColumn( t.Header.Format.ToString() );
                ImGuiUtil.DrawTableColumn( "Mip Levels" );
                ImGuiUtil.DrawTableColumn( t.Header.MipLevels.ToString()) ;
                ImGuiUtil.DrawTableColumn( "Data Size" );
                ImGuiUtil.DrawTableColumn( $"{Functions.HumanReadableSize( t.ImageData.Length )} ({t.ImageData.Length} Bytes)" );
                break;
        }
    }

    private void Clean()
    {
        RGBAPixels = Array.Empty< byte >();
        TextureWrap?.Dispose();
        TextureWrap = null;
        ( BaseImage as IDisposable )?.Dispose();
        BaseImage = null;
        Type      = FileType.Unknown;
        Loaded?.Invoke( false );
    }

    public void Dispose()
        => Clean();

    public event Action< bool >? Loaded;

    private void Load( string path )
    {
        _tmpPath = null;
        if( path == Path )
        {
            return;
        }

        Path = path;
        Clean();
        try
        {
            var _ = System.IO.Path.GetExtension( Path ) switch
            {
                ".dds" => LoadDds(),
                ".png" => LoadPng(),
                ".tex" => LoadTex(),
                _      => true,
            };
            Loaded?.Invoke( true );
        }
        catch( Exception e )
        {
            LoadError = e;
            Clean();
        }
    }

    private bool LoadDds()
    {
        Type = FileType.Dds;
        var scratch = ScratchImage.LoadDDS( Path );
        BaseImage = scratch;
        var rgba = scratch.GetRGBA( out var f ).ThrowIfError( f );
        RGBAPixels = rgba.Pixels[ ..( f.Meta.Width * f.Meta.Height * f.Meta.Format.BitsPerPixel() / 8 ) ].ToArray();
        CreateTextureWrap( f.Meta.Width, f.Meta.Height );
        return true;
    }

    private bool LoadPng()
    {
        Type      = FileType.Png;
        BaseImage = null;
        using var stream = File.OpenRead( Path );
        using var png    = Image.Load< Rgba32 >( stream );
        RGBAPixels = new byte[png.Height * png.Width * 4];
        png.CopyPixelDataTo( RGBAPixels );
        CreateTextureWrap( png.Width, png.Height );
        return true;
    }

    private bool LoadTex()
    {
        Type = FileType.Tex;
        var tex = System.IO.Path.IsPathRooted( Path )
            ? Dalamud.GameData.GameData.GetFileFromDisk< TexFile >( Path )
            : Dalamud.GameData.GetFile< TexFile >( Path );
        BaseImage  = tex ?? throw new Exception( "Could not read .tex file." );
        RGBAPixels = tex.GetRgbaImageData();
        CreateTextureWrap( tex.Header.Width, tex.Header.Height );
        return true;
    }

    private void CreateTextureWrap( int width, int height )
        => TextureWrap = Dalamud.PluginInterface.UiBuilder.LoadImageRaw( RGBAPixels, width, height, 4 );

    private string? _tmpPath;

    public void PathSelectBox( string label, string tooltip, IEnumerable<string> paths )
    {
        ImGui.SetNextItemWidth( -0.0001f );
        var startPath = Path.Length > 0 ? Path : "Choose a modded texture here...";
        using var combo = ImRaii.Combo( label, startPath );
        if( combo )
        {
            foreach( var (path, idx) in paths.WithIndex() )
            {
                using var id = ImRaii.PushId( idx );
                if( ImGui.Selectable( path, path == startPath ) && path != startPath )
                {
                    Load( path );
                }
            }
        }
        ImGuiUtil.HoverTooltip( tooltip );
    }

    public void PathInputBox( string label, string hint, string tooltip, string startPath, FileDialogManager manager )
    {
        _tmpPath ??= Path;
        using var spacing = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing, new Vector2( 3 * ImGuiHelpers.GlobalScale, ImGui.GetStyle().ItemSpacing.Y ) );
        ImGui.SetNextItemWidth( -ImGui.GetFrameHeight() - 3 * ImGuiHelpers.GlobalScale );
        ImGui.InputTextWithHint( label, hint, ref _tmpPath, Utf8GamePath.MaxGamePathLength );
        if( ImGui.IsItemDeactivatedAfterEdit() )
        {
            Load( _tmpPath );
        }

        ImGuiUtil.HoverTooltip( tooltip );
        ImGui.SameLine();
        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Folder.ToIconString(), new Vector2( ImGui.GetFrameHeight() ), string.Empty, false,
               true ) )
        {
            if( Penumbra.Config.DefaultModImportPath.Length > 0 )
            {
                startPath = Penumbra.Config.DefaultModImportPath;
            }

            var texture = this;

            void UpdatePath( bool success, List< string > paths )
            {
                if( success && paths.Count > 0 )
                {
                    texture.Load( paths[ 0 ] );
                }
            }

            manager.OpenFileDialog( "Open Image...", "Textures{.png,.dds,.tex}", UpdatePath, 1, startPath );
        }
    }
}

public static class ScratchImageExtensions
{
    public static Exception? SaveAsTex( this ScratchImage image, string path )
    {
        try
        {
            using var fileStream = File.OpenWrite( path );
            using var bw         = new BinaryWriter( fileStream );

            bw.Write( (uint) image.Meta.GetAttribute()  );
            bw.Write( (uint) image.Meta.GetFormat()  );
            bw.Write( (ushort) image.Meta.Width  );
            bw.Write( (ushort) image.Meta.Height  );
            bw.Write( (ushort) image.Meta.Depth  );
            bw.Write( (ushort) image.Meta.MipLevels  );
        }
        catch( Exception e )
        {
            return e;
        }

        return null;
    }

    public static unsafe TexFile.TexHeader ToTexHeader( this ScratchImage image )
    {
        var ret = new TexFile.TexHeader()
        {
            Type   = image.Meta.GetAttribute(),
            Format = image.Meta.GetFormat(),
            Width  = ( ushort )image.Meta.Width,
            Height = ( ushort )image.Meta.Height,
            Depth  = ( ushort )image.Meta.Depth,
        };
        ret.LodOffset[ 0 ]       = 0;
        ret.LodOffset[ 1 ]       = 1;
        ret.LodOffset[ 2 ]       = 2;
        //foreach(var surface in image.Images)
        //    ret.OffsetToSurface[ 0 ] = 80 + (image.P);
        return ret;
    }

    // Get all known flags for the TexFile.Attribute from the scratch image.
    private static TexFile.Attribute GetAttribute( this TexMeta meta )
    {
        var ret = meta.Dimension switch
        {
            TexDimension.Tex1D => TexFile.Attribute.TextureType1D,
            TexDimension.Tex2D => TexFile.Attribute.TextureType2D,
            TexDimension.Tex3D => TexFile.Attribute.TextureType3D,
            _                  => (TexFile.Attribute) 0,
        };
        if( meta.IsCubeMap )
            ret |= TexFile.Attribute.TextureTypeCube;
        if( meta.Format.IsDepthStencil() )
            ret |= TexFile.Attribute.TextureDepthStencil;
        return ret;
    }

    private static TexFile.TextureFormat GetFormat( this TexMeta meta )
    {
        return meta.Format switch
        {
            _ => TexFile.TextureFormat.Unknown,
        };
    }
}
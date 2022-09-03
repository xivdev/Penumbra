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

public class Texture : IDisposable
{
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

    // Whether the file is successfully loaded and drawable.
    public bool IsLoaded
        => TextureWrap != null;

    public Texture()
    { }

    public void Draw( Vector2 size )
    {
        if( TextureWrap != null )
        {
            ImGui.TextUnformatted( $"Image Dimensions: {TextureWrap.Width} x {TextureWrap.Height}" );
            size = size.X < TextureWrap.Width
                ? size with { Y = TextureWrap.Height * size.X / TextureWrap.Width }
                : new Vector2( TextureWrap.Width, TextureWrap.Height );

            ImGui.Image( TextureWrap.ImGuiHandle, size );
        }
        else if( LoadError != null )
        {
            ImGui.TextUnformatted( "Could not load file:" );
            ImGuiUtil.TextColored( Colors.RegexWarningBorder, LoadError.ToString() );
        }
        else
        {
            ImGui.Dummy( size );
        }
    }

    private void Clean()
    {
        RGBAPixels = Array.Empty< byte >();
        TextureWrap?.Dispose();
        TextureWrap = null;
        ( BaseImage as IDisposable )?.Dispose();
        BaseImage = null;
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
        var scratch = ScratchImage.LoadDDS( Path );
        BaseImage = scratch;
        var rgba = scratch.GetRGBA( out var f ).ThrowIfError( f );
        RGBAPixels = rgba.Pixels[ ..( f.Meta.Width * f.Meta.Height * f.Meta.Format.BitsPerPixel() / 8 ) ].ToArray();
        CreateTextureWrap( f.Meta.Width, f.Meta.Height );
        return true;
    }

    private bool LoadPng()
    {
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

    public void PathInputBox( string label, string hint, string tooltip, string startPath, FileDialogManager manager )
    {
        _tmpPath ??= Path;
        using var spacing = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing, new Vector2( 3 * ImGuiHelpers.GlobalScale, 0 ) );
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
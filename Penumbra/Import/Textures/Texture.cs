using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using ImGuiScene;
using Lumina.Data.Files;
using OtterGui;
using OtterGui.Raii;
using OtterTex;
using Penumbra.Services;
using Penumbra.String.Classes;
using Penumbra.UI;
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
                ImGuiUtil.DrawTableColumn( t.Header.MipLevels.ToString() );
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
        if( path.Length == 0 )
        {
            return;
        }

        try
        {
            var _ = System.IO.Path.GetExtension( Path ).ToLowerInvariant() switch
            {
                ".dds" => LoadDds(),
                ".png" => LoadPng(),
                ".tex" => LoadTex(),
                _      => throw new Exception( $"Extension {System.IO.Path.GetExtension( Path )} unknown." ),
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
        using var stream  = OpenTexStream();
        var       scratch = TexFileParser.Parse( stream );
        BaseImage = scratch;
        var rgba = scratch.GetRGBA( out var f ).ThrowIfError( f );
        RGBAPixels = rgba.Pixels[ ..( f.Meta.Width * f.Meta.Height * f.Meta.Format.BitsPerPixel() / 8 ) ].ToArray();
        CreateTextureWrap( scratch.Meta.Width, scratch.Meta.Height );
        return true;
    }

    private Stream OpenTexStream()
    {
        if( System.IO.Path.IsPathRooted( Path ) )
        {
            return File.OpenRead( Path );
        }

        var file = DalamudServices.SGameData.GetFile( Path );
        return file != null ? new MemoryStream( file.Data ) : throw new Exception( $"Unable to obtain \"{Path}\" from game files." );
    }

    private void CreateTextureWrap( int width, int height )
        => TextureWrap = DalamudServices.PluginInterface.UiBuilder.LoadImageRaw( RGBAPixels, width, height, 4 );

    private string? _tmpPath;

    public void PathSelectBox( string label, string tooltip, IEnumerable< (string, bool) > paths, int skipPrefix )
    {
        ImGui.SetNextItemWidth( -0.0001f );
        var       startPath = Path.Length > 0 ? Path : "Choose a modded texture from this mod here...";
        using var combo     = ImRaii.Combo( label, startPath );
        if( combo )
        {
            foreach( var ((path, game), idx) in paths.WithIndex() )
            {
                if( game )
                {
                    if( !DalamudServices.SGameData.FileExists( path ) )
                    {
                        continue;
                    }
                }
                else if( !File.Exists( path ) )
                {
                    continue;
                }

                using var id = ImRaii.PushId( idx );
                using( var color = ImRaii.PushColor( ImGuiCol.Text, ColorId.FolderExpanded.Value(Penumbra.Config), game ) )
                {
                    var p = game ? $"--> {path}" : path[ skipPrefix.. ];
                    if( ImGui.Selectable( p, path == startPath ) && path != startPath )
                    {
                        Load( path );
                    }
                }

                ImGuiUtil.HoverTooltip( game
                    ? "This is a game path and refers to an unmanipulated file from your game data."
                    : "This is a path to a modded file on your file system." );
            }
        }

        ImGuiUtil.HoverTooltip( tooltip );
    }

    public void PathInputBox( string label, string hint, string tooltip, string startPath, FileDialogService fileDialog )
    {
        _tmpPath ??= Path;
        using var spacing = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing,
            new Vector2( UiHelpers.ScaleX3, ImGui.GetStyle().ItemSpacing.Y ) );
        ImGui.SetNextItemWidth( -2 * ImGui.GetFrameHeight() - 7 * UiHelpers.Scale );
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

            fileDialog.OpenFilePicker( "Open Image...", "Textures{.png,.dds,.tex}", UpdatePath, 1, startPath, false );
        }

        ImGui.SameLine();
        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Recycle.ToIconString(), new Vector2( ImGui.GetFrameHeight() ),
               "Reload the currently selected path.", false,
               true ) )
        {
            var path = Path;
            Path = string.Empty;
            Load( path );
        }
    }
}
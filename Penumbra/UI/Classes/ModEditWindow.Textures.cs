using System;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Interface;
using Dalamud.Logging;
using Dalamud.Utility;
using ImGuiNET;
using ImGuiScene;
using Lumina.Data;
using Lumina.Data.Files;
using OtterGui;
using OtterGui.Raii;
using Penumbra.GameData.ByteString;
using Penumbra.Import.Textures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Penumbra.UI.Classes;

public partial class ModEditWindow
{
    private string _pathLeft  = string.Empty;
    private string _pathRight = string.Empty;
    private string _pathSave  = string.Empty;

    private byte[]? _imageLeft;
    private byte[]? _imageRight;
    private byte[]? _imageCenter;

    private TextureWrap? _wrapLeft;
    private TextureWrap? _wrapRight;
    private TextureWrap? _wrapCenter;

    private Matrix4x4 _multiplierLeft  = Matrix4x4.Identity;
    private Matrix4x4 _multiplierRight = Matrix4x4.Identity;

    private bool DrawMatrixInput( float width, ref Matrix4x4 matrix )
    {
        using var table = ImRaii.Table( string.Empty, 5, ImGuiTableFlags.BordersInner | ImGuiTableFlags.SizingFixedFit );
        if( !table )
        {
            return false;
        }

        var changes = false;

        ImGui.TableNextColumn();
        ImGui.TableNextColumn();
        ImGuiUtil.Center( "R" );
        ImGui.TableNextColumn();
        ImGuiUtil.Center( "G" );
        ImGui.TableNextColumn();
        ImGuiUtil.Center( "B" );
        ImGui.TableNextColumn();
        ImGuiUtil.Center( "A" );

        var inputWidth = width / 6;
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text( "R    " );
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( inputWidth );
        changes |= ImGui.DragFloat( "##RR", ref matrix.M11, 0.001f, -1f, 1f );
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( inputWidth );
        changes |= ImGui.DragFloat( "##RG", ref matrix.M12, 0.001f, -1f, 1f );
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( inputWidth );
        changes |= ImGui.DragFloat( "##RB", ref matrix.M13, 0.001f, -1f, 1f );
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( inputWidth );
        changes |= ImGui.DragFloat( "##RA", ref matrix.M14, 0.001f, -1f, 1f );

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text( "G    " );
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( inputWidth );
        changes |= ImGui.DragFloat( "##GR", ref matrix.M21, 0.001f, -1f, 1f );
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( inputWidth );
        changes |= ImGui.DragFloat( "##GG", ref matrix.M22, 0.001f, -1f, 1f );
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( inputWidth );
        changes |= ImGui.DragFloat( "##GB", ref matrix.M23, 0.001f, -1f, 1f );
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( inputWidth );
        changes |= ImGui.DragFloat( "##GA", ref matrix.M24, 0.001f, -1f, 1f );

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text( "B    " );
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( inputWidth );
        changes |= ImGui.DragFloat( "##BR", ref matrix.M31, 0.001f, -1f, 1f );
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( inputWidth );
        changes |= ImGui.DragFloat( "##BG", ref matrix.M32, 0.001f, -1f, 1f );
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( inputWidth );
        changes |= ImGui.DragFloat( "##BB", ref matrix.M33, 0.001f, -1f, 1f );
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( inputWidth );
        changes |= ImGui.DragFloat( "##BA", ref matrix.M34, 0.001f, -1f, 1f );

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text( "A    " );
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( inputWidth );
        changes |= ImGui.DragFloat( "##AR", ref matrix.M41, 0.001f, -1f, 1f );
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( inputWidth );
        changes |= ImGui.DragFloat( "##AG", ref matrix.M42, 0.001f, -1f, 1f );
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( inputWidth );
        changes |= ImGui.DragFloat( "##AB", ref matrix.M43, 0.001f, -1f, 1f );
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( inputWidth );
        changes |= ImGui.DragFloat( "##AA", ref matrix.M44, 0.001f, -1f, 1f );

        return changes;
    }

    private bool PathInputBox( string label, string hint, string tooltip, ref string path )
    {
        var       tmp     = path;
        using var spacing = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing, new Vector2( 3 * ImGuiHelpers.GlobalScale, 0 ) );
        ImGui.SetNextItemWidth( -ImGui.GetFrameHeight() - 3 * ImGuiHelpers.GlobalScale );
        ImGui.InputTextWithHint( label, hint, ref tmp, Utf8GamePath.MaxGamePathLength );
        var ret = ImGui.IsItemDeactivatedAfterEdit() && tmp != path;
        ImGuiUtil.HoverTooltip( tooltip );
        ImGui.SameLine();
        ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Folder.ToIconString(), new Vector2( ImGui.GetFrameHeight() ), string.Empty, false, true );
        if( ret )
        {
            path = tmp;
        }

        return ret;
    }

    private static (byte[]?, int, int) GetDdsRgbaData( string path )
    {
        try
        {
            using var stream = File.OpenRead( path );
            if( !DdsFile.Load( stream, out var f ) )
            {
                return ( null, 0, 0 );
            }

            f.ConvertToTex( out var bytes );
            using var ms = new MemoryStream( bytes );
            using var sq = new SqPackStream( ms );
            var       x  = sq.ReadFile< TexFile >( 0 );
            return ( x.GetRgbaImageData(), x.Header.Width, x.Header.Height );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not parse DDS {path} to RGBA:\n{e}" );
            return ( null, 0, 0 );
        }
    }

    private static ( byte[]?, int, int) GetTexRgbaData( string path, bool fromDisk )
    {
        try
        {
            var tex = fromDisk ? Dalamud.GameData.GameData.GetFileFromDisk< TexFile >( path ) : Dalamud.GameData.GetFile< TexFile >( path );
            return tex == null
                ? ( null, 0, 0 )
                : ( tex.GetRgbaImageData(), tex.Header.Width, tex.Header.Height );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not parse TEX {path} to RGBA:\n{e}" );
            return ( null, 0, 0 );
        }
    }

    private static (byte[]?, int, int) GetPngRgbaData( string path )
    {
        try
        {
            using var stream = File.OpenRead( path );
            var       png    = Image.Load< Rgba32 >( stream );
            var       bytes  = new byte[png.Height * png.Width * 4];
            png.CopyPixelDataTo( bytes );
            return ( bytes, png.Width, png.Height );
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not parse PNG {path} to RGBA:\n{e}" );
            return ( null, 0, 0 );
        }
    }

    private void UpdateImage( string path, ref byte[]? data, ref TextureWrap? wrap )
    {
        data = null;
        wrap?.Dispose();
        wrap = null;
        var width  = 0;
        var height = 0;

        if( Path.IsPathRooted( path ) )
        {
            if( File.Exists( path ) )
            {
                ( data, width, height ) = Path.GetExtension( path ) switch
                {
                    ".dds" => GetDdsRgbaData( path ),
                    ".png" => GetPngRgbaData( path ),
                    ".tex" => GetTexRgbaData( path, true ),
                    _      => ( null, 0, 0 ),
                };
            }
        }
        else
        {
            ( data, width, height ) = GetTexRgbaData( path, false );
        }

        if( data != null )
        {
            wrap = Dalamud.PluginInterface.UiBuilder.LoadImageRaw( data, width, height, 4 );
        }

        UpdateCenter();
    }

    private void AddPixels( int width, int x, int y )
    {
        var offset = ( y * width + x ) * 4;
        var rgbaLeft = _imageLeft != null
            ? new Rgba32( _imageLeft[ offset ], _imageLeft[ offset + 1 ], _imageLeft[ offset + 2 ], _imageLeft[ offset + 3 ] )
            : new Rgba32();
        var rgbaRight = _imageRight != null
            ? new Rgba32( _imageRight[ offset ], _imageRight[ offset + 1 ], _imageRight[ offset + 2 ], _imageRight[ offset + 3 ] )
            : new Rgba32();
        var transformLeft  = Vector4.Transform( rgbaLeft.ToVector4(), _multiplierLeft );
        var transformRight = Vector4.Transform( rgbaRight.ToVector4(), _multiplierRight );
        var alpha          = transformLeft.Z + transformRight.Z * ( 1 - transformLeft.Z );
        var rgba = alpha == 0
            ? new Rgba32()
            : new Rgba32( ( transformLeft * transformLeft.Z + transformRight * transformRight.Z * ( 1 - transformLeft.Z ) ) / alpha );
        _imageCenter![ offset ]     = rgba.R;
        _imageCenter![ offset + 1 ] = rgba.G;
        _imageCenter![ offset + 2 ] = rgba.B;
        _imageCenter![ offset + 3 ] = rgba.A;
    }

    private void UpdateCenter()
    {
        _wrapCenter?.Dispose();
        if( _imageLeft != null || _imageRight != null )
        {
            var (width, height) = _imageLeft != null ? ( _wrapLeft!.Width, _wrapLeft.Height ) : ( _wrapRight!.Width, _wrapRight.Height );
            if( _imageRight == null || _wrapRight!.Width == width && _wrapRight!.Height == height )
            {
                _imageCenter = new byte[4 * width * height];

                for( var y = 0; y < height; ++y )
                {
                    for( var x = 0; x < width; ++x )
                    {
                        AddPixels( width, x, y );
                    }
                }

                _wrapCenter = Dalamud.PluginInterface.UiBuilder.LoadImageRaw( _imageCenter, width, height, 4 );
                return;
            }
        }

        _imageCenter = null;
        _wrapCenter  = null;
    }

    private static void ScaledImage( TextureWrap? wrap, Vector2 size )
    {
        if( wrap != null )
        {
            size = size with { Y = wrap.Height * size.X / wrap.Width };
            ImGui.Image( wrap.ImGuiHandle, size );
        }
        else
        {
            ImGui.Dummy( size );
        }
    }

    private void DrawTextureTab()
    {
        using var tab = ImRaii.TabItem( "Texture Import/Export" );
        if( !tab )
        {
            return;
        }

        var leftRightWidth = new Vector2( ( ImGui.GetWindowContentRegionWidth() - ImGui.GetStyle().FramePadding.X * 4 ) / 3, -1 );
        var imageSize      = new Vector2( leftRightWidth.X - ImGui.GetStyle().FramePadding.X * 2 );
        using( var child = ImRaii.Child( "ImageLeft", leftRightWidth, true ) )
        {
            if( PathInputBox( "##ImageLeft", "Import Image...", string.Empty, ref _pathLeft ) )
            {
                UpdateImage( _pathLeft, ref _imageLeft, ref _wrapLeft );
            }

            ImGui.NewLine();
            if( DrawMatrixInput( leftRightWidth.X, ref _multiplierLeft ) )
            {
                UpdateCenter();
            }

            ImGui.NewLine();
            ScaledImage( _wrapLeft, imageSize );

        }

        ImGui.SameLine();
        using( var child = ImRaii.Child( "ImageMix", leftRightWidth, true ) )
        {
            ScaledImage( _wrapCenter, imageSize );
        }

        ImGui.SameLine();
        using( var child = ImRaii.Child( "ImageRight", leftRightWidth, true ) )
        {
            if( PathInputBox( "##ImageRight", "Import Image...", string.Empty, ref _pathRight ) )
            {
                UpdateImage( _pathRight, ref _imageRight, ref _wrapRight );
            }

            ImGui.NewLine();
            if( DrawMatrixInput( leftRightWidth.X, ref _multiplierRight ) )
            {
                UpdateCenter();
            }

            ImGui.NewLine();
            ScaledImage( _wrapRight, imageSize );
        }
    }
}
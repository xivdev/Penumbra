using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Logging;
using Dalamud.Utility;
using ImGuiNET;
using ImGuiScene;
using Lumina.Data.Files;
using OtterGui;
using OtterGui.Raii;
using Penumbra.GameData.ByteString;
using Penumbra.Import.Dds;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Penumbra.UI.Classes;

public partial class ModEditWindow
{
    private string _pathLeft  = string.Empty;
    private string _pathRight = string.Empty;

    private byte[]? _imageLeft;
    private byte[]? _imageRight;
    private byte[]? _imageCenter;

    private TextureWrap? _wrapLeft;
    private TextureWrap? _wrapRight;
    private TextureWrap? _wrapCenter;

    private Matrix4x4 _multiplierLeft  = Matrix4x4.Identity;
    private Matrix4x4 _multiplierRight = Matrix4x4.Identity;
    private bool      _invertLeft      = false;
    private bool      _invertRight     = false;
    private int       _offsetX         = 0;
    private int       _offsetY         = 0;

    private readonly FileDialogManager _dialogManager = ConfigWindow.SetupFileManager();

    private static bool DragFloat( string label, float width, ref float value )
    {
        var tmp = value;
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth( width );
        if( ImGui.DragFloat( label, ref tmp, 0.001f, -1f, 1f ) )
        {
            value = tmp;
        }

        return ImGui.IsItemDeactivatedAfterEdit();
    }

    private static bool DrawMatrixInput( float width, ref Matrix4x4 matrix )
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
        changes |= DragFloat( "##RR", inputWidth, ref matrix.M11 );
        changes |= DragFloat( "##RG", inputWidth, ref matrix.M12 );
        changes |= DragFloat( "##RB", inputWidth, ref matrix.M13 );
        changes |= DragFloat( "##RA", inputWidth, ref matrix.M14 );

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text( "G    " );
        changes |= DragFloat( "##GR", inputWidth, ref matrix.M21 );
        changes |= DragFloat( "##GG", inputWidth, ref matrix.M22 );
        changes |= DragFloat( "##GB", inputWidth, ref matrix.M23 );
        changes |= DragFloat( "##GA", inputWidth, ref matrix.M24 );

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text( "B    " );
        changes |= DragFloat( "##BR", inputWidth, ref matrix.M31 );
        changes |= DragFloat( "##BG", inputWidth, ref matrix.M32 );
        changes |= DragFloat( "##BB", inputWidth, ref matrix.M33 );
        changes |= DragFloat( "##BA", inputWidth, ref matrix.M34 );

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text( "A    " );
        changes |= DragFloat( "##AR", inputWidth, ref matrix.M41 );
        changes |= DragFloat( "##AG", inputWidth, ref matrix.M42 );
        changes |= DragFloat( "##AB", inputWidth, ref matrix.M43 );
        changes |= DragFloat( "##AA", inputWidth, ref matrix.M44 );

        return changes;
    }

    private void PathInputBox( string label, string hint, string tooltip, int which )
    {
        var       tmp     = which == 0 ? _pathLeft : _pathRight;
        using var spacing = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing, new Vector2( 3 * ImGuiHelpers.GlobalScale, 0 ) );
        ImGui.SetNextItemWidth( -ImGui.GetFrameHeight() - 3 * ImGuiHelpers.GlobalScale );
        ImGui.InputTextWithHint( label, hint, ref tmp, Utf8GamePath.MaxGamePathLength );
        if( ImGui.IsItemDeactivatedAfterEdit() )
        {
            UpdateImage( tmp, which );
        }

        ImGuiUtil.HoverTooltip( tooltip );
        ImGui.SameLine();
        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Folder.ToIconString(), new Vector2( ImGui.GetFrameHeight() ), string.Empty, false,
               true ) )
        {
            var startPath = Penumbra.Config.DefaultModImportPath.Length > 0 ? Penumbra.Config.DefaultModImportPath : _mod?.ModPath.FullName;

            void UpdatePath( bool success, List< string > paths )
            {
                if( success && paths.Count > 0 )
                {
                    UpdateImage( paths[ 0 ], which );
                }
            }

            _dialogManager.OpenFileDialog( "Open Image...", "Textures{.png,.dds,.tex}", UpdatePath, 1, startPath );
        }
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

            return ( f.RgbaData.ToArray(), f.Header.Width, f.Header.Height );
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
            if( fromDisk )
            {
                var       tmp    = new TmpTexFile();
                using var stream = File.OpenRead( path );
                using var br     = new BinaryReader( stream );
                tmp.Load(br);
                return (tmp.RgbaData, tmp.Header.Width, tmp.Header.Height);
            }


            var tex = fromDisk ? Dalamud.GameData.GameData.GetFileFromDisk< TexFile >( path ) : Dalamud.GameData.GetFile< TexFile >( path );
            if( tex == null )
            {
                return ( null, 0, 0 );
            }

            var rgba = tex.Header.Format == TexFile.TextureFormat.B8G8R8A8
                ? ImageParsing.DecodeUncompressedR8G8B8A8( tex.ImageData, tex.Header.Height, tex.Header.Width )
                : tex.GetRgbaImageData();
            return ( rgba, tex.Header.Width, tex.Header.Height );
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
            using var png    = Image.Load< Rgba32 >( stream );
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

    private void UpdateImage( string newPath, int which )
    {
        if( which is < 0 or > 1 )
        {
            return;
        }

        ref var path = ref which == 0 ? ref _pathLeft : ref _pathRight;
        if( path == newPath )
        {
            return;
        }

        path = newPath;
        ref var data = ref which == 0 ? ref _imageLeft : ref _imageRight;
        ref var wrap = ref which == 0 ? ref _wrapLeft : ref _wrapRight;

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

    private static Vector4 CappedVector( IReadOnlyList< byte >? bytes, int offset, Matrix4x4 transform, bool invert )
    {
        if( bytes == null )
        {
            return Vector4.Zero;
        }

        var rgba        = new Rgba32( bytes[ offset ], bytes[ offset + 1 ], bytes[ offset + 2 ], bytes[ offset + 3 ] );
        var transformed = Vector4.Transform( rgba.ToVector4(), transform );
        if( invert )
        {
            transformed = new Vector4( 1 - transformed.X, 1 - transformed.Y, 1 - transformed.Z, transformed.W );
        }

        transformed.X = Math.Clamp( transformed.X, 0, 1 );
        transformed.Y = Math.Clamp( transformed.Y, 0, 1 );
        transformed.Z = Math.Clamp( transformed.Z, 0, 1 );
        transformed.W = Math.Clamp( transformed.W, 0, 1 );
        return transformed;
    }

    private Vector4 DataLeft( int offset )
        => CappedVector( _imageLeft, offset, _multiplierLeft, _invertLeft );

    private Vector4 DataRight( int x, int y )
    {
        if( _imageRight == null )
        {
            return Vector4.Zero;
        }

        x -= _offsetX;
        y -= _offsetY;
        if( x < 0 || x >= _wrapRight!.Width || y < 0 || y >= _wrapRight!.Height )
        {
            return Vector4.Zero;
        }

        var offset = ( y * _wrapRight!.Width + x ) * 4;
        return CappedVector( _imageRight, offset, _multiplierRight, _invertRight );
    }

    private void AddPixels( int width, int x, int y )
    {
        var offset = ( width * y + x ) * 4;
        var left   = DataLeft( offset );
        var right  = DataRight( x, y );
        var alpha  = right.W + left.W * ( 1 - right.W );
        if( alpha == 0 )
        {
            return;
        }

        var sum  = ( right * right.W + left * left.W * ( 1 - right.W ) ) / alpha;
        var rgba = new Rgba32( sum with { W = alpha } );
        _imageCenter![ offset ]     = rgba.R;
        _imageCenter![ offset + 1 ] = rgba.G;
        _imageCenter![ offset + 2 ] = rgba.B;
        _imageCenter![ offset + 3 ] = rgba.A;
    }

    private void UpdateCenter()
    {
        if( _imageLeft != null && _imageRight == null && _multiplierLeft.IsIdentity && !_invertLeft )
        {
            _imageCenter = _imageLeft;
            _wrapCenter  = _wrapLeft;
            return;
        }

        if( _imageLeft == null && _imageRight != null && _multiplierRight.IsIdentity && !_invertRight )
        {
            _imageCenter = _imageRight;
            _wrapCenter  = _wrapRight;
            return;
        }

        if( !ReferenceEquals( _imageCenter, _imageLeft ) && !ReferenceEquals( _imageCenter, _imageRight ) )
        {
            _wrapCenter?.Dispose();
        }

        if( _imageLeft != null || _imageRight != null )
        {
            var (totalWidth, totalHeight) =
                _imageLeft != null ? ( _wrapLeft!.Width, _wrapLeft.Height ) : ( _wrapRight!.Width, _wrapRight.Height );
            _imageCenter = new byte[4 * totalWidth * totalHeight];

            Parallel.For( 0, totalHeight - 1, ( y, _ ) =>
            {
                for( var x = 0; x < totalWidth; ++x )
                {
                    AddPixels( totalWidth, x, y );
                }
            } );
            _wrapCenter = Dalamud.PluginInterface.UiBuilder.LoadImageRaw( _imageCenter, totalWidth, totalHeight, 4 );
            return;
        }

        _imageCenter = null;
        _wrapCenter  = null;
    }

    private static void ScaledImage( string path, TextureWrap? wrap, Vector2 size )
    {
        if( wrap != null )
        {
            ImGui.TextUnformatted( $"Image Dimensions: {wrap.Width} x {wrap.Height}" );
            size = size with { Y = wrap.Height * size.X / wrap.Width };
            ImGui.Image( wrap.ImGuiHandle, size );
        }
        else if( path.Length > 0 )
        {
            ImGui.TextUnformatted( "Could not load file." );
        }
        else
        {
            ImGui.Dummy( size );
        }
    }

    private void SaveAs( bool success, string path, int type )
    {
        if( !success || _imageCenter == null || _wrapCenter == null )
        {
            return;
        }

        try
        {
            switch( type )
            {
                case 0:
                    var img = Image.LoadPixelData< Rgba32 >( _imageCenter, _wrapCenter.Width, _wrapCenter.Height );
                    img.Save( path, new PngEncoder() { CompressionLevel = PngCompressionLevel.NoCompression } );
                    break;
                case 1:
                    if( TextureImporter.RgbaBytesToTex( _imageCenter, _wrapCenter.Width, _wrapCenter.Height, out var tex ) )
                    {
                        File.WriteAllBytes( path, tex );
                    }

                    break;
                case 2:
                    if( TextureImporter.RgbaBytesToDds( _imageCenter, _wrapCenter.Width, _wrapCenter.Height, out var dds ) )
                    {
                        File.WriteAllBytes( path, dds );
                    }

                    break;
            }
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not save image to {path}:\n{e}" );
        }
    }

    private void SaveAsPng( bool success, string path )
        => SaveAs( success, path, 0 );

    private void SaveAsTex( bool success, string path )
        => SaveAs( success, path, 1 );

    private void SaveAsDds( bool success, string path )
        => SaveAs( success, path, 2 );

    private void DrawTextureTab()
    {
        _dialogManager.Draw();

        using var tab = ImRaii.TabItem( "Texture Import/Export (WIP)" );
        if( !tab )
        {
            return;
        }

        var leftRightWidth = new Vector2( ( ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X - ImGui.GetStyle().FramePadding.X * 4 ) / 3, -1 );
        var imageSize      = new Vector2( leftRightWidth.X - ImGui.GetStyle().FramePadding.X * 2 );
        using( var child = ImRaii.Child( "ImageLeft", leftRightWidth, true ) )
        {
            if( child )
            {
                PathInputBox( "##ImageLeft", "Import Image...", string.Empty, 0 );

                ImGui.NewLine();
                if( DrawMatrixInput( leftRightWidth.X, ref _multiplierLeft ) || ImGui.Checkbox( "Invert##Left", ref _invertLeft ) )
                {
                    UpdateCenter();
                }

                ImGui.NewLine();
                ScaledImage( _pathLeft, _wrapLeft, imageSize );
            }
        }

        ImGui.SameLine();
        using( var child = ImRaii.Child( "ImageMix", leftRightWidth, true ) )
        {
            if( child )
            {
                if( _wrapCenter == null && _wrapLeft != null && _wrapRight != null )
                {
                    ImGui.TextUnformatted( "Images have incompatible resolutions." );
                }
                else if( _wrapCenter != null )
                {
                    if( ImGui.Button( "Save as TEX", -Vector2.UnitX ) )
                    {
                        var fileName = Path.GetFileNameWithoutExtension( _pathLeft.Length > 0 ? _pathLeft : _pathRight );
                        _dialogManager.SaveFileDialog( "Save Texture as TEX...", ".tex", fileName, ".tex", SaveAsTex, _mod!.ModPath.FullName );
                    }

                    if( ImGui.Button( "Save as PNG", -Vector2.UnitX ) )
                    {
                        var fileName = Path.GetFileNameWithoutExtension( _pathRight.Length > 0 ? _pathRight : _pathLeft );
                        _dialogManager.SaveFileDialog( "Save Texture as PNG...", ".png", fileName, ".png", SaveAsPng, _mod!.ModPath.FullName );
                    }

                    if( ImGui.Button( "Save as DDS", -Vector2.UnitX ) )
                    {
                        var fileName = Path.GetFileNameWithoutExtension( _pathRight.Length > 0 ? _pathRight : _pathLeft );
                        _dialogManager.SaveFileDialog( "Save Texture as DDS...", ".dds", fileName, ".dds", SaveAsDds, _mod!.ModPath.FullName );
                    }

                    ImGui.NewLine();
                    ScaledImage( string.Empty, _wrapCenter, imageSize );
                }
            }
        }

        ImGui.SameLine();
        using( var child = ImRaii.Child( "ImageRight", leftRightWidth, true ) )
        {
            if( child )
            {
                PathInputBox( "##ImageRight", "Import Image...", string.Empty, 1 );

                ImGui.NewLine();
                if( DrawMatrixInput( leftRightWidth.X, ref _multiplierRight ) || ImGui.Checkbox( "Invert##Right", ref _invertRight ) )
                {
                    UpdateCenter();
                }

                ImGui.NewLine();
                ScaledImage( _pathRight, _wrapRight, imageSize );
            }
        }
    }
}
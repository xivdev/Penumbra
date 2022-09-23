using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using SixLabors.ImageSharp.PixelFormats;

namespace Penumbra.Import.Textures;

public partial class CombinedTexture
{
    private Matrix4x4 _multiplierLeft  = Matrix4x4.Identity;
    private Matrix4x4 _multiplierRight = Matrix4x4.Identity;
    private bool      _invertLeft      = false;
    private bool      _invertRight     = false;
    private int       _offsetX         = 0;
    private int       _offsetY         = 0;


    private Vector4 DataLeft( int offset )
        => CappedVector( _left.RGBAPixels, offset, _multiplierLeft, _invertLeft );

    private Vector4 DataRight( int offset )
        => CappedVector( _right.RGBAPixels, offset, _multiplierRight, _invertRight );

    private Vector4 DataRight( int x, int y )
    {
        x += _offsetX;
        y += _offsetY;
        if( x < 0 || x >= _right.TextureWrap!.Width || y < 0 || y >= _right.TextureWrap!.Height )
        {
            return Vector4.Zero;
        }

        var offset = ( y * _right.TextureWrap!.Width + x ) * 4;
        return CappedVector( _right.RGBAPixels, offset, _multiplierRight, _invertRight );
    }

    private void AddPixelsMultiplied( int y, ParallelLoopState _ )
    {
        for( var x = 0; x < _left.TextureWrap!.Width; ++x )
        {
            var offset = ( _left.TextureWrap!.Width * y + x ) * 4;
            var left   = DataLeft( offset );
            var right  = DataRight( x, y );
            var alpha  = right.W + left.W * ( 1 - right.W );
            var rgba = alpha == 0
                ? new Rgba32()
                : new Rgba32( ( ( right * right.W + left * left.W * ( 1 - right.W ) ) / alpha ) with { W = alpha } );
            _centerStorage.RGBAPixels[ offset ]     = rgba.R;
            _centerStorage.RGBAPixels[ offset + 1 ] = rgba.G;
            _centerStorage.RGBAPixels[ offset + 2 ] = rgba.B;
            _centerStorage.RGBAPixels[ offset + 3 ] = rgba.A;
        }
    }

    private void MultiplyPixelsLeft( int y, ParallelLoopState _ )
    {
        for( var x = 0; x < _left.TextureWrap!.Width; ++x )
        {
            var offset = ( _left.TextureWrap!.Width * y + x ) * 4;
            var left   = DataLeft( offset );
            var rgba   = new Rgba32( left );
            _centerStorage.RGBAPixels[ offset ]     = rgba.R;
            _centerStorage.RGBAPixels[ offset + 1 ] = rgba.G;
            _centerStorage.RGBAPixels[ offset + 2 ] = rgba.B;
            _centerStorage.RGBAPixels[ offset + 3 ] = rgba.A;
        }
    }

    private void MultiplyPixelsRight( int y, ParallelLoopState _ )
    {
        for( var x = 0; x < _right.TextureWrap!.Width; ++x )
        {
            var offset = ( _right.TextureWrap!.Width * y + x ) * 4;
            var left   = DataRight( offset );
            var rgba   = new Rgba32( left );
            _centerStorage.RGBAPixels[ offset ]     = rgba.R;
            _centerStorage.RGBAPixels[ offset + 1 ] = rgba.G;
            _centerStorage.RGBAPixels[ offset + 2 ] = rgba.B;
            _centerStorage.RGBAPixels[ offset + 3 ] = rgba.A;
        }
    }


    private (int Width, int Height) CombineImage()
    {
        var (width, height) = _left.IsLoaded
            ? ( _left.TextureWrap!.Width, _left.TextureWrap!.Height )
            : ( _right.TextureWrap!.Width, _right.TextureWrap!.Height );
        _centerStorage.RGBAPixels = new byte[width * height * 4];
        _centerStorage.Type       = Texture.FileType.Bitmap;
        if( _left.IsLoaded )
        {
            Parallel.For( 0, height, _right.IsLoaded ? AddPixelsMultiplied : MultiplyPixelsLeft );
        }
        else
        {
            Parallel.For( 0, height, MultiplyPixelsRight );
        }

        return ( width, height );
    }

    private static Vector4 CappedVector( IReadOnlyList< byte > bytes, int offset, Matrix4x4 transform, bool invert )
    {
        if( bytes.Count == 0 )
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

    public void DrawMatrixInputLeft( float width )
    {
        var ret = DrawMatrixInput( ref _multiplierLeft, width );
        ret |= ImGui.Checkbox( "Invert Colors##Left", ref _invertLeft );
        if( ret )
        {
            Update();
        }
    }

    public void DrawMatrixInputRight( float width )
    {
        var ret = DrawMatrixInput( ref _multiplierRight, width );
        ret |= ImGui.Checkbox( "Invert Colors##Right", ref _invertRight );
        ImGui.SameLine();
        ImGui.SetNextItemWidth( 75 );
        ImGui.DragInt( "##XOffset", ref _offsetX, 0.5f );
        ret |= ImGui.IsItemDeactivatedAfterEdit();
        ImGui.SameLine();
        ImGui.SetNextItemWidth( 75 );
        ImGui.DragInt( "Offsets##YOffset", ref _offsetY, 0.5f );
        ret |= ImGui.IsItemDeactivatedAfterEdit();
        if( ret )
        {
            Update();
        }
    }

    private static bool DrawMatrixInput( ref Matrix4x4 multiplier, float width )
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
        changes |= DragFloat( "##RR", inputWidth, ref multiplier.M11 );
        changes |= DragFloat( "##RG", inputWidth, ref multiplier.M12 );
        changes |= DragFloat( "##RB", inputWidth, ref multiplier.M13 );
        changes |= DragFloat( "##RA", inputWidth, ref multiplier.M14 );

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text( "G    " );
        changes |= DragFloat( "##GR", inputWidth, ref multiplier.M21 );
        changes |= DragFloat( "##GG", inputWidth, ref multiplier.M22 );
        changes |= DragFloat( "##GB", inputWidth, ref multiplier.M23 );
        changes |= DragFloat( "##GA", inputWidth, ref multiplier.M24 );

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text( "B    " );
        changes |= DragFloat( "##BR", inputWidth, ref multiplier.M31 );
        changes |= DragFloat( "##BG", inputWidth, ref multiplier.M32 );
        changes |= DragFloat( "##BB", inputWidth, ref multiplier.M33 );
        changes |= DragFloat( "##BA", inputWidth, ref multiplier.M34 );

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text( "A    " );
        changes |= DragFloat( "##AR", inputWidth, ref multiplier.M41 );
        changes |= DragFloat( "##AG", inputWidth, ref multiplier.M42 );
        changes |= DragFloat( "##AB", inputWidth, ref multiplier.M43 );
        changes |= DragFloat( "##AA", inputWidth, ref multiplier.M44 );

        return changes;
    }
}
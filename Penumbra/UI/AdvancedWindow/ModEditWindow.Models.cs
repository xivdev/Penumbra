using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.GameData.Files;
using Penumbra.String.Classes;
using System.Globalization;
using System.Linq;
using Penumbra.UI.AdvancedWindow;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly FileEditor< MdlFile > _modelTab;

    private static bool DrawModelPanel( MdlFile file, bool disabled )
    {
        var ret = false;
        for( var i = 0; i < file.Materials.Length; ++i )
        {
            using var id  = ImRaii.PushId( i );
            var       tmp = file.Materials[ i ];
            if( ImGui.InputText( string.Empty, ref tmp, Utf8GamePath.MaxGamePathLength,
                   disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None )
            && tmp.Length > 0
            && tmp        != file.Materials[ i ] )
            {
                file.Materials[ i ] = tmp;
                ret                 = true;
            }
        }

        ret |= DrawOtherModelDetails( file, disabled );

        return !disabled && ret;
    }

    private static bool DrawOtherModelDetails( MdlFile file, bool _ )
    {
        if( !ImGui.CollapsingHeader( "Further Content" ) )
        {
            return false;
        }

        using( var table = ImRaii.Table( "##data", 2, ImGuiTableFlags.SizingFixedFit ) )
        {
            if( table )
            {
                ImGuiUtil.DrawTableColumn( "Version" );
                ImGuiUtil.DrawTableColumn( file.Version.ToString() );
                ImGuiUtil.DrawTableColumn( "Radius" );
                ImGuiUtil.DrawTableColumn( file.Radius.ToString( CultureInfo.InvariantCulture ) );
                ImGuiUtil.DrawTableColumn( "Model Clip Out Distance" );
                ImGuiUtil.DrawTableColumn( file.ModelClipOutDistance.ToString( CultureInfo.InvariantCulture ) );
                ImGuiUtil.DrawTableColumn( "Shadow Clip Out Distance" );
                ImGuiUtil.DrawTableColumn( file.ShadowClipOutDistance.ToString( CultureInfo.InvariantCulture ) );
                ImGuiUtil.DrawTableColumn( "LOD Count" );
                ImGuiUtil.DrawTableColumn( file.LodCount.ToString() );
                ImGuiUtil.DrawTableColumn( "Enable Index Buffer Streaming" );
                ImGuiUtil.DrawTableColumn( file.EnableIndexBufferStreaming.ToString() );
                ImGuiUtil.DrawTableColumn( "Enable Edge Geometry" );
                ImGuiUtil.DrawTableColumn( file.EnableEdgeGeometry.ToString() );
                ImGuiUtil.DrawTableColumn( "Flags 1" );
                ImGuiUtil.DrawTableColumn( file.Flags1.ToString() );
                ImGuiUtil.DrawTableColumn( "Flags 2" );
                ImGuiUtil.DrawTableColumn( file.Flags2.ToString() );
                ImGuiUtil.DrawTableColumn( "Vertex Declarations" );
                ImGuiUtil.DrawTableColumn( file.VertexDeclarations.Length.ToString() );
                ImGuiUtil.DrawTableColumn( "Bone Bounding Boxes" );
                ImGuiUtil.DrawTableColumn( file.BoneBoundingBoxes.Length.ToString() );
                ImGuiUtil.DrawTableColumn( "Bone Tables" );
                ImGuiUtil.DrawTableColumn( file.BoneTables.Length.ToString() );
                ImGuiUtil.DrawTableColumn( "Element IDs" );
                ImGuiUtil.DrawTableColumn( file.ElementIds.Length.ToString() );
                ImGuiUtil.DrawTableColumn( "Extra LoDs" );
                ImGuiUtil.DrawTableColumn( file.ExtraLods.Length.ToString() );
                ImGuiUtil.DrawTableColumn( "Meshes" );
                ImGuiUtil.DrawTableColumn( file.Meshes.Length.ToString() );
                ImGuiUtil.DrawTableColumn( "Shape Meshes" );
                ImGuiUtil.DrawTableColumn( file.ShapeMeshes.Length.ToString() );
                ImGuiUtil.DrawTableColumn( "LoDs" );
                ImGuiUtil.DrawTableColumn( file.Lods.Length.ToString() );
                ImGuiUtil.DrawTableColumn( "Vertex Declarations" );
                ImGuiUtil.DrawTableColumn( file.VertexDeclarations.Length.ToString() );
                ImGuiUtil.DrawTableColumn( "Stack Size" );
                ImGuiUtil.DrawTableColumn( file.StackSize.ToString() );
            }
        }

        using( var attributes = ImRaii.TreeNode( "Attributes", ImGuiTreeNodeFlags.DefaultOpen ) )
        {
            if( attributes )
            {
                foreach( var attribute in file.Attributes )
                {
                    ImRaii.TreeNode( attribute, ImGuiTreeNodeFlags.Leaf ).Dispose();
                }
            }
        }

        using( var bones = ImRaii.TreeNode( "Bones", ImGuiTreeNodeFlags.DefaultOpen ) )
        {
            if( bones )
            {
                foreach( var bone in file.Bones )
                {
                    ImRaii.TreeNode( bone, ImGuiTreeNodeFlags.Leaf ).Dispose();
                }
            }
        }

        using( var shapes = ImRaii.TreeNode( "Shapes", ImGuiTreeNodeFlags.DefaultOpen ) )
        {
            if( shapes )
            {
                foreach( var shape in file.Shapes )
                {
                    ImRaii.TreeNode( shape.ShapeName, ImGuiTreeNodeFlags.Leaf ).Dispose();
                }
            }
        }

        if( file.RemainingData.Length > 0 )
        {
            using var t = ImRaii.TreeNode( $"Additional Data (Size: {file.RemainingData.Length})###AdditionalData" );
            if( t )
            {
                ImGuiUtil.TextWrapped( string.Join( ' ', file.RemainingData.Select( c => $"{c:X2}" ) ) );
            }
        }

        return false;
    }

}
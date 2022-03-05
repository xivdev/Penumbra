using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using ImGuiNET;
using Penumbra.UI.Custom;

namespace Penumbra.UI;

public partial class SettingsInterface
{
    [StructLayout( LayoutKind.Explicit )]
    private unsafe struct RenderModel
    {
        [FieldOffset( 0x18 )]
        public RenderModel* PreviousModel;

        [FieldOffset( 0x20 )]
        public RenderModel* NextModel;

        [FieldOffset( 0x30 )]
        public ResourceHandle* ResourceHandle;

        [FieldOffset( 0x40 )]
        public Skeleton* Skeleton;

        [FieldOffset( 0x58 )]
        public void** BoneList;

        [FieldOffset( 0x60 )]
        public int BoneListCount;

        [FieldOffset( 0x68 )]
        private void* UnkDXBuffer1;

        [FieldOffset( 0x70 )]
        private void* UnkDXBuffer2;

        [FieldOffset( 0x78 )]
        private void* UnkDXBuffer3;

        [FieldOffset( 0x90 )]
        public void** Materials;

        [FieldOffset( 0x98 )]
        public int MaterialCount;
    }

    [StructLayout( LayoutKind.Explicit )]
    private unsafe struct Material
    {
        [FieldOffset( 0x10 )]
        public ResourceHandle* ResourceHandle;

        [FieldOffset( 0x28 )]
        public void* MaterialData;

        [FieldOffset( 0x48 )]
        public Texture* Tex1;

        [FieldOffset( 0x60 )]
        public Texture* Tex2;

        [FieldOffset( 0x78 )]
        public Texture* Tex3;
    }

    private static unsafe void DrawPlayerModelInfo()
    {
        var player = Dalamud.ClientState.LocalPlayer;
        var name = player?.Name.ToString() ?? "NULL";
        if( !ImGui.CollapsingHeader( $"Player Model Info: {name}##Draw" ) || player == null )
        {
            return;
        }

        var model = ( CharacterBase* )( ( Character* )player.Address )->GameObject.GetDrawObject();
        if( model == null )
        {
            return;
        }

        if( !ImGui.BeginTable( $"##{name}DrawTable", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit ) )
        {
            return;
        }

        using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTable );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Slot" );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Imc Ptr" );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Imc File" );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Model Ptr" );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Model File" );

        for( var i = 0; i < model->SlotCount; ++i )
        {
            var imc = ( ResourceHandle* )model->IMCArray[ i ];
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text( $"Slot {i}" );
            ImGui.TableNextColumn();
            ImGui.Text( imc == null ? "NULL" : $"0x{( ulong )imc:X}" );
            ImGui.TableNextColumn();
            if( imc != null )
            {
                ImGui.Text( imc->FileName.ToString() );
            }

            var mdl = ( RenderModel* )model->ModelArray[ i ];
            ImGui.TableNextColumn();
            ImGui.Text( mdl == null ? "NULL" : $"0x{( ulong )mdl:X}" );
            if( mdl == null || mdl->ResourceHandle == null || mdl->ResourceHandle->Category != ResourceCategory.Chara )
            {
                continue;
            }

            ImGui.TableNextColumn();
            if( mdl != null )
            {
                ImGui.Text( mdl->ResourceHandle->FileName.ToString() );
            }
        }
    }
}
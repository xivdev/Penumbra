using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Interface;
using Dalamud.Interface.Internal.Notifications;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using Penumbra.GameData.Data;
using Penumbra.GameData.Files;
using Penumbra.String.Classes;
using Penumbra.Util;

namespace Penumbra.UI.Classes;

public partial class ModEditWindow
{
    private sealed class MtrlTab : IWritable
    {
        private readonly ModEditWindow _edit;
        public readonly  MtrlFile      Mtrl;

        public uint MaterialNewKeyId      = 0;
        public uint MaterialNewKeyDefault = 0;
        public uint MaterialNewConstantId = 0;
        public uint MaterialNewSamplerId  = 0;


        public          ShpkFile?      AssociatedShpk;
        public readonly List< string > TextureLabels      = new(4);
        public          FullPath       LoadedShpkPath     = FullPath.Empty;
        public          string         LoadedShpkPathName = string.Empty;
        public          float          TextureLabelWidth  = 0f;

        // Shader Key State
        public readonly List< string >           ShaderKeyLabels         = new(16);
        public readonly Dictionary< uint, uint > DefinedShaderKeys       = new(16);
        public readonly List< int >              MissingShaderKeyIndices = new(16);
        public readonly List< uint >             AvailableKeyValues      = new(16);
        public          string                   VertexShaders           = "Vertex Shaders: ???";
        public          string                   PixelShaders            = "Pixel Shaders: ???";

        public FullPath FindAssociatedShpk( out string defaultPath, out Utf8GamePath defaultGamePath )
        {
            defaultPath = GamePaths.Shader.ShpkPath( Mtrl.ShaderPackage.Name );
            if( !Utf8GamePath.FromString( defaultPath, out defaultGamePath, true ) )
            {
                return FullPath.Empty;
            }

            return _edit.FindBestMatch( defaultGamePath );
        }

        public void LoadShpk( FullPath path )
        {
            try
            {
                LoadedShpkPath = path;
                var data = LoadedShpkPath.IsRooted
                    ? File.ReadAllBytes( LoadedShpkPath.FullName )
                    : Dalamud.GameData.GetFile( LoadedShpkPath.InternalName.ToString() )?.Data;
                AssociatedShpk     = data?.Length > 0 ? new ShpkFile( data ) : throw new Exception( "Failure to load file data." );
                LoadedShpkPathName = path.ToPath();
            }
            catch( Exception e )
            {
                LoadedShpkPath     = FullPath.Empty;
                LoadedShpkPathName = string.Empty;
                AssociatedShpk     = null;
                ChatUtil.NotificationMessage( $"Could not load {LoadedShpkPath.ToPath()}:\n{e}", "Penumbra Advanced Editing", NotificationType.Error );
            }

            Update();
        }

        public void UpdateTextureLabels()
        {
            var samplers = Mtrl.GetSamplersByTexture( AssociatedShpk );
            TextureLabels.Clear();
            TextureLabelWidth = 50f * ImGuiHelpers.GlobalScale;
            using( var _ = ImRaii.PushFont( UiBuilder.MonoFont ) )
            {
                for( var i = 0; i < Mtrl.Textures.Length; ++i )
                {
                    var (sampler, shpkSampler) = samplers[ i ];
                    var name = shpkSampler.HasValue ? shpkSampler.Value.Name : sampler.HasValue ? $"0x{sampler.Value.SamplerId:X8}" : $"#{i}";
                    TextureLabels.Add( name );
                    TextureLabelWidth = Math.Max( TextureLabelWidth, ImGui.CalcTextSize( name ).X );
                }
            }

            TextureLabelWidth = TextureLabelWidth / ImGuiHelpers.GlobalScale + 4;
        }

        public void UpdateShaderKeyLabels()
        {
            ShaderKeyLabels.Clear();
            DefinedShaderKeys.Clear();
            foreach( var (key, idx) in Mtrl.ShaderPackage.ShaderKeys.WithIndex() )
            {
                ShaderKeyLabels.Add( $"#{idx}: 0x{key.Category:X8} = 0x{key.Value:X8}###{idx}: 0x{key.Category:X8}" );
                DefinedShaderKeys.Add( key.Category, key.Value );
            }

            MissingShaderKeyIndices.Clear();
            AvailableKeyValues.Clear();
            var vertexShaders = new IndexSet( AssociatedShpk?.VertexShaders.Length ?? 0, false );
            var pixelShaders  = new IndexSet( AssociatedShpk?.PixelShaders.Length  ?? 0, false );
            if( AssociatedShpk != null )
            {
                MissingShaderKeyIndices.AddRange( AssociatedShpk.MaterialKeys.WithIndex().Where( k => !DefinedShaderKeys.ContainsKey( k.Value.Id ) ).WithoutValue() );

                if( MissingShaderKeyIndices.Count > 0 && MissingShaderKeyIndices.All( i => AssociatedShpk.MaterialKeys[ i ].Id != MaterialNewKeyId ) )
                {
                    var key = AssociatedShpk.MaterialKeys[ MissingShaderKeyIndices[ 0 ] ];
                    MaterialNewKeyId      = key.Id;
                    MaterialNewKeyDefault = key.DefaultValue;
                }

                AvailableKeyValues.AddRange( AssociatedShpk.MaterialKeys.Select( k => DefinedShaderKeys.TryGetValue( k.Id, out var value ) ? value : k.DefaultValue ) );
                foreach( var node in AssociatedShpk.Nodes )
                {
                    if( node.MaterialKeys.WithIndex().All( key => key.Value == AvailableKeyValues[ key.Index ] ) )
                    {
                        foreach( var pass in node.Passes )
                        {
                            vertexShaders.Add( ( int )pass.VertexShader );
                            pixelShaders.Add( ( int )pass.PixelShader );
                        }
                    }
                }
            }

            VertexShaders = $"Vertex Shaders: {( vertexShaders.Count > 0 ? string.Join( ", ", vertexShaders.Select( i => $"#{i}" ) ) : "???" )}";
            PixelShaders  = $"Pixel Shaders: {( pixelShaders.Count   > 0 ? string.Join( ", ", pixelShaders.Select( i => $"#{i}" ) ) : "???" )}";
        }

        public void Update()
        {
            UpdateTextureLabels();
            UpdateShaderKeyLabels();
        }

        public MtrlTab( ModEditWindow edit, MtrlFile file )
        {
            _edit = edit;
            Mtrl  = file;
            LoadShpk( FindAssociatedShpk( out _, out _ ) );
        }

        public bool Valid
            => Mtrl.Valid;

        public byte[] Write()
            => Mtrl.Write();
    }
}
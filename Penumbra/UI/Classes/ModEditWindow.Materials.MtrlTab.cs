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
using static Penumbra.GameData.Files.ShpkFile;

namespace Penumbra.UI.Classes;

public partial class ModEditWindow
{
    private sealed class MtrlTab : IWritable
    {
        private readonly ModEditWindow _edit;
        public readonly  MtrlFile      Mtrl;

        public uint MaterialNewKeyId       = 0;
        public uint MaterialNewKeyDefault  = 0;
        public uint MaterialNewConstantId  = 0;
        public int  MaterialNewConstantIdx = 0;
        public uint MaterialNewSamplerId   = 0;


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

        // Material Constants
        public List< (string Name, bool ComponentOnly, int ParamValueOffset) > MaterialConstants        = new(16);
        public List< (string Name, uint Id, ushort ByteSize) >                 MissingMaterialConstants = new(16);

        public string          MaterialConstantLabel         = "Constants###Constants";
        public bool            HasMalformedMaterialConstants = false;
        public IndexSet        OrphanedMaterialValues        = new(0, false);
        public HashSet< uint > DefinedMaterialConstants      = new(16);
        public int             AliasedMaterialValueCount     = 0;

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

        public void UpdateConstantLabels()
        {
            var prefix = AssociatedShpk?.GetConstantById( MaterialParamsConstantId )?.Name ?? string.Empty;
            MaterialConstantLabel = prefix.Length == 0 ? "Constants###Constants" : prefix + "###Constants";

            DefinedMaterialConstants.Clear();
            MaterialConstants.Clear();
            HasMalformedMaterialConstants = false;
            AliasedMaterialValueCount     = 0;
            OrphanedMaterialValues        = new IndexSet( Mtrl.ShaderPackage.ShaderValues.Length, true );
            foreach( var (constant, idx) in Mtrl.ShaderPackage.Constants.WithIndex() )
            {
                DefinedMaterialConstants.Add( constant.Id );
                var values           = Mtrl.GetConstantValues( constant );
                var paramValueOffset = -values.Length;
                if( values.Length > 0 )
                {
                    var shpkParam       = AssociatedShpk?.GetMaterialParamById( constant.Id );
                    var paramByteOffset = shpkParam?.ByteOffset ?? -1;
                    if( ( paramByteOffset & 0x3 ) == 0 )
                    {
                        paramValueOffset = paramByteOffset >> 2;
                    }

                    var unique = OrphanedMaterialValues.RemoveRange( constant.ByteOffset >> 2, values.Length );
                    AliasedMaterialValueCount += values.Length - unique;
                }
                else
                {
                    HasMalformedMaterialConstants = true;
                }

                var (name, componentOnly) = MaterialParamRangeName( prefix, paramValueOffset, values.Length );
                var label = name == null
                    ? $"#{idx:D2} (ID: 0x{constant.Id:X8})###{constant.Id}"
                    : $"#{idx:D2}: {name} (ID: 0x{constant.Id:X8})###{constant.Id}";

                MaterialConstants.Add( ( label, componentOnly, paramValueOffset ) );
            }

            MissingMaterialConstants.Clear();
            if( AssociatedShpk != null )
            {
                var setIdx = false;
                foreach( var param in AssociatedShpk.MaterialParams.Where( m => !DefinedMaterialConstants.Contains(m.Id)) )
                {
                    var (name, _) = MaterialParamRangeName( prefix, param.ByteOffset >> 2, param.ByteSize >> 2 );
                    var label = name == null
                        ? $"(ID: 0x{param.Id:X8})"
                        : $"{name} (ID: 0x{param.Id:X8})";
                    if( MaterialNewConstantId == param.Id )
                    {
                        setIdx                 = true;
                        MaterialNewConstantIdx = MissingMaterialConstants.Count;
                    }
                    MissingMaterialConstants.Add( ( label, param.Id, param.ByteSize ) );
                }

                if (!setIdx && MissingMaterialConstants.Count > 0)
                {
                    MaterialNewConstantIdx = 0;
                    MaterialNewConstantId  = MissingMaterialConstants[ 0 ].Id;
                }
            }
        }

        public void Update()
        {
            UpdateTextureLabels();
            UpdateShaderKeyLabels();
            UpdateConstantLabels();
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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Internal.Notifications;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using Penumbra.GameData;
using Penumbra.GameData.Data;
using Penumbra.GameData.Files;
using Penumbra.GameData.Structs;
using Penumbra.Services;
using Penumbra.String.Classes;
using Penumbra.Util;
using static Penumbra.GameData.Files.ShpkFile;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private sealed class MtrlTab : IWritable, IDisposable
    {
        private readonly ModEditWindow _edit;
        public readonly  MtrlFile      Mtrl;
        public readonly  string        FilePath;
        public readonly  bool          Writable;

        public uint NewKeyId;
        public uint NewKeyDefault;
        public uint NewConstantId;
        public int  NewConstantIdx;
        public uint NewSamplerId;
        public int  NewSamplerIdx;


        public          ShpkFile?      AssociatedShpk;
        public readonly List< string > TextureLabels      = new(4);
        public          FullPath       LoadedShpkPath     = FullPath.Empty;
        public          string         LoadedShpkPathName = string.Empty;
        public          float          TextureLabelWidth;

        // Shader Key State
        public readonly List< string >           ShaderKeyLabels         = new(16);
        public readonly Dictionary< uint, uint > DefinedShaderKeys       = new(16);
        public readonly List< int >              MissingShaderKeyIndices = new(16);
        public readonly List< uint >             AvailableKeyValues      = new(16);
        public          string                   VertexShaders           = "Vertex Shaders: ???";
        public          string                   PixelShaders            = "Pixel Shaders: ???";

        // Material Constants
        public readonly List< (string Name, bool ComponentOnly, int ParamValueOffset) > MaterialConstants        = new(16);
        public readonly List< (string Name, uint Id, ushort ByteSize) >                 MissingMaterialConstants = new(16);
        public readonly HashSet< uint >                                                 DefinedMaterialConstants = new(16);

        public string   MaterialConstantLabel  = "Constants###Constants";
        public IndexSet OrphanedMaterialValues = new(0, false);
        public int      AliasedMaterialValueCount;
        public bool     HasMalformedMaterialConstants;

        // Samplers
        public readonly List< (string Label, string FileName, uint Id) > Samplers         = new(4);
        public readonly List< (string Name, uint Id) >                   MissingSamplers  = new(4);
        public readonly HashSet< uint >                                  DefinedSamplers  = new(4);
        public          IndexSet                                         OrphanedSamplers = new(0, false);
        public          int                                              AliasedSamplerCount;

        // Live-Previewers
        public readonly List<LiveMaterialPreviewer> MaterialPreviewers     = new(4);
        public readonly List<LiveColorSetPreviewer> ColorSetPreviewers     = new(4);
        public          int                         HighlightedColorSetRow = -1;
        public          int                         HighlightTime          = -1;

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
                    : _edit._dalamud.GameData.GetFile( LoadedShpkPath.InternalName.ToString() )?.Data;
                AssociatedShpk     = data?.Length > 0 ? new ShpkFile( data ) : throw new Exception( "Failure to load file data." );
                LoadedShpkPathName = path.ToPath();
            }
            catch( Exception e )
            {
                LoadedShpkPath     = FullPath.Empty;
                LoadedShpkPathName = string.Empty;
                AssociatedShpk     = null;
                Penumbra.Chat.NotificationMessage( $"Could not load {LoadedShpkPath.ToPath()}:\n{e}", "Penumbra Advanced Editing", NotificationType.Error );
            }

            Update();
        }

        public void UpdateTextureLabels()
        {
            var samplers = Mtrl.GetSamplersByTexture( AssociatedShpk );
            TextureLabels.Clear();
            TextureLabelWidth = 50f * UiHelpers.Scale;
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

            TextureLabelWidth = TextureLabelWidth / UiHelpers.Scale + 4;
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

                if( MissingShaderKeyIndices.Count > 0 && MissingShaderKeyIndices.All( i => AssociatedShpk.MaterialKeys[ i ].Id != NewKeyId ) )
                {
                    var key = AssociatedShpk.MaterialKeys[ MissingShaderKeyIndices[ 0 ] ];
                    NewKeyId      = key.Id;
                    NewKeyDefault = key.DefaultValue;
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
                foreach( var param in AssociatedShpk.MaterialParams.Where( m => !DefinedMaterialConstants.Contains( m.Id ) ) )
                {
                    var (name, _) = MaterialParamRangeName( prefix, param.ByteOffset >> 2, param.ByteSize >> 2 );
                    var label = name == null
                        ? $"(ID: 0x{param.Id:X8})"
                        : $"{name} (ID: 0x{param.Id:X8})";
                    if( NewConstantId == param.Id )
                    {
                        setIdx         = true;
                        NewConstantIdx = MissingMaterialConstants.Count;
                    }

                    MissingMaterialConstants.Add( ( label, param.Id, param.ByteSize ) );
                }

                if( !setIdx && MissingMaterialConstants.Count > 0 )
                {
                    NewConstantIdx = 0;
                    NewConstantId  = MissingMaterialConstants[ 0 ].Id;
                }
            }
        }

        public void UpdateSamplers()
        {
            Samplers.Clear();
            DefinedSamplers.Clear();
            OrphanedSamplers = new IndexSet( Mtrl.Textures.Length, true );
            foreach( var (sampler, idx) in Mtrl.ShaderPackage.Samplers.WithIndex() )
            {
                DefinedSamplers.Add( sampler.SamplerId );
                if( !OrphanedSamplers.Remove( sampler.TextureIndex ) )
                {
                    ++AliasedSamplerCount;
                }

                var shpk = AssociatedShpk?.GetSamplerById( sampler.SamplerId );
                var label = shpk.HasValue
                    ? $"#{idx}: {shpk.Value.Name} (ID: 0x{sampler.SamplerId:X8})##{sampler.SamplerId}"
                    : $"#{idx} (ID: 0x{sampler.SamplerId:X8})##{sampler.SamplerId}";
                var fileName = $"Texture #{sampler.TextureIndex} - {Path.GetFileName( Mtrl.Textures[ sampler.TextureIndex ].Path )}";
                Samplers.Add( ( label, fileName, sampler.SamplerId ) );
            }

            MissingSamplers.Clear();
            if( AssociatedShpk != null )
            {
                var setSampler = false;
                foreach( var sampler in AssociatedShpk.Samplers.Where( s => s.Slot == 2 && !DefinedSamplers.Contains( s.Id ) ) )
                {
                    if( sampler.Id == NewSamplerId )
                    {
                        setSampler    = true;
                        NewSamplerIdx = MissingSamplers.Count;
                    }

                    MissingSamplers.Add( ( sampler.Name, sampler.Id ) );
                }

                if( !setSampler && MissingSamplers.Count > 0 )
                {
                    NewSamplerIdx = 0;
                    NewSamplerId  = MissingSamplers[ 0 ].Id;
                }
            }
        }

        public unsafe void BindToMaterialInstances()
        {
            UnbindFromMaterialInstances();

            var localPlayer = FindLocalPlayer(_edit._dalamud.Objects);
            if (null == localPlayer)
                return;

            var drawObject = (CharacterBase*)localPlayer->GameObject.GetDrawObject();
            if (null == drawObject)
                return;

            var instances = FindMaterial(drawObject, -1, FilePath);

            var drawObjects = stackalloc CharacterBase*[4];
            drawObjects[0] = drawObject;

            for (var i = 0; i < 3; ++i)
            {
                var subActor = FindSubActor(localPlayer, i);
                if (null == subActor)
                    continue;

                var subDrawObject = (CharacterBase*)subActor->GameObject.GetDrawObject();
                if (null == subDrawObject)
                    continue;

                instances.AddRange(FindMaterial(subDrawObject, i, FilePath));
                drawObjects[i + 1] = subDrawObject;
            }

            var foundMaterials = new HashSet<nint>();
            foreach (var (subActorType, childObjectIndex, modelSlot, materialSlot) in instances)
            {
                var material = GetDrawObjectMaterial(drawObjects[subActorType + 1], modelSlot, materialSlot);
                if (foundMaterials.Contains((nint)material))
                    continue;
                try
                {
                    MaterialPreviewers.Add(new LiveMaterialPreviewer(_edit._dalamud.Objects, subActorType, childObjectIndex, modelSlot, materialSlot));
                    foundMaterials.Add((nint)material);
                }
                catch (InvalidOperationException)
                {
                    // Carry on without that previewer.
                }
            }

            var colorSet = Mtrl.ColorSets.FirstOrNull(colorSet => colorSet.HasRows);

            if (colorSet.HasValue)
            {
                foreach (var (subActorType, childObjectIndex, modelSlot, materialSlot) in instances)
                {
                    try
                    {
                        ColorSetPreviewers.Add(new LiveColorSetPreviewer(_edit._dalamud.Objects, _edit._dalamud.Framework, subActorType, childObjectIndex, modelSlot, materialSlot));
                    }
                    catch (InvalidOperationException)
                    {
                        // Carry on without that previewer.
                    }
                }
                UpdateColorSetPreview();
            }
        }

        public void UnbindFromMaterialInstances()
        {
            foreach (var previewer in MaterialPreviewers)
                previewer.Dispose();
            MaterialPreviewers.Clear();

            foreach (var previewer in ColorSetPreviewers)
                previewer.Dispose();
            ColorSetPreviewers.Clear();
        }

        public void SetShaderPackageFlags(uint shPkFlags)
        {
            foreach (var previewer in MaterialPreviewers)
                previewer.SetShaderPackageFlags(shPkFlags);
        }

        public void SetMaterialParameter(uint parameterCrc, Index offset, Span<float> value)
        {
            foreach (var previewer in MaterialPreviewers)
                previewer.SetMaterialParameter(parameterCrc, offset, value);
        }

        public void SetSamplerFlags(uint samplerCrc, uint samplerFlags)
        {
            foreach (var previewer in MaterialPreviewers)
                previewer.SetSamplerFlags(samplerCrc, samplerFlags);
        }

        public void HighlightColorSetRow(int rowIdx)
        {
            var oldRowIdx = HighlightedColorSetRow;

            HighlightedColorSetRow = rowIdx;
            HighlightTime          = (HighlightTime + 1) % 32;

            if (oldRowIdx >= 0)
                UpdateColorSetRowPreview(oldRowIdx);
            if (rowIdx >= 0)
                UpdateColorSetRowPreview(rowIdx);
        }

        public void CancelColorSetHighlight()
        {
            var rowIdx = HighlightedColorSetRow;

            HighlightedColorSetRow = -1;
            HighlightTime          = -1;

            if (rowIdx >= 0)
                UpdateColorSetRowPreview(rowIdx);
        }

        public unsafe void UpdateColorSetRowPreview(int rowIdx)
        {
            if (ColorSetPreviewers.Count == 0)
                return;

            var maybeColorSet = Mtrl.ColorSets.FirstOrNull(colorSet => colorSet.HasRows);
            if (!maybeColorSet.HasValue)
                return;

            var colorSet = maybeColorSet.Value;
            var maybeColorDyeSet = Mtrl.ColorDyeSets.FirstOrNull(colorDyeSet => colorDyeSet.Index == colorSet.Index);

            var row = colorSet.Rows[rowIdx];
            if (maybeColorDyeSet.HasValue)
            {
                var stm = _edit._stainService.StmFile;
                var dye = maybeColorDyeSet.Value.Rows[rowIdx];
                if (stm.TryGetValue(dye.Template, (StainId)_edit._stainService.StainCombo.CurrentSelection.Key, out var dyes))
                    ApplyDye(ref row, dye, dyes);
            }

            if (HighlightedColorSetRow == rowIdx)
                ApplyHighlight(ref row, HighlightTime);

            foreach (var previewer in ColorSetPreviewers)
            {
                fixed (Half* pDest = previewer.ColorSet)
                    Buffer.MemoryCopy(&row, pDest + LiveColorSetPreviewer.TextureWidth * 4 * rowIdx, LiveColorSetPreviewer.TextureWidth * 4 * sizeof(Half), sizeof(MtrlFile.ColorSet.Row));
                previewer.ScheduleUpdate();
            }
        }

        public unsafe void UpdateColorSetPreview()
        {
            if (ColorSetPreviewers.Count == 0)
                return;

            var maybeColorSet = Mtrl.ColorSets.FirstOrNull(colorSet => colorSet.HasRows);
            if (!maybeColorSet.HasValue)
                return;

            var colorSet = maybeColorSet.Value;
            var maybeColorDyeSet = Mtrl.ColorDyeSets.FirstOrNull(colorDyeSet => colorDyeSet.Index == colorSet.Index);

            var rows = colorSet.Rows;
            if (maybeColorDyeSet.HasValue)
            {
                var stm = _edit._stainService.StmFile;
                var stainId = (StainId)_edit._stainService.StainCombo.CurrentSelection.Key;
                var colorDyeSet = maybeColorDyeSet.Value;
                for (var i = 0; i < MtrlFile.ColorSet.RowArray.NumRows; ++i)
                {
                    ref var row = ref rows[i];
                    var dye = colorDyeSet.Rows[i];
                    if (stm.TryGetValue(dye.Template, stainId, out var dyes))
                        ApplyDye(ref row, dye, dyes);
                }
            }

            if (HighlightedColorSetRow >= 0)
                ApplyHighlight(ref rows[HighlightedColorSetRow], HighlightTime);

            foreach (var previewer in ColorSetPreviewers)
            {
                fixed (Half* pDest = previewer.ColorSet)
                    Buffer.MemoryCopy(&rows, pDest, LiveColorSetPreviewer.TextureLength * sizeof(Half), sizeof(MtrlFile.ColorSet.RowArray));
                previewer.ScheduleUpdate();
            }
        }

        private static void ApplyDye(ref MtrlFile.ColorSet.Row row, MtrlFile.ColorDyeSet.Row dye, StmFile.DyePack dyes)
        {
            if (dye.Diffuse)
                row.Diffuse = dyes.Diffuse;
            if (dye.Specular)
                row.Specular = dyes.Specular;
            if (dye.SpecularStrength)
                row.SpecularStrength = dyes.SpecularPower;
            if (dye.Emissive)
                row.Emissive = dyes.Emissive;
            if (dye.Gloss)
                row.GlossStrength = dyes.Gloss;
        }

        private static void ApplyHighlight(ref MtrlFile.ColorSet.Row row, int time)
        {
            var level = Math.Sin(time * Math.PI / 16) * 0.5 + 0.5;
            var levelSq = (float)(level * level);

            row.Diffuse = Vector3.Zero;
            row.Specular = Vector3.Zero;
            row.Emissive = new Vector3(levelSq);
        }

        public void Update()
        {
            UpdateTextureLabels();
            UpdateShaderKeyLabels();
            UpdateConstantLabels();
            UpdateSamplers();
        }

        public MtrlTab( ModEditWindow edit, MtrlFile file, string filePath, bool writable )
        {
            _edit    = edit;
            Mtrl     = file;
            FilePath = filePath;
            Writable = writable;
            LoadShpk( FindAssociatedShpk( out _, out _ ) );
            if (writable)
                BindToMaterialInstances();
        }

        ~MtrlTab()
        {
            DoDispose();
        }

        public void Dispose()
        {
            DoDispose();
            GC.SuppressFinalize(this);
        }

        private void DoDispose()
        {
            UnbindFromMaterialInstances();
        }

        public bool Valid
            => Mtrl.Valid;

        public byte[] Write()
            => Mtrl.Write();
    }
}
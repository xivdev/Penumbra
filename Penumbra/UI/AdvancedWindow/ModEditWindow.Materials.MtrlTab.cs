using Dalamud.Interface;
using Dalamud.Interface.Internal.Notifications;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using ImGuiNET;
using Newtonsoft.Json.Linq;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using Penumbra.GameData.Data;
using Penumbra.GameData.Files;
using Penumbra.GameData.Structs;
using Penumbra.Interop.MaterialPreview;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;
using static Penumbra.GameData.Files.ShpkFile;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private sealed class MtrlTab : IWritable, IDisposable
    {
        private const int ShpkPrefixLength = 16;

        private static readonly ByteString ShpkPrefix = ByteString.FromSpanUnsafe("shader/sm5/shpk/"u8, true, true, true);

        private readonly ModEditWindow _edit;
        public readonly  MtrlFile      Mtrl;
        public readonly  string        FilePath;
        public readonly  bool          Writable;

        private string[]? _shpkNames;

        public string    ShaderHeader             = "Shader###Shader";
        public FullPath  LoadedShpkPath           = FullPath.Empty;
        public string    LoadedShpkPathName       = string.Empty;
        public string    LoadedShpkDevkitPathName = string.Empty;
        public string    ShaderComment            = string.Empty;
        public ShpkFile? AssociatedShpk;
        public JObject?  AssociatedShpkDevkit;

        public readonly string   LoadedBaseDevkitPathName;
        public readonly JObject? AssociatedBaseDevkit;

        // Shader Key State
        public readonly
            List<(string Label, int Index, string Description, bool MonoFont, IReadOnlyList<(string Label, uint Value, string Description)>
                Values)> ShaderKeys = new(16);

        public readonly HashSet<int> VertexShaders = new(16);
        public readonly HashSet<int> PixelShaders  = new(16);
        public          bool         ShadersKnown;
        public          string       VertexShadersString = "Vertex Shaders: ???";
        public          string       PixelShadersString  = "Pixel Shaders: ???";

        // Textures & Samplers
        public readonly List<(string Label, int TextureIndex, int SamplerIndex, string Description, bool MonoFont)> Textures = new(4);

        public readonly HashSet<int>  UnfoldedTextures = new(4);
        public readonly HashSet<uint> SamplerIds       = new(16);
        public          float         TextureLabelWidth;

        // Material Constants
        public readonly
            List<(string Header, List<(string Label, int ConstantIndex, Range Slice, string Description, bool MonoFont, IConstantEditor Editor)>
                Constants)> Constants = new(16);

        // Live-Previewers
        public readonly List<LiveMaterialPreviewer>   MaterialPreviewers       = new(4);
        public readonly List<LiveColorTablePreviewer> ColorTablePreviewers     = new(4);
        public          int                           HighlightedColorTableRow = -1;
        public readonly Stopwatch                     HighlightTime            = new();

        public FullPath FindAssociatedShpk(out string defaultPath, out Utf8GamePath defaultGamePath)
        {
            defaultPath = GamePaths.Shader.ShpkPath(Mtrl.ShaderPackage.Name);
            if (!Utf8GamePath.FromString(defaultPath, out defaultGamePath, true))
                return FullPath.Empty;

            return _edit.FindBestMatch(defaultGamePath);
        }

        public string[] GetShpkNames()
        {
            if (null != _shpkNames)
                return _shpkNames;

            var names = new HashSet<string>(StandardShaderPackages);
            names.UnionWith(_edit.FindPathsStartingWith(ShpkPrefix).Select(path => path.ToString()[ShpkPrefixLength..]));

            _shpkNames = names.ToArray();
            Array.Sort(_shpkNames);

            return _shpkNames;
        }

        public void LoadShpk(FullPath path)
        {
            ShaderHeader = $"Shader ({Mtrl.ShaderPackage.Name})###Shader";

            try
            {
                LoadedShpkPath = path;
                var data = LoadedShpkPath.IsRooted
                    ? File.ReadAllBytes(LoadedShpkPath.FullName)
                    : _edit._dalamud.GameData.GetFile(LoadedShpkPath.InternalName.ToString())?.Data;
                AssociatedShpk     = data?.Length > 0 ? new ShpkFile(data) : throw new Exception("Failure to load file data.");
                LoadedShpkPathName = path.ToPath();
            }
            catch (Exception e)
            {
                LoadedShpkPath     = FullPath.Empty;
                LoadedShpkPathName = string.Empty;
                AssociatedShpk     = null;
                Penumbra.Chat.NotificationMessage($"Could not load {LoadedShpkPath.ToPath()}:\n{e}", "Penumbra Advanced Editing",
                    NotificationType.Error);
            }

            if (LoadedShpkPath.InternalName.IsEmpty)
            {
                AssociatedShpkDevkit     = null;
                LoadedShpkDevkitPathName = string.Empty;
            }
            else
            {
                AssociatedShpkDevkit =
                    TryLoadShpkDevkit(Path.GetFileNameWithoutExtension(Mtrl.ShaderPackage.Name), out LoadedShpkDevkitPathName);
            }

            UpdateShaderKeys();
            Update();
        }

        private JObject? TryLoadShpkDevkit(string shpkBaseName, out string devkitPathName)
        {
            try
            {
                if (!Utf8GamePath.FromString("penumbra/shpk_devkit/" + shpkBaseName + ".json", out var devkitPath))
                    throw new Exception("Could not assemble ShPk dev-kit path.");

                var devkitFullPath = _edit.FindBestMatch(devkitPath);
                if (!devkitFullPath.IsRooted)
                    throw new Exception("Could not resolve ShPk dev-kit path.");

                devkitPathName = devkitFullPath.FullName;
                return JObject.Parse(File.ReadAllText(devkitFullPath.FullName));
            }
            catch
            {
                devkitPathName = string.Empty;
                return null;
            }
        }

        private T? TryGetShpkDevkitData<T>(string category, uint? id, bool mayVary) where T : class
            => TryGetShpkDevkitData<T>(AssociatedShpkDevkit,  LoadedShpkDevkitPathName, category, id, mayVary)
             ?? TryGetShpkDevkitData<T>(AssociatedBaseDevkit, LoadedBaseDevkitPathName, category, id, mayVary);

        private T? TryGetShpkDevkitData<T>(JObject? devkit, string devkitPathName, string category, uint? id, bool mayVary) where T : class
        {
            if (devkit == null)
                return null;

            try
            {
                var data = devkit[category];
                if (id.HasValue)
                    data = data?[id.Value.ToString()];

                if (mayVary && (data as JObject)?["Vary"] != null)
                {
                    var selector = BuildSelector(data!["Vary"]!
                        .Select(key => (uint)key)
                        .Select(key => Mtrl.GetShaderKey(key)?.Value ?? AssociatedShpk!.GetMaterialKeyById(key)!.Value.DefaultValue));
                    var index = (int)data["Selectors"]![selector.ToString()]!;
                    data = data["Items"]![index];
                }

                return data?.ToObject(typeof(T)) as T;
            }
            catch (Exception e)
            {
                // Some element in the JSON was undefined or invalid (wrong type, key that doesn't exist in the ShPk, index out of range, …)
                Penumbra.Log.Error($"Error while traversing the ShPk dev-kit file at {devkitPathName}: {e}");
                return null;
            }
        }

        private void UpdateShaderKeys()
        {
            ShaderKeys.Clear();
            if (AssociatedShpk != null)
                foreach (var key in AssociatedShpk.MaterialKeys)
                {
                    var dkData     = TryGetShpkDevkitData<DevkitShaderKey>("ShaderKeys", key.Id, false);
                    var hasDkLabel = !string.IsNullOrEmpty(dkData?.Label);

                    var valueSet = new HashSet<uint>(key.Values);
                    if (dkData != null)
                        valueSet.UnionWith(dkData.Values.Keys);

                    var mtrlKeyIndex = Mtrl.FindOrAddShaderKey(key.Id, key.DefaultValue);
                    var values = valueSet.Select<uint, (string Label, uint Value, string Description)>(value =>
                    {
                        if (dkData != null && dkData.Values.TryGetValue(value, out var dkValue))
                            return (dkValue.Label.Length > 0 ? dkValue.Label : $"0x{value:X8}", value, dkValue.Description);

                        return ($"0x{value:X8}", value, string.Empty);
                    }).ToArray();
                    Array.Sort(values, (x, y) =>
                    {
                        if (x.Value == key.DefaultValue)
                            return -1;
                        if (y.Value == key.DefaultValue)
                            return 1;

                        return string.Compare(x.Label, y.Label, StringComparison.Ordinal);
                    });
                    ShaderKeys.Add((hasDkLabel ? dkData!.Label : $"0x{key.Id:X8}", mtrlKeyIndex, dkData?.Description ?? string.Empty,
                        !hasDkLabel, values));
                }
            else
                foreach (var (key, index) in Mtrl.ShaderPackage.ShaderKeys.WithIndex())
                    ShaderKeys.Add(($"0x{key.Category:X8}", index, string.Empty, true, Array.Empty<(string, uint, string)>()));
        }

        private void UpdateShaders()
        {
            VertexShaders.Clear();
            PixelShaders.Clear();
            if (AssociatedShpk == null)
            {
                ShadersKnown = false;
            }
            else
            {
                ShadersKnown = true;
                var systemKeySelectors  = AllSelectors(AssociatedShpk.SystemKeys).ToArray();
                var sceneKeySelectors   = AllSelectors(AssociatedShpk.SceneKeys).ToArray();
                var subViewKeySelectors = AllSelectors(AssociatedShpk.SubViewKeys).ToArray();
                var materialKeySelector =
                    BuildSelector(AssociatedShpk.MaterialKeys.Select(key => Mtrl.GetOrAddShaderKey(key.Id, key.DefaultValue).Value));
                foreach (var systemKeySelector in systemKeySelectors)
                {
                    foreach (var sceneKeySelector in sceneKeySelectors)
                    {
                        foreach (var subViewKeySelector in subViewKeySelectors)
                        {
                            var selector = BuildSelector(systemKeySelector, sceneKeySelector, materialKeySelector, subViewKeySelector);
                            var node     = AssociatedShpk.GetNodeBySelector(selector);
                            if (node.HasValue)
                                foreach (var pass in node.Value.Passes)
                                {
                                    VertexShaders.Add((int)pass.VertexShader);
                                    PixelShaders.Add((int)pass.PixelShader);
                                }
                            else
                                ShadersKnown = false;
                        }
                    }
                }
            }

            var vertexShaders = VertexShaders.OrderBy(i => i).Select(i => $"#{i}");
            var pixelShaders  = PixelShaders.OrderBy(i => i).Select(i => $"#{i}");

            VertexShadersString = $"Vertex Shaders: {string.Join(", ", ShadersKnown ? vertexShaders : vertexShaders.Append("???"))}";
            PixelShadersString  = $"Pixel Shaders: {string.Join(", ",  ShadersKnown ? pixelShaders : pixelShaders.Append("???"))}";

            ShaderComment = TryGetShpkDevkitData<string>("Comment", null, true) ?? string.Empty;
        }

        private void UpdateTextures()
        {
            Textures.Clear();
            SamplerIds.Clear();
            if (AssociatedShpk == null)
            {
                SamplerIds.UnionWith(Mtrl.ShaderPackage.Samplers.Select(sampler => sampler.SamplerId));
                if (Mtrl.HasTable)
                    SamplerIds.Add(TableSamplerId);

                foreach (var (sampler, index) in Mtrl.ShaderPackage.Samplers.WithIndex())
                    Textures.Add(($"0x{sampler.SamplerId:X8}", sampler.TextureIndex, index, string.Empty, true));
            }
            else
            {
                foreach (var index in VertexShaders)
                    SamplerIds.UnionWith(AssociatedShpk.VertexShaders[index].Samplers.Select(sampler => sampler.Id));
                foreach (var index in PixelShaders)
                    SamplerIds.UnionWith(AssociatedShpk.PixelShaders[index].Samplers.Select(sampler => sampler.Id));
                if (!ShadersKnown)
                {
                    SamplerIds.UnionWith(Mtrl.ShaderPackage.Samplers.Select(sampler => sampler.SamplerId));
                    if (Mtrl.HasTable)
                        SamplerIds.Add(TableSamplerId);
                }

                foreach (var samplerId in SamplerIds)
                {
                    var shpkSampler = AssociatedShpk.GetSamplerById(samplerId);
                    if (shpkSampler is not { Slot: 2 })
                        continue;

                    var dkData     = TryGetShpkDevkitData<DevkitSampler>("Samplers", samplerId, true);
                    var hasDkLabel = !string.IsNullOrEmpty(dkData?.Label);

                    var sampler = Mtrl.GetOrAddSampler(samplerId, dkData?.DefaultTexture ?? string.Empty, out var samplerIndex);
                    Textures.Add((hasDkLabel ? dkData!.Label : shpkSampler.Value.Name, sampler.TextureIndex, samplerIndex,
                        dkData?.Description ?? string.Empty, !hasDkLabel));
                }

                if (SamplerIds.Contains(TableSamplerId))
                    Mtrl.HasTable = true;
            }

            Textures.Sort((x, y) => string.CompareOrdinal(x.Label, y.Label));

            TextureLabelWidth = 50f * UiHelpers.Scale;

            float helpWidth;
            using (var _ = ImRaii.PushFont(UiBuilder.IconFont))
            {
                helpWidth = ImGui.GetStyle().ItemSpacing.X + ImGui.CalcTextSize(FontAwesomeIcon.InfoCircle.ToIconString()).X;
            }

            foreach (var (label, _, _, description, monoFont) in Textures)
            {
                if (!monoFont)
                    TextureLabelWidth = Math.Max(TextureLabelWidth, ImGui.CalcTextSize(label).X + (description.Length > 0 ? helpWidth : 0.0f));
            }

            using (var _ = ImRaii.PushFont(UiBuilder.MonoFont))
            {
                foreach (var (label, _, _, description, monoFont) in Textures)
                {
                    if (monoFont)
                        TextureLabelWidth = Math.Max(TextureLabelWidth,
                            ImGui.CalcTextSize(label).X + (description.Length > 0 ? helpWidth : 0.0f));
                }
            }

            TextureLabelWidth = TextureLabelWidth / UiHelpers.Scale + 4;
        }

        private void UpdateConstants()
        {
            static List<T> FindOrAddGroup<T>(List<(string, List<T>)> groups, string name)
            {
                foreach (var (groupName, group) in groups)
                {
                    if (string.Equals(name, groupName, StringComparison.Ordinal))
                        return group;
                }

                var newGroup = new List<T>(16);
                groups.Add((name, newGroup));
                return newGroup;
            }

            Constants.Clear();
            if (AssociatedShpk == null)
            {
                var fcGroup = FindOrAddGroup(Constants, "Further Constants");
                foreach (var (constant, index) in Mtrl.ShaderPackage.Constants.WithIndex())
                {
                    var values = Mtrl.GetConstantValues(constant);
                    for (var i = 0; i < values.Length; i += 4)
                    {
                        fcGroup.Add(($"0x{constant.Id:X8}", index, i..Math.Min(i + 4, values.Length), string.Empty, true,
                            FloatConstantEditor.Default));
                    }
                }
            }
            else
            {
                var prefix = AssociatedShpk.GetConstantById(MaterialParamsConstantId)?.Name ?? string.Empty;
                foreach (var shpkConstant in AssociatedShpk.MaterialParams)
                {
                    if ((shpkConstant.ByteSize & 0x3) != 0)
                        continue;

                    var constant        = Mtrl.GetOrAddConstant(shpkConstant.Id, shpkConstant.ByteSize >> 2, out var constantIndex);
                    var values          = Mtrl.GetConstantValues(constant);
                    var handledElements = new IndexSet(values.Length, false);

                    var dkData = TryGetShpkDevkitData<DevkitConstant[]>("Constants", shpkConstant.Id, true);
                    if (dkData != null)
                        foreach (var dkConstant in dkData)
                        {
                            var offset = (int)dkConstant.Offset;
                            var length = values.Length - offset;
                            if (dkConstant.Length.HasValue)
                                length = Math.Min(length, (int)dkConstant.Length.Value);
                            if (length <= 0)
                                continue;

                            var editor = dkConstant.CreateEditor();
                            if (editor != null)
                                FindOrAddGroup(Constants, dkConstant.Group.Length > 0 ? dkConstant.Group : "Further Constants")
                                    .Add((dkConstant.Label, constantIndex, offset..(offset + length), dkConstant.Description, false, editor));
                            handledElements.AddRange(offset, length);
                        }

                    var fcGroup = FindOrAddGroup(Constants, "Further Constants");
                    foreach (var (start, end) in handledElements.Ranges(true))
                    {
                        if ((shpkConstant.ByteOffset & 0x3) == 0)
                        {
                            var offset = shpkConstant.ByteOffset >> 2;
                            for (int i = (start & ~0x3) - (offset & 0x3), j = offset >> 2; i < end; i += 4, ++j)
                            {
                                var rangeStart = Math.Max(i, start);
                                var rangeEnd   = Math.Min(i + 4, end);
                                if (rangeEnd > rangeStart)
                                    fcGroup.Add((
                                        $"{prefix}[{j:D2}]{VectorSwizzle((offset + rangeStart) & 0x3, (offset + rangeEnd - 1) & 0x3)} (0x{shpkConstant.Id:X8})",
                                        constantIndex, rangeStart..rangeEnd, string.Empty, true, FloatConstantEditor.Default));
                            }
                        }
                        else
                        {
                            for (var i = start; i < end; i += 4)
                            {
                                fcGroup.Add(($"0x{shpkConstant.Id:X8}", constantIndex, i..Math.Min(i + 4, end), string.Empty, true,
                                    FloatConstantEditor.Default));
                            }
                        }
                    }
                }
            }

            Constants.RemoveAll(group => group.Constants.Count == 0);
            Constants.Sort((x, y) =>
            {
                if (string.Equals(x.Header, "Further Constants", StringComparison.Ordinal))
                    return 1;
                if (string.Equals(y.Header, "Further Constants", StringComparison.Ordinal))
                    return -1;

                return string.Compare(x.Header, y.Header, StringComparison.Ordinal);
            });
            // HACK the Replace makes w appear after xyz, for the cbuffer-location-based naming scheme
            foreach (var (_, group) in Constants)
            {
                group.Sort((x, y) => string.CompareOrdinal(
                    x.MonoFont ? x.Label.Replace("].w", "].{") : x.Label,
                    y.MonoFont ? y.Label.Replace("].w", "].{") : y.Label));
            }
        }

        public unsafe void BindToMaterialInstances()
        {
            UnbindFromMaterialInstances();

            var instances = MaterialInfo.FindMaterials(_edit._dalamud.Objects, FilePath);

            var foundMaterials = new HashSet<nint>();
            foreach (var materialInfo in instances)
            {
                var drawObject = (CharacterBase*)MaterialInfo.GetDrawObject(materialInfo.Type, _edit._dalamud.Objects);
                var material   = materialInfo.GetDrawObjectMaterial(drawObject);
                if (foundMaterials.Contains((nint)material))
                    continue;

                try
                {
                    MaterialPreviewers.Add(new LiveMaterialPreviewer(_edit._dalamud.Objects, materialInfo));
                    foundMaterials.Add((nint)material);
                }
                catch (InvalidOperationException)
                {
                    // Carry on without that previewer.
                }
            }

            UpdateMaterialPreview();

            if (!Mtrl.HasTable)
                return;

            foreach (var materialInfo in instances)
            {
                try
                {
                    ColorTablePreviewers.Add(new LiveColorTablePreviewer(_edit._dalamud.Objects, _edit._dalamud.Framework, materialInfo));
                }
                catch (InvalidOperationException)
                {
                    // Carry on without that previewer.
                }
            }

            UpdateColorTablePreview();
        }

        private void UnbindFromMaterialInstances()
        {
            foreach (var previewer in MaterialPreviewers)
                previewer.Dispose();
            MaterialPreviewers.Clear();

            foreach (var previewer in ColorTablePreviewers)
                previewer.Dispose();
            ColorTablePreviewers.Clear();
        }

        private unsafe void UnbindFromDrawObjectMaterialInstances(nint characterBase)
        {
            for (var i = MaterialPreviewers.Count; i-- > 0;)
            {
                var previewer = MaterialPreviewers[i];
                if ((nint)previewer.DrawObject != characterBase)
                    continue;

                previewer.Dispose();
                MaterialPreviewers.RemoveAt(i);
            }

            for (var i = ColorTablePreviewers.Count; i-- > 0;)
            {
                var previewer = ColorTablePreviewers[i];
                if ((nint)previewer.DrawObject != characterBase)
                    continue;

                previewer.Dispose();
                ColorTablePreviewers.RemoveAt(i);
            }
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

        private void UpdateMaterialPreview()
        {
            SetShaderPackageFlags(Mtrl.ShaderPackage.Flags);
            foreach (var constant in Mtrl.ShaderPackage.Constants)
            {
                var values = Mtrl.GetConstantValues(constant);
                if (values != null)
                    SetMaterialParameter(constant.Id, 0, values);
            }

            foreach (var sampler in Mtrl.ShaderPackage.Samplers)
                SetSamplerFlags(sampler.SamplerId, sampler.Flags);
        }

        public void HighlightColorTableRow(int rowIdx)
        {
            var oldRowIdx = HighlightedColorTableRow;

            if (HighlightedColorTableRow != rowIdx)
            {
                HighlightedColorTableRow = rowIdx;
                HighlightTime.Restart();
            }

            if (oldRowIdx >= 0)
                UpdateColorTableRowPreview(oldRowIdx);
            if (rowIdx >= 0)
                UpdateColorTableRowPreview(rowIdx);
        }

        public void CancelColorTableHighlight()
        {
            var rowIdx = HighlightedColorTableRow;

            HighlightedColorTableRow = -1;
            HighlightTime.Reset();

            if (rowIdx >= 0)
                UpdateColorTableRowPreview(rowIdx);
        }

        public void UpdateColorTableRowPreview(int rowIdx)
        {
            if (ColorTablePreviewers.Count == 0)
                return;

            if (!Mtrl.HasTable)
                return;

            var row = Mtrl.Table[rowIdx];
            if (Mtrl.HasDyeTable)
            {
                var stm = _edit._stainService.StmFile;
                var dye = Mtrl.DyeTable[rowIdx];
                if (stm.TryGetValue(dye.Template, _edit._stainService.StainCombo.CurrentSelection.Key, out var dyes))
                    row.ApplyDyeTemplate(dye, dyes);
            }

            if (HighlightedColorTableRow == rowIdx)
                ApplyHighlight(ref row, (float)HighlightTime.Elapsed.TotalSeconds);

            foreach (var previewer in ColorTablePreviewers)
            {
                row.AsHalves().CopyTo(previewer.ColorTable.AsSpan()
                    .Slice(LiveColorTablePreviewer.TextureWidth * 4 * rowIdx, LiveColorTablePreviewer.TextureWidth * 4));
                previewer.ScheduleUpdate();
            }
        }

        public void UpdateColorTablePreview()
        {
            if (ColorTablePreviewers.Count == 0)
                return;

            if (!Mtrl.HasTable)
                return;

            var rows = Mtrl.Table;
            if (Mtrl.HasDyeTable)
            {
                var stm         = _edit._stainService.StmFile;
                var stainId     = (StainId)_edit._stainService.StainCombo.CurrentSelection.Key;
                for (var i = 0; i < MtrlFile.ColorTable.NumRows; ++i)
                {
                    ref var row = ref rows[i];
                    var     dye = Mtrl.DyeTable[i];
                    if (stm.TryGetValue(dye.Template, stainId, out var dyes))
                        row.ApplyDyeTemplate(dye, dyes);
                }
            }

            if (HighlightedColorTableRow >= 0)
                ApplyHighlight(ref rows[HighlightedColorTableRow], (float)HighlightTime.Elapsed.TotalSeconds);

            foreach (var previewer in ColorTablePreviewers)
            {
                rows.AsHalves().CopyTo(previewer.ColorTable);
                previewer.ScheduleUpdate();
            }
        }

        private static void ApplyHighlight(ref MtrlFile.ColorTable.Row row, float time)
        {
            var level     = (MathF.Sin(time * 2.0f * MathF.PI) + 2.0f) / 3.0f / 255.0f;
            var baseColor = ColorId.InGameHighlight.Value();
            var color     = level * new Vector3(baseColor & 0xFF, (baseColor >> 8) & 0xFF, (baseColor >> 16) & 0xFF);

            row.Diffuse  = Vector3.Zero;
            row.Specular = Vector3.Zero;
            row.Emissive = color * color;
        }

        public void Update()
        {
            UpdateShaders();
            UpdateTextures();
            UpdateConstants();
        }

        public MtrlTab(ModEditWindow edit, MtrlFile file, string filePath, bool writable)
        {
            _edit                = edit;
            Mtrl                 = file;
            FilePath             = filePath;
            Writable             = writable;
            AssociatedBaseDevkit = TryLoadShpkDevkit("_base", out LoadedBaseDevkitPathName);
            LoadShpk(FindAssociatedShpk(out _, out _));
            if (writable)
            {
                _edit._gameEvents.CharacterBaseDestructor += UnbindFromDrawObjectMaterialInstances;
                BindToMaterialInstances();
            }
        }

        public void Dispose()
        {
            UnbindFromMaterialInstances();
            if (Writable)
                _edit._gameEvents.CharacterBaseDestructor -= UnbindFromDrawObjectMaterialInstances;
        }

        public bool Valid
            => ShadersKnown && Mtrl.Valid;

        public byte[] Write()
        {
            var output = Mtrl.Clone();
            output.GarbageCollect(AssociatedShpk, SamplerIds);

            return output.Write();
        }

        private sealed class DevkitShaderKeyValue
        {
            public string Label       = string.Empty;
            public string Description = string.Empty;
        }

        private sealed class DevkitShaderKey
        {
            public string                                 Label       = string.Empty;
            public string                                 Description = string.Empty;
            public Dictionary<uint, DevkitShaderKeyValue> Values      = new();
        }

        private sealed class DevkitSampler
        {
            public string Label          = string.Empty;
            public string Description    = string.Empty;
            public string DefaultTexture = string.Empty;
        }

        private enum DevkitConstantType
        {
            Hidden  = -1,
            Float   = 0,
            Integer = 1,
            Color   = 2,
            Enum    = 3,
        }

        private sealed class DevkitConstantValue
        {
            public string Label       = string.Empty;
            public string Description = string.Empty;
            public float  Value       = 0;
        }

        private sealed class DevkitConstant
        {
            public uint               Offset      = 0;
            public uint?              Length      = null;
            public string             Group       = string.Empty;
            public string             Label       = string.Empty;
            public string             Description = string.Empty;
            public DevkitConstantType Type        = DevkitConstantType.Float;

            public float? Minimum       = null;
            public float? Maximum       = null;
            public float? Speed         = null;
            public float  RelativeSpeed = 0.0f;
            public float  Factor        = 1.0f;
            public float  Bias          = 0.0f;
            public byte   Precision     = 3;
            public string Unit          = string.Empty;

            public bool SquaredRgb = false;
            public bool Clamped    = false;

            public DevkitConstantValue[] Values = Array.Empty<DevkitConstantValue>();

            public IConstantEditor? CreateEditor()
                => Type switch
                {
                    DevkitConstantType.Hidden => null,
                    DevkitConstantType.Float => new FloatConstantEditor(Minimum, Maximum, Speed ?? 0.1f, RelativeSpeed, Factor, Bias, Precision,
                        Unit),
                    DevkitConstantType.Integer => new IntConstantEditor(ToInteger(Minimum), ToInteger(Maximum), Speed ?? 0.25f, RelativeSpeed,
                        Factor, Bias, Unit),
                    DevkitConstantType.Color => new ColorConstantEditor(SquaredRgb, Clamped),
                    DevkitConstantType.Enum => new EnumConstantEditor(Array.ConvertAll(Values,
                        value => (value.Label, value.Value, value.Description))),
                    _ => FloatConstantEditor.Default,
                };

            private static int? ToInteger(float? value)
                => value.HasValue ? (int)Math.Clamp(MathF.Round(value.Value), int.MinValue, int.MaxValue) : null;
        }
    }
}

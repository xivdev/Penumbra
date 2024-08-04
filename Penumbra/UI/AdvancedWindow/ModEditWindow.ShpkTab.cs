using Dalamud.Utility;
using Newtonsoft.Json.Linq;
using OtterGui;
using OtterGui.Classes;
using Penumbra.GameData.Files;
using Penumbra.GameData.Files.ShaderStructs;
using Penumbra.GameData.Interop;
using Penumbra.GameData.Structs;
using Penumbra.UI.AdvancedWindow.Materials;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private class ShpkTab : IWritable
    {
        public readonly ShpkFile Shpk;
        public readonly string   FilePath;

        public Name  NewMaterialParamName = string.Empty;
        public short NewMaterialParamStart;
        public short NewMaterialParamEnd;

        public readonly SharedSet<uint, uint>[] FilterSystemValues;
        public readonly SharedSet<uint, uint>[] FilterSceneValues;
        public readonly SharedSet<uint, uint>[] FilterMaterialValues;
        public readonly SharedSet<uint, uint>[] FilterSubViewValues;
        public          SharedSet<uint, uint>   FilterPasses;

        public readonly int FilterMaximumPopCount;
        public          int FilterPopCount;

        public readonly FileDialogService FileDialog;

        public readonly string Header;
        public readonly string Extension;

        public ShpkTab(FileDialogService fileDialog, byte[] bytes, string filePath)
        {
            FileDialog = fileDialog;
            try
            {
                Shpk = new ShpkFile(bytes, true);
            }
            catch (NotImplementedException)
            {
                Shpk = new ShpkFile(bytes, false);
            }

            FilePath = filePath;

            Header = $"Shader Package for DirectX {(int)Shpk.DirectXVersion}";
            Extension = Shpk.DirectXVersion switch
            {
                ShpkFile.DxVersion.DirectX9  => ".cso",
                ShpkFile.DxVersion.DirectX11 => ".dxbc",
                _                            => throw new NotImplementedException(),
            };

            FilterSystemValues   = Array.ConvertAll(Shpk.SystemKeys,   key => key.Values.FullSet());
            FilterSceneValues    = Array.ConvertAll(Shpk.SceneKeys,    key => key.Values.FullSet());
            FilterMaterialValues = Array.ConvertAll(Shpk.MaterialKeys, key => key.Values.FullSet());
            FilterSubViewValues  = Array.ConvertAll(Shpk.SubViewKeys,  key => key.Values.FullSet());
            FilterPasses         = Shpk.Passes.FullSet();

            FilterMaximumPopCount = FilterPasses.Count;
            foreach (var key in Shpk.SystemKeys)
                FilterMaximumPopCount += key.Values.Count;
            foreach (var key in Shpk.SceneKeys)
                FilterMaximumPopCount += key.Values.Count;
            foreach (var key in Shpk.MaterialKeys)
                FilterMaximumPopCount += key.Values.Count;
            foreach (var key in Shpk.SubViewKeys)
                FilterMaximumPopCount += key.Values.Count;

            FilterPopCount = FilterMaximumPopCount;

            UpdateNameCache();
            Shpk.UpdateFilteredUsed(IsFilterMatch);
            Update();
        }

        [Flags]
        public enum ColorType : byte
        {
            Used         = 1,
            FilteredUsed = 2,
            Continuation = 4,
        }

        public          (string Name, string Tooltip, short Index, ColorType Color)[,] Matrix              = null!;
        public readonly List<string>                                                   MalformedParameters = new();
        public readonly HashSet<uint>                                                  UsedIds             = new(16);
        public readonly List<(string Name, short Index)>                               Orphans             = new(16);

        private readonly Dictionary<uint, Name>                    _nameCache           = [];
        private readonly Dictionary<SharedSet<uint, uint>, string> _nameSetCache        = [];
        private readonly Dictionary<SharedSet<uint, uint>, string> _nameSetWithIdsCache = [];

        public void AddNameToCache(Name name)
        {
            if (name.Value != null)
                _nameCache.TryAdd(name.Crc32, name);

            _nameSetCache.Clear();
            _nameSetWithIdsCache.Clear();
        }

        private void UpdateNameCache()
        {
            CollectResourceNames(_nameCache, Shpk.Constants);
            CollectResourceNames(_nameCache, Shpk.Samplers);
            CollectResourceNames(_nameCache, Shpk.Textures);
            CollectResourceNames(_nameCache, Shpk.Uavs);

            CollectKeyNames(_nameCache, Shpk.SystemKeys);
            CollectKeyNames(_nameCache, Shpk.SceneKeys);
            CollectKeyNames(_nameCache, Shpk.MaterialKeys);
            CollectKeyNames(_nameCache, Shpk.SubViewKeys);

            _nameSetCache.Clear();
            _nameSetWithIdsCache.Clear();
            return;

            static void CollectKeyNames(Dictionary<uint, Name> nameCache, ShpkFile.Key[] keys)
            {
                foreach (var key in keys)
                {
                    var keyName    = nameCache.TryResolve(Names.KnownNames, key.Id);
                    var valueNames = keyName.WithKnownSuffixes();
                    foreach (var value in key.Values)
                    {
                        var valueName = valueNames.TryResolve(value);
                        if (valueName.Value != null)
                            nameCache.TryAdd(value, valueName);
                    }
                }
            }

            static void CollectResourceNames(Dictionary<uint, Name> nameCache, ShpkFile.Resource[] resources)
            {
                foreach (var resource in resources)
                    nameCache.TryAdd(resource.Id, resource.Name);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Name TryResolveName(uint crc32)
            => _nameCache.TryResolve(Names.KnownNames, crc32);

        public string NameSetToString(SharedSet<uint, uint> nameSet, bool withIds = false)
        {
            var cache = withIds ? _nameSetWithIdsCache : _nameSetCache;
            if (cache.TryGetValue(nameSet, out var nameSetStr))
                return nameSetStr;

            if (withIds)
                nameSetStr = string.Join(", ", nameSet.Select(id => $"{TryResolveName(id)} (0x{id:X8})"));
            else
                nameSetStr = string.Join(", ", nameSet.Select(TryResolveName));
            cache.Add(nameSet, nameSetStr);
            return nameSetStr;
        }

        public void UpdateFilteredUsed()
        {
            Shpk.UpdateFilteredUsed(IsFilterMatch);

            var materialParams = Shpk.GetConstantById(ShpkFile.MaterialParamsConstantId);
            UpdateColors(materialParams);
        }

        public void Update()
        {
            var materialParams = Shpk.GetConstantById(ShpkFile.MaterialParamsConstantId);
            var numParameters  = ((Shpk.MaterialParamsSize + 0xFu) & ~0xFu) >> 4;
            var defaults       = Shpk.MaterialParamsDefaults != null ? (ReadOnlySpan<byte>)Shpk.MaterialParamsDefaults : [];
            var defaultFloats  = MemoryMarshal.Cast<byte, float>(defaults);
            Matrix = new (string Name, string Tooltip, short Index, ColorType Color)[numParameters, 4];

            MalformedParameters.Clear();
            UsedIds.Clear();
            foreach (var (param, idx) in Shpk.MaterialParams.WithIndex())
            {
                UsedIds.Add(param.Id);
                var iStart = param.ByteOffset >> 4;
                var jStart = (param.ByteOffset >> 2) & 3;
                var iEnd   = (param.ByteOffset + param.ByteSize - 1) >> 4;
                var jEnd   = ((param.ByteOffset + param.ByteSize - 1) >> 2) & 3;
                if ((param.ByteOffset & 0x3) != 0 || (param.ByteSize & 0x3) != 0)
                {
                    MalformedParameters.Add(
                        $"ID: {TryResolveName(param.Id)} (0x{param.Id:X8}), offset: 0x{param.ByteOffset:X4}, size: 0x{param.ByteSize:X4}");
                    continue;
                }

                if (iEnd >= numParameters)
                {
                    MalformedParameters.Add(
                        $"{MtrlTab.MaterialParamRangeName(materialParams?.Name ?? string.Empty, param.ByteOffset >> 2, param.ByteSize >> 2)} ({TryResolveName(param.Id)}, 0x{param.Id:X8})");
                    continue;
                }

                for (var i = iStart; i <= iEnd; ++i)
                {
                    var end = i == iEnd ? jEnd : 3;
                    for (var j = i == iStart ? jStart : 0; j <= end; ++j)
                    {
                        var component = (i << 2) | j;
                        var tt =
                            $"{MtrlTab.MaterialParamRangeName(materialParams?.Name ?? string.Empty, param.ByteOffset >> 2, param.ByteSize >> 2).Item1} ({TryResolveName(param.Id)}, 0x{param.Id:X8})";
                        if (component < defaultFloats.Length)
                            tt +=
                                $"\n\nDefault value: {defaultFloats[component]} ({defaults[component << 2]:X2} {defaults[(component << 2) | 1]:X2} {defaults[(component << 2) | 2]:X2} {defaults[(component << 2) | 3]:X2})";
                        Matrix[i, j] = (TryResolveName(param.Id).ToString(), tt, (short)idx, 0);
                    }
                }
            }

            UpdateOrphans(materialParams);
            UpdateColors(materialParams);
        }

        public void UpdateOrphanStart(int orphanStart)
        {
            var oldEnd = Orphans.Count > 0 ? Orphans[NewMaterialParamEnd].Index : -1;
            UpdateOrphanStart(orphanStart, oldEnd);
        }

        private void UpdateOrphanStart(int orphanStart, int oldEnd)
        {
            var count = Math.Min(NewMaterialParamEnd - NewMaterialParamStart + orphanStart + 1, Orphans.Count);
            NewMaterialParamStart = (short)orphanStart;
            var current = Orphans[NewMaterialParamStart].Index;
            for (var i = NewMaterialParamStart; i < count; ++i)
            {
                var next = Orphans[i].Index;
                if (current++ != next)
                {
                    NewMaterialParamEnd = (short)(i - 1);
                    return;
                }

                if (next == oldEnd)
                {
                    NewMaterialParamEnd = i;
                    return;
                }
            }

            NewMaterialParamEnd = (short)(count - 1);
        }

        private void UpdateOrphans(ShpkFile.Resource? materialParams)
        {
            var oldStart = Orphans.Count > 0 ? Orphans[NewMaterialParamStart].Index : -1;
            var oldEnd   = Orphans.Count > 0 ? Orphans[NewMaterialParamEnd].Index : -1;

            Orphans.Clear();
            short newMaterialParamStart = 0;
            for (var i = 0; i < Matrix.GetLength(0); ++i)
            {
                for (var j = 0; j < 4; ++j)
                {
                    if (!Matrix[i, j].Name.IsNullOrEmpty())
                        continue;

                    Matrix[i, j] = ("(none)", string.Empty, -1, 0);
                    var linear = (short)(4 * i + j);
                    if (oldStart == linear)
                        newMaterialParamStart = (short)Orphans.Count;

                    Orphans.Add(($"{materialParams?.Name ?? ShpkFile.MaterialParamsConstantName}{MtrlTab.MaterialParamName(false, linear)}",
                        linear));
                }
            }

            if (Orphans.Count == 0)
                return;

            UpdateOrphanStart(newMaterialParamStart, oldEnd);
        }

        private void UpdateColors(ShpkFile.Resource? materialParams)
        {
            var lastIndex = -1;
            for (var i = 0; i < Matrix.GetLength(0); ++i)
            {
                var usedComponents = (materialParams?.Used?[i] ?? DisassembledShader.VectorComponents.All)
                  | (materialParams?.UsedDynamically ?? 0);
                var filteredUsedComponents = (materialParams?.FilteredUsed?[i] ?? DisassembledShader.VectorComponents.All)
                  | (materialParams?.FilteredUsedDynamically ?? 0);
                for (var j = 0; j < 4; ++j)
                {
                    ColorType color = 0;
                    if (((byte)usedComponents & (1 << j)) != 0)
                        color |= ColorType.Used;
                    if (((byte)filteredUsedComponents & (1 << j)) != 0)
                        color |= ColorType.FilteredUsed;
                    if (Matrix[i, j].Index == lastIndex || Matrix[i, j].Index < 0)
                        color |= ColorType.Continuation;

                    lastIndex          = Matrix[i, j].Index;
                    Matrix[i, j].Color = color;
                }
            }
        }

        public bool IsFilterMatch(ShpkFile.Shader shader)
        {
            if (!FilterPasses.Overlaps(shader.Passes))
                return false;

            for (var i = 0; i < shader.SystemValues!.Length; ++i)
            {
                if (!FilterSystemValues[i].Overlaps(shader.SystemValues[i]))
                    return false;
            }

            for (var i = 0; i < shader.SceneValues!.Length; ++i)
            {
                if (!FilterSceneValues[i].Overlaps(shader.SceneValues[i]))
                    return false;
            }

            for (var i = 0; i < shader.MaterialValues!.Length; ++i)
            {
                if (!FilterMaterialValues[i].Overlaps(shader.MaterialValues[i]))
                    return false;
            }

            for (var i = 0; i < shader.SubViewValues!.Length; ++i)
            {
                if (!FilterSubViewValues[i].Overlaps(shader.SubViewValues[i]))
                    return false;
            }

            return true;
        }

        public bool IsFilterMatch(ShpkFile.Node node)
        {
            if (!node.Passes.Any(pass => FilterPasses.Contains(pass.Id)))
                return false;

            for (var i = 0; i < node.SystemValues!.Length; ++i)
            {
                if (!FilterSystemValues[i].Overlaps(node.SystemValues[i]))
                    return false;
            }

            for (var i = 0; i < node.SceneValues!.Length; ++i)
            {
                if (!FilterSceneValues[i].Overlaps(node.SceneValues[i]))
                    return false;
            }

            for (var i = 0; i < node.MaterialValues!.Length; ++i)
            {
                if (!FilterMaterialValues[i].Overlaps(node.MaterialValues[i]))
                    return false;
            }

            for (var i = 0; i < node.SubViewValues!.Length; ++i)
            {
                if (!FilterSubViewValues[i].Overlaps(node.SubViewValues[i]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Generates a minimal material dev-kit file for the given shader package.
        /// 
        /// This file currently only hides globally unused material constants.
        /// </summary>
        public JObject ExportDevkit()
        {
            var devkit = new JObject();

            var maybeMaterialParameter = Shpk.GetConstantById(ShpkFile.MaterialParamsConstantId);
            if (maybeMaterialParameter.HasValue)
            {
                var materialParameter      = maybeMaterialParameter.Value;
                var materialParameterUsage = new IndexSet(materialParameter.Size << 2, true);

                var used            = materialParameter.Used ?? [];
                var usedDynamically = materialParameter.UsedDynamically ?? 0;
                for (var i = 0; i < used.Length; ++i)
                {
                    for (var j = 0; j < 4; ++j)
                    {
                        if (!(used[i] | usedDynamically).HasFlag((DisassembledShader.VectorComponents)(1 << j)))
                            materialParameterUsage[(i << 2) | j] = false;
                    }
                }

                var dkConstants = new JObject();
                foreach (var param in Shpk.MaterialParams)
                {
                    // Don't handle misaligned parameters.
                    if ((param.ByteOffset & 0x3) != 0 || (param.ByteSize & 0x3) != 0)
                        continue;

                    var start  = param.ByteOffset >> 2;
                    var length = param.ByteSize >> 2;

                    // If the parameter is fully used, don't include it.
                    if (!materialParameterUsage.Indices(start, length, true).Any())
                        continue;

                    var unusedSlices = new JArray();

                    if (materialParameterUsage.Indices(start, length).Any())
                        foreach (var (rgStart, rgEnd) in materialParameterUsage.Ranges(start, length, true))
                        {
                            unusedSlices.Add(new JObject
                            {
                                ["Type"]   = "Hidden",
                                ["Offset"] = rgStart,
                                ["Length"] = rgEnd - rgStart,
                            });
                        }
                    else
                        unusedSlices.Add(new JObject
                        {
                            ["Type"] = "Hidden",
                        });

                    dkConstants[param.Id.ToString()] = unusedSlices;
                }

                devkit["Constants"] = dkConstants;
            }

            return devkit;
        }

        public bool Valid
            => Shpk.Valid;

        public byte[] Write()
            => Shpk.Write();
    }
}

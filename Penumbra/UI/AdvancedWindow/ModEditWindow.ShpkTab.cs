using Dalamud.Utility;
using Lumina.Misc;
using OtterGui;
using Penumbra.GameData.Data;
using Penumbra.GameData.Files;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private class ShpkTab : IWritable
    {
        public readonly ShpkFile Shpk;

        public string NewMaterialParamName = string.Empty;
        public uint   NewMaterialParamId   = Crc32.Get(string.Empty, 0xFFFFFFFFu);
        public short  NewMaterialParamStart;
        public short  NewMaterialParamEnd;

        public readonly FileDialogService FileDialog;

        public readonly string Header;
        public readonly string Extension;

        public ShpkTab(FileDialogService fileDialog, byte[] bytes)
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

            Header = $"Shader Package for DirectX {(int)Shpk.DirectXVersion}";
            Extension = Shpk.DirectXVersion switch
            {
                ShpkFile.DxVersion.DirectX9  => ".cso",
                ShpkFile.DxVersion.DirectX11 => ".dxbc",
                _                            => throw new NotImplementedException(),
            };
            Update();
        }

        [Flags]
        public enum ColorType : byte
        {
            Unused       = 0,
            Used         = 1,
            Continuation = 2,
        }

        public          (string Name, string Tooltip, short Index, ColorType Color)[,] Matrix              = null!;
        public readonly List<string>                                                   MalformedParameters = new();
        public readonly HashSet<uint>                                                  UsedIds             = new(16);
        public readonly List<(string Name, short Index)>                               Orphans             = new(16);

        public void Update()
        {
            var materialParams = Shpk.GetConstantById(ShpkFile.MaterialParamsConstantId);
            var numParameters  = ((Shpk.MaterialParamsSize + 0xFu) & ~0xFu) >> 4;
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
                    MalformedParameters.Add($"ID: 0x{param.Id:X8}, offset: 0x{param.ByteOffset:X4}, size: 0x{param.ByteSize:X4}");
                    continue;
                }

                if (iEnd >= numParameters)
                {
                    MalformedParameters.Add(
                        $"{MaterialParamRangeName(materialParams?.Name ?? string.Empty, param.ByteOffset >> 2, param.ByteSize >> 2)} (ID: 0x{param.Id:X8})");
                    continue;
                }

                for (var i = iStart; i <= iEnd; ++i)
                {
                    var end = i == iEnd ? jEnd : 3;
                    for (var j = i == iStart ? jStart : 0; j <= end; ++j)
                    {
                        var tt =
                            $"{MaterialParamRangeName(materialParams?.Name ?? string.Empty, param.ByteOffset >> 2, param.ByteSize >> 2).Item1} (ID: 0x{param.Id:X8})";
                        Matrix[i, j] = ($"0x{param.Id:X8}", tt, (short)idx, 0);
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

                    Orphans.Add(($"{materialParams?.Name ?? string.Empty}{MaterialParamName(false, linear)}", linear));
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
                for (var j = 0; j < 4; ++j)
                {
                    var color = ((byte)usedComponents & (1 << j)) != 0
                        ? ColorType.Used
                        : 0;
                    if (Matrix[i, j].Index == lastIndex || Matrix[i, j].Index < 0)
                        color |= ColorType.Continuation;

                    lastIndex          = Matrix[i, j].Index;
                    Matrix[i, j].Color = color;
                }
            }
        }

        public bool Valid
            => Shpk.Valid;

        public byte[] Write()
            => Shpk.Write();
    }
}

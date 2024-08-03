using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.GameData.Files.MaterialStructs;
using Penumbra.GameData.Structs;
using Penumbra.Interop.MaterialPreview;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow.Materials;

public partial class MtrlTab
{
    public readonly List<LiveMaterialPreviewer>   MaterialPreviewers        = new(4);
    public readonly List<LiveColorTablePreviewer> ColorTablePreviewers      = new(4);
    public          int                           HighlightedColorTablePair = -1;
    public readonly Stopwatch                     HighlightTime             = new();

    private void DrawMaterialLivePreviewRebind(bool disabled)
    {
        if (disabled)
            return;

        if (ImGui.Button("Reload live preview"))
            BindToMaterialInstances();

        if (MaterialPreviewers.Count != 0 || ColorTablePreviewers.Count != 0)
            return;

        ImGui.SameLine();
        using var c = ImRaii.PushColor(ImGuiCol.Text, Colors.RegexWarningBorder);
        ImUtf8.Text(
            "The current material has not been found on your character. Please check the Import from Screen tab for more information."u8);
    }

    public unsafe void BindToMaterialInstances()
    {
        UnbindFromMaterialInstances();

        var instances = MaterialInfo.FindMaterials(_resourceTreeFactory.GetLocalPlayerRelatedCharacters().Select(ch => ch.Address),
            FilePath);

        var foundMaterials = new HashSet<nint>();
        foreach (var materialInfo in instances)
        {
            var material = materialInfo.GetDrawObjectMaterial(_objects);
            if (foundMaterials.Contains((nint)material))
                continue;

            try
            {
                MaterialPreviewers.Add(new LiveMaterialPreviewer(_objects, materialInfo));
                foundMaterials.Add((nint)material);
            }
            catch (InvalidOperationException)
            {
                // Carry on without that previewer.
            }
        }

        UpdateMaterialPreview();

        if (Mtrl.Table == null)
            return;

        foreach (var materialInfo in instances)
        {
            try
            {
                ColorTablePreviewers.Add(new LiveColorTablePreviewer(_objects, _framework, materialInfo));
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

    private unsafe void UnbindFromDrawObjectMaterialInstances(CharacterBase* characterBase)
    {
        for (var i = MaterialPreviewers.Count; i-- > 0;)
        {
            var previewer = MaterialPreviewers[i];
            if (previewer.DrawObject != characterBase)
                continue;

            previewer.Dispose();
            MaterialPreviewers.RemoveAt(i);
        }

        for (var i = ColorTablePreviewers.Count; i-- > 0;)
        {
            var previewer = ColorTablePreviewers[i];
            if (previewer.DrawObject != characterBase)
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

    public void SetMaterialParameter(uint parameterCrc, Index offset, Span<byte> value)
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
            var values = Mtrl.GetConstantValue<byte>(constant);
            if (values != null)
                SetMaterialParameter(constant.Id, 0, values);
        }

        foreach (var sampler in Mtrl.ShaderPackage.Samplers)
            SetSamplerFlags(sampler.SamplerId, sampler.Flags);
    }

    public void HighlightColorTablePair(int pairIdx)
    {
        var oldPairIdx = HighlightedColorTablePair;

        if (HighlightedColorTablePair != pairIdx)
        {
            HighlightedColorTablePair = pairIdx;
            HighlightTime.Restart();
        }

        if (oldPairIdx >= 0)
        {
            UpdateColorTableRowPreview(oldPairIdx << 1);
            UpdateColorTableRowPreview((oldPairIdx << 1) | 1);
        }
        if (pairIdx >= 0)
        {
            UpdateColorTableRowPreview(pairIdx << 1);
            UpdateColorTableRowPreview((pairIdx << 1) | 1);
        }
    }

    public void CancelColorTableHighlight()
    {
        var pairIdx = HighlightedColorTablePair;

        HighlightedColorTablePair = -1;
        HighlightTime.Reset();

        if (pairIdx >= 0)
        {
            UpdateColorTableRowPreview(pairIdx << 1);
            UpdateColorTableRowPreview((pairIdx << 1) | 1);
        }
    }

    public void UpdateColorTableRowPreview(int rowIdx)
    {
        if (ColorTablePreviewers.Count == 0)
            return;

        if (Mtrl.Table == null)
            return;

        var row = Mtrl.Table switch
        {
            LegacyColorTable legacyTable => new ColorTable.Row(legacyTable[rowIdx]),
            ColorTable       table       => table[rowIdx],
            _                            => throw new InvalidOperationException($"Unsupported color table type {Mtrl.Table.GetType()}"),
        };
        if (Mtrl.DyeTable != null)
        {
            var dyeRow = Mtrl.DyeTable switch
            {
                LegacyColorDyeTable legacyDyeTable => new ColorDyeTable.Row(legacyDyeTable[rowIdx]),
                ColorDyeTable       dyeTable       => dyeTable[rowIdx],
                _                                  => throw new InvalidOperationException($"Unsupported color dye table type {Mtrl.DyeTable.GetType()}"),
            };
            if (dyeRow.Channel < StainService.ChannelCount)
            {
                StainId stainId = _stainService.GetStainCombo(dyeRow.Channel).CurrentSelection.Key;
                if (_stainService.LegacyStmFile.TryGetValue(dyeRow.Template, stainId, out var legacyDyes))
                    row.ApplyDye(dyeRow, legacyDyes);
                if (_stainService.GudStmFile.TryGetValue(dyeRow.Template, stainId, out var gudDyes))
                    row.ApplyDye(dyeRow, gudDyes);
            }
        }

        if (HighlightedColorTablePair << 1 == rowIdx)
            ApplyHighlight(ref row, ColorId.InGameHighlight, (float)HighlightTime.Elapsed.TotalSeconds);
        else if (((HighlightedColorTablePair << 1) | 1) == rowIdx)
            ApplyHighlight(ref row, ColorId.InGameHighlight2, (float)HighlightTime.Elapsed.TotalSeconds);

        foreach (var previewer in ColorTablePreviewers)
        {
            row[..].CopyTo(previewer.GetColorRow(rowIdx));
            previewer.ScheduleUpdate();
        }
    }

    public void UpdateColorTablePreview()
    {
        if (ColorTablePreviewers.Count == 0)
            return;

        if (Mtrl.Table == null)
            return;

        var rows    = new ColorTable(Mtrl.Table);
        var dyeRows = Mtrl.DyeTable != null ? ColorDyeTable.CastOrConvert(Mtrl.DyeTable) : null;
        if (dyeRows != null)
        {
            ReadOnlySpan<StainId> stainIds = [
                _stainService.StainCombo1.CurrentSelection.Key,
                _stainService.StainCombo2.CurrentSelection.Key,
            ];
            rows.ApplyDye(_stainService.LegacyStmFile, stainIds, dyeRows);
            rows.ApplyDye(_stainService.GudStmFile,    stainIds, dyeRows);
        }

        if (HighlightedColorTablePair >= 0)
        {
            ApplyHighlight(ref rows[HighlightedColorTablePair << 1],       ColorId.InGameHighlight,  (float)HighlightTime.Elapsed.TotalSeconds);
            ApplyHighlight(ref rows[(HighlightedColorTablePair << 1) | 1], ColorId.InGameHighlight2, (float)HighlightTime.Elapsed.TotalSeconds);
        }

        foreach (var previewer in ColorTablePreviewers)
        {
            rows.AsHalves().CopyTo(previewer.ColorTable);
            previewer.ScheduleUpdate();
        }
    }

    private static void ApplyHighlight(ref ColorTable.Row row, ColorId colorId, float time)
    {
        var level      = (MathF.Sin(time * 2.0f * MathF.PI) + 2.0f) / 3.0f / 255.0f;
        var baseColor  = colorId.Value();
        var color     = level * new Vector3(baseColor & 0xFF, (baseColor >> 8) & 0xFF, (baseColor >> 16) & 0xFF);
        var halfColor = (HalfColor)(color * color);
            
        row.DiffuseColor  = halfColor;
        row.SpecularColor = halfColor;
        row.EmissiveColor = halfColor;
    }
}

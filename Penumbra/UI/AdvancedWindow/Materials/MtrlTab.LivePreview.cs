using ImSharp;
using Penumbra.GameData.Files.MaterialStructs;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Hooks.Objects;
using Penumbra.Interop.MaterialPreview;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow.Materials;

public partial class MtrlTab
{
    private readonly List<LiveMaterialPreviewer>   _materialPreviewers        = new(4);
    private readonly List<LiveColorTablePreviewer> _colorTablePreviewers      = new(4);
    private          int                           _highlightedColorTableRow  = -1;
    private          int                           _highlightedColorTablePair = -1;
    private readonly Stopwatch                     _highlightTime             = new();

    private void DrawMaterialLivePreviewRebind(bool disabled)
    {
        if (disabled)
            return;

        if (Im.Button("Reload live preview"u8))
            BindToMaterialInstances();

        if (_materialPreviewers.Count is not 0 || _colorTablePreviewers.Count is not 0)
            return;

        Im.Line.Same();
        Im.Text("The current material has not been found on your character. Please check the Import from Screen tab for more information."u8,
            Colors.RegexWarningBorder);
    }

    private unsafe void BindToMaterialInstances()
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
                _materialPreviewers.Add(new LiveMaterialPreviewer(_objects, materialInfo));
                foundMaterials.Add((nint)material);
            }
            catch (InvalidOperationException)
            {
                // Carry on without that previewer.
            }
        }

        UpdateMaterialPreview();

        if (Mtrl.Table is null)
            return;

        foreach (var materialInfo in instances)
        {
            try
            {
                _colorTablePreviewers.Add(new LiveColorTablePreviewer(_objects, _framework, materialInfo));
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
        foreach (var previewer in _materialPreviewers)
            previewer.Dispose();
        _materialPreviewers.Clear();

        foreach (var previewer in _colorTablePreviewers)
            previewer.Dispose();
        _colorTablePreviewers.Clear();
    }

    private unsafe void UnbindFromDrawObjectMaterialInstances(in CharacterBaseDestructor.Arguments arguments)
    {
        for (var i = _materialPreviewers.Count; i-- > 0;)
        {
            var previewer = _materialPreviewers[i];
            if (previewer.DrawObject != arguments.CharacterBase)
                continue;

            previewer.Dispose();
            _materialPreviewers.RemoveAt(i);
        }

        for (var i = _colorTablePreviewers.Count; i-- > 0;)
        {
            var previewer = _colorTablePreviewers[i];
            if (previewer.DrawObject != arguments.CharacterBase)
                continue;

            previewer.Dispose();
            _colorTablePreviewers.RemoveAt(i);
        }
    }

    private void SetShaderPackageFlags(uint shPkFlags)
    {
        foreach (var previewer in _materialPreviewers)
            previewer.SetShaderPackageFlags(shPkFlags);
    }

    private void SetMaterialParameter(uint parameterCrc, Index offset, Span<byte> value)
    {
        foreach (var previewer in _materialPreviewers)
            previewer.SetMaterialParameter(parameterCrc, offset, value);
    }

    private void SetSamplerFlags(uint samplerCrc, uint samplerFlags)
    {
        foreach (var previewer in _materialPreviewers)
            previewer.SetSamplerFlags(samplerCrc, samplerFlags);
    }

    private void UpdateMaterialPreview()
    {
        SetShaderPackageFlags(Mtrl.ShaderPackage.Flags);
        foreach (var constant in Mtrl.ShaderPackage.Constants)
        {
            var values = Mtrl.GetConstantValue<byte>(constant);
            if (values != [])
                SetMaterialParameter(constant.Id, 0, values);
        }

        foreach (var sampler in Mtrl.ShaderPackage.Samplers)
            SetSamplerFlags(sampler.SamplerId, sampler.Flags);
    }

    private void HighlightColorTablePair(int pairIdx)
    {
        var oldPairIdx = _highlightedColorTablePair;

        if (_highlightedColorTablePair != pairIdx)
        {
            _highlightedColorTablePair = pairIdx;
            _highlightTime.Restart();
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

    private void HighlightColorTableRow(int rowIdx)
    {
        var oldRowIdx = _highlightedColorTableRow;

        if (_highlightedColorTableRow != rowIdx)
        {
            _highlightedColorTableRow = rowIdx;
            _highlightTime.Restart();
        }

        if (oldRowIdx >= 0)
            UpdateColorTableRowPreview(oldRowIdx);

        if (rowIdx >= 0)
            UpdateColorTableRowPreview(rowIdx);
    }

    private void CancelColorTableHighlight()
    {
        var rowIdx  = _highlightedColorTableRow;
        var pairIdx = _highlightedColorTablePair;

        _highlightedColorTableRow  = -1;
        _highlightedColorTablePair = -1;
        _highlightTime.Reset();

        if (rowIdx >= 0)
            UpdateColorTableRowPreview(rowIdx);

        if (pairIdx >= 0)
        {
            UpdateColorTableRowPreview(pairIdx << 1);
            UpdateColorTableRowPreview((pairIdx << 1) | 1);
        }
    }

    private void UpdateColorTableRowPreview(int rowIdx)
    {
        if (_colorTablePreviewers.Count is 0)
            return;

        if (Mtrl.Table is null)
            return;

        var row = Mtrl.Table switch
        {
            LegacyColorTable legacyTable => new ColorTableRow(legacyTable[rowIdx]),
            ColorTable table             => table[rowIdx],
            _                            => throw new InvalidOperationException($"Unsupported color table type {Mtrl.Table.GetType()}"),
        };
        if (Mtrl.DyeTable is not null)
        {
            var dyeRow = Mtrl.DyeTable switch
            {
                LegacyColorDyeTable legacyDyeTable => new ColorDyeTableRow(legacyDyeTable[rowIdx]),
                ColorDyeTable dyeTable => dyeTable[rowIdx],
                _ => throw new InvalidOperationException($"Unsupported color dye table type {Mtrl.DyeTable.GetType()}"),
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

        if (_highlightedColorTablePair << 1 == rowIdx || _highlightedColorTableRow == rowIdx)
            ApplyHighlight(ref row, ColorId.InGameHighlight, (float)_highlightTime.Elapsed.TotalSeconds);
        else if (((_highlightedColorTablePair << 1) | 1) == rowIdx)
            ApplyHighlight(ref row, ColorId.InGameHighlight2, (float)_highlightTime.Elapsed.TotalSeconds);

        foreach (var previewer in _colorTablePreviewers)
        {
            row[..].CopyTo(previewer.GetColorRow(rowIdx));
            previewer.ScheduleUpdate();
        }
    }

    private void UpdateColorTablePreview()
    {
        if (_colorTablePreviewers.Count is 0)
            return;

        if (Mtrl.Table is null)
            return;

        var rows    = new ColorTable(Mtrl.Table);
        var dyeRows = Mtrl.DyeTable is not null ? ColorDyeTable.CastOrConvert(Mtrl.DyeTable) : null;
        if (dyeRows is not null)
        {
            ReadOnlySpan<StainId> stainIds =
            [
                _stainService.StainCombo1.CurrentSelection.Key,
                _stainService.StainCombo2.CurrentSelection.Key,
            ];
            rows.ApplyDye(_stainService.LegacyStmFile, stainIds, dyeRows);
            rows.ApplyDye(_stainService.GudStmFile,    stainIds, dyeRows);
        }

        if (_highlightedColorTableRow >= 0)
            ApplyHighlight(ref rows[_highlightedColorTableRow], ColorId.InGameHighlight, (float)_highlightTime.Elapsed.TotalSeconds);

        if (_highlightedColorTablePair >= 0)
        {
            ApplyHighlight(ref rows[_highlightedColorTablePair << 1], ColorId.InGameHighlight, (float)_highlightTime.Elapsed.TotalSeconds);
            ApplyHighlight(ref rows[(_highlightedColorTablePair << 1) | 1], ColorId.InGameHighlight2,
                (float)_highlightTime.Elapsed.TotalSeconds);
        }

        foreach (var previewer in _colorTablePreviewers)
        {
            rows.AsHalves().CopyTo(previewer.ColorTable);
            previewer.ScheduleUpdate();
        }
    }

    private static void ApplyHighlight(ref ColorTableRow row, ColorId colorId, float time)
    {
        var level     = (MathF.Sin(time * 2.0f * MathF.PI) + 2.0f) / 3.0f / 255.0f;
        var baseColor = colorId.Value();
        var color     = level * new Vector3(baseColor.R, baseColor.G, baseColor.B);
        var halfColor = (HalfColor)(color * color);

        row.DiffuseColor  = halfColor;
        row.SpecularColor = halfColor;
        row.EmissiveColor = halfColor;
    }
}

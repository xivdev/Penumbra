using ImSharp;
using Penumbra.GameData.Files.MaterialStructs;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Hooks.Objects;
using Penumbra.Interop.MaterialPreview;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.FileEditing.Materials;

public partial class MaterialEditor
{
    private readonly List<LiveMaterialPreviewer>   _materialPreviewers            = new(4);
    private readonly List<LiveColorTablePreviewer> _colorTablePreviewers          = new(4);
    private          int                           _highlightedColorTableRowStart = 0;
    private          int                           _highlightedColorTableRowCount = 0;
    private          bool                          _highlightPreserve             = false;
    private readonly Stopwatch                     _highlightTime                 = new();

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

    private void HighlightColorTableRows(int start, int count)
    {
        if (count is 0 && _highlightedColorTableRowCount is 0)
            return;

        _highlightPreserve = count is not 0;

        if (start == _highlightedColorTableRowStart && count == _highlightedColorTableRowCount)
        {
            UpdateColorTableRowsPreview(start, count);
            return;
        }

        var oldStart = _highlightedColorTableRowStart;
        var oldCount = _highlightedColorTableRowCount;

        _highlightedColorTableRowStart = start;
        _highlightedColorTableRowCount = count;

        if (count is not 0)
            _highlightTime.Restart();
        else
            _highlightTime.Reset();

        if (start + count < oldStart || oldStart + oldCount < start)
        {
            UpdateColorTableRowsPreview(oldStart, oldCount);
            UpdateColorTableRowsPreview(start, count);
        }
        else
        {
            var unionStart = Math.Min(start, oldStart);
            UpdateColorTableRowsPreview(unionStart, Math.Max(start + count, oldStart + oldCount) - unionStart);
        }
    }

    private void UpdateColorTableRowsPreview(int rowStart, int rowCount)
    {
        if (rowStart is 0 && rowCount is ColorTable.NumRows)
        {
            UpdateColorTablePreview();
            return;
        }

        for (var i = 0; i < rowCount; ++i)
        {
            UpdateColorTableRowPreview(rowStart + i);
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
                StainId stainId = _stainService.GetStainCombo(dyeRow.Channel).CurrentSelection.Id;
                if (_stainService.LegacyStmFile.TryGetValue(dyeRow.Template, stainId, out var legacyDyes))
                    row.ApplyDye(dyeRow, legacyDyes);
                if (_stainService.GudStmFile.TryGetValue(dyeRow.Template, stainId, out var gudDyes))
                    row.ApplyDye(dyeRow, gudDyes);
            }
        }

        var highlightIndex = rowIdx - _highlightedColorTableRowStart;
        if (highlightIndex >= 0 && highlightIndex < _highlightedColorTableRowCount)
            ApplyHighlight(ref row, highlightIndex, (float)_highlightTime.Elapsed.TotalSeconds);

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
            Span<StainId> stainIds = stackalloc StainId[StainService.ChannelCount];
            _stainService.GetCurrentSelection(stainIds);
            rows.ApplyDye(_stainService.LegacyStmFile, stainIds, dyeRows);
            rows.ApplyDye(_stainService.GudStmFile,    stainIds, dyeRows);
        }

        for (var i = 0; i < _highlightedColorTableRowCount; ++i)
        {
            ApplyHighlight(ref rows[_highlightedColorTableRowStart + i], i, (float)_highlightTime.Elapsed.TotalSeconds);
        }

        foreach (var previewer in _colorTablePreviewers)
        {
            rows.AsHalves().CopyTo(previewer.ColorTable);
            previewer.ScheduleUpdate();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyHighlight(ref ColorTableRow row, int highlightIndex, float time)
        => ApplyHighlight(ref row, (highlightIndex & 1) is 0 ? ColorId.InGameHighlight : ColorId.InGameHighlight2, time);

    private static void ApplyHighlight(ref ColorTableRow row, ColorId colorId, float time)
    {
        var level     = (MathF.Sin(time * 2.0f * MathF.PI) + 2.0f) / 3.0f;
        var baseColor = colorId.Vector;
        var color     = level * new Vector3(baseColor.X, baseColor.Y, baseColor.Z);
        var halfColor = (HalfColor)(color * color);

        row.DiffuseColor  = halfColor;
        row.SpecularColor = halfColor;
        row.EmissiveColor = halfColor;
    }

    private ref struct AutoHighlightCancellation : IDisposable
    {
        private readonly MaterialEditor _owner;

        public AutoHighlightCancellation(MaterialEditor owner)
        {
            _owner = owner;

            owner._highlightPreserve = false;
        }

        public void Dispose()
        {
            if (!_owner._highlightPreserve)
                _owner.HighlightColorTableRows(0, 0);
        }
    }
}

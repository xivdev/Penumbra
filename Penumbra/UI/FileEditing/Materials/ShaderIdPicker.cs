using ImSharp;
using Luna;
using Penumbra.GameData.Files;
using Penumbra.GameData.Interop;

namespace Penumbra.UI.FileEditing.Materials;

public sealed class ShaderIdPicker : IUiService, IEditor<float>
{
    // This only supports the Chara SPM for now, like TextureArraySlicePickers.
    // TODO Figure out how to determine whether a material is Bg, Chara or Common (maybe using the ShPk?) and add support here and in TASP.

    private readonly ShaderParameterAccessor _spmAccessor;

    public readonly IEditor<byte> Picker;

    public ShaderIdPicker(ShaderParameterAccessor spmAccessor)
    {
        _spmAccessor = spmAccessor;
        Picker       = ((IEditor<float>)this).Reinterpreting<byte>();
    }

    public bool DrawShaderIdPicker(ReadOnlySpan<byte> label, ReadOnlySpan<byte> description, ref byte value)
    {
        var tmp    = value;
        var result = Im.Drag(label, ref tmp, "%d"u8, byte.MinValue, byte.MaxValue, 0.25f, SliderFlags.AlwaysClamp);
        if (Im.Item.Hovered(HoveredFlags.AllowWhenDisabled))
        {
            using var tooltip = Im.Tooltip.Begin();
            if (!description.IsEmpty)
                Im.Text(description);

            var spm = _spmAccessor.CharaSpmFile;
            if (spm.RowDictionary.TryGetValue((SpmFile.Table.Chara, tmp), out var rowIndex) && !spm.Rows[rowIndex].IsBlank)
            {
                if (!description.IsEmpty)
                    Im.Separator();

                DrawShaderParameters(spm, rowIndex);
            }
        }

        if (!result || tmp == value)
            return false;

        value = tmp;
        return true;
    }

    private static void DrawShaderParameters(SpmFile spm, int rowIndex)
    {
        var maxNameWidth = 0.0f;
        foreach (var column in spm.Columns)
        {
            if (column.ConstantValue.HasValue)
                continue;

            maxNameWidth = MathF.Max(maxNameWidth, Im.Font.CalculateSize(column.Name.ToNameU8()).X);
        }

        var values = spm.Rows[rowIndex].Values;
        foreach (var (index, column) in spm.Columns.Index())
        {
            if (column.ConstantValue.HasValue)
                continue;

            var name  = column.Name.ToNameU8();
            var width = Im.Font.CalculateSize(name).X;
            Im.Cursor.X += maxNameWidth - width;
            Im.Text(name);
            Im.Line.Same();
            Im.Text(values[index].ToString(column.Type, CultureInfo.CurrentCulture));
        }
    }

    bool IEditor<float>.Draw(Span<float> values, bool disabled)
    {
        var helper = Editors.PrepareMultiComponent(values.Length);
        var ret    = false;

        for (var valueIdx = 0; valueIdx < values.Length; ++valueIdx)
        {
            helper.SetupComponent(valueIdx);

            var value = byte.CreateSaturating(MathF.Round(values[valueIdx]));
            if (disabled)
            {
                using var _ = Im.Disabled();
                DrawShaderIdPicker(helper.Id, default, ref value);
            }
            else
            {
                if (DrawShaderIdPicker(helper.Id, default, ref value))
                {
                    values[valueIdx] = value;
                    ret              = true;
                }
            }
        }

        return ret;
    }
}

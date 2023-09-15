using System.Globalization;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using Penumbra.GameData;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private interface IConstantEditor
    {
        bool Draw(Span<float> values, bool disabled);
    }

    private sealed class FloatConstantEditor : IConstantEditor
    {
        public static readonly FloatConstantEditor Default = new(null, null, 0.1f, 0.0f, 1.0f, 0.0f, 3, string.Empty);

        private readonly float? _minimum;
        private readonly float? _maximum;
        private readonly float  _speed;
        private readonly float  _relativeSpeed;
        private readonly float  _factor;
        private readonly float  _bias;
        private readonly string _format;

        public FloatConstantEditor(float? minimum, float? maximum, float speed, float relativeSpeed, float factor, float bias, byte precision,
            string unit)
        {
            _minimum       = minimum;
            _maximum       = maximum;
            _speed         = speed;
            _relativeSpeed = relativeSpeed;
            _factor        = factor;
            _bias          = bias;
            _format        = $"%.{Math.Min(precision, (byte)9)}f";
            if (unit.Length > 0)
                _format = $"{_format} {unit.Replace("%", "%%")}";
        }

        public bool Draw(Span<float> values, bool disabled)
        {
            var spacing    = ImGui.GetStyle().ItemInnerSpacing.X;
            var fieldWidth = (ImGui.CalcItemWidth() - (values.Length - 1) * spacing) / values.Length;

            var ret = false;

            // Not using DragScalarN because of _relativeSpeed and other points of lost flexibility.
            for (var valueIdx = 0; valueIdx < values.Length; ++valueIdx)
            {
                if (valueIdx > 0)
                    ImGui.SameLine(0.0f, spacing);

                ImGui.SetNextItemWidth(MathF.Round(fieldWidth * (valueIdx + 1)) - MathF.Round(fieldWidth * valueIdx));

                var value = (values[valueIdx] - _bias) / _factor;
                if (disabled)
                {
                    ImGui.DragFloat($"##{valueIdx}", ref value, Math.Max(_speed, value * _relativeSpeed), value, value, _format);
                }
                else
                {
                    if (ImGui.DragFloat($"##{valueIdx}", ref value, Math.Max(_speed, value * _relativeSpeed), _minimum ?? 0.0f,
                            _maximum ?? 0.0f, _format))
                    {
                        values[valueIdx] = Clamp(value) * _factor + _bias;
                        ret              = true;
                    }
                }
            }

            return ret;
        }

        private float Clamp(float value)
            => Math.Clamp(value, _minimum ?? float.NegativeInfinity, _maximum ?? float.PositiveInfinity);
    }

    private sealed class IntConstantEditor : IConstantEditor
    {
        private readonly int?   _minimum;
        private readonly int?   _maximum;
        private readonly float  _speed;
        private readonly float  _relativeSpeed;
        private readonly float  _factor;
        private readonly float  _bias;
        private readonly string _format;

        public IntConstantEditor(int? minimum, int? maximum, float speed, float relativeSpeed, float factor, float bias, string unit)
        {
            _minimum       = minimum;
            _maximum       = maximum;
            _speed         = speed;
            _relativeSpeed = relativeSpeed;
            _factor        = factor;
            _bias          = bias;
            _format        = "%d";
            if (unit.Length > 0)
                _format = $"{_format} {unit.Replace("%", "%%")}";
        }

        public bool Draw(Span<float> values, bool disabled)
        {
            var spacing    = ImGui.GetStyle().ItemInnerSpacing.X;
            var fieldWidth = (ImGui.CalcItemWidth() - (values.Length - 1) * spacing) / values.Length;

            var ret = false;

            // Not using DragScalarN because of _relativeSpeed and other points of lost flexibility.
            for (var valueIdx = 0; valueIdx < values.Length; ++valueIdx)
            {
                if (valueIdx > 0)
                    ImGui.SameLine(0.0f, spacing);

                ImGui.SetNextItemWidth(MathF.Round(fieldWidth * (valueIdx + 1)) - MathF.Round(fieldWidth * valueIdx));

                var value = (int)Math.Clamp(MathF.Round((values[valueIdx] - _bias) / _factor), int.MinValue, int.MaxValue);
                if (disabled)
                {
                    ImGui.DragInt($"##{valueIdx}", ref value, Math.Max(_speed, value * _relativeSpeed), value, value, _format);
                }
                else
                {
                    if (ImGui.DragInt($"##{valueIdx}", ref value, Math.Max(_speed, value * _relativeSpeed), _minimum ?? 0, _maximum ?? 0,
                            _format))
                    {
                        values[valueIdx] = Clamp(value) * _factor + _bias;
                        ret              = true;
                    }
                }
            }

            return ret;
        }

        private int Clamp(int value)
            => Math.Clamp(value, _minimum ?? int.MinValue, _maximum ?? int.MaxValue);
    }

    private sealed class ColorConstantEditor : IConstantEditor
    {
        private readonly bool _squaredRgb;
        private readonly bool _clamped;

        public ColorConstantEditor(bool squaredRgb, bool clamped)
        {
            _squaredRgb = squaredRgb;
            _clamped    = clamped;
        }

        public bool Draw(Span<float> values, bool disabled)
        {
            switch (values.Length)
            {
                case 3:
                {
                    var value = new Vector3(values);
                    if (_squaredRgb)
                        value = PseudoSqrtRgb(value);
                    if (!ImGui.ColorEdit3("##0", ref value, ImGuiColorEditFlags.Float | (_clamped ? 0 : ImGuiColorEditFlags.HDR)) || disabled)
                        return false;

                    if (_squaredRgb)
                        value = PseudoSquareRgb(value);
                    if (_clamped)
                        value = Vector3.Clamp(value, Vector3.Zero, Vector3.One);
                    value.CopyTo(values);
                    return true;
                }
                case 4:
                {
                    var value = new Vector4(values);
                    if (_squaredRgb)
                        value = PseudoSqrtRgb(value);
                    if (!ImGui.ColorEdit4("##0", ref value,
                            ImGuiColorEditFlags.Float | ImGuiColorEditFlags.AlphaPreviewHalf | (_clamped ? 0 : ImGuiColorEditFlags.HDR))
                     || disabled)
                        return false;

                    if (_squaredRgb)
                        value = PseudoSquareRgb(value);
                    if (_clamped)
                        value = Vector4.Clamp(value, Vector4.Zero, Vector4.One);
                    value.CopyTo(values);
                    return true;
                }
                default: return FloatConstantEditor.Default.Draw(values, disabled);
            }
        }
    }

    private sealed class EnumConstantEditor : IConstantEditor
    {
        private readonly IReadOnlyList<(string Label, float Value, string Description)> _values;

        public EnumConstantEditor(IReadOnlyList<(string Label, float Value, string Description)> values)
            => _values = values;

        public bool Draw(Span<float> values, bool disabled)
        {
            var spacing    = ImGui.GetStyle().ItemInnerSpacing.X;
            var fieldWidth = (ImGui.CalcItemWidth() - (values.Length - 1) * spacing) / values.Length;

            var ret = false;

            for (var valueIdx = 0; valueIdx < values.Length; ++valueIdx)
            {
                using var id = ImRaii.PushId(valueIdx);
                if (valueIdx > 0)
                    ImGui.SameLine(0.0f, spacing);

                ImGui.SetNextItemWidth(MathF.Round(fieldWidth * (valueIdx + 1)) - MathF.Round(fieldWidth * valueIdx));

                var currentValue = values[valueIdx];
                var currentLabel = _values.FirstOrNull(v => v.Value == currentValue)?.Label
                 ?? currentValue.ToString(CultureInfo.CurrentCulture);
                ret = disabled
                    ? ImGui.InputText(string.Empty, ref currentLabel, (uint)currentLabel.Length, ImGuiInputTextFlags.ReadOnly)
                    : DrawCombo(currentLabel, ref values[valueIdx]);
            }

            return ret;
        }

        private bool DrawCombo(string label, ref float currentValue)
        {
            using var c = ImRaii.Combo(string.Empty, label);
            if (!c)
                return false;

            var ret = false;
            foreach (var (valueLabel, value, valueDescription) in _values)
            {
                if (ImGui.Selectable(valueLabel, value == currentValue))
                {
                    currentValue = value;
                    ret          = true;
                }

                if (valueDescription.Length > 0)
                    ImGuiUtil.SelectableHelpMarker(valueDescription);
            }

            return ret;
        }
    }
}

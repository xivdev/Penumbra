using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using Penumbra.GameData;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private interface IConstantEditor
    {
        bool Draw(Span<float> values, bool disabled, float editorWidth);
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

        public FloatConstantEditor(float? minimum, float? maximum, float speed, float relativeSpeed, float factor, float bias, byte precision, string unit)
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

        public bool Draw(Span<float> values, bool disabled, float editorWidth)
        {
            var fieldWidth = (editorWidth - (values.Length - 1) * ImGui.GetStyle().ItemSpacing.X) / values.Length;

            var ret = false;

            for (var valueIdx = 0; valueIdx < values.Length; ++valueIdx)
            {
                if (valueIdx > 0)
                    ImGui.SameLine();

                ImGui.SetNextItemWidth(MathF.Round(fieldWidth * (valueIdx + 1)) - MathF.Round(fieldWidth * valueIdx));

                var value = (values[valueIdx] - _bias) / _factor;
                if (disabled)
                    ImGui.DragFloat($"##{valueIdx}", ref value, Math.Max(_speed, value * _relativeSpeed), value, value, _format);
                else
                {
                    if (ImGui.DragFloat($"##{valueIdx}", ref value, Math.Max(_speed, value * _relativeSpeed), _minimum ?? 0.0f, _maximum ?? 0.0f, _format))
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

        public bool Draw(Span<float> values, bool disabled, float editorWidth)
        {
            var fieldWidth = (editorWidth - (values.Length - 1) * ImGui.GetStyle().ItemSpacing.X) / values.Length;

            var ret = false;

            for (var valueIdx = 0; valueIdx < values.Length; ++valueIdx)
            {
                if (valueIdx > 0)
                    ImGui.SameLine();

                ImGui.SetNextItemWidth(MathF.Round(fieldWidth * (valueIdx + 1)) - MathF.Round(fieldWidth * valueIdx));

                var value = (int)Math.Clamp(MathF.Round((values[valueIdx] - _bias) / _factor), int.MinValue, int.MaxValue);
                if (disabled)
                    ImGui.DragInt($"##{valueIdx}", ref value, Math.Max(_speed, value * _relativeSpeed), value, value, _format);
                else
                {
                    if (ImGui.DragInt($"##{valueIdx}", ref value, Math.Max(_speed, value * _relativeSpeed), _minimum ?? 0, _maximum ?? 0, _format))
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

        public bool Draw(Span<float> values, bool disabled, float editorWidth)
        {
            if (values.Length == 3)
            {
                ImGui.SetNextItemWidth(editorWidth);
                var value = new Vector3(values);
                if (_squaredRgb)
                    value = Vector3.SquareRoot(value);
                if (ImGui.ColorEdit3("##0", ref value) && !disabled)
                {
                    if (_squaredRgb)
                        value *= value;
                    if (_clamped)
                        value = Vector3.Clamp(value, Vector3.Zero, Vector3.One);
                    value.CopyTo(values);
                    return true;
                }

                return false;
            }
            else if (values.Length == 4)
            {
                ImGui.SetNextItemWidth(editorWidth);
                var value = new Vector4(values);
                if (_squaredRgb)
                    value = new Vector4(MathF.Sqrt(value.X), MathF.Sqrt(value.Y), MathF.Sqrt(value.Z), value.W);
                if (ImGui.ColorEdit4("##0", ref value) && !disabled)
                {
                    if (_squaredRgb)
                        value *= new Vector4(value.X, value.Y, value.Z, 1.0f);
                    if (_clamped)
                        value = Vector4.Clamp(value, Vector4.Zero, Vector4.One);
                    value.CopyTo(values);
                    return true;
                }

                return false;
            }
            else
                return FloatConstantEditor.Default.Draw(values, disabled, editorWidth);
        }
    }

    private sealed class EnumConstantEditor : IConstantEditor
    {
        private readonly IReadOnlyList<(string Label, float Value, string Description)> _values;

        public EnumConstantEditor(IReadOnlyList<(string Label, float Value, string Description)> values)
        {
            _values = values;
        }

        public bool Draw(Span<float> values, bool disabled, float editorWidth)
        {
            var fieldWidth = (editorWidth - (values.Length - 1) * ImGui.GetStyle().ItemSpacing.X) / values.Length;

            var ret = false;

            for (var valueIdx = 0; valueIdx < values.Length; ++valueIdx)
            {
                if (valueIdx > 0)
                    ImGui.SameLine();

                ImGui.SetNextItemWidth(MathF.Round(fieldWidth * (valueIdx + 1)) - MathF.Round(fieldWidth * valueIdx));

                var currentValue = values[valueIdx];
                var (currentLabel, _, currentDescription) = _values.FirstOrNull(v => v.Value == currentValue) ?? (currentValue.ToString(), currentValue, string.Empty);
                if (disabled)
                    ImGui.InputText($"##{valueIdx}", ref currentLabel, (uint)currentLabel.Length, ImGuiInputTextFlags.ReadOnly);
                else
                {
                    using var c = ImRaii.Combo($"##{valueIdx}", currentLabel);
                    {
                        if (c)
                            foreach (var (valueLabel, value, valueDescription) in _values)
                            {
                                if (ImGui.Selectable(valueLabel, value == currentValue))
                                {
                                    values[valueIdx] = value;
                                    ret              = true;
                                }

                                if (valueDescription.Length > 0)
                                    ImGuiUtil.SelectableHelpMarker(valueDescription);
                            }
                    }
                }
            }

            return ret;
        }
    }
}

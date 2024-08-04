using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using OtterGui.Text.Widget.Editors;
using Penumbra.String.Classes;
using static Penumbra.GameData.Files.ShpkFile;

namespace Penumbra.UI.AdvancedWindow.Materials;

public partial class MtrlTab
{
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
        => TryGetShpkDevkitData<T>(_associatedShpkDevkit,  _loadedShpkDevkitPathName, category, id, mayVary)
         ?? TryGetShpkDevkitData<T>(_associatedBaseDevkit, _loadedBaseDevkitPathName, category, id, mayVary);

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
                    .Select(key => Mtrl.GetShaderKey(key)?.Value ?? _associatedShpk!.GetMaterialKeyById(key)!.Value.DefaultValue));
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

    [UsedImplicitly]
    private sealed class DevkitShaderKeyValue
    {
        public string Label       = string.Empty;
        public string Description = string.Empty;
    }

    [UsedImplicitly]
    private sealed class DevkitShaderKey
    {
        public string                                 Label       = string.Empty;
        public string                                 Description = string.Empty;
        public Dictionary<uint, DevkitShaderKeyValue> Values      = [];
    }

    [UsedImplicitly]
    private sealed class DevkitSampler
    {
        public string Label          = string.Empty;
        public string Description    = string.Empty;
        public string DefaultTexture = string.Empty;
    }

    private enum DevkitConstantType
    {
        Hidden = -1,
        Float  = 0,

        /// <summary> Integer encoded as a float. </summary>
        Integer = 1,
        Color = 2,
        Enum  = 3,

        /// <summary> Native integer. </summary>
        Int32 = 4,
        Int32Enum      = 5,
        Int8           = 6,
        Int8Enum       = 7,
        Int16          = 8,
        Int16Enum      = 9,
        Int64          = 10,
        Int64Enum      = 11,
        Half           = 12,
        Double         = 13,
        TileIndex      = 14,
        SphereMapIndex = 15,
    }

    [UsedImplicitly]
    private sealed class DevkitConstantValue
    {
        public string Label       = string.Empty;
        public string Description = string.Empty;
        public double Value       = 0;
    }

    [UsedImplicitly]
    private sealed class DevkitConstant
    {
        public uint               Offset      = 0;
        public uint?              Length      = null;
        public uint?              ByteOffset  = null;
        public uint?              ByteSize    = null;
        public string             Group       = string.Empty;
        public string             Label       = string.Empty;
        public string             Description = string.Empty;
        public DevkitConstantType Type        = DevkitConstantType.Float;

        public float? Minimum       = null;
        public float? Maximum       = null;
        public float  Step          = 0.0f;
        public float  StepFast      = 0.0f;
        public float? Speed         = null;
        public float  RelativeSpeed = 0.0f;
        public float  Exponent      = 1.0f;
        public float  Factor        = 1.0f;
        public float  Bias          = 0.0f;
        public byte   Precision     = 3;
        public bool   Hex           = false;
        public bool   Slider        = true;
        public bool   Drag          = true;
        public string Unit          = string.Empty;

        public bool SquaredRgb = false;
        public bool Clamped    = false;

        public DevkitConstantValue[] Values = [];

        public uint EffectiveByteOffset
            => ByteOffset ?? Offset * ValueSize;

        public uint? EffectiveByteSize
            => ByteSize ?? Length * ValueSize;

        public unsafe uint ValueSize
            => Type switch
            {
                DevkitConstantType.Hidden         => sizeof(byte),
                DevkitConstantType.Float          => sizeof(float),
                DevkitConstantType.Integer        => sizeof(float),
                DevkitConstantType.Color          => sizeof(float),
                DevkitConstantType.Enum           => sizeof(float),
                DevkitConstantType.Int32          => sizeof(int),
                DevkitConstantType.Int32Enum      => sizeof(int),
                DevkitConstantType.Int8           => sizeof(byte),
                DevkitConstantType.Int8Enum       => sizeof(byte),
                DevkitConstantType.Int16          => sizeof(short),
                DevkitConstantType.Int16Enum      => sizeof(short),
                DevkitConstantType.Int64          => sizeof(long),
                DevkitConstantType.Int64Enum      => sizeof(long),
                DevkitConstantType.Half           => (uint)sizeof(Half),
                DevkitConstantType.Double         => sizeof(double),
                DevkitConstantType.TileIndex      => sizeof(float),
                DevkitConstantType.SphereMapIndex => sizeof(float),
                _                                 => sizeof(float),
            };

        public IEditor<byte>? CreateEditor(MaterialTemplatePickers? materialTemplatePickers)
            => Type switch
            {
                DevkitConstantType.Hidden         => null,
                DevkitConstantType.Float          => CreateFloatEditor<float>().AsByteEditor(),
                DevkitConstantType.Integer        => CreateIntegerEditor<int>().IntAsFloatEditor().AsByteEditor(),
                DevkitConstantType.Color          => ColorEditor.Get(!Clamped).WithExponent(SquaredRgb ? 2.0f : 1.0f).AsByteEditor(),
                DevkitConstantType.Enum           => CreateEnumEditor(float.CreateSaturating).AsByteEditor(),
                DevkitConstantType.Int32          => CreateIntegerEditor<int>().AsByteEditor(),
                DevkitConstantType.Int32Enum      => CreateEnumEditor(ToInteger<int>).AsByteEditor(),
                DevkitConstantType.Int8           => CreateIntegerEditor<byte>(),
                DevkitConstantType.Int8Enum       => CreateEnumEditor(ToInteger<byte>),
                DevkitConstantType.Int16          => CreateIntegerEditor<short>().AsByteEditor(),
                DevkitConstantType.Int16Enum      => CreateEnumEditor(ToInteger<short>).AsByteEditor(),
                DevkitConstantType.Int64          => CreateIntegerEditor<long>().AsByteEditor(),
                DevkitConstantType.Int64Enum      => CreateEnumEditor(ToInteger<long>).AsByteEditor(),
                DevkitConstantType.Half           => CreateFloatEditor<Half>().AsByteEditor(),
                DevkitConstantType.Double         => CreateFloatEditor<double>().AsByteEditor(),
                DevkitConstantType.TileIndex      => materialTemplatePickers?.TileIndexPicker ?? ConstantEditors.DefaultIntAsFloat,
                DevkitConstantType.SphereMapIndex => materialTemplatePickers?.SphereMapIndexPicker ?? ConstantEditors.DefaultIntAsFloat,
                _                                 => ConstantEditors.DefaultFloat,
            };

        private IEditor<T> CreateIntegerEditor<T>()
            where T : unmanaged, INumber<T>
            => ((Drag || Slider) && !Hex
                    ? Drag
                        ? (IEditor<T>)DragEditor<T>.CreateInteger(ToInteger<T>(Minimum), ToInteger<T>(Maximum), Speed ?? 0.25f, RelativeSpeed,
                            Unit, 0)
                        : SliderEditor<T>.CreateInteger(ToInteger<T>(Minimum) ?? default, ToInteger<T>(Maximum) ?? default, Unit, 0)
                    : InputEditor<T>.CreateInteger(ToInteger<T>(Minimum), ToInteger<T>(Maximum), ToInteger<T>(Step), ToInteger<T>(StepFast),
                        Hex, Unit, 0))
                .WithFactorAndBias(ToInteger<T>(Factor), ToInteger<T>(Bias));

        private IEditor<T> CreateFloatEditor<T>()
            where T : unmanaged, INumber<T>, IPowerFunctions<T>
            => (Drag || Slider
                    ? Drag
                        ? (IEditor<T>)DragEditor<T>.CreateFloat(ToFloat<T>(Minimum), ToFloat<T>(Maximum), Speed ?? 0.1f, RelativeSpeed,
                            Precision, Unit, 0)
                        : SliderEditor<T>.CreateFloat(ToFloat<T>(Minimum) ?? default, ToFloat<T>(Maximum) ?? default, Precision, Unit, 0)
                    : InputEditor<T>.CreateFloat(ToFloat<T>(Minimum), ToFloat<T>(Maximum), T.CreateSaturating(Step),
                        T.CreateSaturating(StepFast), Precision, Unit, 0))
                .WithExponent(T.CreateSaturating(Exponent))
                .WithFactorAndBias(T.CreateSaturating(Factor), T.CreateSaturating(Bias));

        private EnumEditor<T> CreateEnumEditor<T>(Func<double, T> convertValue)
            where T : unmanaged, IUtf8SpanFormattable, IEqualityOperators<T, T, bool>
            => new(Array.ConvertAll(Values, value => (ToUtf8(value.Label), convertValue(value.Value), ToUtf8(value.Description))));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T ToInteger<T>(float value) where T : struct, INumberBase<T>
            => T.CreateSaturating(MathF.Round(value));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T ToInteger<T>(double value) where T : struct, INumberBase<T>
            => T.CreateSaturating(Math.Round(value));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T? ToInteger<T>(float? value) where T : struct, INumberBase<T>
            => value.HasValue ? ToInteger<T>(value.Value) : null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T? ToFloat<T>(float? value) where T : struct, INumberBase<T>
            => value.HasValue ? T.CreateSaturating(value.Value) : null;

        private static ReadOnlyMemory<byte> ToUtf8(string value)
            => Encoding.UTF8.GetBytes(value);
    }
}

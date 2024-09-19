using Newtonsoft.Json;

namespace Penumbra.Mods.Settings;

[JsonConverter(typeof(Converter))]
public readonly record struct ModPriority(int Value) :
    IComparisonOperators<ModPriority, ModPriority, bool>,
    IAdditionOperators<ModPriority, ModPriority, ModPriority>,
    IAdditionOperators<ModPriority, int, ModPriority>,
    ISubtractionOperators<ModPriority, ModPriority, ModPriority>,
    ISubtractionOperators<ModPriority, int, ModPriority>,
    IIncrementOperators<ModPriority>,
    IComparable<ModPriority>
{
    public static readonly ModPriority Default  = new(0);
    public static readonly ModPriority MaxValue = new(int.MaxValue);

    public bool IsDefault
        => Value == Default.Value;

    public Setting AsSetting
        => new((uint)Value);

    public ModPriority Max(ModPriority other)
        => this < other ? other : this;

    public override string ToString()
        => Value.ToString();

    private class Converter : JsonConverter<ModPriority>
    {
        public override void WriteJson(JsonWriter writer, ModPriority value, JsonSerializer serializer)
            => serializer.Serialize(writer, value.Value);

        public override ModPriority ReadJson(JsonReader reader, Type objectType, ModPriority existingValue, bool hasExistingValue,
            JsonSerializer serializer)
            => new(serializer.Deserialize<int>(reader));
    }

    public static bool operator >(ModPriority left, ModPriority right)
        => left.Value > right.Value;

    public static bool operator >=(ModPriority left, ModPriority right)
        => left.Value >= right.Value;

    public static bool operator <(ModPriority left, ModPriority right)
        => left.Value < right.Value;

    public static bool operator <=(ModPriority left, ModPriority right)
        => left.Value <= right.Value;

    public static ModPriority operator +(ModPriority left, ModPriority right)
        => new(left.Value + right.Value);

    public static ModPriority operator +(ModPriority left, int right)
        => new(left.Value + right);

    public static ModPriority operator -(ModPriority left, ModPriority right)
        => new(left.Value - right.Value);

    public static ModPriority operator -(ModPriority left, int right)
        => new(left.Value - right);

    public static ModPriority operator ++(ModPriority value)
        => new(value.Value + 1);

    public int CompareTo(ModPriority other)
        => Value.CompareTo(other.Value);

    public const int HiddenMin = -84037;
    public const int HiddenMax = HiddenMin + 1000;

    public bool IsHidden
        => Value is > HiddenMin and < HiddenMax;
}

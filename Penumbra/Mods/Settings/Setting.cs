using Newtonsoft.Json;
using OtterGui;

namespace Penumbra.Mods.Settings;

[JsonConverter(typeof(Converter))]
public readonly record struct Setting(ulong Value)
{
    public static readonly Setting Zero       = new(0);
    public static readonly Setting True       = new(1);
    public static readonly Setting False      = new(0);
    public static readonly Setting Indefinite = new(ulong.MaxValue);

    public static Setting Multi(int idx)
        => new(1ul << idx);

    public static Setting Single(int idx)
        => new(Math.Max(0ul, (ulong)idx));

    public static Setting operator |(Setting lhs, Setting rhs)
        => new(lhs.Value | rhs.Value);

    public int AsIndex
        => (int)Math.Clamp(Value, 0ul, int.MaxValue);

    public bool HasFlag(int idx)
        => idx >= 0 && (Value & (1ul << idx)) != 0;

    public Setting MoveBit(int idx1, int idx2)
        => new(Functions.MoveBit(Value, idx1, idx2));

    public Setting RemoveBit(int idx)
        => new(Functions.RemoveBit(Value, idx));

    public Setting SetBit(int idx, bool value)
        => new(value ? Value | (1ul << idx) : Value & ~(1ul << idx));

    public static Setting AllBits(int count)
        => new((1ul << Math.Clamp(count, 0, 63)) - 1);

    public Setting TurnMulti(int count)
        => new(Math.Max((ulong)Math.Min(count - 1, BitOperations.TrailingZeroCount(Value)), 0));

    public Setting RemoveSingle(int singleIdx)
    {
        var settingIndex = AsIndex;
        if (settingIndex >= singleIdx)
            return settingIndex > 1 ? Single(settingIndex - 1) : Zero;

        return this;
    }

    public Setting MoveSingle(int singleIdxFrom, int singleIdxTo)
    {
        var currentIndex = AsIndex;
        if (currentIndex == singleIdxFrom)
            return Single(singleIdxTo);

        if (singleIdxFrom < singleIdxTo)
        {
            if (currentIndex > singleIdxFrom && currentIndex <= singleIdxTo)
                return Single(currentIndex - 1);
        }
        else if (currentIndex < singleIdxFrom && currentIndex >= singleIdxTo)
        {
            return Single(currentIndex + 1);
        }

        return this;
    }

    public ModPriority AsPriority
        => new((int)(Value & 0xFFFFFFFF));

    public static Setting FromBool(bool value)
        => value ? True : False;

    public bool AsBool
        => Value != 0;

    private class Converter : JsonConverter<Setting>
    {
        public override void WriteJson(JsonWriter writer, Setting value, JsonSerializer serializer)
            => serializer.Serialize(writer, value.Value);

        public override Setting ReadJson(JsonReader reader, Type objectType, Setting existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            try
            {
                return new Setting(serializer.Deserialize<ulong>(reader));
            }
            catch (Exception e)
            {
                Penumbra.Log.Warning($"Could not deserialize setting {reader.Value} to unsigned long:\n{e}");
                try
                {
                    return new Setting((ulong)serializer.Deserialize<long>(reader));
                }
                catch
                {
                    Penumbra.Log.Warning($"Could not deserialize setting {reader.Value} to long:\n{e}");
                    return Zero;
                }
            }
        }
    }
}

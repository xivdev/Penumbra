using System.Text.Json;
using Luna;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;

namespace Penumbra.Mods.Groups;

public readonly record struct ModSettingContext(Mod Mod, ModSettings Settings);

public sealed class AllSettingsCondition(params IReadOnlyCollection<Guid> options) : MultiSettingCondition(options)
{
    protected override ReadOnlySpan<byte> Type
        => "AllSettings"u8;

    public override ICondition<ModSettingContext> DeepCopy()
        => new AllSettingsCondition(this);

    public override bool Evaluate(in ModSettingContext context)
    {
        foreach (var optionId in this)
        {
            if (!context.Mod.SubObjects.TryGetValue(optionId, out var o) || o is not IModOption option)
                return false;

            var group   = context.Mod.Groups[option.GroupIndex];
            var setting = context.Settings.IsEmpty ? group.DefaultSettings : context.Settings.Settings[option.GroupIndex];
            switch (group.Behaviour)
            {
                case GroupDrawBehaviour.MultiSelection when !setting.HasFlag(option.Index):
                case GroupDrawBehaviour.SingleSelection when setting.AsIndex != option.Index:
                    return false;
            }
        }

        return true;
    }

    public override bool Equals(ICondition<ModSettingContext>? other)
        => other is AllSettingsCondition s && s.SequenceEqual(this);

    public override int GetHashCode()
        => this.Aggregate(HashCode.Combine(typeof(AllSettingsCondition).GetHashCode()), HashCode.Combine);
}

public sealed class AnySettingCondition(params IReadOnlyCollection<Guid> options) : MultiSettingCondition(options)
{
    protected override ReadOnlySpan<byte> Type
        => "AnySetting"u8;

    public override bool Evaluate(in ModSettingContext context)
    {
        foreach (var optionId in this)
        {
            if (!context.Mod.SubObjects.TryGetValue(optionId, out var o) || o is not IModOption option)
                continue;

            var group   = context.Mod.Groups[option.GroupIndex];
            var setting = context.Settings.IsEmpty ? group.DefaultSettings : context.Settings.Settings[option.GroupIndex];
            switch (group.Behaviour)
            {
                case GroupDrawBehaviour.MultiSelection when setting.HasFlag(option.Index):
                case GroupDrawBehaviour.SingleSelection when setting.AsIndex == option.Index:
                    return true;
            }
        }

        return false;
    }

    public override ICondition<ModSettingContext> DeepCopy()
        => new AnySettingCondition(this);

    public override bool Equals(ICondition<ModSettingContext>? other)
        => other is AnySettingCondition s && s.SequenceEqual(this);

    public override int GetHashCode()
        => this.Aggregate(HashCode.Combine(typeof(AnySettingCondition).GetHashCode()), HashCode.Combine);
}

public abstract class MultiSettingCondition : List<Guid>, ICondition<ModSettingContext>
{
    protected MultiSettingCondition(IReadOnlyCollection<Guid> options)
    {
        EnsureCapacity(options.Count);
        AddRange(options);
    }

    public abstract    ICondition<ModSettingContext> DeepCopy();
    protected abstract ReadOnlySpan<byte>            Type { get; }

    public abstract bool Evaluate(in ModSettingContext context);

    public virtual ICondition<ModSettingContext> Reduce()
    {
        this.RemoveDuplicates();
        return this;
    }

    public void WriteJson(Utf8JsonWriter j)
    {
        j.WriteStartObject();
        j.WriteString("Type"u8, Type);
        j.WriteStartArray("Options"u8);
        foreach (var option in this)
            j.WriteStringValue(option);
        j.WriteEndArray();
        j.WriteEndObject();
    }

    public IEnumerable<ICondition<ModSettingContext>> Subconditions
        => [];

    public int RemoveSubconditions(Func<ICondition<ModSettingContext>, bool> predicate)
        => 0;

    public abstract bool Equals(ICondition<ModSettingContext>? other);
}

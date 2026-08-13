using Penumbra.Api.Preset;
using Penumbra.Mods.Groups;
using Penumbra.Mods.SubMods;

namespace Penumbra.Mods.Settings;

public static class PresetExtensions
{
    extension(ref readonly ModObjectIdentifier id)
    {
        public static ModObjectIdentifier From(IModObject obj)
            => new(obj.Id, obj.Name);

        public IModGroup? FindGroup(Mod? mod)
        {
            if (mod is null)
                return null;

            var groupIdx = id.BestMatch(mod.Groups.Select(g => ModObjectIdentifier.From(g)));
            return groupIdx < 0 ? null : mod.Groups[groupIdx];
        }

        public IModOption? FindOption(IModGroup? group)
        {
            if (group is null)
                return null;

            var optionIdx = id.BestMatch(group.Options.Select(o => ModObjectIdentifier.From(o)));
            return optionIdx < 0 ? null : group.Options[optionIdx];
        }

        public IModOption? FindOption(Mod mod, ModObjectIdentifier groupIdentifier)
        {
            if (groupIdentifier.FindGroup(mod) is not { } group)
                return null;

            return id.FindOption(group);
        }
    }

    extension(SettingPresetData preset)
    {
        public static SettingPresetData FromMod(Mod mod, ModSettings? settings)
        {
            var ret = ValueTuple<SettingsDictionary, int, short, bool, byte>.Create();
            ret.SetState(
                settings is null || settings.IsEmpty ? ModState.Inherited :
                settings.Enabled                     ? ModState.Enabled : ModState.Disabled, true);
            ret.SetPriority(settings?.Priority.Value, true);
            foreach (var group in mod.Groups)
            {
                var config  = settings is null || settings.IsEmpty ? group.DefaultSettings : settings.Settings[group.Index];
                var groupId = ModObjectIdentifier.From(group);
                foreach (var option in group.Options)
                {
                    var optionId = ModObjectIdentifier.From(option);
                    var state = group.Behaviour is GroupDrawBehaviour.SingleSelection
                        ? config.AsIndex == option.Index
                            ? OptionState.Enabled
                            : OptionState.Disabled
                        : config.HasFlag(option.Index)
                            ? OptionState.Enabled
                            : OptionState.Disabled;
                    ret.SetOption(groupId, optionId, state, true);
                }
            }

            return ret;
        }

        public bool Update(Mod mod, ModSettings? settings, bool force)
            => preset.Update(FromMod(mod, settings), force);
    }
}

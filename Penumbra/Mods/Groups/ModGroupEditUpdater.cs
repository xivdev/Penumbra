using Luna;
using Penumbra.Communication;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Services;

namespace Penumbra.Mods.Groups;

public sealed class ModGroupEditUpdater : IRequiredService, IDisposable
{
    private readonly CommunicatorService _communicator;

    public ModGroupEditUpdater(CommunicatorService communicator)
    {
        _communicator = communicator;
        _communicator.ModOptionChanged.Subscribe(OnModOptionChange, ModOptionChanged.Priority.ModGroupEditUpdater);
    }

    private static void OnModOptionChange(in ModOptionChanged.Arguments arguments)
    {
        switch (arguments.Type)
        {
            case ModOptionChangeType.GroupRenamed:
                foreach (var group in arguments.Mod.Groups)
                {
                    if (group.ParentSetting.Group == arguments.OldName)
                        group.ParentSetting = new ParentSetting(arguments.Group!.Name, group.ParentSetting.Option);

                    Fix(group.Condition, arguments.OldName!, arguments.Group!.Name, true);
                    foreach (var option in group.Options)
                        Fix(option.Condition, arguments.OldName!, arguments.Group!.Name, true);
                }

                break;
            case ModOptionChangeType.OptionRenamed:
                foreach (var group in arguments.Mod.Groups)
                {
                    if (group.ParentSetting.Option == arguments.OldName)
                        group.ParentSetting = new ParentSetting(group.ParentSetting.Group!, arguments.Option!.Name);

                    Fix(group.Condition, arguments.OldName!, arguments.Option!.Name, false);
                    foreach (var option in group.Options)
                        Fix(option.Condition, arguments.OldName!, arguments.Option!.Name, false);
                }

                break;
            case ModOptionChangeType.PrepareGroupDeletion:
                foreach (var group in arguments.Mod.Groups)
                {
                    if (group.ParentSetting.Group == arguments.Group!.Name)
                        group.ParentSetting = ParentSetting.None;
                }

                break;
            case ModOptionChangeType.OptionDeleted:
                foreach (var group in arguments.Mod.Groups)
                {
                    if (group.ParentSetting.Option == arguments.OldName)
                        group.ParentSetting = new ParentSetting(group.ParentSetting.Group);
                }

                break;
        }
    }

    private static void Fix(ICondition<ModSettingContext>? condition, string oldText, string newText, bool group)
    {
        switch (condition)
        {
            case MultiSettingCondition all when group:
                if (all.Group == oldText)
                    all.Group = newText;
                break;
            case MultiSettingCondition all:
                for (var i = 0; i < all.Count; ++i)
                {
                    if (all[i] == oldText)
                        all[i] = newText;
                }

                break;
            case SingleSettingCondition single when group:
                if (single.Group == oldText)
                    single.Group = newText;
                break;
            case SingleSettingCondition single:
                if (single.Option == oldText)
                    single.Option = newText;
                break;
        }
    }

    private static IEnumerable<ICondition<ModSettingContext>> GetAllConditions(Mod mod)
    {
        foreach (var group in mod.Groups)
        {
            if (group.Condition is not null)
                yield return group.Condition;

            foreach (var option in group.Options)
            {
                if (option.Condition is not null)
                    yield return option.Condition;
            }
        }
    }

    public void Dispose()
        => _communicator.ModOptionChanged.Unsubscribe(OnModOptionChange);
}

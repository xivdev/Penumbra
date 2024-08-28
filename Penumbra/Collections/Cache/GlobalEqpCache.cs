using OtterGui.Classes;
using OtterGui.Services;
using Penumbra.GameData.Structs;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;

namespace Penumbra.Collections.Cache;

public class GlobalEqpCache : ReadWriteDictionary<GlobalEqpManipulation, IMod>, IService
{
    private readonly HashSet<PrimaryId> _doNotHideEarrings  = [];
    private readonly HashSet<PrimaryId> _doNotHideNecklace  = [];
    private readonly HashSet<PrimaryId> _doNotHideBracelets = [];
    private readonly HashSet<PrimaryId> _doNotHideRingL     = [];
    private readonly HashSet<PrimaryId> _doNotHideRingR     = [];
    private          bool               _doNotHideVieraHats;
    private          bool               _doNotHideHrothgarHats;

    public new void Clear()
    {
        base.Clear();
        _doNotHideEarrings.Clear();
        _doNotHideNecklace.Clear();
        _doNotHideBracelets.Clear();
        _doNotHideRingL.Clear();
        _doNotHideRingR.Clear();
        _doNotHideHrothgarHats = false;
        _doNotHideVieraHats    = false;
    }

    public unsafe EqpEntry Apply(EqpEntry original, CharacterArmor* armor)
    {
        if (Count == 0)
            return original;

        if (_doNotHideVieraHats)
            original |= EqpEntry.HeadShowVieraHat;

        if (_doNotHideHrothgarHats)
            original |= EqpEntry.HeadShowHrothgarHat;

        if (_doNotHideEarrings.Contains(armor[5].Set))
            original |= EqpEntry.HeadShowEarrings | EqpEntry.HeadShowEarringsAura | EqpEntry.HeadShowEarringsHuman;

        if (_doNotHideNecklace.Contains(armor[6].Set))
            original |= EqpEntry.BodyShowNecklace | EqpEntry.HeadShowNecklace;

        if (_doNotHideBracelets.Contains(armor[7].Set))
            original |= EqpEntry.BodyShowBracelet | EqpEntry.HandShowBracelet;

        if (_doNotHideRingR.Contains(armor[8].Set))
            original |= EqpEntry.HandShowRingR;

        if (_doNotHideRingL.Contains(armor[9].Set))
            original |= EqpEntry.HandShowRingL;
        return original;
    }

    public bool ApplyMod(IMod mod, GlobalEqpManipulation manipulation)
    {
        if (Remove(manipulation, out var oldMod) && oldMod == mod)
            return false;

        this[manipulation] = mod;
        _ = manipulation.Type switch
        {
            GlobalEqpType.DoNotHideEarrings     => _doNotHideEarrings.Add(manipulation.Condition),
            GlobalEqpType.DoNotHideNecklace     => _doNotHideNecklace.Add(manipulation.Condition),
            GlobalEqpType.DoNotHideBracelets    => _doNotHideBracelets.Add(manipulation.Condition),
            GlobalEqpType.DoNotHideRingR        => _doNotHideRingR.Add(manipulation.Condition),
            GlobalEqpType.DoNotHideRingL        => _doNotHideRingL.Add(manipulation.Condition),
            GlobalEqpType.DoNotHideHrothgarHats => !_doNotHideHrothgarHats && (_doNotHideHrothgarHats = true),
            GlobalEqpType.DoNotHideVieraHats    => !_doNotHideVieraHats && (_doNotHideVieraHats       = true),
            _                                   => false,
        };
        return true;
    }

    public bool RevertMod(GlobalEqpManipulation manipulation, [NotNullWhen(true)] out IMod? mod)
    {
        if (!Remove(manipulation, out mod))
            return false;

        _ = manipulation.Type switch
        {
            GlobalEqpType.DoNotHideEarrings     => _doNotHideEarrings.Remove(manipulation.Condition),
            GlobalEqpType.DoNotHideNecklace     => _doNotHideNecklace.Remove(manipulation.Condition),
            GlobalEqpType.DoNotHideBracelets    => _doNotHideBracelets.Remove(manipulation.Condition),
            GlobalEqpType.DoNotHideRingR        => _doNotHideRingR.Remove(manipulation.Condition),
            GlobalEqpType.DoNotHideRingL        => _doNotHideRingL.Remove(manipulation.Condition),
            GlobalEqpType.DoNotHideHrothgarHats => _doNotHideHrothgarHats && !(_doNotHideHrothgarHats = false),
            GlobalEqpType.DoNotHideVieraHats    => _doNotHideVieraHats && !(_doNotHideVieraHats       = false),
            _                                   => false,
        };
        return true;
    }
}

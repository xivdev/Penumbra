using OtterGui;
using Penumbra.GameData.Structs;
using Penumbra.Mods.Groups;
using Penumbra.Mods.SubMods;

namespace Penumbra.Mods.Manager.OptionEditor;

public unsafe ref struct ImcAttributeCache
{
    private fixed bool _canChange[ImcEntry.NumAttributes];
    private fixed byte _option[ImcEntry.NumAttributes];

    /// <summary> Obtain the earliest unset flag, or 0 if none are unset. </summary>
    public readonly ushort LowestUnsetMask;

    public ImcAttributeCache(ImcModGroup group)
    {
        for (var i = 0; i < ImcEntry.NumAttributes; ++i)
        {
            _canChange[i] = true;
            _option[i]    = byte.MaxValue;

            var flag = (ushort)(1 << i);
            foreach (var (option, idx) in group.OptionData.WithIndex())
            {
                if ((option.AttributeMask & flag) == 0)
                    continue;

                _canChange[i] = option.AttributeMask != flag;
                _option[i]    = (byte)idx;
                break;
            }

            if (_option[i] == byte.MaxValue && LowestUnsetMask is 0)
                LowestUnsetMask = flag;
        }
    }


    /// <summary> Checks whether an attribute flag can be set by anything, i.e. if it might be the only flag for an option and thus could not be removed from that option. </summary>
    public readonly bool CanChange(int idx)
        => _canChange[idx];

    /// <summary> Set a default attribute flag to a value if possible, remove it from its prior option if necessary, and return if anything changed. </summary>
    public readonly bool Set(ImcModGroup group, int idx, bool value)
    {
        var flag    = 1 << idx;
        var oldMask = group.DefaultEntry.AttributeMask;
        if (!value)
        {
            var newMask = (ushort)(oldMask & ~flag);
            if (oldMask == newMask)
                return false;

            group.DefaultEntry = group.DefaultEntry with { AttributeMask = newMask };
            return true;
        }

        var mask = (ushort)(oldMask | flag);
        if (oldMask == mask)
            return false;

        group.DefaultEntry = group.DefaultEntry with { AttributeMask = mask };
        return true;
    }

    /// <summary> Set an attribute flag to a value if possible, remove it from its prior option or the default entry if necessary, and return if anything changed. </summary>
    public readonly bool Set(ImcSubMod option, int idx, bool value, bool turnOffDefault = false)
    {
        if (!_canChange[idx])
            return false;

        var flag    = 1 << idx;
        var oldMask = option.AttributeMask;
        if (!value)
        {
            var newMask = (ushort)(oldMask & ~flag);
            if (oldMask == newMask)
                return false;

            option.AttributeMask = newMask;
            return true;
        }

        var mask = (ushort)(oldMask | flag);
        if (oldMask == mask)
            return false;

        option.AttributeMask = mask;
        if (_option[idx] <= ImcEntry.NumAttributes)
        {
            var oldOption = option.Group.OptionData[_option[idx]];
            oldOption.AttributeMask = (ushort)(oldOption.AttributeMask & ~flag);
        }
        else if (turnOffDefault && _option[idx] is byte.MaxValue - 1)
        {
            option.Group.DefaultEntry = option.Group.DefaultEntry with
            {
                AttributeMask = (ushort)(option.Group.DefaultEntry.AttributeMask & ~flag),
            };
        }

        return true;
    }
}

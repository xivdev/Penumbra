using ImSharp;
using Luna;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;
using Penumbra.Interop.Services;
using Penumbra.Meta.Files;

namespace Penumbra.UI.Tabs.Debug;

public sealed unsafe class CmpDrawer(CharacterUtility utility) : IUiService
{
    private CmpData* _ptr;

    public void Draw()
    {
        var (cmpFilePtr, size) = utility.DefaultResource(CmpFile.InternalIndex);
        var requiredSize = sizeof(CmpData);
        if (cmpFilePtr is 0 || size < requiredSize)
            return;

        _ptr = (CmpData*)cmpFilePtr;
        DrawScales();
        DrawColors();
    }

    private void DrawScales()
    {
        using var scales = Im.Tree.Node("Scales"u8);
        if (!scales)
            return;

        using var table = Im.Table.Begin("t"u8, (int)RspAttribute.NumAttributes + 1,
            TableFlags.Borders | TableFlags.RowBackground | TableFlags.SizingFixedFit);
        if (!table)
            return;

        table.SetupColumn("Clan"u8);
        foreach (var attribute in RspAttribute.NamesU8.SkipLast(1))
            table.SetupColumn(attribute);
        table.HeaderRow();

        foreach (var (name, race) in SubRace.NamesAndValuesU8.Skip(1))
        {
            table.DrawColumn(name);
            ref var values = ref _ptr->GetScale(race);
            foreach (var attribute in RspAttribute.Values.SkipLast(1))
                table.DrawColumn($"{values.Get(attribute):F4}");
        }
    }

    private void DrawColors()
    { }
}

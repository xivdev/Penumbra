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
    {
        using var colors = Im.Tree.Node("Colors"u8);
        if (!colors)
            return;

        using var table = Im.Table.Begin("t"u8, 1 + 5 + 32,
            TableFlags.Borders | TableFlags.SizingFixedFit | TableFlags.RowBackground | TableFlags.ScrollX | TableFlags.ScrollY,
            Im.ContentRegion.Available with { Y = Im.Style.FrameHeightWithSpacing * 10 });
        if (!table)
            return;

        table.SetupScrollFreeze(1, 1);
        Im.Table.NextRow(TableRowFlags.Headers);
        table.NextColumn();
        ImEx.TextCentered("#Color"u8);

        table.NextColumn();
        ImEx.TextCentered("Eyes"u8);
        ImEx.TextCentered("Global"u8);

        table.NextColumn();
        ImEx.TextCentered("Highlights"u8);
        ImEx.TextCentered("Global"u8);

        table.NextColumn();
        ImEx.TextCentered("Features"u8);
        ImEx.TextCentered("Global"u8);

        table.NextColumn();
        ImEx.TextCentered("Lips"u8);
        ImEx.TextCentered("Global"u8);

        table.NextColumn();
        ImEx.TextCentered("FacePaint"u8);
        ImEx.TextCentered("Global"u8);

        foreach (var name in SubRace.Values.Skip(1))
        {
            table.NextColumn();
            ImEx.TextCentered("Skin"u8);
            ImEx.TextCentered($"{name.ToShortNameU8()}");

            table.NextColumn();
            ImEx.TextCentered("Hair"u8);
            ImEx.TextCentered($"{name.ToShortNameU8()}");
        }

        ref var eyesUi       = ref _ptr->Interface.Eyes;
        ref var highlightsUi = ref _ptr->Interface.HairHighlights;
        ref var featuresUi   = ref _ptr->Interface.Features;
        var     lipsUi       = new CmpData.ColorsPair(ref _ptr->Interface.LipsDark,      ref _ptr->Interface.LipsLight);
        var     facePaintUi  = new CmpData.ColorsPair(ref _ptr->Interface.FacePaintDark, ref _ptr->Interface.FacePaintLight);

        ref var eyes       = ref _ptr->Parameters.Eyes;
        ref var highlights = ref _ptr->Parameters.HairHighlights;
        ref var features   = ref _ptr->Parameters.Features;
        var     lips       = new CmpData.ColorsPair(ref _ptr->Parameters.LipsDark,      ref _ptr->Parameters.LipsLight);
        var     facePaint  = new CmpData.ColorsPair(ref _ptr->Parameters.FacePaintDark, ref _ptr->Parameters.FacePaintLight);

        using var clip     = new Im.ListClipper(256, Im.Style.FrameHeightWithSpacing);
        foreach (var index in clip)
        {
            using var id = Im.Id.Push(index);
            table.DrawFrameColumn($"#{index + 1:D3}");

            table.NextColumn();
            Im.Color.Button("Eye UI Color"u8, eyesUi[index]);
            Im.Line.SameInner();
            Im.Color.Button("Eye Color"u8, eyes[index]);

            table.NextColumn();
            Im.Color.Button("Highlights UI Color"u8, highlightsUi[index]);
            Im.Line.SameInner();
            Im.Color.Button("Highlights Color"u8, highlights[index]);

            table.NextColumn();
            Im.Color.Button("Features UI Color"u8, featuresUi[index]);
            Im.Line.SameInner();
            Im.Color.Button("Features Color"u8, features[index]);

            table.NextColumn();
            Im.Color.Button("Lips UI Color"u8, lipsUi[index]);
            Im.Line.SameInner();
            Im.Color.Button("Lips Color"u8, lips[index]);

            table.NextColumn();
            Im.Color.Button("Face Paint UI Color"u8, facePaintUi[index]);
            Im.Line.SameInner();
            Im.Color.Button("Face Paint Color"u8, facePaint[index]);

            foreach (var race in SubRace.Values.Skip(1))
            {
                var name = race.ToNameU8();
                table.NextColumn();
                ImEx.TextFramed("♂"u8, default, Rgba32.Transparent);
                Im.Line.SameInner();
                Im.Color.Button($"Skin UI Color ({name} Male)", _ptr->GetSkin(race, Gender.Male, true)[index]);
                Im.Line.SameInner();
                Im.Color.Button($"Skin Color ({name} Male)",    _ptr->GetSkin(race, Gender.Male, false)[index]);
                Im.Line.Same();
                ImEx.TextFramed("♀"u8, default, Rgba32.Transparent);
                Im.Line.SameInner();
                Im.Color.Button($"Skin UI Color ({name} Female)", _ptr->GetSkin(race, Gender.Female, true)[index]);
                Im.Line.SameInner();
                Im.Color.Button($"Skin Color ({name} Female)", _ptr->GetSkin(race, Gender.Female, false)[index]);

                table.NextColumn();
                ImEx.TextFramed("♂"u8, default, Rgba32.Transparent);
                Im.Line.SameInner();
                Im.Color.Button($"Hair UI Color ({name} Male)", _ptr->GetHairUi(race, Gender.Male)[index]);
                Im.Line.SameInner();
                Im.Color.Button($"Hair Color ({name} Male)", _ptr->GetHair(race, Gender.Male)[index].Main);
                Im.Line.Same();
                ImEx.TextFramed("♀"u8, default, Rgba32.Transparent);
                Im.Line.SameInner();
                Im.Color.Button($"Hair UI Color ({name} Female)", _ptr->GetHairUi(race, Gender.Female)[index]);
                Im.Line.SameInner();
                Im.Color.Button($"Hair Color ({name} Female)", _ptr->GetHair(race, Gender.Female)[index].Main);
            }
        }
    }
}

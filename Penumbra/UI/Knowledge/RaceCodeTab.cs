using ImSharp;
using Penumbra.GameData.Enums;

namespace Penumbra.UI.Knowledge;

public sealed class RaceCodeTab : IKnowledgeTab
{
    public ReadOnlySpan<byte> Name
        => "Race Codes"u8;

    public ReadOnlySpan<byte> SearchTags
        => "deformersracecodesmodel"u8;

    public void Draw()
    {
        var size = new Vector2((Im.ContentRegion.Available.X - Im.Style.ItemSpacing.X) / 2, 0);
        using (var table = Im.Table.Begin("adults"u8, 4, TableFlags.BordersOuter, size))
        {
            if (!table)
                return;

            DrawHeaders(table);
            foreach (var gr in Enum.GetValues<GenderRace>())
            {
                var (gender, race) = gr.Split();
                if (gender is not Gender.Male and not Gender.Female || race is ModelRace.Unknown)
                    continue;

                DrawRow(table, gender, race, false);
            }
        }

        Im.Line.Same();

        using (var table = Im.Table.Begin("children"u8, 4, TableFlags.BordersOuter, size))
        {
            if (!table)
                return;

            DrawHeaders(table);
            foreach (var race in (ReadOnlySpan<ModelRace>)
                     [ModelRace.Midlander, ModelRace.Elezen, ModelRace.Miqote, ModelRace.AuRa, ModelRace.Unknown])
            {
                foreach (var gender in (ReadOnlySpan<Gender>)[Gender.Male, Gender.Female])
                    DrawRow(table, gender, race, true);
            }
        }

        return;

        static void DrawHeaders(in Im.TableDisposable table)
        {
            table.NextColumn();
            table.Header("Race"u8);
            table.NextColumn();
            table.Header("Gender"u8);
            table.NextColumn();
            table.Header("Age"u8);
            table.NextColumn();
            table.Header("Race Code"u8);
        }

        static void DrawRow(in Im.TableDisposable table, Gender gender, ModelRace race, bool child)
        {
            var gr = child
                ? Names.CombinedRace(gender is Gender.Male ? Gender.MaleNpc : Gender.FemaleNpc, race)
                : Names.CombinedRace(gender,                                                    race);

            table.DrawColumn(race.ToNameU8());
            table.DrawColumn(gender.ToNameU8());
            table.DrawColumn(child ? "Child"u8 : "Adult"u8);

            table.NextColumn();
            ImEx.CopyOnClickSelectable(gr.ToRaceCode());
        }
    }
}

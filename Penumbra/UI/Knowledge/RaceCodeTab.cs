using ImGuiNET;
using OtterGui.Text;
using Penumbra.GameData.Enums;

namespace Penumbra.UI.Knowledge;

public sealed class RaceCodeTab() : IKnowledgeTab
{
    public ReadOnlySpan<byte> Name
        => "Race Codes"u8;

    public ReadOnlySpan<byte> SearchTags
        => "deformersracecodesmodel"u8;

    public void Draw()
    {
        var size = new Vector2((ImGui.GetContentRegionAvail().X - ImUtf8.ItemSpacing.X) / 2, 0);
        using (var table = ImUtf8.Table("adults"u8, 4, ImGuiTableFlags.BordersOuter, size))
        {
            if (!table)
                return;

            DrawHeaders();
            foreach (var gr in Enum.GetValues<GenderRace>())
            {
                var (gender, race) = gr.Split();
                if (gender is not Gender.Male and not Gender.Female || race is ModelRace.Unknown)
                    continue;

                DrawRow(gender, race, false);
            }
        }

        ImGui.SameLine();

        using (var table = ImUtf8.Table("children"u8, 4, ImGuiTableFlags.BordersOuter, size))
        {
            if (!table)
                return;

            DrawHeaders();
            foreach (var race in (ReadOnlySpan<ModelRace>)
                     [ModelRace.Midlander, ModelRace.Elezen, ModelRace.Miqote, ModelRace.AuRa, ModelRace.Unknown])
            {
                foreach (var gender in (ReadOnlySpan<Gender>) [Gender.Male, Gender.Female])
                    DrawRow(gender, race, true);
            }
        }

        return;

        static void DrawHeaders()
        {
            ImGui.TableNextColumn();
            ImUtf8.TableHeader("Race"u8);
            ImGui.TableNextColumn();
            ImUtf8.TableHeader("Gender"u8);
            ImGui.TableNextColumn();
            ImUtf8.TableHeader("Age"u8);
            ImGui.TableNextColumn();
            ImUtf8.TableHeader("Race Code"u8);
        }

        static void DrawRow(Gender gender, ModelRace race, bool child)
        {
            var gr = child
                ? Names.CombinedRace(gender is Gender.Male ? Gender.MaleNpc : Gender.FemaleNpc, race)
                : Names.CombinedRace(gender,                                                    race);
            ImGui.TableNextColumn();
            ImUtf8.Text(race.ToName());

            ImGui.TableNextColumn();
            ImUtf8.Text(gender.ToName());

            ImGui.TableNextColumn();
            ImUtf8.Text(child ? "Child"u8 : "Adult"u8);

            ImGui.TableNextColumn();
            ImUtf8.CopyOnClickSelectable(gr.ToRaceCode());
        }
    }
}

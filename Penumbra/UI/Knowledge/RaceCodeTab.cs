using ImGuiNET;
using OtterGui.Text;
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
        using var table = ImUtf8.Table("table"u8, 4, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        ImUtf8.TableHeader("Race Code"u8);
        ImUtf8.TableHeader("Race"u8);
        ImUtf8.TableHeader("Gender"u8);
        ImUtf8.TableHeader("NPC"u8);

        foreach (var genderRace in Enum.GetValues<GenderRace>())
        {
            ImGui.TableNextColumn();
            ImUtf8.Text(genderRace.ToRaceCode());

            var (gender, race) = genderRace.Split();
            ImGui.TableNextColumn();
            ImUtf8.Text($"{race}");

            ImGui.TableNextColumn();
            ImUtf8.Text($"{gender}");

            ImGui.TableNextColumn();
            ImUtf8.Text(((ushort)genderRace & 0xF) != 1 ? "NPC"u8 : "Normal"u8);
        }
    }
}

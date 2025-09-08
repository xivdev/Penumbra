using Dalamud.Bindings.ImGui;
using OtterGui.Text;
using Penumbra.GameData.Files;
using Penumbra.GameData.Files.AtchStructs;

namespace Penumbra.UI.Tabs.Debug;

public static class AtchDrawer
{
    public static void Draw(AtchFile file)
    {
        using (ImUtf8.Group())
        {
            ImUtf8.Text("Entries: "u8);
            ImUtf8.Text("States: "u8);
        }

        ImGui.SameLine();
        using (ImUtf8.Group())
        {
            ImUtf8.Text($"{file.Points.Count}");
            if (file.Points.Count == 0)
            {
                ImUtf8.Text("0"u8);
                return;
            }

            ImUtf8.Text($"{file.Points[0].Entries.Length}");
        }

        foreach (var (index, entry) in file.Points.Index())
        {
            using var id   = ImUtf8.PushId(index);
            using var tree = ImUtf8.TreeNode($"{index:D3}: {entry.Type.ToName()}");
            if (tree)
            {
                ImUtf8.TreeNode(entry.Accessory ? "Accessory"u8 : "Weapon"u8, ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.Leaf).Dispose();
                foreach (var (i, state) in entry.Entries.Index())
                {
                    id.Push(i);
                    using var t = ImUtf8.TreeNode(state.Bone);
                    if (t)
                    {
                        ImUtf8.TreeNode($"Scale: {state.Scale}", ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.Leaf).Dispose();
                        ImUtf8.TreeNode($"Offset: {state.Offset.X} | {state.Offset.Y} | {state.Offset.Z}",
                            ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.Leaf).Dispose();
                        ImUtf8.TreeNode($"Rotation: {state.Rotation.X} | {state.Rotation.Y} | {state.Rotation.Z}",
                            ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.Leaf).Dispose();
                    }

                    id.Pop();
                }
            }
        }
    }
}

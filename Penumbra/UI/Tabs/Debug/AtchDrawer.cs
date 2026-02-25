using ImSharp;
using Penumbra.GameData.Files;
using Penumbra.GameData.Files.AtchStructs;

namespace Penumbra.UI.Tabs.Debug;

public static class AtchDrawer
{
    public static void Draw(AtchFile file)
    {
        using (Im.Group())
        {
            Im.Text("Entries: "u8);
            Im.Text("States: "u8);
        }

        Im.Line.Same();
        using (Im.Group())
        {
            Im.Text($"{file.Points.Count}");
            if (file.Points.Count == 0)
            {
                Im.Text("0"u8);
                return;
            }

            Im.Text($"{file.Points[0].Entries.Length}");
        }

        foreach (var (index, entry) in file.Points.Index())
        {
            using var id   = Im.Id.Push(index);
            using var tree = Im.Tree.Node($"{index:D3}: {entry.Type.ToName()}");
            if (!tree)
                continue;

            Im.Tree.Leaf(entry.Accessory ? "Accessory"u8 : "Weapon"u8);
            foreach (var (i, state) in entry.Entries.Index())
            {
                id.Push(i);
                using var t = Im.Tree.Node(state.Bone);
                if (t)
                {
                    Im.Tree.Leaf($"Scale: {state.Scale}");
                    Im.Tree.Leaf($"Offset: {state.Offset.X} | {state.Offset.Y} | {state.Offset.Z}");
                    Im.Tree.Leaf($"Rotation: {state.Rotation.X} | {state.Rotation.Y} | {state.Rotation.Z}");
                }

                id.Pop();
            }
        }
    }
}

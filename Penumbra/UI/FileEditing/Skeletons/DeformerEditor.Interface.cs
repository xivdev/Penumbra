using ImSharp;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.UI.Classes;

namespace Penumbra.UI.FileEditing.Skeletons;

public partial class DeformerEditor
{
    bool IFileEditor.DrawToolbar(bool disabled)
        => false;

    public bool DrawPanel(bool disabled)
    {
        Update(File);
        DrawGenderRaceSelector();
        Im.Line.Same();
        DrawBoneSelector();
        Im.Line.Same();
        return DrawBoneData(disabled);
    }

    private void DrawGenderRaceSelector()
    {
        using var group = Im.Group();
        var       width = Im.Font.CalculateSize("Hellsguard - Female (Child)____0000"u8).X + 2 * Im.Style.WindowPadding.X;
        using (ImStyleSingle.FrameRounding.Push(0)
                   .Push(ImStyleDouble.ItemSpacing, Vector2.Zero))
        {
            Im.Item.SetNextWidth(width);
            Im.Input.Text("##grFilter"u8, ref RaceCodeFilter, "Filter..."u8);
        }

        using var child = Im.Child.Begin("GenderRace"u8,
            new Vector2(width, Im.ContentRegion.Maximum.Y - Im.Style.FrameHeight - Im.Style.WindowPadding.Y), true);
        if (!child)
            return;

        var metaColor = ColorId.ItemId.Value();
        foreach (var (index, deformer) in File.Deformers.Index())
        {
            var name     = deformer.GenderRace.ToName();
            var raceCode = deformer.GenderRace.ToRaceCode();
            // No clipping necessary since this are not that many objects anyway.
            if (!name.Contains(RaceCodeFilter) && !raceCode.Contains(RaceCodeFilter))
                continue;

            using var id    = Im.Id.Push(index);
            using var color = ImGuiColor.Text.Push(Im.Style[ImGuiColor.TextDisabled], deformer.RacialDeformer.IsEmpty);
            if (Im.Selectable(name, deformer.GenderRace == SelectedRaceCode))
            {
                SelectedRaceCode = deformer.GenderRace;
                SelectedDeformer = deformer.RacialDeformer;
            }

            Im.Line.Same();
            color.Push(ImGuiColor.Text, metaColor);
            ImEx.TextRightAligned(raceCode);
        }
    }

    private sealed class BoneCache(DeformerEditor owner) : BasicFilterCache<string>(owner.BoneFilter)
    {
        protected override IEnumerable<string> GetItems()
            => owner.SelectedDeformer is null || owner.SelectedDeformer.IsEmpty ? [] : owner.SelectedDeformer.DeformMatrices.Keys;
    }

    private void DrawBoneSelector()
    {
        var       cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Get((int)SelectedRaceCode), () => new BoneCache(this));
        using var group = Im.Group();
        var       width = 200 * Im.Style.GlobalScale;
        BoneFilter.DrawFilter("Filter..."u8, new Vector2(width, Im.Style.FrameHeight));
        Im.Cursor.Y -= Im.Style.ItemSpacing.Y;
        using var child = Im.Child.Begin("Bone"u8,
            new Vector2(width, Im.ContentRegion.Maximum.Y - Im.Style.FrameHeight - Im.Style.WindowPadding.Y), true);
        if (!child)
            return;

        if (SelectedDeformer is null)
            return;

        if (cache.AllItems.Count is 0)
            Im.Text("<Empty>"u8);
        else
            foreach (var item in cache)
            {
                if (Im.Selectable(item, item == SelectedBone))
                    SelectedBone = item;
            }
    }

    private bool DrawBoneData(bool disabled)
    {
        using var child = Im.Child.Begin("Data"u8,
            Im.ContentRegion.Available with { Y = Im.ContentRegion.Maximum.Y - Im.Style.WindowPadding.Y }, true);
        if (!child)
            return false;

        if (SelectedBone is null)
            return false;

        if (!SelectedDeformer!.DeformMatrices.TryGetValue(SelectedBone, out var matrix))
            return false;

        var width       = Im.Font.Mono.GetCharacterAdvance('0') * 12 + Im.Style.FramePadding.X * 2;
        var dummyHeight = Im.Style.TextHeight / 2;
        var ret         = DrawAddNewBone(disabled, matrix, width);

        Im.Dummy(0, dummyHeight);
        Im.Separator();
        Im.Dummy(0, dummyHeight);
        ret |= DrawDeformerMatrix(disabled, matrix, width);
        Im.Dummy(0, dummyHeight);
        ret |= DrawCopyPasteButtons(disabled, matrix, width);


        Im.Dummy(0, dummyHeight);
        Im.Separator();
        Im.Dummy(0, dummyHeight);
        ret |= DrawDecomposedData(disabled, matrix, width);

        return ret;
    }

    private bool DrawAddNewBone(bool disabled, in TransformMatrix matrix, float width)
    {
        var ret = false;
        ImEx.TextFrameAligned("Copy the values of the bone "u8);
        Im.Line.NoSpacing();
        using (ImGuiColor.Text.Push(ColorId.NewMod.Value()))
        {
            ImEx.TextFrameAligned(SelectedBone!);
        }

        Im.Line.NoSpacing();
        ImEx.TextFrameAligned(" to a new bone of name"u8);

        var fullWidth = width * 4 + Im.Style.ItemSpacing.X * 3;
        Im.Item.SetNextWidth(fullWidth);
        Im.Input.Text("##newBone"u8, ref NewBoneName, "New Bone Name..."u8);
        ImEx.TextFrameAligned("for all races that have a corresponding bone."u8);
        Im.Line.Same(0, fullWidth - width - Im.Item.Size.X);
        if (ImEx.Button("Apply"u8, new Vector2(width, 0), StringU8.Empty,
                disabled || NewBoneName.Length is 0 || SelectedBone is null))
        {
            foreach (var deformer in File.Deformers)
            {
                if (!deformer.RacialDeformer.DeformMatrices.TryGetValue(SelectedBone!, out var existingMatrix))
                    continue;

                if (!deformer.RacialDeformer.DeformMatrices.TryAdd(NewBoneName, existingMatrix)
                 && deformer.RacialDeformer.DeformMatrices.TryGetValue(NewBoneName, out var newBoneMatrix)
                 && !newBoneMatrix.Equals(existingMatrix))
                    Penumbra.Messager.AddMessage(new Luna.Notification(
                        $"Could not add deformer matrix to {deformer.GenderRace.ToName()}, Bone {NewBoneName} because it already has a deformer that differs from the intended one."));
                else
                    ret = true;
            }

            NewBoneName = string.Empty;
        }

        if (ImEx.Button("Copy Values to Single New Bone Entry"u8, new Vector2(fullWidth, 0), StringU8.Empty,
                disabled || NewBoneName.Length is 0 || SelectedDeformer!.DeformMatrices.ContainsKey(NewBoneName)))
        {
            SelectedDeformer!.DeformMatrices[NewBoneName] = matrix;
            ret                                           = true;
            NewBoneName                                   = string.Empty;
        }

        return ret;
    }

    private bool DrawDeformerMatrix(bool disabled, in TransformMatrix matrix, float width)
    {
        using var font = Im.Font.PushMono();
        using var _    = Im.Disabled(disabled);
        var       ret  = false;
        for (var i = 0; i < 3; ++i)
        {
            for (var j = 0; j < 4; ++j)
            {
                using var id = Im.Id.Push(i * 4 + j);
                Im.Item.SetNextWidth(width);
                var tmp = matrix[i, j];
                if (Im.Input.Scalar(StringU8.Empty, ref tmp, "% 12.8f"u8))
                {
                    ret                                             = true;
                    SelectedDeformer!.DeformMatrices[SelectedBone!] = matrix.ChangeValue(i, j, tmp);
                }

                Im.Line.Same();
            }

            Im.Line.New();
        }

        return ret;
    }

    private bool DrawCopyPasteButtons(bool disabled, in TransformMatrix matrix, float width)
    {
        var size = new Vector2(width, 0);
        if (Im.Button("Copy Values"u8, size))
            CopiedMatrix = matrix;

        Im.Line.Same();

        var ret = false;
        if (ImEx.Button("Paste Values"u8, size, StringU8.Empty, disabled || !CopiedMatrix.HasValue))
        {
            SelectedDeformer!.DeformMatrices[SelectedBone!] = CopiedMatrix!.Value;
            ret                                             = true;
        }

        var modifier = configuration.DeleteModModifier.IsActive();
        Im.Line.Same();
        if (modifier)
        {
            if (ImEx.Button("Delete"u8, size, "Delete this bone entry."u8, disabled))
            {
                ret          |= SelectedDeformer!.DeformMatrices.Remove(SelectedBone!);
                SelectedBone =  null;
            }
        }
        else
        {
            ImEx.Button("Delete"u8, size, $"Delete this bone entry. Hold {configuration.DeleteModModifier} to delete.", true);
        }

        return ret;
    }

    private bool DrawDecomposedData(bool disabled, in TransformMatrix matrix, float width)
    {
        var ret = false;


        if (!matrix.TryDecompose(out var scale, out var rotation, out var translation))
            return false;

        using (Im.Group())
        {
            using var font = Im.Font.PushMono();
            using var _    = Im.Disabled(disabled);

            Im.Item.SetNextWidth(width);
            ret |= Im.Input.Scalar("##ScaleX"u8, ref scale.X, "% 12.8f"u8);

            Im.Line.Same();
            Im.Item.SetNextWidth(width);
            ret |= Im.Input.Scalar("##ScaleY"u8, ref scale.Y, "% 12.8f"u8);

            Im.Line.Same();
            Im.Item.SetNextWidth(width);
            ret |= Im.Input.Scalar("##ScaleZ"u8, ref scale.Z, "% 12.8f"u8);


            Im.Item.SetNextWidth(width);
            ret |= Im.Input.Scalar("##TranslationX"u8, ref translation.X, "% 12.8f"u8);

            Im.Line.Same();
            Im.Item.SetNextWidth(width);
            ret |= Im.Input.Scalar("##TranslationY"u8, ref translation.Y, "% 12.8f"u8);

            Im.Line.Same();
            Im.Item.SetNextWidth(width);
            ret |= Im.Input.Scalar("##TranslationZ"u8, ref translation.Z, "% 12.8f"u8);


            Im.Item.SetNextWidth(width);
            ret |= Im.Input.Scalar("##RotationR"u8, ref rotation.W, "% 12.8f"u8);

            Im.Line.Same();
            Im.Item.SetNextWidth(width);
            ret |= Im.Input.Scalar("##RotationI"u8, ref rotation.X, "% 12.8f"u8);

            Im.Line.Same();
            Im.Item.SetNextWidth(width);
            ret |= Im.Input.Scalar("##RotationJ"u8, ref rotation.Y, "% 12.8f"u8);
            Im.Line.Same();
            Im.Item.SetNextWidth(width);
            ret |= Im.Input.Scalar("##RotationK"u8, ref rotation.Z, "% 12.8f"u8);
        }

        Im.Line.Same();
        using (Im.Group())
        {
            ImEx.TextFrameAligned("Scale"u8);
            ImEx.TextFrameAligned("Translation"u8);
            ImEx.TextFrameAligned("Rotation (Quaternion, rijk)"u8);
        }

        if (ret)
            SelectedDeformer!.DeformMatrices[SelectedBone!] = TransformMatrix.Compose(scale, rotation, translation);
        return ret;
    }
}

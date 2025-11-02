using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using ImSharp;
using OtterGui;
using OtterGui.Text;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly FileEditor<PbdTab> _pbdTab;
    private readonly PbdData            _pbdData = new();

    private bool DrawDeformerPanel(PbdTab tab, bool disabled)
    {
        _pbdData.Update(tab.File);
        DrawGenderRaceSelector(tab);
        Im.Line.Same();
        DrawBoneSelector();
        Im.Line.Same();
        return DrawBoneData(tab, disabled);
    }

    private void DrawGenderRaceSelector(PbdTab tab)
    {
        using var group = ImUtf8.Group();
        var       width = ImUtf8.CalcTextSize("Hellsguard - Female (Child)____0000"u8).X + 2 * Im.Style.WindowPadding.X;
        using (ImStyleSingle.FrameRounding.Push(0)
                   .Push(ImStyleDouble.ItemSpacing, Vector2.Zero))
        {
            Im.Item.SetNextWidth(width);
            ImUtf8.InputText("##grFilter"u8, ref _pbdData.RaceCodeFilter, "Filter..."u8);
        }

        using var child = ImUtf8.Child("GenderRace"u8,
            new Vector2(width, ImGui.GetContentRegionMax().Y - Im.Style.FrameHeight - Im.Style.WindowPadding.Y), true);
        if (!child)
            return;

        var metaColor = ColorId.ItemId.Value();
        foreach (var (index, deformer) in tab.File.Deformers.Index())
        {
            var name     = deformer.GenderRace.ToName();
            var raceCode = deformer.GenderRace.ToRaceCode();
            // No clipping necessary since this are not that many objects anyway.
            if (!name.Contains(_pbdData.RaceCodeFilter) && !raceCode.Contains(_pbdData.RaceCodeFilter))
                continue;

            using var id    = ImUtf8.PushId(index);
            using var color = ImGuiColor.Text.Push(Im.Style[ImGuiColor.TextDisabled], deformer.RacialDeformer.IsEmpty);
            if (ImUtf8.Selectable(name, deformer.GenderRace == _pbdData.SelectedRaceCode))
            {
                _pbdData.SelectedRaceCode = deformer.GenderRace;
                _pbdData.SelectedDeformer = deformer.RacialDeformer;
            }

            Im.Line.Same();
            color.Push(ImGuiColor.Text, metaColor);
            ImUtf8.TextRightAligned(raceCode);
        }
    }

    private void DrawBoneSelector()
    {
        using var group = ImUtf8.Group();
        var       width = 200 * Im.Style.GlobalScale;
        using (ImStyleSingle.FrameRounding.Push(0)
                   .Push(ImStyleDouble.ItemSpacing, Vector2.Zero))
        {
            Im.Item.SetNextWidth(width);
            ImUtf8.InputText("##boneFilter"u8, ref _pbdData.BoneFilter, "Filter..."u8);
        }

        using var child = ImUtf8.Child("Bone"u8,
            new Vector2(width, ImGui.GetContentRegionMax().Y - Im.Style.FrameHeight - Im.Style.WindowPadding.Y), true);
        if (!child)
            return;

        if (_pbdData.SelectedDeformer == null)
            return;

        if (_pbdData.SelectedDeformer.IsEmpty)
        {
            ImUtf8.Text("<Empty>"u8);
        }
        else
        {
            var height = Im.Style.TextHeightWithSpacing;
            var skips  = ImGuiClip.GetNecessarySkips(height);
            var remainder = ImGuiClip.FilteredClippedDraw(_pbdData.SelectedDeformer.DeformMatrices.Keys, skips,
                b => b.Contains(_pbdData.BoneFilter), bone
                    =>
                {
                    if (ImUtf8.Selectable(bone, bone == _pbdData.SelectedBone))
                        _pbdData.SelectedBone = bone;
                });
            ImGuiClip.DrawEndDummy(remainder, height);
        }
    }

    private bool DrawBoneData(PbdTab tab, bool disabled)
    {
        using var child = ImUtf8.Child("Data"u8,
            Im.ContentRegion.Available with { Y = ImGui.GetContentRegionMax().Y - Im.Style.WindowPadding.Y }, true);
        if (!child)
            return false;

        if (_pbdData.SelectedBone == null)
            return false;

        if (!_pbdData.SelectedDeformer!.DeformMatrices.TryGetValue(_pbdData.SelectedBone, out var matrix))
            return false;

        var width       = UiBuilder.MonoFont.GetCharAdvance('0') * 12 + Im.Style.FramePadding.X * 2;
        var dummyHeight = Im.Style.TextHeight / 2;
        var ret         = DrawAddNewBone(tab, disabled, matrix, width);

        ImUtf8.Dummy(0, dummyHeight);
        Im.Separator();
        ImUtf8.Dummy(0, dummyHeight);
        ret |= DrawDeformerMatrix(disabled, matrix, width);
        ImUtf8.Dummy(0, dummyHeight);
        ret |= DrawCopyPasteButtons(disabled, matrix, width);


        ImUtf8.Dummy(0, dummyHeight);
        Im.Separator();
        ImUtf8.Dummy(0, dummyHeight);
        ret |= DrawDecomposedData(disabled, matrix, width);

        return ret;
    }

    private bool DrawAddNewBone(PbdTab tab, bool disabled, in TransformMatrix matrix, float width)
    {
        var ret = false;
        ImUtf8.TextFrameAligned("Copy the values of the bone "u8);
        Im.Line.Same(0, 0);
        using (ImGuiColor.Text.Push(ColorId.NewMod.Value()))
        {
            ImUtf8.TextFrameAligned(_pbdData.SelectedBone);
        }

        Im.Line.Same(0, 0);
        ImUtf8.TextFrameAligned(" to a new bone of name"u8);

        var fullWidth = width * 4 + Im.Style.ItemSpacing.X * 3;
        Im.Item.SetNextWidth(fullWidth);
        ImUtf8.InputText("##newBone"u8, ref _pbdData.NewBoneName, "New Bone Name..."u8);
        ImUtf8.TextFrameAligned("for all races that have a corresponding bone."u8);
        Im.Line.Same(0, fullWidth - width - ImGui.GetItemRectSize().X);
        if (ImUtf8.ButtonEx("Apply"u8, ""u8, new Vector2(width, 0),
                disabled || _pbdData.NewBoneName.Length == 0 || _pbdData.SelectedBone == null))
        {
            foreach (var deformer in tab.File.Deformers)
            {
                if (!deformer.RacialDeformer.DeformMatrices.TryGetValue(_pbdData.SelectedBone!, out var existingMatrix))
                    continue;

                if (!deformer.RacialDeformer.DeformMatrices.TryAdd(_pbdData.NewBoneName, existingMatrix)
                 && deformer.RacialDeformer.DeformMatrices.TryGetValue(_pbdData.NewBoneName, out var newBoneMatrix)
                 && !newBoneMatrix.Equals(existingMatrix))
                    Penumbra.Messager.AddMessage(new Luna.Notification(
                        $"Could not add deformer matrix to {deformer.GenderRace.ToName()}, Bone {_pbdData.NewBoneName} because it already has a deformer that differs from the intended one.",
                        NotificationType.Warning));
                else
                    ret = true;
            }

            _pbdData.NewBoneName = string.Empty;
        }

        if (ImUtf8.ButtonEx("Copy Values to Single New Bone Entry"u8, ""u8, new Vector2(fullWidth, 0),
                disabled || _pbdData.NewBoneName.Length == 0 || _pbdData.SelectedDeformer!.DeformMatrices.ContainsKey(_pbdData.NewBoneName)))
        {
            _pbdData.SelectedDeformer!.DeformMatrices[_pbdData.NewBoneName] = matrix;
            ret                                                             = true;
            _pbdData.NewBoneName                                            = string.Empty;
        }


        return ret;
    }

    private bool DrawDeformerMatrix(bool disabled, in TransformMatrix matrix, float width)
    {
        using var font = ImRaii.PushFont(UiBuilder.MonoFont);
        using var _    = ImRaii.Disabled(disabled);
        var       ret  = false;
        for (var i = 0; i < 3; ++i)
        {
            for (var j = 0; j < 4; ++j)
            {
                using var id = ImUtf8.PushId(i * 4 + j);
                Im.Item.SetNextWidth(width);
                var tmp = matrix[i, j];
                if (ImUtf8.InputScalar(""u8, ref tmp, "% 12.8f"u8))
                {
                    ret                                                               = true;
                    _pbdData.SelectedDeformer!.DeformMatrices[_pbdData.SelectedBone!] = matrix.ChangeValue(i, j, tmp);
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
        if (ImUtf8.Button("Copy Values"u8, size))
            _pbdData.CopiedMatrix = matrix;

        Im.Line.Same();

        var ret = false;
        if (ImUtf8.ButtonEx("Paste Values"u8, ""u8, size, disabled || !_pbdData.CopiedMatrix.HasValue))
        {
            _pbdData.SelectedDeformer!.DeformMatrices[_pbdData.SelectedBone!] = _pbdData.CopiedMatrix!.Value;
            ret                                                               = true;
        }

        var modifier = _config.DeleteModModifier.IsActive();
        Im.Line.Same();
        if (modifier)
        {
            if (ImUtf8.ButtonEx("Delete"u8, "Delete this bone entry."u8, size, disabled))
            {
                ret                   |= _pbdData.SelectedDeformer!.DeformMatrices.Remove(_pbdData.SelectedBone!);
                _pbdData.SelectedBone =  null;
            }
        }
        else
        {
            ImUtf8.ButtonEx("Delete"u8, $"Delete this bone entry. Hold {_config.DeleteModModifier} to delete.", size, true);
        }

        return ret;
    }

    private bool DrawDecomposedData(bool disabled, in TransformMatrix matrix, float width)
    {
        var ret = false;


        if (!matrix.TryDecompose(out var scale, out var rotation, out var translation))
            return false;

        using (ImUtf8.Group())
        {
            using var font = ImRaii.PushFont(UiBuilder.MonoFont);
            using var _    = ImRaii.Disabled(disabled);

            Im.Item.SetNextWidth(width);
            ret |= ImUtf8.InputScalar("##ScaleX"u8, ref scale.X, "% 12.8f"u8);

            Im.Line.Same();
            Im.Item.SetNextWidth(width);
            ret |= ImUtf8.InputScalar("##ScaleY"u8, ref scale.Y, "% 12.8f"u8);

            Im.Line.Same();
            Im.Item.SetNextWidth(width);
            ret |= ImUtf8.InputScalar("##ScaleZ"u8, ref scale.Z, "% 12.8f"u8);


            Im.Item.SetNextWidth(width);
            ret |= ImUtf8.InputScalar("##TranslationX"u8, ref translation.X, "% 12.8f"u8);

            Im.Line.Same();
            Im.Item.SetNextWidth(width);
            ret |= ImUtf8.InputScalar("##TranslationY"u8, ref translation.Y, "% 12.8f"u8);

            Im.Line.Same();
            Im.Item.SetNextWidth(width);
            ret |= ImUtf8.InputScalar("##TranslationZ"u8, ref translation.Z, "% 12.8f"u8);


            Im.Item.SetNextWidth(width);
            ret |= ImUtf8.InputScalar("##RotationR"u8, ref rotation.W, "% 12.8f"u8);

            Im.Line.Same();
            Im.Item.SetNextWidth(width);
            ret |= ImUtf8.InputScalar("##RotationI"u8, ref rotation.X, "% 12.8f"u8);

            Im.Line.Same();
            Im.Item.SetNextWidth(width);
            ret |= ImUtf8.InputScalar("##RotationJ"u8, ref rotation.Y, "% 12.8f"u8);
            Im.Line.Same();
            Im.Item.SetNextWidth(width);
            ret |= ImUtf8.InputScalar("##RotationK"u8, ref rotation.Z, "% 12.8f"u8);
        }

        Im.Line.Same();
        using (ImUtf8.Group())
        {
            ImUtf8.TextFrameAligned("Scale"u8);
            ImUtf8.TextFrameAligned("Translation"u8);
            ImUtf8.TextFrameAligned("Rotation (Quaternion, rijk)"u8);
        }

        if (ret)
            _pbdData.SelectedDeformer!.DeformMatrices[_pbdData.SelectedBone!] = TransformMatrix.Compose(scale, rotation, translation);
        return ret;
    }

    public class PbdTab(byte[] data, string filePath) : IWritable
    {
        public readonly string FilePath = filePath;

        public readonly PbdFile File = new(data);

        public bool Valid
            => File.Valid;

        public byte[] Write()
            => File.Write();
    }

    private class PbdData
    {
        public GenderRace      SelectedRaceCode = GenderRace.Unknown;
        public RacialDeformer? SelectedDeformer;
        public string?         SelectedBone;
        public string          NewBoneName    = string.Empty;
        public string          BoneFilter     = string.Empty;
        public string          RaceCodeFilter = string.Empty;

        public TransformMatrix? CopiedMatrix;

        public void Update(PbdFile file)
        {
            if (SelectedRaceCode is GenderRace.Unknown)
            {
                SelectedDeformer = null;
            }
            else
            {
                SelectedDeformer = file.Deformers.FirstOrDefault(p => p.GenderRace == SelectedRaceCode).RacialDeformer;
                if (SelectedDeformer is null)
                    SelectedRaceCode = GenderRace.Unknown;
            }
        }
    }
}

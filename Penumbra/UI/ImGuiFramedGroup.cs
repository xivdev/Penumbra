using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;

namespace Penumbra.UI
{
    public static partial class ImGuiCustom
    {
        public static void BeginFramedGroup(string label) => BeginFramedGroupInternal(ref label, ZeroVector, false);
        public static void BeginFramedGroup(string label, Vector2 minSize) => BeginFramedGroupInternal(ref label, minSize, false);

        public static bool BeginFramedGroupEdit(ref string label) => BeginFramedGroupInternal(ref label, ZeroVector, true);
        public static bool BeginFramedGroupEdit(ref string label, Vector2 minSize) => BeginFramedGroupInternal(ref label, minSize, true);

        private static bool BeginFramedGroupInternal(ref string label, Vector2 minSize, bool edit)
        {
            var itemSpacing     = ImGui.GetStyle().ItemSpacing;
            var frameHeight     = ImGui.GetFrameHeight();
            var halfFrameHeight = new Vector2(ImGui.GetFrameHeight() / 2, 0);

            ImGui.BeginGroup(); // First group

            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, ZeroVector);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,  ZeroVector);

            ImGui.BeginGroup(); // Second group

            var effectiveSize = minSize;
            if (effectiveSize.X < 0)
                effectiveSize.X = ImGui.GetContentRegionAvail().X;

            // Ensure width.
            ImGui.Dummy(new(effectiveSize.X, 0));
            // Ensure left half boundary width/distance.
            ImGui.Dummy(halfFrameHeight);

            ImGui.SameLine();
            ImGui.BeginGroup(); // Third group.
            // Ensure right half of boundary width/distance
            ImGui.Dummy(halfFrameHeight);

            // Label block
            ImGui.SameLine();
            var ret = false;
            if (edit)
                ret = ImGuiCustom.ResizingTextInput(ref label, 1024);
            else
                ImGui.TextUnformatted(label);

            var labelMin = ImGui.GetItemRectMin();
            var labelMax = ImGui.GetItemRectMax();
            ImGui.SameLine();
            // Ensure height and distance to label.
            ImGui.Dummy(new Vector2(0, frameHeight + itemSpacing.Y));

            ImGui.BeginGroup(); // Fourth Group.

            ImGui.PopStyleVar(2);

            ImGui.SetWindowSize(new Vector2(ImGui.GetWindowSize().X - frameHeight, ImGui.GetWindowSize().Y));

            var itemWidth = ImGui.CalcItemWidth();
            ImGui.PushItemWidth(Math.Max(0f, itemWidth - frameHeight));

            labelStack.Add((labelMin, labelMax));
            return ret;
        }

        private static void DrawClippedRect(Vector2 clipMin, Vector2 clipMax, Vector2 drawMin, Vector2 drawMax, uint color, float thickness)
        {
            ImGui.PushClipRect(clipMin, clipMax, true);
            ImGui.GetWindowDrawList().AddRect(drawMin, drawMax, color, thickness);
            ImGui.PopClipRect();
        }

        public static void EndFramedGroup()
        {
            uint    borderColor     = ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.Border]);
            Vector2 itemSpacing     = ImGui.GetStyle().ItemSpacing;
            float   frameHeight     = ImGui.GetFrameHeight();
            Vector2 halfFrameHeight = new(ImGui.GetFrameHeight() / 2, 0);

            ImGui.PopItemWidth();

            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, ZeroVector);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,  ZeroVector);

            ImGui.EndGroup(); // Close fourth group
            ImGui.EndGroup(); // Close third group

            ImGui.SameLine();
            // Ensure right distance.
            ImGui.Dummy(halfFrameHeight);
            // Ensure bottom distance
            ImGui.Dummy(new Vector2(0, frameHeight/2 - itemSpacing.Y));
            ImGui.EndGroup(); // Close second group

            var itemMin = ImGui.GetItemRectMin();
            var itemMax = ImGui.GetItemRectMax();
            var (currentLabelMin, currentLabelMax) = labelStack[labelStack.Count - 1];
            labelStack.RemoveAt(labelStack.Count - 1);

            var halfFrame = new Vector2(frameHeight / 8, frameHeight / 2);
            currentLabelMin.X -= itemSpacing.X;
            currentLabelMax.X += itemSpacing.X;
            var frameMin = itemMin + halfFrame;
            var frameMax = itemMax - new Vector2(halfFrame.X, 0);

            // Left
            DrawClippedRect(new(-float.MaxValue  , -float.MaxValue  ), new(currentLabelMin.X, float.MaxValue   ), frameMin, frameMax, borderColor, halfFrame.X);
            // Right
            DrawClippedRect(new(currentLabelMax.X, -float.MaxValue  ), new(float.MaxValue   , float.MaxValue   ), frameMin, frameMax, borderColor, halfFrame.X);
            // Top
            DrawClippedRect(new(currentLabelMin.X, -float.MaxValue  ), new(currentLabelMax.X, currentLabelMin.Y), frameMin, frameMax, borderColor, halfFrame.X);
            // Bottom
            DrawClippedRect(new(currentLabelMin.X, currentLabelMax.Y), new(currentLabelMax.X, float.MaxValue   ), frameMin, frameMax, borderColor, halfFrame.X);

            ImGui.PopStyleVar(2);
            ImGui.SetWindowSize(new Vector2(ImGui.GetWindowSize().X + frameHeight, ImGui.GetWindowSize().Y));
            ImGui.Dummy(ZeroVector);

            ImGui.EndGroup(); // Close first group
        }

        private static readonly Vector2 ZeroVector = new(0, 0);

        private static readonly List<(Vector2, Vector2)> labelStack = new();
    }
}
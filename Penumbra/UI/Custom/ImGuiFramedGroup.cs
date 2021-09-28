using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;

namespace Penumbra.UI.Custom
{
    public static partial class ImGuiCustom
    {
        public static void BeginFramedGroup( string label )
            => BeginFramedGroupInternal( ref label, Vector2.Zero, false );

        public static void BeginFramedGroup( string label, Vector2 minSize )
            => BeginFramedGroupInternal( ref label, minSize, false );

        public static bool BeginFramedGroupEdit( ref string label )
            => BeginFramedGroupInternal( ref label, Vector2.Zero, true );

        public static bool BeginFramedGroupEdit( ref string label, Vector2 minSize )
            => BeginFramedGroupInternal( ref label, minSize, true );

        private static bool BeginFramedGroupInternal( ref string label, Vector2 minSize, bool edit )
        {
            var itemSpacing     = ImGui.GetStyle().ItemSpacing;
            var frameHeight     = ImGui.GetFrameHeight();
            var halfFrameHeight = new Vector2( ImGui.GetFrameHeight() / 2, 0 );

            ImGui.BeginGroup(); // First group

            ImGui.PushStyleVar( ImGuiStyleVar.FramePadding, Vector2.Zero );
            ImGui.PushStyleVar( ImGuiStyleVar.ItemSpacing, Vector2.Zero );

            ImGui.BeginGroup(); // Second group

            var effectiveSize = minSize;
            if( effectiveSize.X < 0 )
            {
                effectiveSize.X = ImGui.GetContentRegionAvail().X;
            }

            // Ensure width.
            ImGui.Dummy( Vector2.UnitX * effectiveSize.X );
            // Ensure left half boundary width/distance.
            ImGui.Dummy( halfFrameHeight );

            ImGui.SameLine();
            ImGui.BeginGroup(); // Third group.
            // Ensure right half of boundary width/distance
            ImGui.Dummy( halfFrameHeight );

            // Label block
            ImGui.SameLine();
            var ret = false;
            if( edit )
            {
                ret = ResizingTextInput( ref label, 1024 );
            }
            else
            {
                ImGui.TextUnformatted( label );
            }

            var labelMin = ImGui.GetItemRectMin();
            var labelMax = ImGui.GetItemRectMax();
            ImGui.SameLine();
            // Ensure height and distance to label.
            ImGui.Dummy( Vector2.UnitY * ( frameHeight + itemSpacing.Y ) );

            ImGui.BeginGroup(); // Fourth Group.

            ImGui.PopStyleVar( 2 );

            // This seems wrong?
            //ImGui.SetWindowSize( new Vector2( ImGui.GetWindowSize().X - frameHeight, ImGui.GetWindowSize().Y ) );

            var itemWidth = ImGui.CalcItemWidth();
            ImGui.PushItemWidth( Math.Max( 0f, itemWidth - frameHeight ) );

            LabelStack.Add( ( labelMin, labelMax ) );
            return ret;
        }

        private static void DrawClippedRect( Vector2 clipMin, Vector2 clipMax, Vector2 drawMin, Vector2 drawMax, uint color, float thickness )
        {
            ImGui.PushClipRect( clipMin, clipMax, true );
            ImGui.GetWindowDrawList().AddRect( drawMin, drawMax, color, thickness );
            ImGui.PopClipRect();
        }

        public static void EndFramedGroup()
        {
            var borderColor     = ImGui.ColorConvertFloat4ToU32( ImGui.GetStyle().Colors[ ( int )ImGuiCol.Border ] );
            var itemSpacing     = ImGui.GetStyle().ItemSpacing;
            var frameHeight     = ImGui.GetFrameHeight();
            var halfFrameHeight = new Vector2( ImGui.GetFrameHeight() / 2, 0 );

            ImGui.PopItemWidth();

            ImGui.PushStyleVar( ImGuiStyleVar.FramePadding, Vector2.Zero );
            ImGui.PushStyleVar( ImGuiStyleVar.ItemSpacing, Vector2.Zero );

            ImGui.EndGroup(); // Close fourth group
            ImGui.EndGroup(); // Close third group

            ImGui.SameLine();
            // Ensure right distance.
            ImGui.Dummy( halfFrameHeight );
            // Ensure bottom distance
            ImGui.Dummy( Vector2.UnitY * ( frameHeight / 2 - itemSpacing.Y ) );
            ImGui.EndGroup(); // Close second group

            var itemMin = ImGui.GetItemRectMin();
            var itemMax = ImGui.GetItemRectMax();
            var (currentLabelMin, currentLabelMax) = LabelStack[ ^1 ];
            LabelStack.RemoveAt( LabelStack.Count - 1 );

            var halfFrame = new Vector2( frameHeight / 8, frameHeight / 2 );
            currentLabelMin.X -= itemSpacing.X;
            currentLabelMax.X += itemSpacing.X;
            var frameMin = itemMin + halfFrame;
            var frameMax = itemMax - Vector2.UnitX * halfFrame.X;

            // Left
            DrawClippedRect( new Vector2( -float.MaxValue, -float.MaxValue ), new Vector2( currentLabelMin.X, float.MaxValue ), frameMin,
                frameMax, borderColor, halfFrame.X );
            // Right
            DrawClippedRect( new Vector2( currentLabelMax.X, -float.MaxValue ), new Vector2( float.MaxValue, float.MaxValue ), frameMin,
                frameMax, borderColor, halfFrame.X );
            // Top
            DrawClippedRect( new Vector2( currentLabelMin.X, -float.MaxValue ), new Vector2( currentLabelMax.X, currentLabelMin.Y ), frameMin,
                frameMax, borderColor, halfFrame.X );
            // Bottom
            DrawClippedRect( new Vector2( currentLabelMin.X, currentLabelMax.Y ), new Vector2( currentLabelMax.X, float.MaxValue ), frameMin,
                frameMax, borderColor, halfFrame.X );

            ImGui.PopStyleVar( 2 );
            // This seems wrong?
            // ImGui.SetWindowSize( new Vector2( ImGui.GetWindowSize().X + frameHeight, ImGui.GetWindowSize().Y ) );
            ImGui.Dummy( Vector2.Zero );

            ImGui.EndGroup(); // Close first group
        }

        private static readonly List< (Vector2, Vector2) > LabelStack = new();
    }
}
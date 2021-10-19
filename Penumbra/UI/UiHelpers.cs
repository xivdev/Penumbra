using System.Numerics;
using ImGuiNET;
using Lumina.Data.Parsing;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Enums;
using Penumbra.UI.Custom;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        internal void DrawChangedItem( string name, object? data )
        {
            var ret = ImGui.Selectable( name ) ? MouseButton.Left : MouseButton.None;
            ret = ImGui.IsItemClicked( ImGuiMouseButton.Right ) ? MouseButton.Right : ret;
            ret = ImGui.IsItemClicked( ImGuiMouseButton.Middle ) ? MouseButton.Middle : ret;

            if( ret != MouseButton.None )
            {
                _penumbra.Api.InvokeClick( ret, data );
            }

            if( _penumbra.Api.HasTooltip && ImGui.IsItemHovered() )
            {
                ImGui.BeginTooltip();
                using var tooltip = ImGuiRaii.DeferredEnd( ImGui.EndTooltip );
                _penumbra.Api.InvokeTooltip( data );
            }

            if( data is Item it )
            {
                var modelId = $"({( ( Quad )it.ModelMain ).A})";
                var offset  = ImGui.CalcTextSize( modelId ).X       - ImGui.GetStyle().ItemInnerSpacing.X;
                ImGui.SameLine( ImGui.GetWindowContentRegionWidth() - offset );
                ImGui.TextColored( new Vector4( 0.5f, 0.5f, 0.5f, 1 ), modelId );
            }
        }
    }
}
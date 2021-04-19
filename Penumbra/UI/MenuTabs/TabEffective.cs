using System.IO;
using System.Linq;
using ImGuiNET;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class TabEffective
        {
            private const string LabelTab        = "Effective File List";
            private const float  TextSizePadding = 5f;

            private ModManager _mods => Service< ModManager >.Get();
            private float _maxGamePath;

            public TabEffective( SettingsInterface ui )
            {
                RebuildFileList( ui._plugin!.Configuration!.ShowAdvanced );
            }

            public void RebuildFileList( bool advanced )
            {
                if( advanced )
                {
                    _maxGamePath = TextSizePadding + ( _mods.ResolvedFiles.Count > 0
                        ? _mods.ResolvedFiles.Keys.Max( f => ImGui.CalcTextSize( f ).X )
                        : 0f );
                }
                else
                {
                    _maxGamePath = 0f;
                }
            }

            private void DrawFileLine( FileInfo file, GamePath path )
            {
                ImGui.Selectable( path );
                ImGui.SameLine();
                ImGui.SetCursorPosX( _maxGamePath );
                ImGui.TextUnformatted( "  <-- " );
                ImGui.SameLine();
                ImGui.Selectable( file.FullName );
            }

            public void Draw()
            {
                var ret = ImGui.BeginTabItem( LabelTab );
                if( !ret )
                {
                    return;
                }

                if( ImGui.BeginListBox( "##effective_files", AutoFillSize ) )
                {
                    foreach( var file in _mods.ResolvedFiles )
                    {
                        DrawFileLine( file.Value, file.Key );
                    }

                    ImGui.EndListBox();
                }

                ImGui.EndTabItem();
            }
        }
    }
}
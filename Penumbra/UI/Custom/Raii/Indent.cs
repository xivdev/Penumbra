using System;
using System.Diagnostics;
using Dalamud.Interface;
using ImGuiNET;

namespace Penumbra.UI.Custom
{
    public static partial class ImGuiRaii
    {
        public static Indent PushIndent( float f, bool scaled = true, bool condition = true )
            => new Indent().Push( f, condition );

        public static Indent PushIndent( int i = 1, bool scaled = true, bool condition = true )
            => new Indent().Push( i, condition );

        public class Indent : IDisposable
        {
            private float _indentation;

            public Indent Push( float indent, bool scaled = true, bool condition = true )
            {
                Debug.Assert( indent >= 0f );
                if( condition )
                {
                    if( scaled )
                    {
                        indent *= ImGuiHelpers.GlobalScale;
                    }

                    ImGui.Indent( indent );
                    _indentation += indent;
                }

                return this;
            }

            public Indent Push( uint i = 1, bool scaled = true, bool condition = true )
            {
                if( condition )
                {
                    var spacing = i * ImGui.GetStyle().IndentSpacing * ( scaled ? ImGuiHelpers.GlobalScale : 1f );
                    ImGui.Indent( spacing );
                    _indentation += spacing;
                }

                return this;
            }

            public void Pop( float indent, bool scaled = true )
            {
                if( scaled )
                {
                    indent *= ImGuiHelpers.GlobalScale;
                }

                Debug.Assert( indent >= 0f );
                ImGui.Unindent( indent );
                _indentation -= indent;
            }

            public void Pop( uint i, bool scaled = true )
            {
                var spacing = i * ImGui.GetStyle().IndentSpacing * ( scaled ? ImGuiHelpers.GlobalScale : 1f );
                ImGui.Unindent( spacing );
                _indentation += spacing;
            }

            public void Dispose()
                => Pop( _indentation, false );
        }
    }
}
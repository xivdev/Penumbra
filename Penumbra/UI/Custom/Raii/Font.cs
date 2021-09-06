using System;
using ImGuiNET;

namespace Penumbra.UI.Custom
{
    public static partial class ImGuiRaii
    {
        public static Font PushFont( ImFontPtr font )
            => new( font );

        public class Font : IDisposable
        {
            private int _count;

            public Font( ImFontPtr font )
                => Push( font );

            public Font Push( ImFontPtr font )
            {
                ImGui.PushFont( font );
                ++_count;
                return this;
            }

            public void Pop( int num = 1 )
            {
                num    =  Math.Min( num, _count );
                _count -= num;
                while( num-- > 0 )
                {
                    ImGui.PopFont();
                }
            }

            public void Dispose()
                => Pop( _count );
        }
    }
}
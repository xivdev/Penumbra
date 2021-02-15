using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Actors;
using Dalamud.Game.ClientState.Actors.Types;
using System.Threading.Tasks;

namespace Penumbra
{
    public static class RefreshActors
    {
        private const int RenderModeOffset      = 0x0104;
        private const int RenderTaskPlayerDelay = 75;
        private const int RenderTaskOtherDelay  = 25;
        private const int ModelInvisibilityFlag = 0b10;

        private static async void Redraw(Actor actor)
        {
            var ptr = actor.Address;
            var renderModePtr = ptr + RenderModeOffset;
            var renderStatus = Marshal.ReadInt32(renderModePtr);

            async void DrawObject(int delay)
            {
                Marshal.WriteInt32(renderModePtr, renderStatus | ModelInvisibilityFlag);
                await Task.Delay(delay);
                Marshal.WriteInt32(renderModePtr, renderStatus & ~ModelInvisibilityFlag);
            }

            if (actor.ObjectKind == Dalamud.Game.ClientState.Actors.ObjectKind.Player)
            {
                DrawObject(RenderTaskPlayerDelay);
                await Task.Delay(RenderTaskPlayerDelay);
            }
            else
                DrawObject(RenderTaskOtherDelay);

        }

        public static void RedrawSpecific(ActorTable actors, string name)
        {
            if (name?.Length == 0)
                RedrawAll(actors);

            foreach (var actor in actors)
                if (actor.Name == name)
                    Redraw(actor);
        }

        public static void RedrawAll(ActorTable actors)
        {
            foreach (var actor in actors)
                Redraw(actor);
        }
    }
}

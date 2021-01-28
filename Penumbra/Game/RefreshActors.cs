using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Actors;
using Dalamud.Game.ClientState.Actors.Types;
using System.Threading.Tasks;

namespace Penumbra
{
    public static class RefreshActors
    {
        private const int ObjectKindOffset = 0x008C;
        private const int RenderModeOffset = 0x0104;
        private const int RenderTaskDelay  = 75;

        private enum RenderMode : int
        {
            Draw   = 0,
            Unload = 2
        }

        private static async void Redraw(Actor actor)
        {
            var ptr = actor.Address;
            var objectKindPtr = ptr + ObjectKindOffset;
            var renderModePtr = ptr + RenderModeOffset;

            async void DrawObject()
            {
                Marshal.WriteInt32(renderModePtr, (int) RenderMode.Unload);
                await Task.Delay(RenderTaskDelay);
                Marshal.WriteInt32(renderModePtr, (int) RenderMode.Draw);
            }

            if (actor.ObjectKind == Dalamud.Game.ClientState.Actors.ObjectKind.Player)
            {
                Marshal.WriteByte(objectKindPtr, (byte) Dalamud.Game.ClientState.Actors.ObjectKind.BattleNpc);
                DrawObject();
                await Task.Delay(RenderTaskDelay);
                Marshal.WriteByte(objectKindPtr, (byte) Dalamud.Game.ClientState.Actors.ObjectKind.Player);
                Marshal.WriteInt32(renderModePtr, (int) RenderMode.Draw);
            }
            else
                DrawObject();
            await Task.Delay(RenderTaskDelay);
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

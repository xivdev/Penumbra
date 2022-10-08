using System;
using Dalamud.Plugin;
using Penumbra.Api.Helpers;

namespace Penumbra.Api;

public static partial class Ipc
{
    public static class GetPlayerMetaManipulations
    {
        public const string Label = $"Penumbra.{nameof( GetPlayerMetaManipulations )}";

        public static FuncProvider< string > Provider( DalamudPluginInterface pi, Func< string > func )
            => new(pi, Label, func);

        public static FuncSubscriber< string > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class GetMetaManipulations
    {
        public const string Label = $"Penumbra.{nameof( GetMetaManipulations )}";

        public static FuncProvider< string, string > Provider( DalamudPluginInterface pi, Func< string, string > func )
            => new(pi, Label, func);

        public static FuncSubscriber< string, string > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }
}
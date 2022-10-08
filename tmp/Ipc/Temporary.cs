using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;

namespace Penumbra.Api;

public static partial class Ipc
{
    public static class CreateTemporaryCollection
    {
        public const string Label = $"Penumbra.{nameof( CreateTemporaryCollection )}";

        public static FuncProvider< string, string, bool, (PenumbraApiEc, string) > Provider( DalamudPluginInterface pi,
            Func< string, string, bool, (PenumbraApiEc, string) > func )
            => new(pi, Label, func);

        public static FuncSubscriber< string, string, bool, (PenumbraApiEc, string) > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class RemoveTemporaryCollection
    {
        public const string Label = $"Penumbra.{nameof( RemoveTemporaryCollection )}";

        public static FuncProvider< string, PenumbraApiEc > Provider( DalamudPluginInterface pi,
            Func< string, PenumbraApiEc > func )
            => new(pi, Label, func);

        public static FuncSubscriber< string, PenumbraApiEc > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class AddTemporaryModAll
    {
        public const string Label = $"Penumbra.{nameof( AddTemporaryModAll )}";

        public static FuncProvider< string, Dictionary< string, string >, string, int, PenumbraApiEc > Provider(
            DalamudPluginInterface pi, Func< string, Dictionary< string, string >, string, int, PenumbraApiEc > func )
            => new(pi, Label, func);

        public static FuncSubscriber< string, Dictionary< string, string >, string, int, PenumbraApiEc > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class AddTemporaryMod
    {
        public const string Label = $"Penumbra.{nameof( AddTemporaryMod )}";

        public static FuncProvider< string, string, Dictionary< string, string >, string, int, PenumbraApiEc > Provider(
            DalamudPluginInterface pi, Func< string, string, Dictionary< string, string >, string, int, PenumbraApiEc > func )
            => new(pi, Label, func);

        public static FuncSubscriber< string, string, Dictionary< string, string >, string, int, PenumbraApiEc > Subscriber(
            DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class RemoveTemporaryModAll
    {
        public const string Label = $"Penumbra.{nameof( RemoveTemporaryModAll )}";

        public static FuncProvider< string, int, PenumbraApiEc > Provider(
            DalamudPluginInterface pi, Func< string, int, PenumbraApiEc > func )
            => new(pi, Label, func);

        public static FuncSubscriber< string, int, PenumbraApiEc > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class RemoveTemporaryMod
    {
        public const string Label = $"Penumbra.{nameof( RemoveTemporaryMod )}";

        public static FuncProvider< string, string, int, PenumbraApiEc > Provider(
            DalamudPluginInterface pi, Func< string, string, int, PenumbraApiEc > func )
            => new(pi, Label, func);

        public static FuncSubscriber< string, string, int, PenumbraApiEc > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }
}
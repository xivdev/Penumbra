using Luna;
using Newtonsoft.Json.Linq;
using Penumbra.Collections;
using Penumbra.Mods;

namespace Penumbra.Communication;

/// <summary> Triggered when the character.json file for a .pcp file is parsed and applied. </summary>
public sealed class PcpParsing(Logger log) : EventBase<PcpParsing.Arguments, PcpParsing.Priority>(nameof(PcpParsing), log)
{
    public enum Priority
    {
        /// <seealso cref="Api.Api.ModsApi"/>
        ApiMods = int.MinValue,
    }

    /// <summary> The arguments for a PcpParsing event. </summary>
    /// <param name="JObject"> The parsed JObject that contains the data. </param>
    /// <param name="Mod"> The created mod. </param>
    /// <param name="Collection"> The created collection, if any. </param>
    public readonly record struct Arguments(JObject JObject, Mod Mod, ModCollection? Collection);
}

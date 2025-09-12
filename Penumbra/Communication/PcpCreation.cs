using Luna;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Structs;

namespace Penumbra.Communication;

/// <summary> Triggered when the character.json file for a .pcp file is written. </summary>
public sealed class PcpCreation(Logger log) : EventBase<PcpCreation.Arguments, PcpCreation.Priority>(nameof(PcpCreation), log)
{
    public enum Priority
    {
        /// <seealso cref="Api.Api.ModsApi"/>
        ApiMods = int.MinValue,
    }

    /// <summary> The arguments for a PcpCreation event. </summary>
    /// <param name="JObject"> The JObject that gets written to file. </param>
    /// <param name="ObjectIndex"> The object index of the game object this is written for. </param>
    /// <param name="DirectoryPath"> The full path to the directory being set up for the PCP creation. </param>
    public readonly record struct Arguments(JObject JObject, ObjectIndex ObjectIndex, string DirectoryPath);
}

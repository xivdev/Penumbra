using Newtonsoft.Json.Linq;
using OtterGui.Classes;

namespace Penumbra.Communication;

/// <summary>
/// Triggered when the character.json file for a .pcp file is written.
/// <list type="number">
///     <item>Parameter is the JObject that gets written to file. </item>
///     <item>Parameter is the object index of the game object this is written for. </item>
///     <item>Parameter is the full path to the directory being set up for the PCP creation. </item>
/// </list>
/// </summary>
public sealed class PcpCreation() : EventWrapper<JObject, ushort, string, PcpCreation.Priority>(nameof(PcpCreation))
{
    public enum Priority
    {
        /// <seealso cref="Api.Api.ModsApi"/>
        ModsApi = int.MinValue,
    }
}

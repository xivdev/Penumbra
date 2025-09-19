using Newtonsoft.Json.Linq;
using OtterGui.Classes;

namespace Penumbra.Communication;

/// <summary>
/// Triggered when the character.json file for a .pcp file is parsed and applied.
/// <list type="number">
///     <item>Parameter is parsed JObject that contains the data. </item>
///     <item>Parameter is the identifier of the created mod. </item>
///     <item>Parameter is the GUID of the created collection. </item>
/// </list>
/// </summary>
public sealed class PcpParsing() : EventWrapper<JObject, string, Guid, PcpParsing.Priority>(nameof(PcpParsing))
{
    public enum Priority
    {
        /// <seealso cref="Api.Api.ModsApi"/>
        ModsApi = int.MinValue,
    }
}

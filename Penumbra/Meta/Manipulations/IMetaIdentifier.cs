using Newtonsoft.Json.Linq;
using Penumbra.GameData.Data;
using Penumbra.Interop.Structs;

namespace Penumbra.Meta.Manipulations;

public interface IMetaIdentifier
{
    public void AddChangedItems(ObjectIdentification identifier, IDictionary<string, object?> changedItems);

    public MetaIndex FileIndex();

    public bool Validate();

    public JObject AddToJson(JObject jObj);
}

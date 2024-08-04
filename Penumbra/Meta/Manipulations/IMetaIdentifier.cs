using Newtonsoft.Json.Linq;
using Penumbra.GameData.Data;
using Penumbra.Interop.Structs;

namespace Penumbra.Meta.Manipulations;

public enum MetaManipulationType : byte
{
    Unknown   = 0,
    Imc       = 1,
    Eqdp      = 2,
    Eqp       = 3,
    Est       = 4,
    Gmp       = 5,
    Rsp       = 6,
    GlobalEqp = 7,
}

public interface IMetaIdentifier
{
    public void AddChangedItems(ObjectIdentification identifier, IDictionary<string, IIdentifiedObjectData?> changedItems);

    public MetaIndex FileIndex();

    public bool Validate();

    public JObject AddToJson(JObject jObj);

    public MetaManipulationType Type { get; }

    public string ToString();
}

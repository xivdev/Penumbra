using System.Text.Json;
using ImSharp;
using Luna;

namespace Penumbra.Mods.Groups;

public sealed class ModGroupConditionParser : ConditionParser<ModSettingContext>, IService
{
    protected override ICondition<ModSettingContext>? ParseCustomType(ref Utf8JsonReader reader, Utf8JsonObjectReader obj, StringU8 type)
    {
        if (type.Equals("SingleSetting"u8))
        {
            var group  = Guid.Empty;
            var option = Guid.Empty;
            while (obj.Read(ref reader))
            {
                if (reader.TokenType is not JsonTokenType.PropertyName)
                    continue;

                if (reader.CheckPropertyValue("Group"u8, JsonTokenType.String))
                    reader.TryGetGuid(out group);
                else if (reader.CheckPropertyValue("Option"u8, JsonTokenType.String))
                    reader.TryGetGuid(out option);
            }

            if (group == Guid.Empty || option == Guid.Empty)
                return null;

            return new SingleSettingCondition(group, option);
        }

        if (type.Equals("MultiSettingAll"u8))
        {
            var (group, options) = GetMulti(obj, ref reader);
            if (group == Guid.Empty || options is null)
                return null;

            return new MultiSettingAllCondition(group, options);
        }

        if (type.Equals("MultiSettingAny"u8))
        {
            var (group, options) = GetMulti(obj, ref reader);
            if (group == Guid.Empty || options is null)
                return null;

            return new MultiSettingAnyCondition(group, options);
        }

        return null;

        static (Guid, Guid[]?) GetMulti(Utf8JsonObjectReader obj, ref Utf8JsonReader reader)
        {
            var         group   = Guid.Empty;
            List<Guid>? options = null;
            while (obj.Read(ref reader))
            {
                if (reader.TokenType is not JsonTokenType.PropertyName)
                    continue;

                if (reader.CheckPropertyValue("Group"u8, JsonTokenType.String))
                    reader.TryGetGuid(out group);
                else if (reader.CheckPropertyValue("Options"u8))
                    options = reader.ReadGuidUtf8Array();
            }

            return (group, options?.ToArray());
        }
    }
}

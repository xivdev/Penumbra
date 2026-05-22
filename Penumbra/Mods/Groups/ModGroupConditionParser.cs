using System.Text.Json;
using ImSharp;
using Luna;

namespace Penumbra.Mods.Groups;

public sealed class ModGroupConditionParser : ConditionParser<ModSettingContext>, IService
{
    protected override ICondition<ModSettingContext>? ParseCustomType(ref Utf8JsonReader reader, Utf8JsonObjectLimit obj, StringU8 type)
    {
        if (type.Equals("AllSettings"u8))
        {
            var options = GetMulti(obj, ref reader);
            return new AllSettingsCondition(options ?? []);
        }

        if (type.Equals("AnySetting"u8))
        {
            var options = GetMulti(obj, ref reader);
            return new AnySettingCondition(options ?? []);
        }

        return null;

        static List<Guid>? GetMulti(Utf8JsonObjectLimit obj, ref Utf8JsonReader reader)
        {
            List<Guid>? options = null;
            while (obj.Read(ref reader))
            {
                if (reader.TokenType is not JsonTokenType.PropertyName)
                    continue;

                if (reader.ArrayProperty("Options"u8, out _))
                    options = reader.ReadGuidArray(true) ?? [];
                else
                    reader.Skip();
            }

            return options;
        }
    }
}

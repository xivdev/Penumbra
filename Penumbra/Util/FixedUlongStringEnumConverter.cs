using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Penumbra.Util;

// Json.Net has a bug
// ulong enums can not be correctly deserialized if they exceed long.MaxValue.
// These converters fix this, taken from https://stackoverflow.com/questions/61740964/json-net-unable-to-deserialize-ulong-flag-type-enum/
public class ForceNumericFlagEnumConverter : FixedUlongStringEnumConverter
{
    private static bool HasFlagsAttribute( Type? objectType )
        => objectType != null && Attribute.IsDefined( Nullable.GetUnderlyingType( objectType ) ?? objectType, typeof( System.FlagsAttribute ) );

    public override void WriteJson( JsonWriter writer, object? value, JsonSerializer serializer )
    {
        var enumType = value?.GetType();
        if( HasFlagsAttribute( enumType ) )
        {
            var underlyingType  = Enum.GetUnderlyingType( enumType! );
            var underlyingValue = Convert.ChangeType( value, underlyingType );
            writer.WriteValue( underlyingValue );
        }
        else
        {
            base.WriteJson( writer, value, serializer );
        }
    }
}

public class FixedUlongStringEnumConverter : StringEnumConverter
{
    public override object? ReadJson( JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer )
    {
        if( reader.MoveToContentAndAssert().TokenType != JsonToken.Integer || reader.ValueType != typeof( System.Numerics.BigInteger ) )
        {
            return base.ReadJson( reader, objectType, existingValue, serializer );
        }

        // Todo: throw an exception if !this.AllowIntegerValues
        // https://www.newtonsoft.com/json/help/html/P_Newtonsoft_Json_Converters_StringEnumConverter_AllowIntegerValues.htm
        var enumType = Nullable.GetUnderlyingType( objectType ) ?? objectType;
        if( Enum.GetUnderlyingType( enumType ) == typeof( ulong ) )
        {
            var bigInteger = ( System.Numerics.BigInteger )reader.Value!;
            if( bigInteger >= ulong.MinValue && bigInteger <= ulong.MaxValue )
            {
                return Enum.ToObject( enumType, checked( ( ulong )bigInteger ) );
            }
        }

        return base.ReadJson( reader, objectType, existingValue, serializer );
    }
}

public static partial class JsonExtensions
{
    public static JsonReader MoveToContentAndAssert( this JsonReader reader )
    {
        if( reader == null )
        {
            throw new ArgumentNullException();
        }

        if( reader.TokenType == JsonToken.None ) // Skip past beginning of stream.
        {
            reader.ReadAndAssert();
        }

        while( reader.TokenType == JsonToken.Comment ) // Skip past comments.
        {
            reader.ReadAndAssert();
        }

        return reader;
    }

    private static JsonReader ReadAndAssert( this JsonReader reader )
    {
        if( reader == null )
        {
            throw new ArgumentNullException();
        }

        if( !reader.Read() )
        {
            throw new JsonReaderException( "Unexpected end of JSON stream." );
        }

        return reader;
    }
}
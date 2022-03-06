using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Penumbra.Util;

public class SingleOrArrayConverter< T > : JsonConverter
{
    public override bool CanConvert( Type objectType )
        => objectType == typeof( HashSet< T > );

    public override object ReadJson( JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer )
    {
        var token = JToken.Load( reader );

        if( token.Type == JTokenType.Array )
        {
            return token.ToObject< HashSet< T > >() ?? new HashSet< T >();
        }

        var tmp = token.ToObject< T >();
        return tmp != null
            ? new HashSet< T > { tmp }
            : new HashSet< T >();
    }

    public override bool CanWrite
        => true;

    public override void WriteJson( JsonWriter writer, object? value, JsonSerializer serializer )
    {
        writer.WriteStartArray();
        if( value != null )
        {
            var v = ( HashSet< T > )value;
            foreach( var val in v )
            {
                serializer.Serialize( writer, val?.ToString() );
            }
        }

        writer.WriteEndArray();
    }
}
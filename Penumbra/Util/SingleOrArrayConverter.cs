using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class SingleOrArrayConverter< T > : JsonConverter
{
    public override bool CanConvert( Type objectType ) => objectType == typeof( HashSet< T > );

    public override object ReadJson( JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer )
    {
        var token = JToken.Load( reader );
        return token.Type == JTokenType.Array
            ? token.ToObject< HashSet< T > >()
            : new HashSet< T > { token.ToObject< T >() };
    }

    public override bool CanWrite => false;

    public override void WriteJson( JsonWriter writer, object value, JsonSerializer serializer )
    {
        throw new NotImplementedException();
    }
}

public class DictSingleOrArrayConverter< T, U > : JsonConverter
{
    public override bool CanConvert( Type objectType ) => objectType == typeof( Dictionary< T, HashSet< U > > );

    public override object ReadJson( JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer )
    {
        var token = JToken.Load( reader );

        if( token.Type == JTokenType.Array )
        {
            return token.ToObject< HashSet< T > >();
        }

        return new HashSet< T > { token.ToObject< T >() };
    }

    public override bool CanWrite => false;

    public override void WriteJson( JsonWriter writer, object value, JsonSerializer serializer )
    {
        throw new NotImplementedException();
    }
}
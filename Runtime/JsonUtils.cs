using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine;

public static class JsonSettings
{
    public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.Auto,
        Formatting = Formatting.Indented,
        Converters = new List<JsonConverter>
        {
            new Vector3Converter(),
            new QuaternionConverter(),
            new ColorConverter()
        }
    };
}

public class Vector3Converter : JsonConverter<Vector3>
{
    public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
    {
        JObject obj = new JObject
        {
            { "x", value.x },
            { "y", value.y },
            { "z", value.z }
        };
        obj.WriteTo(writer);
    }

    public override Vector3 ReadJson(JsonReader reader, System.Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        JObject obj = JObject.Load(reader);
        float x = obj["x"].Value<float>();
        float y = obj["y"].Value<float>();
        float z = obj["z"].Value<float>();
        return new Vector3(x, y, z);
    }
}

public class QuaternionConverter : JsonConverter<Quaternion>
{
    public override void WriteJson(JsonWriter writer, Quaternion value, JsonSerializer serializer)
    {
        JObject obj = new JObject
        {
            { "x", value.x },
            { "y", value.y },
            { "z", value.z },
            { "w", value.w }
        };
        obj.WriteTo(writer);
    }

    public override Quaternion ReadJson(JsonReader reader, System.Type objectType, Quaternion existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        JObject obj = JObject.Load(reader);
        float x = obj["x"].Value<float>();
        float y = obj["y"].Value<float>();
        float z = obj["z"].Value<float>();
        float w = obj["w"].Value<float>();
        return new Quaternion(x, y, z, w);
    }
}

public class ColorConverter : JsonConverter<Color>
{
    public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
    {
        JObject obj = new JObject
        {
            { "r", value.r },
            { "g", value.g },
            { "b", value.b },
            { "a", value.a }
        };
        obj.WriteTo(writer);
    }

    public override Color ReadJson(JsonReader reader, System.Type objectType, Color existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        JObject obj = JObject.Load(reader);

        float r = obj["r"].Value<float>();
        float g = obj["g"].Value<float>();
        float b = obj["b"].Value<float>();
        float a = obj["a"].Value<float>();

        return new Color(r, g, b, a);
    }
}

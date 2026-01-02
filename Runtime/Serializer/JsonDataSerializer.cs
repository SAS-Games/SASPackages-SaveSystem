using Newtonsoft.Json;
using System.Text;

public class JsonDataSerializer : IDataSerializer
{
    public string FileExtension => ".json";

    public byte[] Serialize<T>(T data)
    {
        string json = JsonConvert.SerializeObject(data, JsonSettings.Settings);
        return Encoding.UTF8.GetBytes(json);
    }

    public T Deserialize<T>(byte[] bytes)
    {
        string json = Encoding.UTF8.GetString(bytes);
        return JsonConvert.DeserializeObject<T>(json, JsonSettings.Settings);
    }
}

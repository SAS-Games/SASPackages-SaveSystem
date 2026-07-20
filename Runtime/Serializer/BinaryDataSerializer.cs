using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

public class BinaryDataSerializer : IDataSerializer
{
    public string FileExtension => ".dat";

    public byte[] Serialize<T>(T data, int userId, string dir, string fileName)
    {
#pragma warning disable SYSLIB0011
        using (var ms = new MemoryStream())
        {
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(ms, data);
            return ms.ToArray();
        }
#pragma warning restore SYSLIB0011
    }

    public T Deserialize<T>(byte[] bytes, int userId, string dir, string fileName)
    {
#pragma warning disable SYSLIB0011
        using (var ms = new MemoryStream(bytes))
        {
            BinaryFormatter formatter = new BinaryFormatter();
            return (T)formatter.Deserialize(ms);
        }
#pragma warning restore SYSLIB0011
    }
}

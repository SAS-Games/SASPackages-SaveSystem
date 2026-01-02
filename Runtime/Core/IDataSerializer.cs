public interface IDataSerializer
{
    byte[] Serialize<T>(T data);
    T Deserialize<T>(byte[] bytes);
    string FileExtension { get; }
}

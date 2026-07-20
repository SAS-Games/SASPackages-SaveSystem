public interface IDataSerializer
{
    byte[] Serialize<T>(T data, int userId, string dir, string fileName);
    T Deserialize<T>(byte[] bytes,  int userId, string dir, string fileName);
    string FileExtension { get; }
}

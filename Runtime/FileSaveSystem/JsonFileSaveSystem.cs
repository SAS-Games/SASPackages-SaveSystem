public class JsonFileSaveSystem : FileSaveSystemBase
{
    protected override IDataSerializer Serializer { get; }
    public JsonFileSaveSystem(string rootDirPath) : base(rootDirPath)
    {
        Serializer = new JsonDataSerializer();
    }
}
public class BinaryFileSaveSystem : FileSaveSystemBase
{
    protected override IDataSerializer Serializer { get; }

    public BinaryFileSaveSystem(string rootDirPath) : base(rootDirPath)
    {
        Serializer = new BinaryDataSerializer();
    }
}
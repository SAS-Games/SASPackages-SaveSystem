using SAS.Core.TagSystem;

public class DummyUserModel : IUserModel
{
    public DummyUserModel(IContextBinder _) { }
    int IUserModel.GetActiveUserId()
    {
        return 0;
    }
}

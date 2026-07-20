public class DummyUserModel : IUserModel
{
    int IUserModel.GetActiveUserId()
    {
        return 0;
    }
}

namespace MyProject.Services.Interfaces
{
    public interface IPlayerManagementService
    {
        void SaveAllCachesToDB();
        void SaveCacheToDB(ulong steamId);
    }
}

namespace MyProject.Interface
{
    public interface IMyBase
    {
        int PlayerCount { get; }
        int RoundNum { get; }
        string GetTargetName(string name);
    }
}

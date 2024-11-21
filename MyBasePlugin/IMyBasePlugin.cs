namespace MyProject
{
    public interface IMyBasePlugin
    {
        int PlayerCount { get; }
        int RoundNum { get; }
        string GetTargetName(string name);
    }
}

namespace MyProject.PluginInterfaces
{
    public interface IBot
    {
        int CurrentLevel { get; }

        void MapStartBehavior();

        void WarmupEndBehavior(int botQuota);

        void RoundStartBehavior(int roundCount);

        void RoundEndBehavior(int botQuota, int roundCount, int winStreak, int looseStreak);
    }
}

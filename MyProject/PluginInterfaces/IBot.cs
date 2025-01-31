namespace MyProject.PluginInterfaces
{
    public interface IBot
    {
        void MapStartBehavior();

        void WarmupEndBehavior(int botQuota);

        void RoundStartBehavior(int roundCount);

        void RoundEndBehavior(int botQuota, int roundCount);
    }
}

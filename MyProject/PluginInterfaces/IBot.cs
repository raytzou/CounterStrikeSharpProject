namespace MyProject.PluginInterfaces
{
    public interface IBot
    {
        void MapStartBehavior();

        void WarmupEndBehavior(int botQuota);

        void RoundStartBehavior();

        void RoundEndBehavior(int botQuota, int roundCount);
    }
}

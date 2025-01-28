namespace MyProject.PluginInterfaces
{
    public interface IBot
    {
        void WarmupBehavior();

        void WarmupEndBehavior(int botQuota);

        void RoundStartBehavior();

        void RoundEndBehavior(int botQuota, int roundCount);
    }
}

namespace MyProject.PluginInterfaces
{
    public interface IBot
    {
        void WarmupBehavior();

        void WarmupEndBehavior(int botQuota);

        void RoundStartBehavior(int roundCount, ref bool isBotFilled, int botQuota);

        void RoundEndBehavior(int botQuota);
    }
}

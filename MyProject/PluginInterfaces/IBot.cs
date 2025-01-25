namespace MyProject.PluginInterfaces
{
    public interface IBot
    {
        void WarmupBehavior(string currentMap);

        void WarmupEndBehavior();

        void RoundStartBehavior(int roundCount, ref bool isBotFilled, int botQuota, string currentMap);

        void RoundEndBehavior(ref bool isBotFilled);
    }
}

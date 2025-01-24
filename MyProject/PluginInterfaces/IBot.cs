namespace MyProject.PluginInterfaces
{
    public interface IBot
    {
        void RoundStartBehavior(int roundCount, ref bool isBotFilled, int botQuota, string currentMap);

        void RoundEndBehavior(ref bool isBotFilled);
    }
}

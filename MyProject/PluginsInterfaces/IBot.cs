﻿namespace MyProject.PluginsInterfaces
{
    public interface IBot
    {
        void RoundStartBehavior(int roundCount, ref bool isBotFilled, int botQuota, string currentMap);
    }
}

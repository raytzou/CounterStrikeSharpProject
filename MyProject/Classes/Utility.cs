using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;

namespace MyProject.Classes
{
    public class Utility
    {
        public readonly static List<CounterStrikeSharp.API.Modules.Timers.Timer> Timers = new List<CounterStrikeSharp.API.Modules.Timers.Timer>();

        /// <summary>
        /// This is for debugging purposes only. There should be no references to this method.
        /// </summary>
        public static void DebugLogger<T>(ILogger<T> logger, string content) where T : class
        {
            logger.LogInformation(content);
        }

        /// <summary>
        /// It's the same as AddTimer() in BasicPlugin, but I want to use it elsewhere
        /// </summary>
        public static CounterStrikeSharp.API.Modules.Timers.Timer MyAddTimer(float interval, Action callback, TimerFlags? flags = null)
        {
            var timer = new CounterStrikeSharp.API.Modules.Timers.Timer(interval, callback, flags ?? 0);
            Timers.Add(timer);
            return timer;
        }
    }
}

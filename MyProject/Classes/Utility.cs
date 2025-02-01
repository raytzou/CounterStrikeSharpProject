using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Runtime.Serialization;

namespace MyProject.Classes
{
    public class Utility
    {
        public readonly static List<CounterStrikeSharp.API.Modules.Timers.Timer> Timers = [];

        /// <summary>
        /// This is for debugging purposes only. There should be no references to this method.
        /// </summary>
        public static void DebugLogger<T>(ILogger<T> logger, string content) where T : class
        {
            logger.LogInformation("{content}", content);
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

        /// <summary>
        /// Uses reflection to get the <see cref="EnumMemberAttribute"/> value of a CsItem enum.
        /// </summary>
        /// <param name="item">The enum option of <see cref="CsItem"/>.</param>
        /// <returns><c>string</c> - The item entity name.</returns>
        public static string GetCsItemEnumValue(CsItem item)
        {
            return item.GetType().GetMember(item.ToString())[0].GetCustomAttribute<EnumMemberAttribute>()!.Value!;
        }
    }
}

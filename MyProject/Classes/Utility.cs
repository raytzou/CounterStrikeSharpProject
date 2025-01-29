using Microsoft.Extensions.Logging;

namespace MyProject.Classes
{
    public class Utility
    {
        /// <summary>
        /// This is for debugging purposes only. There should be no references to this method.
        /// </summary>
        public static void DebugLogger<T>(ILogger<T> logger, string content) where T : class
        {
            logger.LogInformation(content);
        }
    }
}

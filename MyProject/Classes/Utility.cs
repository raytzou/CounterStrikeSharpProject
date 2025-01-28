using Microsoft.Extensions.Logging;

namespace MyProject.Classes
{
    public class Utility
    {
        public static void DebugLogger<T>(ILogger<T> logger, string content) where T : class
        {
            logger.LogInformation(content);
        }
    }
}

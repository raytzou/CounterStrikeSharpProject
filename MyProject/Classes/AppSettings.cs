using CounterStrikeSharp.API.Core;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace MyProject.Classes
{
    public class AppSettings : BasePluginConfig
    {
        private const string DefaultCaller = "NOT_CALL_FROM_MAIN";

        [JsonPropertyName("DebugMode")]
        public bool IsDebugMode { get; private set; }

        internal void SetDebugMode(bool value, [CallerMemberName] string caller = DefaultCaller)
        {
            // Personally, I just want to make this method only accessible in Main.OnConfigParsed.
            // Bad design anyway
            if (caller != nameof(Main.OnConfigParsed))
                throw new InvalidOperationException($"You can only set DebugMode in {nameof(Main.OnConfigParsed)}！");

            IsDebugMode = value;
        }
    }
}

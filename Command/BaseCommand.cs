using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;

namespace MyProject
{
    public class BaseCommand : MyBasePlugin
    {
        public BaseCommand(ILogger<BaseCommand> logger) : base(logger)
        {
            _logger = logger;
        }

        public override string ModuleName => "Base Command";
        public override string ModuleVersion => "0.87";

        private readonly ILogger<BaseCommand> _logger;

        public override void Load(bool hotReload)
        {
            Server.PrintToConsole("base command plugin works");
        }
    }
}

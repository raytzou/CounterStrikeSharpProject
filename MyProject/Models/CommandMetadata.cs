using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;

namespace MyProject.Models
{
    public record CommandMetadata
    {
        public required string Name { get; init; }
        public required string Description { get; init; }
        public string[]? Permissions { get; init; }
    }
}

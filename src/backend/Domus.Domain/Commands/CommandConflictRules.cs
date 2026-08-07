using Domus.Domain.Enums;

namespace Domus.Domain.Commands;

public static class CommandConflictRules
{
    public static bool IsInFlight(CommandStatus status) =>
        status is CommandStatus.Pending or CommandStatus.Sent or CommandStatus.Delivered;

    /// <summary>
    /// OPEN/CLOSE conflitam entre si e consigo. STOP só conflita com outro STOP
    /// (permite interromper um OPEN/CLOSE em andamento).
    /// </summary>
    public static bool Conflicts(CommandAction existing, CommandAction incoming)
    {
        if (existing == CommandAction.Stop || incoming == CommandAction.Stop)
        {
            return existing == CommandAction.Stop && incoming == CommandAction.Stop;
        }

        return true;
    }
}

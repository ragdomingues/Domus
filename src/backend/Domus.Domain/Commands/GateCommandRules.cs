using Domus.Domain.Enums;

namespace Domus.Domain.Commands;

public static class GateCommandRules
{
    public static bool IsRedundant(CommandAction action, GateState state) =>
        (action == CommandAction.Open && state == GateState.Open) ||
        (action == CommandAction.Close && state == GateState.Closed);
}

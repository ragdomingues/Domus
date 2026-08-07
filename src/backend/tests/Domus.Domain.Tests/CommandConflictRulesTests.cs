using Domus.Domain.Commands;
using Domus.Domain.Enums;
using FluentAssertions;

namespace Domus.Domain.Tests;

public class CommandConflictRulesTests
{
    [Theory]
    [InlineData(CommandAction.Open, CommandAction.Close, true)]
    [InlineData(CommandAction.Close, CommandAction.Open, true)]
    [InlineData(CommandAction.Open, CommandAction.Open, true)]
    [InlineData(CommandAction.Open, CommandAction.Stop, false)]
    [InlineData(CommandAction.Stop, CommandAction.Close, false)]
    [InlineData(CommandAction.Stop, CommandAction.Stop, true)]
    public void Conflicts_matrix(CommandAction existing, CommandAction incoming, bool expected)
    {
        CommandConflictRules.Conflicts(existing, incoming).Should().Be(expected);
    }

    [Theory]
    [InlineData(CommandAction.Open, GateState.Open, true)]
    [InlineData(CommandAction.Close, GateState.Closed, true)]
    [InlineData(CommandAction.Open, GateState.Closed, false)]
    [InlineData(CommandAction.Close, GateState.Open, false)]
    [InlineData(CommandAction.Open, GateState.Moving, false)]
    [InlineData(CommandAction.Stop, GateState.Open, false)]
    public void Gate_redundant_rules(CommandAction action, GateState state, bool expected)
    {
        GateCommandRules.IsRedundant(action, state).Should().Be(expected);
    }
}

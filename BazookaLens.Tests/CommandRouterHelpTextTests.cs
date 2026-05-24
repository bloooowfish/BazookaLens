using BazookaLens.Commands;

namespace BazookaLens.Tests;

public sealed class CommandRouterHelpTextTests
{
    public static IEnumerable<object[]> HelpEntries =>
        CommandRouter.HelpEntries.Select(entry => new object[] { entry });

    [Theory]
    [MemberData(nameof(HelpEntries))]
    internal void HelpTextIncludesEveryUsage(BlensCommandHelpEntry entry)
    {
        Assert.Contains(entry.Usage, CommandRouter.HelpText, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(HelpEntries))]
    internal void HelpEntrySampleInvocationParses(BlensCommandHelpEntry entry)
    {
        var parsed = CommandRouter.Parse(entry.SampleInvocation);

        Assert.Equal(entry.ExpectedCommand, parsed.Command);
    }
}

using Jellyfin.Plugin.ContentWarnings.Core;

namespace Jellyfin.Plugin.ContentWarnings.Tests;

public class WarningEvaluatorTests
{
    private static readonly WarningOptions Defaults = new(
        MinYesVotes: 3,
        MinYesRatio: 0.6,
        EnabledTopicIds: [],
        TopicLabelOverrides: null,
        IncludeSpoilerTopics: false,
        TagPrefix: "CW: ");

    [Fact]
    public void Evaluate_AppliesVoteThreshold()
    {
        var warnings = WarningEvaluator.Evaluate([new TopicVote(1, "a dog dies", 2, 0)], Defaults);

        Assert.Empty(warnings);
    }

    [Fact]
    public void Evaluate_AppliesRatioThreshold()
    {
        var warnings = WarningEvaluator.Evaluate([new TopicVote(1, "a dog dies", 3, 7)], Defaults);

        Assert.Empty(warnings);
    }

    [Fact]
    public void Evaluate_IncludesDefaultTopicAboveThresholds()
    {
        var warnings = WarningEvaluator.Evaluate([new TopicVote(153, "a dog dies", 41, 2)], Defaults);

        var warning = Assert.Single(warnings);
        Assert.Equal("Dog Death", warning.Label);
        Assert.Equal("CW: Dog Death", warning.Tag);
        Assert.Equal(41, warning.Yes);
        Assert.Equal(2, warning.No);
    }

    [Fact]
    public void Evaluate_FiltersNonDefaultTopicsWhenNoExplicitList()
    {
        var warnings = WarningEvaluator.Evaluate([new TopicVote(999, "there is a car crash", 10, 0)], Defaults);

        Assert.Empty(warnings);
    }

    [Fact]
    public void Evaluate_ExplicitTopicListIncludesNonDefaultTopics()
    {
        var options = Defaults with { EnabledTopicIds = [999] };

        var warnings = WarningEvaluator.Evaluate([new TopicVote(999, "there is a car crash", 10, 0)], options);

        var warning = Assert.Single(warnings);
        Assert.Equal("Car crash", warning.Label);
    }

    [Fact]
    public void Evaluate_ExplicitTopicListExcludesDefaultsOutsideTheList()
    {
        var options = Defaults with { EnabledTopicIds = [999] };

        var warnings = WarningEvaluator.Evaluate(
            [
                new TopicVote(153, "a dog dies", 41, 2),
                new TopicVote(999, "there is a car crash", 10, 0)
            ],
            options);

        Assert.Single(warnings);
        Assert.Equal(999, warnings[0].TopicId);
    }

    [Fact]
    public void Evaluate_ExcludesSpoilersByDefault()
    {
        var warnings = WarningEvaluator.Evaluate(
            [new TopicVote(153, "a dog dies", 41, 2, IsSpoiler: true)],
            Defaults);

        Assert.Empty(warnings);
    }

    [Fact]
    public void Evaluate_IncludesSpoilersWhenConfigured()
    {
        var options = Defaults with { IncludeSpoilerTopics = true };

        var warnings = WarningEvaluator.Evaluate(
            [new TopicVote(153, "a dog dies", 41, 2, IsSpoiler: true)],
            options);

        Assert.Single(warnings);
        Assert.True(warnings[0].IsSpoiler);
    }

    [Fact]
    public void Evaluate_AppliesLabelOverrideAndPrefix()
    {
        var options = Defaults with
        {
            TopicLabelOverrides = new Dictionary<int, string> { [153] = "Muerte de perro" },
            TagPrefix = "AVISO: "
        };

        var warning = Assert.Single(
            WarningEvaluator.Evaluate([new TopicVote(153, "a dog dies", 41, 2)], options));

        Assert.Equal("AVISO: Muerte de perro", warning.Tag);
    }

    [Fact]
    public void Evaluate_UsesDoesNameForExplicitNonDefaultTopic()
    {
        var options = Defaults with { EnabledTopicIds = [999] };

        var warning = Assert.Single(WarningEvaluator.Evaluate(
            [new TopicVote(999, "trigger", 10, 0, "Are there loud noises?")],
            options));

        Assert.Equal("Loud noises", warning.Label);
    }

    [Fact]
    public void Evaluate_OrdersByLabel()
    {
        var warnings = WarningEvaluator.Evaluate(
            [
                new TopicVote(1, "there are spiders", 10, 0),
                new TopicVote(2, "a dog dies", 10, 0)
            ],
            Defaults);

        Assert.Equal(["Dog Death", "Spiders"], warnings.Select(warning => warning.Label));
    }

    [Fact]
    public void Evaluate_IgnoresZeroVotes()
    {
        var warnings = WarningEvaluator.Evaluate([new TopicVote(1, "a dog dies", 0, 0)], Defaults);

        Assert.Empty(warnings);
    }

    [Fact]
    public void Evaluate_RoundsRatio()
    {
        var warning = Assert.Single(WarningEvaluator.Evaluate(
            [new TopicVote(153, "a dog dies", 1, 2)],
            Defaults with { MinYesVotes = 1, MinYesRatio = 0.3 }));

        Assert.Equal(0.3333, warning.Ratio, 4);
    }
}

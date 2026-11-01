using Jellyfin.Plugin.ContentWarnings.Core;

namespace Jellyfin.Plugin.ContentWarnings.Tests;

public class MatchScorerTests
{
    [Fact]
    public void SelectBest_AcceptsExactTitleYearAndType()
    {
        var decision = MatchScorer.SelectBest(
            "The Thing",
            1982,
            true,
            [new MatchCandidate(1, "The Thing", 1982, true)]);

        Assert.True(decision.IsMatch);
        Assert.Equal(1, decision.DtddId);
        Assert.Equal(100, decision.Score);
    }

    [Fact]
    public void SelectBest_AcceptsHighSimilarityWithYear()
    {
        var decision = MatchScorer.SelectBest(
            "Blade Runner",
            1982,
            true,
            [new MatchCandidate(2, "Blade Runners", 1982, true)]);

        Assert.True(decision.IsMatch);
    }

    [Fact]
    public void SelectBest_RejectsAmbiguousTie()
    {
        var decision = MatchScorer.SelectBest(
            "Home",
            2015,
            true,
            [
                new MatchCandidate(1, "Home", 2015, true),
                new MatchCandidate(2, "Home", 2015, true)
            ]);

        Assert.False(decision.IsMatch);
        Assert.Equal(100, decision.Score);
        Assert.Equal(100, decision.RunnerUpScore);
    }

    [Fact]
    public void SelectBest_RejectsCloseRunnerUp()
    {
        var decision = MatchScorer.SelectBest(
            "The Thing",
            1982,
            true,
            [
                new MatchCandidate(1, "The Thing", 1982, true),
                new MatchCandidate(2, "The Thing", 1982, false)
            ]);

        Assert.False(decision.IsMatch);
    }

    [Fact]
    public void SelectBest_RejectsWeakScore()
    {
        var decision = MatchScorer.SelectBest(
            "Alien",
            1979,
            true,
            [new MatchCandidate(1, "Aliens", 1986, true)]);

        Assert.False(decision.IsMatch);
        Assert.Equal(10, decision.Score);
    }

    [Fact]
    public void SelectBest_YearDeltaOneGivesPartialCredit()
    {
        var score = MatchScorer.Score("The Thing", 1982, true, new MatchCandidate(1, "The Thing", 1983, true));

        Assert.Equal(85, score);
    }

    [Fact]
    public void SelectBest_EmptyCandidates()
    {
        var decision = MatchScorer.SelectBest("Home", 2015, true, []);

        Assert.False(decision.IsMatch);
        Assert.Equal(0, decision.Score);
    }

    [Fact]
    public void SelectBest_PicksClearWinner()
    {
        var decision = MatchScorer.SelectBest(
            "Halloween",
            1978,
            true,
            [
                new MatchCandidate(1, "Halloween", 1978, true),
                new MatchCandidate(2, "Halloween II", 1981, true),
                new MatchCandidate(3, "Halloween", 2018, true)
            ]);

        Assert.True(decision.IsMatch);
        Assert.Equal(1, decision.DtddId);
    }
}

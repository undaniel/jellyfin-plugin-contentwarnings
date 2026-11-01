namespace Jellyfin.Plugin.ContentWarnings.Core;

/// <summary>
/// A candidate returned by the DoesTheDogDie search endpoint.
/// </summary>
/// <param name="Id">DoesTheDogDie item id.</param>
/// <param name="Name">Candidate title.</param>
/// <param name="Year">Release year, if known.</param>
/// <param name="IsMovie">True for movies, false for series.</param>
public sealed record MatchCandidate(int Id, string Name, int? Year, bool IsMovie);

/// <summary>
/// The outcome of scoring a list of candidates.
/// </summary>
/// <param name="DtddId">The accepted id, or null when no candidate is safe.</param>
/// <param name="Score">Score of the best candidate.</param>
/// <param name="RunnerUpScore">Score of the second best candidate.</param>
public sealed record MatchDecision(int? DtddId, double Score, double RunnerUpScore)
{
    /// <summary>
    /// Gets a value indicating whether a candidate was accepted.
    /// </summary>
    public bool IsMatch => DtddId.HasValue;
}

/// <summary>
/// Scores Jellyfin items against DoesTheDogDie search candidates.
/// </summary>
public static class MatchScorer
{
    /// <summary>
    /// Minimum score for an automatic match.
    /// </summary>
    public const double MinimumScore = 80;

    /// <summary>
    /// Minimum lead over the runner-up for an automatic match.
    /// </summary>
    public const double MinimumLead = 15;

    /// <summary>
    /// Scores one candidate.
    /// </summary>
    /// <param name="itemTitle">Jellyfin item title.</param>
    /// <param name="itemYear">Jellyfin production year, if known.</param>
    /// <param name="itemIsMovie">True when the Jellyfin item is a movie.</param>
    /// <param name="candidate">Candidate to score.</param>
    /// <returns>A score between 0 and 100.</returns>
    public static int Score(string? itemTitle, int? itemYear, bool itemIsMovie, MatchCandidate candidate)
    {
        var score = 0;
        var itemNormalized = TitleNormalizer.Normalize(itemTitle);
        var candidateNormalized = TitleNormalizer.Normalize(candidate.Name);

        if (itemNormalized.Length > 0 && itemNormalized == candidateNormalized)
        {
            score += 60;
        }
        else if (TitleNormalizer.Similarity(itemTitle, candidate.Name) >= 0.9)
        {
            score += 40;
        }

        if (itemYear.HasValue && candidate.Year.HasValue)
        {
            var delta = Math.Abs(itemYear.Value - candidate.Year.Value);
            if (delta == 0)
            {
                score += 30;
            }
            else if (delta == 1)
            {
                score += 15;
            }
        }

        if (candidate.IsMovie == itemIsMovie)
        {
            score += 10;
        }

        return score;
    }

    /// <summary>
    /// Selects the best candidate, rejecting ties and weak leads.
    /// </summary>
    /// <param name="itemTitle">Jellyfin item title.</param>
    /// <param name="itemYear">Jellyfin production year, if known.</param>
    /// <param name="itemIsMovie">True when the Jellyfin item is a movie.</param>
    /// <param name="candidates">Candidates returned by the search.</param>
    /// <returns>The decision.</returns>
    public static MatchDecision SelectBest(
        string? itemTitle,
        int? itemYear,
        bool itemIsMovie,
        IEnumerable<MatchCandidate> candidates)
    {
        var scored = candidates
            .Select(candidate => (Candidate: candidate, Score: Score(itemTitle, itemYear, itemIsMovie, candidate)))
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Candidate.Id)
            .ToList();

        if (scored.Count == 0)
        {
            return new MatchDecision(null, 0, 0);
        }

        var best = scored[0];
        var runnerUp = scored.Count > 1 ? scored[1].Score : 0;

        if (best.Score < MinimumScore || best.Score - runnerUp < MinimumLead)
        {
            return new MatchDecision(null, best.Score, runnerUp);
        }

        return new MatchDecision(best.Candidate.Id, best.Score, runnerUp);
    }
}

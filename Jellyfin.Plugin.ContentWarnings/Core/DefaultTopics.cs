namespace Jellyfin.Plugin.ContentWarnings.Core;

/// <summary>
/// Curated high-value topics used when the administrator has not chosen an explicit list.
/// </summary>
public static class DefaultTopics
{
    /// <summary>
    /// A curated default topic. Patterns are matched against normalized topic names.
    /// </summary>
    /// <param name="Key">Stable key.</param>
    /// <param name="Label">Short English tag label.</param>
    /// <param name="Patterns">Normalized substrings that identify the topic.</param>
    public sealed record Rule(string Key, string Label, string[] Patterns);

    /// <summary>
    /// The curated rules, most specific first.
    /// </summary>
    public static readonly IReadOnlyList<Rule> Rules =
    [
        new("dog-death", "Dog Death", ["dog dies", "dog die", "dog death", "dog killed", "dog is killed", "dogs die"]),
        new("cat-death", "Cat Death", ["cat dies", "cat die", "cat death", "cat killed", "cat is killed", "cats die"]),
        new("animal-death", "Animal Death", ["animal dies", "animal die", "animal death", "animal killed", "animals die"]),
        new("animal-cruelty", "Animal Cruelty", ["animal cruelty", "animal abuse", "animal is abused", "animal is hurt", "animal is tortured"]),
        new("spiders", "Spiders", ["spider"]),
        new("snakes", "Snakes", ["snake"]),
        new("needles", "Needles", ["needle", "syringe", "injection"]),
        new("flashing-lights", "Flashing Lights", ["flashing light", "flashing", "strobe", "flickering light", "epilep"]),
        new("jump-scares", "Jump Scares", ["jump scare"]),
        new("blood-gore", "Blood/Gore", ["gore", "graphic violence", "blood"]),
        new("vomit", "Vomit", ["vomit", "throw up", "puke"]),
        new("sexual-assault", "Sexual Assault", ["sexual assault", "rape", "sexual violence"]),
        new("domestic-violence", "Domestic Violence", ["domestic abuse", "domestic violence", "abusive relationship"]),
        new("child-abuse", "Child Abuse", ["child abuse", "child is abused", "child is hit", "child is beaten"]),
        new("child-death", "Child Death", ["child dies", "child death", "kid dies", "baby dies", "infant dies"]),
        new("parent-death", "Parent Death", ["parent dies", "parent death", "mother dies", "father dies", "mom dies", "dad dies"]),
        new("suicide", "Suicide", ["suicide", "kills himself", "kills herself", "takes his own life", "takes her own life"]),
        new("self-harm", "Self-Harm", ["self harm", "cutting", "self mutilation"]),
        new("drugs", "Drug Use", ["drug use", "drug abuse", "drugs", "cocaine", "heroin", "methamphetamine", "overdose"]),
        new("claustrophobia", "Claustrophobia", ["claustrophob", "confined space", "buried alive"]),
        new("drowning", "Drowning", ["drown"])
    ];

    /// <summary>
    /// Returns the matching rule for a topic, or null.
    /// </summary>
    /// <param name="topicName">DoesTheDogDie topic name, for example "a dog dies".</param>
    /// <param name="doesName">DoesTheDogDie question form, if known.</param>
    /// <returns>The first matching rule.</returns>
    public static Rule? Match(string? topicName, string? doesName = null)
    {
        var normalized = TitleNormalizer.Normalize(topicName) + " " + TitleNormalizer.Normalize(doesName);
        foreach (var rule in Rules)
        {
            foreach (var pattern in rule.Patterns)
            {
                if (normalized.Contains(pattern, StringComparison.Ordinal))
                {
                    return rule;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Returns whether a topic belongs to the curated defaults.
    /// </summary>
    /// <param name="topicName">DoesTheDogDie topic name.</param>
    /// <param name="doesName">DoesTheDogDie question form, if known.</param>
    /// <returns>True when the topic is a default.</returns>
    public static bool IsDefault(string? topicName, string? doesName = null) => Match(topicName, doesName) is not null;

    /// <summary>
    /// Returns the short label for a default topic, or null.
    /// </summary>
    /// <param name="topicName">DoesTheDogDie topic name.</param>
    /// <param name="doesName">DoesTheDogDie question form, if known.</param>
    /// <returns>The label, or null when the topic is not a default.</returns>
    public static string? GetLabel(string? topicName, string? doesName = null) => Match(topicName, doesName)?.Label;
}

namespace Jellyfin.Plugin.ContentWarnings.Dtdd;

/// <summary>
/// Base class for DoesTheDogDie client failures.
/// </summary>
public class DtddException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DtddException"/> class.
    /// </summary>
    /// <param name="message">Message.</param>
    public DtddException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DtddException"/> class.
    /// </summary>
    /// <param name="message">Message.</param>
    /// <param name="innerException">Inner exception.</param>
    public DtddException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The API key is missing, invalid or not allowed.
/// </summary>
public sealed class DtddAuthException : DtddException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DtddAuthException"/> class.
    /// </summary>
    /// <param name="message">Message.</param>
    public DtddAuthException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// DoesTheDogDie returned 404 for the requested item.
/// </summary>
public sealed class DtddNotFoundException : DtddException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DtddNotFoundException"/> class.
    /// </summary>
    /// <param name="message">Message.</param>
    public DtddNotFoundException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// DoesTheDogDie is rate limiting, failing, or the circuit breaker opened.
/// </summary>
public sealed class DtddUnavailableException : DtddException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DtddUnavailableException"/> class.
    /// </summary>
    /// <param name="message">Message.</param>
    public DtddUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DtddUnavailableException"/> class.
    /// </summary>
    /// <param name="message">Message.</param>
    /// <param name="innerException">Inner exception.</param>
    public DtddUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

namespace AISwarm.Infrastructure.Eventing;

/// <summary>
/// Utility class for common parameter validation in event services
/// </summary>
public static class EventValidation
{
    public static void ValidateRequiredId(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} must be provided", paramName);
    }

    public static void ValidateRequiredCollection<T>(IReadOnlyList<T>? collection, string paramName)
    {
        if (collection == null || collection.Count == 0)
            throw new ArgumentException($"{paramName} must be provided and not empty", paramName);
    }
}

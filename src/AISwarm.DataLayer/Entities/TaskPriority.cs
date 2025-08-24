namespace AISwarm.DataLayer.Entities;

/// <summary>
///     Priority levels for work items, ordered from lowest to highest priority
/// </summary>
public enum TaskPriority
{
    /// <summary>
    ///     Low priority tasks - documentation, cleanup, non-urgent improvements
    /// </summary>
    Low = 1,

    /// <summary>
    ///     Normal priority tasks - regular development work, standard reviews
    /// </summary>
    Normal = 2,

    /// <summary>
    ///     High priority tasks - important features, critical reviews
    /// </summary>
    High = 3,

    /// <summary>
    ///     Critical priority tasks - security issues, production bugs, urgent fixes
    /// </summary>
    Critical = 4
}

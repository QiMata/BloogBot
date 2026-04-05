namespace BloogBot.AI.States;

/// <summary>
/// Represents a minor state scoped to a specific BotActivity.
/// Minor states provide granular tracking within major activity states.
/// Each minor state has a parent activity, ensuring it cannot be used
/// outside its intended context.
/// </summary>
public sealed record MinorState
{
    /// <summary>
    /// The parent BotActivity this minor state belongs to.
    /// </summary>
    public BotActivity ParentActivity { get; }

    /// <summary>
    /// The name of the minor state (verb-based, e.g., "Approaching", "Casting").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Human-readable description of what this minor state represents.
    /// </summary>
    public string Description { get; }

    public MinorState(BotActivity parentActivity, string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Minor state name cannot be empty", nameof(name));

        ParentActivity = parentActivity;
        Name = name;
        Description = description ?? string.Empty;
    }

    /// <summary>
    /// Creates a "None" minor state for the specified activity.
    /// Used as the default when no specific minor state is active.
    /// </summary>
    public static MinorState None(BotActivity activity) =>
        new(activity, "None", "Default state - no specific minor state active");

    /// <summary>
    /// Returns a string representation for logging and auditing.
    /// </summary>
    public override string ToString() => $"{ParentActivity}.{Name}";
}

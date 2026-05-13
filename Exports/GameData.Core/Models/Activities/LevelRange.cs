namespace GameData.Core.Models.Activities
{
    /// <summary>
    /// Inclusive level band (1..60) for an activity. Per Spec/04 the catalog
    /// test asserts <c>1 &lt;= Min &lt;= Max &lt;= 60</c>.
    /// </summary>
    public sealed record LevelRange(int Min, int Max);
}

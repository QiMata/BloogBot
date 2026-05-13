using System.Collections.Generic;
using GameData.Core.Models.Activities;

namespace WoWStateManager.Activities
{
    /// <summary>
    /// Read-only view onto the compiled <see cref="ActivityDefinition"/>
    /// catalog. The implementation (<see cref="ActivityCatalog"/>) is a
    /// singleton built from the literals authored under
    /// <c>docs/Plan/Activities/_catalog_rows/*.md</c>.
    /// </summary>
    public interface IActivityCatalog
    {
        /// <summary>
        /// Monotonically-increasing version stamp bumped whenever the
        /// compiled catalog changes shape. Consumed by tests and
        /// observability sinks.
        /// </summary>
        int CatalogVersion { get; }

        /// <summary>
        /// All catalog rows, in <c>docs/Plan/Activities/00_INDEX.md</c>
        /// order.
        /// </summary>
        IReadOnlyList<ActivityDefinition> All { get; }

        /// <summary>
        /// Lookup by <see cref="ActivityDefinition.Id"/>. Returns false
        /// (and a null <paramref name="def"/>) when no row matches.
        /// </summary>
        bool TryGetById(string id, out ActivityDefinition? def);
    }
}

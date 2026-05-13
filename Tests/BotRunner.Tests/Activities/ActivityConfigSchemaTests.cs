using Xunit;

namespace BotRunner.Tests.Activities;

// =============================================================================
// STUB — Phase 0 slot S0.11
// =============================================================================
// Eventual assertion (wired by a follow-up slot once a JSON Schema library
// is added as a dependency):
//
//   1. Enumerate every file matching `Config/activities/*.json`.
//   2. Load `Config/schema/activity.schema.json` as a JSON Schema
//      draft-2020-12 document.
//   3. For each config file:
//        a. Parse the JSON.
//        b. Validate against the schema.
//        c. Collect every validation error with file path + JSON pointer
//           + error message.
//   4. Assert zero errors. On failure, the assertion message lists each
//      error so the operator can fix the misformatted config without
//      re-running.
//   5. Additional cross-checks (still inside this same test or split):
//        a. `ActivityId` field matches the file's basename
//           (e.g. `dungeon.ragefire-chasm.json` -> `dungeon.ragefire-chasm`).
//        b. Every `ActivityId` is a row in `ActivityCatalog`
//           (Services/WoWStateManager/Activities/ActivityCatalog.cs).
//        c. Every `StagingLocation` resolves in `Bot/named-locations.json`.
//        d. `RoleOverrides` (when non-null) sums to a count inside the
//           catalog row's [MinPlayers..MaxPlayers] window.
//        e. The `Loadout` sub-document matches the same shape consumed
//           by `LoadoutSpecConverter.ToProto`
//           (Services/WoWStateManager/Settings/LoadoutSpecConverter.cs)
//           — round-trip deserialize -> serialize must be lossless.
//
// JSON Schema library status (as of S0.11 authorship 2026-05-12):
//
//   - Neither `JsonSchema.Net` (Json.Everything) nor
//     `Newtonsoft.Json.Schema` is currently a dependency of BotRunner.Tests
//     or Tests.Infrastructure.
//   - The follow-up slot that lands the real assertion should add ONE of
//     them. Preference: `JsonSchema.Net` for native draft-2020-12
//     support (matches the `$schema` URI in
//     `Config/schema/activity.schema.json`).
// =============================================================================
public sealed class ActivityConfigSchemaTests
{
    [Fact(Skip = "Wired by follow-up slot once a JSON Schema library is added to BotRunner.Tests dependencies.")]
    public void AllConfigsValidateAgainstSchema()
    {
        // Intentionally empty stub. See class-level comment for the
        // eventual assertion.
    }
}

using Tests.Infrastructure;

namespace Navigation.Physics.Tests;

/// <summary>
/// Smoke tests that verify Navigation.dll exists and can be loaded.
/// These run first (no services needed) and fail fast if the DLL is missing.
/// </summary>
[Trait(TestCategories.Feature, TestCategories.NativeDll)]
public class DllAvailabilityTests
{
    [Fact]
    public void NavigationDll_ShouldExistInOutputDirectory()
    {
        var dllPath = Path.Combine(AppContext.BaseDirectory, "Navigation.dll");
        Assert.True(File.Exists(dllPath),
            $"Navigation.dll not found at {dllPath}. Build the Navigation C++ project (x64) first.");
    }

    [Fact]
    public void NavigationDll_ShouldLoadAndInitializePhysics()
    {
        try
        {
            var result = NavigationInterop.InitializePhysics();
            Assert.True(result, "InitializePhysics() returned false — DLL loaded but initialization failed.");
            NavigationInterop.ShutdownPhysics();
        }
        catch (DllNotFoundException ex)
        {
            Assert.Fail($"Navigation.dll could not be loaded: {ex.Message}");
        }
        catch (BadImageFormatException ex)
        {
            Assert.Fail($"Navigation.dll architecture mismatch (test=x64, DLL may be x86): {ex.Message}");
        }
    }

    [Fact]
    public void PhysicsEngine_Constants_ShouldReturnSaneValues()
    {
        PhysicsEngineFixture? fixture = null;
        try
        {
            fixture = new PhysicsEngineFixture();
            if (!fixture.IsInitialized)
            {
                // DLL not loadable — first two tests cover this case with clear messages
                return;
            }

            NavigationInterop.GetPhysicsConstants(
                out float gravity,
                out float jumpVelocity,
                out float stepHeight,
                out float stepDownHeight,
                out float walkableMinNormalZ);

            // Gravity should be positive (magnitude, applied downward by the engine)
            Assert.True(gravity > 0, $"Expected positive gravity magnitude, got {gravity:F3}");
            Assert.InRange(gravity, 15.0f, 25.0f); // WoW uses ~19.29

            // Jump velocity should be positive (upward)
            Assert.True(jumpVelocity > 0, $"Expected positive jump velocity, got {jumpVelocity:F3}");

            // Step height should be reasonable (WoW uses ~2.1 yards)
            Assert.InRange(stepHeight, 0.1f, 5.0f);

            // Walkable normal Z should be between 0 and 1
            Assert.InRange(walkableMinNormalZ, 0.1f, 1.0f);
        }
        finally
        {
            fixture?.Dispose();
        }
    }
}

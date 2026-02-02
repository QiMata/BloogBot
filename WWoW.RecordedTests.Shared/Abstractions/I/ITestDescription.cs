using System.Threading;
using System.Threading.Tasks;

namespace WWoW.RecordedTests.Shared.Abstractions.I;

public interface ITestDescription
{
    string Name { get; }
    Task<OrchestrationResult> ExecuteAsync(IRecordedTestContext context, CancellationToken cancellationToken);
}
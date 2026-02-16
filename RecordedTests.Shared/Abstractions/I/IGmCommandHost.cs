using System.Threading;
using System.Threading.Tasks;

namespace RecordedTests.Shared.Abstractions.I;

public interface IGmCommandHost
{
    Task<GmCommandExecutionResult> ExecuteGmCommandAsync(string command, CancellationToken cancellationToken);
}

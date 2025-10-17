namespace WWoW.RecordedTests.Shared.Abstractions.I;

using System.Threading;
using System.Threading.Tasks;

public interface IRecordedTestStorage
{
    Task StoreAsync(RecordedTestStorageContext context, CancellationToken cancellationToken);
}

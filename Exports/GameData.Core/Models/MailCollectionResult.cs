namespace GameData.Core.Models
{
    public sealed record MailCollectionResult(
        bool MailboxOpened,
        int InboxCount,
        int CollectedCount,
        uint MoneyRequestedCopper,
        int DeletedEmptyMessages,
        bool CoinageIncreaseObserved,
        string CollectedSubjects,
        string DeletedSubjects)
    {
        public static MailCollectionResult UnknownSuccess { get; } = new(
            MailboxOpened: true,
            InboxCount: 0,
            CollectedCount: 0,
            MoneyRequestedCopper: 0,
            DeletedEmptyMessages: 0,
            CoinageIncreaseObserved: false,
            CollectedSubjects: string.Empty,
            DeletedSubjects: string.Empty);
    }
}

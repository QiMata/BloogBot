using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Client;

namespace WoWSharpClient.Screens
{
    public class LoginScreen(WoWClient woWClient) : ILoginScreen
    {
        public bool IsOpen => true;
        public uint QueuePosition => 0;
        public bool IsLoggedIn => woWClient.IsLoggedIn;

        public void CancelLogin()
        {
        }

        public void Login(string username, string password) => _ = woWClient.LoginAsync(username, password);
    }

    public class RealmSelectScreen(WoWClient woWClient) : IRealmSelectScreen
    {
        public bool IsOpen => true;
        public Realm? CurrentRealm { get; private set; }

        public void CancelRealmSelection()
        {
        }

        public List<Realm> GetRealmList() => woWClient.GetRealmListAsync().GetAwaiter().GetResult();

        public void SelectRealm(Realm realm)
        {
            _ = woWClient?.SelectRealmAsync(realm);
            CurrentRealm = realm;
        }

        public void SelectRealmType(RealmType realmType)
        {
        }
    }

    public class CharacterSelectScreen(WoWClient woWClient) : ICharacterSelectScreen
    {
        internal static TimeSpan CharacterListRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

        private long _characterListRetryAttemptId;

        public bool IsOpen => true;
        public bool HasReceivedCharacterList { get; set; }
        public bool HasRequestedCharacterList { get; set; }
        public bool IsCharacterCreationPending { get; private set; }
        public DateTime LastCharacterListRequestUtc { get; private set; }
        public int CharacterCreateAttempts { get; private set; }

        public void RefreshCharacterListFromServer()
        {
            if (woWClient.WorldClient?.IsAuthenticated != true)
            {
                ResetCharacterListRequest();
                return;
            }

            _ = woWClient.RefreshCharacterSelectsAsync();
            HasRequestedCharacterList = true;
            HasReceivedCharacterList = false;
            LastCharacterListRequestUtc = DateTime.UtcNow;
            ScheduleCharacterListRetry();
        }

        public void CreateCharacter(
            string name,
            Race race,
            Gender gender,
            Class @class,
            byte skinColor,
            byte face,
            byte hairStyle,
            byte hairColor,
            byte facialHair,
            byte outfitId)
        {
            if (woWClient.WorldClient?.IsAuthenticated != true)
            {
                ResetCharacterListRequest();
                return;
            }

            _ = woWClient.SendCharacterCreateAsync(
                name,
                race,
                @class,
                gender,
                skinColor,
                face,
                hairStyle,
                hairColor,
                facialHair,
                outfitId);
            CancelCharacterListRetry();
            HasRequestedCharacterList = false;
            HasReceivedCharacterList = false;
            IsCharacterCreationPending = true;
            CharacterCreateAttempts++;
        }

        public void DeleteCharacter(ulong characterGuid)
        {
            _ = woWClient.SendCharacterDeleteAsync(characterGuid);
            // Do NOT reset HasReceivedCharacterList/HasRequestedCharacterList here.
            // HandleCharDelete resets them on SMSG_CHAR_DELETE success, preventing
            // premature CMSG_CHAR_ENUM spam before the server processes the delete.
        }

        public void MarkCharacterListLoaded()
        {
            CancelCharacterListRetry();
            HasRequestedCharacterList = false;
            HasReceivedCharacterList = true;

            if (CharacterSelects.Count > 0)
            {
                IsCharacterCreationPending = false;
                CharacterCreateAttempts = 0;
            }
        }

        public void ResetCharacterListRequest()
        {
            CancelCharacterListRetry();
            HasRequestedCharacterList = false;
            HasReceivedCharacterList = false;
            IsCharacterCreationPending = false;
        }

        public void HandleCharacterCreateResponse(CreateCharacterResult result)
        {
            switch (result)
            {
                case CreateCharacterResult.Success:
                    IsCharacterCreationPending = false;
                    RefreshCharacterListFromServer();
                    return;

                case CreateCharacterResult.InProgress:
                    IsCharacterCreationPending = true;
                    RefreshCharacterListFromServer();
                    return;

                default:
                    ResetCharacterListRequest();
                    return;
            }
        }

        public bool ShouldRetryCharacterListRequest(TimeSpan retryAfter, DateTime utcNow)
        {
            return HasRequestedCharacterList
                && !HasReceivedCharacterList
                && LastCharacterListRequestUtc != default
                && utcNow - LastCharacterListRequestUtc >= retryAfter;
        }

        private void ScheduleCharacterListRetry()
        {
            var attemptId = Interlocked.Increment(ref _characterListRetryAttemptId);
            Task.Delay(CharacterListRetryDelay).ContinueWith(_ =>
            {
                if (Interlocked.Read(ref _characterListRetryAttemptId) != attemptId)
                    return;

                if (HasReceivedCharacterList || !HasRequestedCharacterList)
                    return;

                if (woWClient.WorldClient?.IsAuthenticated != true)
                {
                    ResetCharacterListRequest();
                    return;
                }

                RefreshCharacterListFromServer();
            }, TaskScheduler.Default);
        }

        private void CancelCharacterListRetry() => Interlocked.Increment(ref _characterListRetryAttemptId);

        public List<CharacterSelect> CharacterSelects { get; } = [];
    }
}

using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Models;
using System.Collections.Generic;

namespace WoWSharpClient.Screens
{
    internal sealed class InProcessLoginScreen : ILoginScreen
    {
        public bool IsOpen => false; // injected process assumed already logged in
        public uint QueuePosition => 0;
        public bool IsLoggedIn => true;
        public void CancelLogin() { }
        public void Login(string username, string password) { }
    }

    internal sealed class InProcessRealmSelectScreen : IRealmSelectScreen
    {
        public bool IsOpen => false;
        public Realm? CurrentRealm { get; private set; }
        public void CancelRealmSelection() { }
        public List<Realm> GetRealmList() => [];
        public void SelectRealm(Realm realm) { CurrentRealm = realm; }
        public void SelectRealmType(RealmType realmType) { }
    }

    internal sealed class InProcessCharacterSelectScreen : ICharacterSelectScreen
    {
        public bool IsOpen => false;
        public bool HasReceivedCharacterList { get; set; } = true;
        public bool HasRequestedCharacterList { get; set; } = true;
        public List<CharacterSelect> CharacterSelects { get; } = [];
        public void CreateCharacter(string name, Race race, Gender gender, Class @class, byte skinColor, byte face, byte hairStyle, byte hairColor, byte facialHair, byte outfitId) { }
        public void DeleteCharacter(ulong characterGuid) { }
        public void RefreshCharacterListFromServer() { }
    }
}

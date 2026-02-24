using GameData.Core.Enums;
using System.Collections.Generic;
using Xas.FluentBehaviourTree;

namespace BotRunner
{
    public partial class BotRunnerService
    {
        /// <summary>
        /// Sequence to log the bot into the server to get a session key.
        /// </summary>
        /// <param name="username">The bot's username.</param>
        /// <param name="password">The bot's password.</param>
        /// <returns>IBehaviourTreeNode that manages the login process.</returns>
        private IBehaviourTreeNode BuildLoginSequence(string username, string password) => new BehaviourTreeBuilder()
            .Sequence("Login Sequence")
                // Ensure the bot is on the login screen
                .Condition("Is On Login Screen", time => _objectManager.LoginScreen.IsOpen)

                // Input credentials and wait for async login to complete
                .Do("Input Credentials", time =>
                {
                    if (_objectManager.LoginScreen.IsLoggedIn) return BehaviourTreeStatus.Success;

                    _objectManager.LoginScreen.Login(username, password);
                    return BehaviourTreeStatus.Running; // Stay running while async login completes
                })

                // Verify login completed
                .Condition("Waiting in queue", time => _objectManager.LoginScreen.IsLoggedIn)
                .Do("Select Realm", time =>
                {
                    if (_objectManager.LoginScreen.QueuePosition > 0)
                        return BehaviourTreeStatus.Running;
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        /// <summary>
        /// Sequence to select a realm from the realm list.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages the realm selection process.</returns>
        private IBehaviourTreeNode BuildRealmSelectionSequence() => new BehaviourTreeBuilder()
            .Sequence("Realm Selection Sequence")
                // Select the first available realm
                .Condition("On Realm Selection Screen", time => _objectManager.RealmSelectScreen.IsOpen && _objectManager.LoginScreen.IsLoggedIn)
                .Do("Select Realm", time =>
                {
                    if (_objectManager.RealmSelectScreen.CurrentRealm != null) return BehaviourTreeStatus.Success;

                    _objectManager.RealmSelectScreen.SelectRealm(_objectManager.RealmSelectScreen.GetRealmList()[0]);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        /// <summary>
        /// Sequence to log the bot out of the game.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages the logout process.</returns>
        private IBehaviourTreeNode LogoutSequence => new BehaviourTreeBuilder()
            .Sequence("Logout Sequence")
                // Ensure the bot can log out (not in combat, etc.)
                .Condition("Can Log Out", time => !_objectManager.LoginScreen.IsOpen)

                // Perform the logout action
                .Do("Log Out", time =>
                {
                    _objectManager.Logout();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to request the character list from the server.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages requesting the character list.</returns>
        private IBehaviourTreeNode BuildRequestCharacterSequence() => new BehaviourTreeBuilder()
            .Sequence("Create Character Sequence")
                // Ensure the bot is on the character creation screen
                .Condition("On Character Creation Screen", time => _objectManager.CharacterSelectScreen.IsOpen)

                // Create the new character with the specified details
                .Do("Request Character List", time =>
                {
                    _objectManager.CharacterSelectScreen.RefreshCharacterListFromServer();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to create a new character with specified name, race, and class.
        /// </summary>
        /// <param name="parameters">A list containing the name, race, and class of the new character.</param>
        /// <returns>IBehaviourTreeNode that manages creating the character.</returns>
        private IBehaviourTreeNode BuildCreateCharacterSequence(List<object> parameters) => new BehaviourTreeBuilder()
            .Sequence("Create Character Sequence")
                // Ensure the bot is on the character creation screen
                .Condition("On Character Creation Screen", time => _objectManager.CharacterSelectScreen.IsOpen)

                // Create the new character with the specified details
                .Do("Create Character", time =>
                {
                    var name = (string)parameters[0];
                    var race = (Race)parameters[1];
                    var gender = (Gender)parameters[2];
                    var characterClass = (Class)parameters[3];

                    _objectManager.CharacterSelectScreen.CreateCharacter(name, race, gender, characterClass, 0, 0, 0, 0, 0, 0);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to delete an existing character based on character ID.
        /// </summary>
        /// <param name="characterId">The ID of the character to delete.</param>
        /// <returns>IBehaviourTreeNode that manages deleting the character.</returns>
        private IBehaviourTreeNode BuildDeleteCharacterSequence(ulong characterId) => new BehaviourTreeBuilder()
            .Sequence("Delete Character Sequence")
                // Ensure the bot is on the character selection screen
                .Condition("On Character Select Screen", time => _objectManager.CharacterSelectScreen.IsOpen)

                // Delete the specified character
                .Do("Delete Character", time =>
                {
                    _objectManager.CharacterSelectScreen.DeleteCharacter(characterId);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to enter the game world with a selected character.
        /// </summary>
        /// <param name="characterGuid">The GUID of the character to enter the world with.</param>
        /// <returns>IBehaviourTreeNode that manages entering the game world.</returns>
        private IBehaviourTreeNode BuildEnterWorldSequence(ulong characterGuid) => new BehaviourTreeBuilder()
            .Sequence("Enter World Sequence")
                // Ensure the bot is on the character select screen
                .Condition("On Character Select Screen", time => _objectManager.CharacterSelectScreen.IsOpen)

                // Enter the world with the specified character
                .Do("Enter World", time =>
                {
                    _objectManager.EnterWorld(characterGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
    }
}

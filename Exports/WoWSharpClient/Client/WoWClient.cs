﻿using GameData.Core.Enums;
using GameData.Core.Models;
using System.Net;

namespace WoWSharpClient.Client
{
    public class WoWClient : IDisposable
    {
        private IPAddress _ipAddress;
        private AuthLoginClient _loginClient;
        private readonly WorldClient _worldClient = new(new());
        private bool _isLoggedIn;
        public bool IsLoggedIn => _isLoggedIn;
        private uint _pingCounter = 0;
        public void Dispose()
        {
            _loginClient?.Dispose();
            _worldClient?.Dispose();
        }

        public bool IsLoginConnected() => _loginClient.IsConnected;
        public bool IsWorldConnected() => _worldClient.IsConnected;

        private void ConnectToLoginServer()
        {
            _loginClient = new(_ipAddress.ToString());
            _loginClient.Connect();

            WoWSharpEventEmitter.Instance.FireOnLoginConnect();
        }

        public void Login(string username, string password)
        {
            try
            {
                if (_loginClient == null || !_loginClient.IsConnected)
                    ConnectToLoginServer();

                _loginClient.Login(username, password);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occured during login: {ex}");
                _isLoggedIn = false;
            }
            _isLoggedIn = true;
        }

        public List<Realm> GetRealmList() => _loginClient.GetRealmList();
        public void SelectRealm(Realm realm) => _worldClient.Connect(_loginClient.Username, _ipAddress, _loginClient.SessionKey, realm.AddressPort);

        public void RefreshCharacterSelects() => _worldClient.SendCMSGCharEnum();
        public void SendCharacterCreate(string name, Race race, Class clazz, Gender gender, byte skin, byte face, byte hairStyle, byte hairColor, byte facialHair, byte outfitId)
            => _worldClient.SendCMSGCreateCharacter(name, race, clazz, gender, skin, face, hairStyle, hairColor, facialHair, outfitId);
        public void EnterWorld(ulong guid) => _worldClient.SendCMSGPlayerLogin(guid);
        public void SendChatMessage(ChatMsg chatMsgType, Language orcish, string destination, string text) => _worldClient.SendCMSGMessageChat(chatMsgType, orcish, destination, text);
        public virtual void SendNameQuery(ulong guid) => _worldClient.SendCMSGNameTypeQuery(Opcode.CMSG_NAME_QUERY, guid);
        public void SendMoveWorldPortAcknowledge() => _worldClient.SendMSGMoveWorldportAck();
        public void SendSetActiveMover(ulong guid) => _worldClient.SendCMSGSetActiveMover(guid);
        internal void SendMovementOpcode(Opcode opcode, byte[] movementInfo) => _worldClient.SendMSGMove(opcode, movementInfo);
        internal void SendMSGPacked(Opcode opcode, byte[] payload) => _worldClient.SendMSGPacked(opcode, payload);
        internal void SendPing() => _worldClient.SendCMSGPing(_pingCounter++);
        internal void QueryTime() => _worldClient.SendCMSGQueryTime();

        public void SetIpAddress(string v)
        {
            _ipAddress = IPAddress.Parse(v);
        }
    }
}

using NUnit.Framework;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Network;

namespace AshenThrone.Tests.Network
{
    [TestFixture]
    public class PhotonManagerTests
    {
        private GameObject _go;
        private PhotonManager _photon;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Initialize();
            EventBus.Initialize();
            _go = new GameObject("PhotonTest");
            _photon = _go.AddComponent<PhotonManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            EventBus.Shutdown();
            ServiceLocator.Shutdown();
        }

        [Test]
        public void Connect_SetsIsConnectedTrue()
        {
            _photon.Connect();
            Assert.IsTrue(_photon.IsConnected);
        }

        [Test]
        public void Connect_FiresOnConnectedEvent()
        {
            bool fired = false;
            _photon.OnConnectedToServer += () => fired = true;
            _photon.Connect();
            Assert.IsTrue(fired);
        }

        [Test]
        public void Disconnect_SetsIsConnectedFalse()
        {
            _photon.Connect();
            _photon.Disconnect();
            Assert.IsFalse(_photon.IsConnected);
        }

        [Test]
        public void JoinOrCreateRoom_SetsCurrentRoomName()
        {
            _photon.Connect();
            _photon.JoinOrCreateRoom("alliance_123");
            Assert.AreEqual("alliance_123", _photon.CurrentRoomName);
        }

        [Test]
        public void JoinOrCreateRoom_WhenNotConnected_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _photon.JoinOrCreateRoom("test"));
            Assert.IsNull(_photon.CurrentRoomName);
        }

        [Test]
        public void LeaveRoom_ClearsCurrentRoomName()
        {
            _photon.Connect();
            _photon.JoinOrCreateRoom("room1");
            _photon.LeaveRoom();
            Assert.IsNull(_photon.CurrentRoomName);
        }

        [Test]
        public void LeaveRoom_WhenNotInRoom_DoesNotThrow()
        {
            _photon.Connect();
            Assert.DoesNotThrow(() => _photon.LeaveRoom());
        }

        [Test]
        public void SendChatMessage_WhenInRoom_DoesNotThrow()
        {
            _photon.Connect();
            _photon.JoinOrCreateRoom("chat_room");
            Assert.DoesNotThrow(() => _photon.SendChatMessage("Hello!"));
        }

        [Test]
        public void SendChatMessage_WhenNotInRoom_DoesNotThrow()
        {
            _photon.Connect();
            Assert.DoesNotThrow(() => _photon.SendChatMessage("Hello!"));
        }

        [Test]
        public void SendChatMessage_EmptyMessage_DoesNotThrow()
        {
            _photon.Connect();
            _photon.JoinOrCreateRoom("room1");
            Assert.DoesNotThrow(() => _photon.SendChatMessage(""));
        }

        [Test]
        public void SendChatMessage_TooLong_DoesNotThrow()
        {
            _photon.Connect();
            _photon.JoinOrCreateRoom("room1");
            Assert.DoesNotThrow(() => _photon.SendChatMessage(new string('x', 501)));
        }

        [Test]
        public void BroadcastData_DoesNotThrow()
        {
            _photon.Connect();
            _photon.JoinOrCreateRoom("room1");
            Assert.DoesNotThrow(() => _photon.BroadcastData(new byte[] { 1, 2, 3 }));
        }

        [Test]
        public void RegistersWithServiceLocator()
        {
            Assert.IsTrue(ServiceLocator.TryGet<PhotonManager>(out var svc));
            Assert.AreSame(_photon, svc);
        }

        [Test]
        public void JoinRoom_FiresOnRoomJoinedEvent()
        {
            string joined = null;
            _photon.OnRoomJoined += (name) => joined = name;
            _photon.Connect();
            _photon.JoinOrCreateRoom("test_room");
            Assert.AreEqual("test_room", joined);
        }

        [Test]
        public void PlayerCount_UpdatesOnJoin()
        {
            _photon.Connect();
            Assert.AreEqual(0, _photon.PlayerCount);
            _photon.JoinOrCreateRoom("room1");
            Assert.AreEqual(1, _photon.PlayerCount);
        }
    }
}

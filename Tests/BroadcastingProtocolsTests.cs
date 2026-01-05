using Networking.Broadcasting.Channels;
using Networking.Broadcasting.Packets.Reliable;
using Networking.Broadcasting.Packets.Unreliable;
using Networking.Connections;
using Networking.Data;
using Networking.Data.Core;
using Networking.Utils;

namespace Tests
{
    public class BroadcastingProtocolsTests
    {
        private Connection _testConnection;
        private UnreliableChannelBroadcaster _unreliableChannelBroadcaster;
        private ReliableChannelBroadcaster _reliableChannelBroadcasterSender;
        private ReliableChannelBroadcaster _reliableChannelBroadcasterReceiver;

        private List<ChatMessage> _testPackets = new()
        {
            new ChatMessage()
            {
                Author = "author1",
                Message = "message1"
            },
            new ChatMessage()
            {
                Author = "author2",
                Message = "message2"
            },
            new ChatMessage()
            {
                Author = "author3",
                Message = "message4"
            },
        };

        [SetUp]
        public void Setup()
        {
            _testConnection = new Connection("127.0.0.1:1909".ParseToIpEndPoint());

            _unreliableChannelBroadcaster = new UnreliableChannelBroadcaster();
            _reliableChannelBroadcasterSender = new ReliableChannelBroadcaster();
            _reliableChannelBroadcasterReceiver = new ReliableChannelBroadcaster();

            DataTypeRegister.Init();
            DataTypeRegister.Register(typeof(ChatMessage));
        }

        /// <summary>
        /// Basic serialization and deserialization test with making sure that protocol returns correct type of packets
        /// And doesn't send it multiple times
        /// </summary>
        [Test]
        public void UnreliableChannelBroadcaster_SerializationDeserializationTest()
        {
            foreach (var testPacket in _testPackets)
            {
                _unreliableChannelBroadcaster.AddDataToSend(_testConnection, testPacket);
            }

            var packetsToBroadcast = _unreliableChannelBroadcaster.GetPacketsToBroadcast();
            Assert.Multiple(() =>
            {
                Assert.That(packetsToBroadcast, Has.Count.EqualTo(_testPackets.Count));
                Assert.That(packetsToBroadcast.All(x => x.connection == _testConnection));
                Assert.That(packetsToBroadcast.All(x => x.packet is UnreliablePacket));
            });

            var index = 0;
            foreach (var (connection, packet) in packetsToBroadcast)
            {
                var networkData = _unreliableChannelBroadcaster.GetDataFromPacket(connection, packet);

                Assert.That(networkData, Is.Not.EqualTo(null));
                Assert.That(networkData is ChatMessage);

                var chatMessage = networkData as ChatMessage;
                Assert.That(chatMessage, Is.Not.EqualTo(null));

                Assert.Multiple(() =>
                {
                    Assert.That(chatMessage.Author, Is.EqualTo(_testPackets[index].Author));
                    Assert.That(chatMessage.Message, Is.EqualTo(_testPackets[index].Message));
                });

                index++;
            }

            var packetsToBroadcastRepeated = _unreliableChannelBroadcaster.GetPacketsToBroadcast();
            Assert.That(packetsToBroadcastRepeated.Count, Is.EqualTo(0));
        }


        /// <summary>
        /// Basic serialization and deserialization test with making sure that protocol returns correct type of packets
        /// And doesn't send it multiple times
        /// </summary>
        [Test]
        public void ReliableChannelBroadcaster_SerializationDeserializationTest()
        {
            foreach (var testPacket in _testPackets)
            {
                _reliableChannelBroadcasterSender.AddDataToSend(_testConnection, testPacket);
            }

            var packetsToBroadcast = _reliableChannelBroadcasterSender.GetPacketsToBroadcast();
            Assert.Multiple(() =>
            {
                Assert.That(packetsToBroadcast, Has.Count.EqualTo(_testPackets.Count));
                Assert.That(packetsToBroadcast.All(x => x.connection == _testConnection));
                Assert.That(packetsToBroadcast.All(x => x.packet is ReliablePacket));
            });

            var index = 0;
            foreach (var (connection, packet) in packetsToBroadcast)
            {
                var networkData = _reliableChannelBroadcasterReceiver.GetDataFromPacket(connection, packet);

                Assert.That(networkData, Is.Not.EqualTo(null));
                Assert.That(networkData is ChatMessage);

                var chatMessage = networkData as ChatMessage;
                Assert.That(chatMessage, Is.Not.EqualTo(null));

                Assert.Multiple(() =>
                {
                    Assert.That(chatMessage.Author, Is.EqualTo(_testPackets[index].Author));
                    Assert.That(chatMessage.Message, Is.EqualTo(_testPackets[index].Message));
                });

                index++;
            }

            var packetsToBroadcastAgain = _reliableChannelBroadcasterSender.GetPacketsToBroadcast();
            Assert.That(packetsToBroadcastAgain.Count, Is.EqualTo(0));
        }

        /// <summary>
        /// Test for ack packets answering with ack id check and making sure that ack packets sent only once
        /// </summary>
        [Test]
        public void ReliableChannelBroadcaster_AckPacketTest()
        {
            foreach (var testPacket in _testPackets)
            {
                _reliableChannelBroadcasterSender.AddDataToSend(_testConnection, testPacket);
            }

            var packetsToBroadcast = _reliableChannelBroadcasterSender.GetPacketsToBroadcast();
            var ackIndexes = new List<int>();

            foreach (var (connection, packet) in packetsToBroadcast)
            {
                var networkData = _reliableChannelBroadcasterReceiver.GetDataFromPacket(connection, packet);
                ackIndexes.Add((packet as ReliablePacket).AckId);
            }

            Assert.That(ackIndexes.Count(), Is.EqualTo(ackIndexes.Distinct().Count()));

            var ackPackets = _reliableChannelBroadcasterReceiver.GetPacketsToBroadcast();

            // Test for all of the ack packets correctly set in terms of ack indexes
            Assert.Multiple(() =>
            {
                Assert.That(ackPackets, Has.Count.EqualTo(_testPackets.Count));
                Assert.That(ackPackets.All(x => x.connection == _testConnection));
                Assert.That(ackPackets.All(x => x.packet is ReliableAckPacket));
                Assert.That(ackPackets.All(x => (x.packet as ReliableAckPacket).AckId == ackIndexes[ackPackets.IndexOf(x)]));
            });

            var ackPacketsToBroadcastAgain = _reliableChannelBroadcasterReceiver.GetPacketsToBroadcast();
            Assert.That(ackPacketsToBroadcastAgain.Count, Is.EqualTo(0));
        }

        /// <summary>
        /// Test of resending packets if haven't received ack packets in required amount of time
        /// Then extra check for discarding repeatedly arrived packets
        /// </summary>
        [Test]
        public void ReliableChannelBroadcaster_DelayedPackets()
        {
            foreach (var testPacket in _testPackets)
            {
                _reliableChannelBroadcasterSender.AddDataToSend(_testConnection, testPacket);
            }

            var packetsToBroadcast = _reliableChannelBroadcasterSender.GetPacketsToBroadcast();

            Thread.Sleep(500);

            // All packets got delayed, should receive all of them again to send
            var packetsToBroadcastAgain = _reliableChannelBroadcasterSender.GetPacketsToBroadcast();
            Assert.That(packetsToBroadcastAgain.Count, Is.EqualTo(_testPackets.Count));

            for (var i = 0; i < packetsToBroadcast.Count; i++)
            {
                Assert.That(packetsToBroadcast[i].packet == packetsToBroadcastAgain[i].packet);
            }

            foreach (var (connection, packet) in packetsToBroadcast)
            {
                var networkData = _reliableChannelBroadcasterReceiver.GetDataFromPacket(connection, packet);
            }

            var ackPackets = _reliableChannelBroadcasterReceiver.GetPacketsToBroadcast();

            foreach (var (connection, packet) in packetsToBroadcast)
            {
                Assert.That(_reliableChannelBroadcasterReceiver.GetDataFromPacket(connection, packet), Is.Null);
            }

            foreach (var (connection, packet) in ackPackets)
            {
                var networkData = _reliableChannelBroadcasterSender.GetDataFromPacket(connection, packet);
            }

            packetsToBroadcastAgain = _reliableChannelBroadcasterSender.GetPacketsToBroadcast();
            Assert.That(packetsToBroadcastAgain.Count, Is.EqualTo(0));
        }

        /// <summary>
        /// Test of resending and accepting lost data packet and lost ack packet
        /// If no ack packet received, packet should be resent
        /// If the same packet received, it should be discarded, but ack should be send anyway to prevent repeating sends
        /// </summary>
        [Test]
        public void ReliableChannelBroadcaster_LostPacketsAndAckPackets()
        {
            foreach (var testPacket in _testPackets)
            {
                _reliableChannelBroadcasterSender.AddDataToSend(_testConnection, testPacket);
            }

            var packetsToBroadcast = _reliableChannelBroadcasterSender.GetPacketsToBroadcast();
            var lostPacket = packetsToBroadcast[0];
            packetsToBroadcast.RemoveAt(0);
            foreach (var (connection, packet) in packetsToBroadcast)
            {
                var networkData = _reliableChannelBroadcasterReceiver.GetDataFromPacket(connection, packet);
            }

            var ackPacketsToBroadcast = _reliableChannelBroadcasterReceiver.GetPacketsToBroadcast();
            Assert.That(ackPacketsToBroadcast, Has.Count.EqualTo(packetsToBroadcast.Count));

            var lostAckPacket = ackPacketsToBroadcast[0];
            ackPacketsToBroadcast.RemoveAt(0);

            Thread.Sleep(500);

            foreach (var (connection, packet) in ackPacketsToBroadcast)
            {
                var networkData = _reliableChannelBroadcasterSender.GetDataFromPacket(connection, packet);
            }
            
            var packetsToBroadcastAgain = _reliableChannelBroadcasterSender.GetPacketsToBroadcast();
            Assert.That(packetsToBroadcastAgain, Has.Count.EqualTo(2));
            Assert.That(packetsToBroadcastAgain[0], Is.EqualTo(lostPacket));

            var index = 0;
            foreach (var (connection, packet) in packetsToBroadcastAgain)
            {
                var networkData = _reliableChannelBroadcasterReceiver.GetDataFromPacket(connection, packet);
                if (index == 1)
                    Assert.That(networkData, Is.EqualTo(null));

                index++;
            }

            var ackPacketsToBroadcastAgain = _reliableChannelBroadcasterReceiver.GetPacketsToBroadcast();
            Assert.That(ackPacketsToBroadcastAgain.Count, Is.EqualTo(packetsToBroadcastAgain.Count));
        }
    }
}
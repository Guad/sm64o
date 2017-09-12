using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Lidgren.Network;

namespace SM64O
{
    public class GameServer
    {
        public Client[] Clients;
        public int MaxPlayers = 23;

        public NetServer Server;
        public int Port;

        public GameServer(int port)
        {
            Port = port;

            var config = new NetPeerConfiguration("SM64ONLINE");
            config.Port = Port;
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            config.EnableMessageType(NetIncomingMessageType.StatusChanged);
            config.MaximumConnections = 30;
            config.ConnectionTimeout = 5f;
            config.PingInterval = 2f;

            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            Server = new NetServer(config);
            Server.RegisterReceivedCallback(MessageReceive, SynchronizationContext.Current);
        }

        public void Start()
        {
            Clients = new Client[MaxPlayers];
            Server.Start();
        }

        private void NewConnection(NetIncomingMessage msg)
        {
            try
            {
                /*
                if (_bands.Contains(e.Connection.EndPoint.ToString()))
                {
                    sendChatTo("banned", e.Connection);
                    e.Connection.Close();
                    return;
                }
                */
                // TODO: Bans

                for (int i = 0; i < Clients.Length; i++)
                {
                    if (Clients[i] == null)
                    {
                        Clients[i] = new Client(msg.SenderConnection);
                        Clients[i].Id = i;
                        Clients[i].Name = "anon";

                        int playerIDB = i + 2;
                        byte[] playerID = new byte[] { (byte)playerIDB };
                        Thread.Sleep(500);
                        Clients[i].SendBytes(playerID, Server, NetDeliveryMethod.ReliableOrdered, 1);
                        string character = "Unk Char";
                        string vers = "Default Client";

                        byte verIndex = msg.ReadByte();
                        byte charIndex = msg.ReadByte();
                        Clients[i].MinorVersion = verIndex;
                        Clients[i].CharacterId = charIndex;

                        switch (charIndex)
                        {
                            case 0:
                                character = "Mario";
                                break;
                            case 1:
                                character = "Luigi";
                                break;
                            case 2:
                                character = "Yoshi";
                                break;
                            case 3:
                                character = "Wario";
                                break;
                            case 4:
                                character = "Peach";
                                break;
                            case 5:
                                character = "Toad";
                                break;
                            case 6:
                                character = "Waluigi";
                                break;
                            case 7:
                                character = "Rosalina";
                                break;
                        }

                        Clients[i].MajorVersion = msg.ReadByte();

                        byte usernameLen = msg.ReadByte();
                        string name = Encoding.ASCII.GetString(msg.ReadBytes(usernameLen), 0, usernameLen);
                        Clients[i].Name = name;

                        Clients[i].CharacterName = character;

                        Form1.Instance.listBox1.Items.Add(playerClient[i]);

                        string chatmsg = string.Format("{0} joined", playerClient[i].Name);
                        if (chatmsg.Length > MaxChatLength)
                            chatmsg = chatmsg.Substring(0, 24);

                        sendAllChat(msg);
                        return;
                    }
                }

                // Server is full
                sendChatTo("server is full", e.Connection);
                e.Connection.Close();
            }
            finally
            {
                playersOnline.Text = "Players Online: " + playerClient.Count(c => c != null) + "/" + playerClient.Length;
            }



            msg.SenderConnection.Deny("Occupied");

            // TODO: Password

            _client = message.SenderConnection;

            _client.Approve();
        }

        private void MessageReceive(object peer)
        {
            var serv = (NetPeer) peer;
            var msg = serv.ReadMessage();

            switch (msg.MessageType)
            {
                case NetIncomingMessageType.ConnectionApproval:
                    NewConnection(msg);
                    break;
                case NetIncomingMessageType.StatusChanged:
                    var newStatus = (NetConnectionStatus)message.ReadByte();

                    switch (newStatus)
                    {
                        case NetConnectionStatus.Disconnected:
                            if (_client != null && message.SenderConnection.RemoteUniqueIdentifier == _client.RemoteUniqueIdentifier)
                                _client = null;
                            Console.WriteLine("Client disconnected...");
                            break;
                    }
                    break;
                case NetIncomingMessageType.Data:
                    // TODO: actions
                    break;
                case NetIncomingMessageType.VerboseDebugMessage:
                case NetIncomingMessageType.DebugMessage:
                case NetIncomingMessageType.WarningMessage:
                case NetIncomingMessageType.ErrorMessage:
                    // TODO: log
                    break;
            }

            serv.Recycle(msg);
        }
    }
}
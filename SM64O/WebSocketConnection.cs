using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace SM64O {
    public class WebSocketConnection : WebSocketBehavior
    {
        //private static List<WebSocketConnection> _ws = new List<WebSocketConnection>();
        private static WebSocketConnection[] _connections = new WebSocketConnection[24];
        private static int _index = 0;

        private int _id;
        private Client _client;

        public WebSocketConnection()
        {
            if (_index > 24) 
            {
                // Server is full
                //Send("server is full");
                this.Context.WebSocket.Close();
                return;
            }
            //_id = _ws.Count;
            _id = _index;
            _index++;
            _connections[_id] = this;

            //try
            //{
                // TODO bans
                /* if (_bands.Contains(e.Connection.EndPoint.ToString().Split(':')[0]))
                {
                    sendChatTo("banned", e.Connection);
                    e.Connection.Close();
                    return;
                } */
                _client = new Client(this);
                _client.Id = (byte)(_id + 1);

            //}
            //finally
            //{
                //Form1.Form.labelPlayersOnline.Text = "Players Online: " + (pos+1) + "/24";
            //}
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            byte[] data = e.RawData;
            if (data.Length == 0) return;

            PacketType type = (PacketType)data[0];
            byte[] payload = data.Skip(1).ToArray();

            switch (type)
            {
                case PacketType.Handshake:
                    onHandShake(payload);
                    break;
                case PacketType.PlayerData:
                    onPlayerData(payload);
                    break;
                case PacketType.ChatMessage:
                    onChatMessage(data);
                    break;
                case PacketType.RoundtripPing:
                    _client.Connection.Context.WebSocket.Send(data);
                    break;
                case PacketType.CharacterSwitch:
                    _client.SwitchCharacter((int)payload[0]);
                    break;
            }
        }

        protected override void OnClose(CloseEventArgs e)
        {
            try {
                Form1.Form.listBoxPlayers.Items.Remove(_client);
            }
            catch 
            {
                // connection might not yet be open, ignore
            }

            _index--;
            if (_id == _index)
            {
                // player with highest player ID left
                _connections[_id] = null;
                return;
            }
            // another player left
            // move last player to his spot and resend player ID for moving player
            _connections[_id] = _connections[_index];
            byte id = (byte)(_id + 1);
            _connections[_id]._client.Id = id;
            _connections[_id]._id = _id;
            try
            {
                _connections[_id]._client.SendPacket(PacketType.Handshake, new byte[]{ id });
            }
            catch
            {
                // connection might not yet be open, ignore
            }
            _connections[_index] = null;

            string msg = string.Format("{0} left", _client.Username);
            if (msg.Length > Form1.MAX_CHAT_LENGTH)
                msg = msg.Substring(0, 24);
            sendAllChat(msg);
        }

        private void onHandShake(byte[] payload)
        {
            _client.Major = payload[0];
            _client.Minor = payload[1];
            if ((int)_client.Major != Form1.COMPAT_MAJOR_VERSION || (int)_client.Minor != Form1.COMPAT_MINOR_VERSION) {
                byte[] res = new byte[]{ _client.Major, _client.Minor };
                _client.SendPacket(PacketType.WrongVersion, res);
                return;
            }
            _client.CharacterId = (int)payload[2];
            int usernameLength = (int)payload[3];
            byte[] usernameBytes = new byte[usernameLength];
            Array.Copy(payload, 4, usernameBytes, 0, usernameLength);
            _client.Username = Encoding.ASCII.GetString(usernameBytes);
            
            _client.SendPacket(PacketType.Handshake, new byte[]{ _client.Id });
            _client.SendPacket(PacketType.GameMode, new byte[]{ 0 });

            Form1.Form.listBoxPlayers.Items.Add(_client);

            string msg = string.Format("{0} joined", _client.Username);
            if (msg.Length > Form1.MAX_CHAT_LENGTH)
                msg = msg.Substring(0, 24);
            sendAllChat(msg);
        }

        private void onPlayerData(byte[] payload)
        {
            _client.SetPlayerData(payload);
        }

        private void onChatMessage(byte[] payload)
        {
            for (int i = 0; i < 24; i++)
            {
                try
                {
                    _client.Connection.Context.WebSocket.Send(payload);
                }
                catch{}
            }
        }

        private void sendAllChat(string message)
        {
            string name = "[SERVER]";

            if (message.Length > Form1.MAX_CHAT_LENGTH)
                message = message.Substring(0, Form1.MAX_CHAT_LENGTH);

            if (name.Length > Form1.MAX_CHAT_LENGTH)
                name = name.Substring(0, Form1.MAX_CHAT_LENGTH);

            byte[] messageBytes = Encoding.ASCII.GetBytes(message);
            byte[] usernameBytes = Encoding.ASCII.GetBytes(name);
            byte[] payload = new byte[1 + messageBytes.Length + 1 + usernameBytes.Length];
            payload[0] = (byte)messageBytes.Length;
            Array.Copy(messageBytes, 0, payload, 1, messageBytes.Length);
            payload[messageBytes.Length + 1] = (byte)usernameBytes.Length;
            Array.Copy(usernameBytes, 0, payload, 1 + messageBytes.Length + 1, usernameBytes.Length);

            for (int i = 0; i < 24; i++)
            {
                if (_connections[i] == null || _connections[i]._client == null) continue;
                _connections[i]._client.SendPacket(PacketType.ChatMessage, payload);
            }
        }

        public static void SendPlayerData()
        {
            byte[] payload = new byte[0];
            for (int i = 0, j = 0; i < 24; i++)
            {
                if (_connections[i] == null) continue;
                var client = _connections[i]._client;
                if (client == null) continue;
                if (client.PlayerData != null && client.PlayerData[3] != 0)
                {
                    client.PlayerData[3] = client.Id;
                    Array.Resize<byte>(ref payload, j + 0x18);
                    Array.Copy(client.PlayerData, 0, payload, j, 0x18);
                    j += 0x18;
                }
            }
            using (MemoryStream res = new MemoryStream())
            {
                using (GZipStream gs = new GZipStream(res, CompressionMode.Compress))
                using (MemoryStream ms = new MemoryStream(payload))
                {
                    ms.CopyTo(gs);
                }
                byte[] data = res.ToArray();
                for (int i = 0; i < 24; i++)
                {
                    if (_connections[i] == null || _connections[i]._client == null) continue;
                    _connections[i]._client.SendPacket(PacketType.PlayerData, data);
                }
            }
        }

        private static string PrintBytes(byte[] byteArray)
        {
            var sb = new StringBuilder("new byte[] { ");
            for(var i = 0; i < byteArray.Length; i++)
            {
                var b = byteArray[i];
                sb.Append(b);
                if (i < byteArray.Length -1)
                {
                    sb.Append(", ");
                }
            }
            sb.Append(" }");
            return sb.ToString();
        }
    }
}
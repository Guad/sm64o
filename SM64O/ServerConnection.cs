using System;
using System.Net;
using System.Diagnostics;
using System.Text;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using System.IO.Compression;
using WebSocketSharp;

namespace SM64O
{
    public class ServerConnection
    {
        private readonly byte[] EMPTY = new byte[0x18];

        public byte PlayerID = 0;

        private Form1 _gui;
        private WebSocket _ws;
        private IEmulatorAccessor _memory;

        public ServerConnection (string address, string port, IEmulatorAccessor memory, Form1 gui)
        {
            _ws = new WebSocket("ws://" + address + ":" + port);
            _gui = gui;
            _memory = memory;
            _ws.OnMessage += (sender, e) =>
            {
                onMessage(e);
            };
            _ws.OnOpen += (sender, e) =>
            {
                //Console.WriteLine("Connected");
                _gui.AddChatMessage("[SERVER]", "Connected");

                byte[] payload = new byte[Form1.HANDSHAKE_LENGTH];
                payload[0] = (byte)PacketType.Handshake;
                payload[1] = (byte)Form1.COMPAT_MAJOR_VERSION;
                payload[2] = (byte)Form1.COMPAT_MINOR_VERSION;
                payload[3] = (byte)_gui.GetCharacter();

                string username = _gui.GetUsername();

                if (string.IsNullOrWhiteSpace(username))
                {
                    username = getRandomUsername();
                    _gui.SetUsername(username);
                }

                byte[] usernameBytes = Encoding.ASCII.GetBytes(username);
                int len = usernameBytes.Length;
                if (len > 24) // Arbitrary max length
                    len = 24;

                payload[4] = (byte)len;
                Array.Copy(usernameBytes, 0, payload, 5, len);
                _ws.Send(payload);
                SetMessage("connected");
            };
            _ws.OnError += (sender, e) =>
            {
                MessageBox.Show(null, e.Message + "\n\n" + e.Exception, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            };
            _ws.OnClose += (sender, e) =>
            {
                _memory.WriteMemory(0x365FFC, new byte[1], 1);
                _gui.AddChatMessage("[SERVER]", "Disconnected");
                SetMessage("disconnected");
                MessageBox.Show(null, "Server closed", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            };
            _ws.Connect();
        }

        private void OnOpen ()
        {

        }
        
        private void onMessage (MessageEventArgs e)
        {
            byte[] data = e.RawData;
            if (data.Length == 0) return;

            PacketType type = (PacketType)data[0];
            byte[] payload = data.Skip(1).ToArray();

            switch (type)
            {
                case PacketType.Handshake:
                    _memory.WriteMemory(0x365FFC, new byte[1]{ 1 }, 1); // let client think that he is host
                    _memory.WriteMemory(0x367703, new byte[1]{ 1 }, 1); // let client think that he has player ID 1
                    PlayerID = payload[0];
                    _gui.AddChatMessage("[SERVER]", "Your player ID is: " + PlayerID);
                    break;
                case PacketType.PlayerData:
                    onPlayerData(payload);
                    break;
                case PacketType.GameMode:
                    _memory.WriteMemory(0x365FF7, payload, 1);
                    break;
                case PacketType.ChatMessage:
                    onChatMessage(payload);
                    break;
                case PacketType.RoundtripPing:
                    _gui.SetPing((Environment.TickCount - BitConverter.ToInt32(payload, 0)) / 2);
                    break;
                case PacketType.WrongVersion:
                    int major = (int)payload[0];
                    int minor = (int)payload[1];
                    MessageBox.Show(null, "Wrong version!\n\nPlease check if there is a new version of Net64", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                    break;
            }
        }

        private void onPlayerData(byte[] compressed)
        {
            using (MemoryStream ms = new MemoryStream(compressed))
            using (GZipStream gs = new GZipStream(ms, CompressionMode.Decompress))
            using (MemoryStream res = new MemoryStream())
            {
                gs.CopyTo(res);
                byte[] data = res.ToArray();
                byte[] playerData = new byte[0x18];
                int j = 2;
                for (int i = 0; i < data.Length; i += 0x18)
                {
                    if (PlayerID == data[i + 3]) continue;
                    Array.Copy(data, i, playerData, 0, 0x18);
                    playerData[3] = (byte)j;
                    _memory.WriteMemory(0x367700 + 0x100 * j, playerData, 0x18);
                    j++;
                }
                for (; j < 24; j++) {
                    _memory.WriteMemory(0x367700 + 0x100 * j, EMPTY, 0x18);
                }
            }
        }

        private void onChatMessage(byte[] data)
        {
            string message = "";
            string sender = "";

            int msgLen = data[0];
            message = Program.GetASCIIString(data, 1, msgLen);
            int nameLen = data[msgLen + 1];
            sender = Program.GetASCIIString(data, msgLen + 2, nameLen);
            _gui.AddChatMessage(sender, message);
            SetMessage(message);
        }

        public void SendPlayerData()
        {
            byte[] payload = new byte[0x18];
            _memory.ReadMemory(0x367700, payload, 0x18);
            if (payload[0xF] != 0)
            {
                SendPacket(PacketType.PlayerData, payload);
                _memory.WriteMemory(0x367800, payload, 0x18);
            }
        }
        
        public void SetMessage(string msg)
        {
            byte[] strBuf = Encoding.ASCII.GetBytes(msg.Where(isPrintable).ToArray());
            byte[] buffer = new byte[strBuf.Length + 4];
            strBuf.CopyTo(buffer, 0);
            for (int i = 0; i < buffer.Length; i += 4)
            {
                byte[] buf = buffer.Skip(i).Take(4).ToArray();
                buf = buf.Reverse().ToArray();
                _memory.WriteMemory(0x367684 + i, buf, 4);
            }

            byte[] empty = new byte[4];
            _memory.WriteMemory(0x367680, empty, 4);
        }

        private static readonly char[] _printables = new[] { ' ', '+', '-', ',', };
        private static bool isPrintable(char c)
        {
            if (char.IsLetterOrDigit(c)) return true;
            if (Array.IndexOf(_printables, c) != -1) return true;
            return false;
        }

        public void SendAllChat(string username, string message)
        {
            string name = "HOST";

            if (!string.IsNullOrWhiteSpace(username))
                name = username;

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

            SendPacket(PacketType.ChatMessage, payload);

        }

        public void SendChatTo(string username, string message)
        {
            string name = "HOST";

            if (!string.IsNullOrWhiteSpace(username))
                name = username;

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

            SendPacket(PacketType.ChatMessage, payload);
        }

        public void SetCharacter(int index)
        {
            _memory.WriteMemory(0x365FF3, new byte[] { (byte)(index + 1) }, 1);
            SendPacket(PacketType.CharacterSwitch, new byte[] { (byte)(index) });
        }

        public void Ping()
        {
            byte[] buffer = new byte[4];
            Array.Copy(BitConverter.GetBytes(Environment.TickCount), 0, buffer, 0, 4);

            SendPacket(PacketType.RoundtripPing, buffer);
        }

        public void SendPacket(PacketType type, byte[] payload)
        {
            byte[] buffer = new byte[payload.Length + 1];
            buffer[0] = (byte)type;
            Array.Copy(payload, 0, buffer, 1, payload.Length);
            _ws.Send(buffer);
        }

        private Random _r = new Random();
        private string getRandomUsername()
        {
            string[] usernames = new[]
            {
                "TheFloorIsLava",
                "BonelessPizza",
                "WOAH",
                "MahBoi",
                "Shoutouts",
                "Sigmario",
                "HeHasNoGrace",
                "Memetopia",
                "ShrekIsLove",
                //mind if I...?
                "CoconutCreamPie", //is this too long?
            };

            return usernames[_r.Next(usernames.Length)];
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
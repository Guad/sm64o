using System;
using System.Text;

namespace SM64O
{
    public class Client
    {
        public WebSocketConnection Connection;
        public byte Id;
        public byte Major;
        public byte Minor;
        public int CharacterId;
        public string Username;
        public byte[] PlayerData;

        public Client()
        {}

        public Client(WebSocketConnection conn)
        {
            Connection = conn;
        }

        public static implicit operator WebSocketConnection(Client c)
        {
            return c.Connection;
        }
        
        public void SendPacket(PacketType type, byte[] data)
        {
            byte[] buffer = new byte[data.Length + 1];
            buffer[0] = (byte) type;
            Array.Copy(data, 0, buffer, 1, data.Length);

            try{
                Connection.Context.WebSocket.Send(buffer);
            }
            catch
            {
                // connection might not be open, ignore
            }
        }

        public void SetPlayerData(byte[] playerData)
        {
            PlayerData = playerData;
        }

        public void SwitchCharacter(int characterId)
        {
            CharacterId = characterId;
        }

        public override string ToString()
        {
            return Id + " | " + Username;
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
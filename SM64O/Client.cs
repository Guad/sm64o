
using Lidgren.Network;

namespace SM64O
{
    public class Client
    {
        public Client()
        {}

        public Client(NetConnection conn)
        {
            Connection = conn;
        }

        public static implicit operator NetConnection(Client c)
        {
            return c.Connection;
        }

        public void SendBytes(byte[] data, NetServer server, NetDeliveryMethod m = NetDeliveryMethod.Unreliable, int channel = 0)
        {
            var msg = server.CreateMessage();

            msg.Write(data);
            
            Connection.SendMessage(msg, m, channel);
        }

        public NetConnection Connection { get; private set; }
        public string Name { get; set; }
        public string CharacterName { get; set; }
        public int CharacterId { get; set; }
        public byte MajorVersion { get; set; }
        public byte MinorVersion { get; set; }
        public int Id { get; set; }

        public override string ToString()
        {
            return string.Format("[{0}] {1} ({2}) v{3}.{4}", Id, Name, CharacterName, MajorVersion, MinorVersion);
        }
    }
}
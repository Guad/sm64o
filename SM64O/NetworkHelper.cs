using System;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Hazel;
using Hazel.Udp;

namespace SM64O
{
    public static class NetworkHelper
    {
        public static string ExternalIp;
        public static int ConfirmedOpenPort;

        private const string ServiceAddress = "napi.sm64o.com";
        private const int ServicePort = 6460;

        private static EndPoint Service = null;

        private static bool _success; // this is dirty

        private static bool _errOnce = false; // only show err message once

        public static async Task<bool> RequestAssistance(int port)
        {
            if (ConfirmedOpenPort == port)
                return true;

            if (Service == null)
            {
                IPAddress address = Program.ResolveAddress(ServiceAddress);

                // If api.sm64o.com closed
                if (address == null)
                    return true;
                Service = new IPEndPoint(address, ServicePort);
            }

            var server = ((UdpConnectionListener) Form1.listener);

            if (server == null) return false;

            _success = false;
            ConfirmedOpenPort = 0;
            ExternalIp = null;

            server.UnconnectedDataReceived += DataReceived;

            int tries = 1;
            byte[] packet = BuildStartPacket((short) port);

            do
            {
                await Task.Run(() => server.SendUnconnectedBytes(packet, Service));

                await Task.Delay(1000 * tries);
                tries++;
            } while (tries < 4 && !_success);

            server.UnconnectedDataReceived -= DataReceived;

            if (_success)
                ConfirmedOpenPort = port;

            return _success;
        }

        private static void DataReceived(object sender, UnconnectedDataReceivedEventArgs e)
        {
            // We only want data from the source
            if (e.Sender.GetHashCode() != Service.GetHashCode())
                return;

            // check response data
            var response = System.Text.Encoding.ASCII.GetString(e.Data);

            if (response.StartsWith("BADVER"))
            {
                if (!_errOnce)
                    System.Windows.Forms.MessageBox.Show("Failed to communicate with Net64 open port checker - your application is out-of-date.", "Out Of Date", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                _errOnce = true;
                return;
            }

            if (response.StartsWith("OK "))
            {
                // read our IP
                var wbf = response.Substring(3);
                IPAddress add = IPAddress.Parse(wbf);
                ExternalIp = add.ToString();
                /*

                // Don't need to send back ACK

                byte[] ack = new byte[4];

                ack[0] = 0x41;
                ack[0] = 0x43;
                ack[0] = 0x4b;
                ack[0] = 0x00;

                ((UdpConnectionListener)Form1.listener).SendUnconnectedBytes(ack, Service);
                */
                _success = true;
            }
        }

        private static byte[] BuildStartPacket(short port)
        {
            byte[] buffer = new byte[400];

            buffer[0] = 0x6e; // 'n'
            buffer[1] = 0x65; // 'e'
            buffer[2] = 0x74; // 't'
            buffer[3] = 0x36; // '6'
            buffer[4] = 0x34; // '4'
            buffer[5] = 0x69; // 'i'
            buffer[6] = 0x70; // 'p'
            buffer[7] = 0x63; // 'c'
            buffer[8] = 0x30; // '0'
            buffer[9] = 0x30; // '0'
            buffer[10] = 0x30; // '0'
            buffer[11] = 0x30; // '0'

            Array.Copy(BitConverter.GetBytes(port), 0, buffer, 12, 2);

            return buffer;
        }
    }
}

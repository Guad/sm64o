using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace SM64O
{
    public class NetworkLogger
    {
        public static Lazy<NetworkLogger> Singleton = new Lazy<NetworkLogger>(() => return new NetworkLogger());

        public NetworkLogger()
        {
            Enabled = true;
        }

        public bool Enabled { get; set; }

        private object _bufferLock = new object();
        private List<string> _buffer = new List<string>();

        public void LogOutgoingPacket(byte[] packet, string origin)
        {
            if (!Enabled) return;

            DateTime now = DateTime.UtcNow;
            string data = string.Join(" ", packet.Select(b => b.ToString("X")));

            string formatted = string.Format("OUT [{0}:{1}:{2}.{3}}] {{{4}}} ({5}): {6}",
                now.Hours, now.Minutes, now.Seconds, now.Milliseconds,
                origin,
                data.Length,
                data
                );

            lock (_bufferLock)
                _buffer.Add(formatted);
        }

        public void LogIncomingPacket(byte[] packet, string origin)
        {
            if (!Enabled) return;

            DateTime now = DateTime.UtcNow;
            string data = string.Join(" ", packet.Select(b => b.ToString("X")));

            string formatted = string.Format("OUT [{0}:{1}:{2}.{3}}] {{{4}}} ({5}): {6}",
                now.Hours, now.Minutes, now.Seconds, now.Milliseconds,
                origin,
                data.Length,
                data
                );

            lock (_bufferLock)
                _buffer.Add(formatted);
        }

        public void Flush()
        {
            lock (_bufferLock)
            {
                if (_buffer.Count == 0) return;
                
                File.AppendAllLines("network.log", _buffer.ToArray());
                _buffer.Clear();
            }
        }
    }
}
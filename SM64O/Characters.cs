using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SM64O
{
    public static class Characters
    {
        public static void setMessage(string msg, IEmulatorAccessor mem)
        {
            //int bytesWritten = 0;
            byte[] buffer = Encoding.ASCII.GetBytes(msg.Where(isPrintable).ToArray());

            byte[] newArray = new byte[buffer.Length + 4];
            buffer.CopyTo(newArray, 0);

            for (int i = 0; i < newArray.Length; i += 4)
            {
                byte[] newBuffer = newArray.Skip(i).Take(4).ToArray();
                newBuffer = newBuffer.Reverse().ToArray();
                mem.WriteMemory(0xFF7684 + i, newBuffer, newBuffer.Length);
            }

            byte[] overWriteBuffer = new byte[] { 0x00, 0x00, 0x00, 0x00 };
            //overWriteBuffer = overWriteBuffer.Reverse().ToArray();
            mem.WriteMemory(0xFF7680, overWriteBuffer, overWriteBuffer.Length);
        }


        private static readonly char[] _printables = new[]
        {
            ' ',
            '+',
            '-',
            ',',
        };
        private static bool isPrintable(char c)
        {
            if (char.IsLetterOrDigit(c)) return true;
            if (Array.IndexOf(_printables, c) != -1) return true;
            return false;
        }

        public static void setCharacter(int id, IEmulatorAccessor mem)
        {
            mem.WriteMemory(0xFF5FF3, new byte[] { (byte) (id + 1) }, 1);
        }
    }
}

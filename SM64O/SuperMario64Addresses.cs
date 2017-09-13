using System;

namespace SM64O
{
    public struct Vector3F
    {
        public float X, Y, Z;
    }

    public struct Vector3Short
    {
        public short X, Y, Z;
    }

    public static class SuperMario64Addresses
    {
        public const int CameraPositionX = 0x33c6a4;
        public const int CameraPositionY = 0x33c6a8;
        public const int CameraPositionZ = 0x33c6ac;

        public const int CameraFocalX = 0x33c524;
        public const int CameraFocalY = 0x33c528;
        public const int CameraFocalZ = 0x33c52c;

        // 3 shorts for x, y, z
        public const int PlayersPositionsStart = 0x367704;
        public const int PlayerPositionsSize = 0x100;

        // PlayerPos.X = 04
        // PlayerPos.Y = 06
        // PlayerPos.Z = 0a

        public static Vector3F GetCameraPosition(IEmulatorAccessor m)
        {
            Vector3F v = new Vector3F();

            int len = 3 * sizeof(float);

            byte[] meme = new byte[len];

            m.ReadMemory(CameraPositionX, meme, len);

            v.X = BitConverter.ToSingle(meme, 0);
            v.Y = BitConverter.ToSingle(meme, sizeof(float));
            v.Z = BitConverter.ToSingle(meme, sizeof(float) * 2);

            return v;
        }

        public static Vector3F GetCameraFocalPoint(IEmulatorAccessor m)
        {
            Vector3F v = new Vector3F();

            int len = 3 * sizeof(float);

            byte[] meme = new byte[len];

            m.ReadMemory(CameraFocalX, meme, len);

            v.X = BitConverter.ToSingle(meme, 0);
            v.Y = BitConverter.ToSingle(meme, sizeof(float));
            v.Z = BitConverter.ToSingle(meme, sizeof(float) * 2);

            return v;
        }

        public static Vector3Short GetPlayerPosition(IEmulatorAccessor m, int id)
        {
            Vector3Short v = new Vector3Short();

            int len = 3 * sizeof(short) + 2; // padding

            byte[] meme = new byte[len];

            m.ReadMemory(PlayersPositionsStart + PlayerPositionsSize * id, meme, len);

            v.Y = BitConverter.ToInt16(meme, 0);
            v.X = BitConverter.ToInt16(meme, sizeof(short));
            v.Z = BitConverter.ToInt16(meme, sizeof(short) * 2 + 2);

            return v;
        }
    }
}
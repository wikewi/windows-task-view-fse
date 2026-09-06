using System.Runtime.InteropServices;

namespace WindowsTaskViewFSE.Helpers;

public static class XInputInterop
{
    public const int ERROR_SUCCESS = 0;
    public const int ERROR_DEVICE_NOT_CONNECTED = 1167;

    public const ushort XINPUT_GAMEPAD_DPAD_UP = 0x0001;
    public const ushort XINPUT_GAMEPAD_DPAD_DOWN = 0x0002;
    public const ushort XINPUT_GAMEPAD_DPAD_LEFT = 0x0004;
    public const ushort XINPUT_GAMEPAD_DPAD_RIGHT = 0x0008;
    public const ushort XINPUT_GAMEPAD_START = 0x0010;
    public const ushort XINPUT_GAMEPAD_BACK = 0x0020;
    public const ushort XINPUT_GAMEPAD_LEFT_THUMB = 0x0040;
    public const ushort XINPUT_GAMEPAD_RIGHT_THUMB = 0x0080;
    public const ushort XINPUT_GAMEPAD_LEFT_SHOULDER = 0x0100;
    public const ushort XINPUT_GAMEPAD_RIGHT_SHOULDER = 0x0200;
    public const ushort XINPUT_GAMEPAD_A = 0x1000;
    public const ushort XINPUT_GAMEPAD_B = 0x2000;
    public const ushort XINPUT_GAMEPAD_X = 0x4000;
    public const ushort XINPUT_GAMEPAD_Y = 0x8000;

    public const short XINPUT_GAMEPAD_LEFT_THUMB_DEADZONE = 7849;
    public const short XINPUT_GAMEPAD_RIGHT_THUMB_DEADZONE = 8689;
    public const byte XINPUT_GAMEPAD_TRIGGER_THRESHOLD = 30;

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_VIBRATION
    {
        public ushort wLeftMotorSpeed;
        public ushort wRightMotorSpeed;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_CAPABILITIES
    {
        public byte Type;
        public byte SubType;
        public ushort Flags;
        public XINPUT_GAMEPAD Gamepad;
        public XINPUT_VIBRATION Vibration;
    }

    private static class XInput14
    {
        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        public static extern int XInputGetState(int dwUserIndex, out XINPUT_STATE pState);

        [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
        public static extern int XInputSetState(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetCapabilities")]
        public static extern int XInputGetCapabilities(int dwUserIndex, uint dwFlags, out XINPUT_CAPABILITIES pCapabilities);
    }

    private static class XInput13
    {
        [DllImport("xinput1_3.dll", EntryPoint = "XInputGetState")]
        public static extern int XInputGetState(int dwUserIndex, out XINPUT_STATE pState);

        [DllImport("xinput1_3.dll", EntryPoint = "XInputSetState")]
        public static extern int XInputSetState(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

        [DllImport("xinput1_3.dll", EntryPoint = "XInputGetCapabilities")]
        public static extern int XInputGetCapabilities(int dwUserIndex, uint dwFlags, out XINPUT_CAPABILITIES pCapabilities);
    }

    private static class XInput910
    {
        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        public static extern int XInputGetState(int dwUserIndex, out XINPUT_STATE pState);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputSetState")]
        public static extern int XInputSetState(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetCapabilities")]
        public static extern int XInputGetCapabilities(int dwUserIndex, uint dwFlags, out XINPUT_CAPABILITIES pCapabilities);
    }

    private static int _activeVersion = -1; // -1: untested, 0: none, 14: 1.4, 13: 1.3, 91: 9.1.0

    public static int GetState(int dwUserIndex, out XINPUT_STATE state)
    {
        state = default;
        try
        {
            if (_activeVersion == 14 || _activeVersion == -1)
            {
                try
                {
                    int res = XInput14.XInputGetState(dwUserIndex, out state);
                    _activeVersion = 14;
                    return res;
                }
                catch (DllNotFoundException)
                {
                    if (_activeVersion == 14) _activeVersion = -1;
                }
                catch (EntryPointNotFoundException)
                {
                    if (_activeVersion == 14) _activeVersion = -1;
                }
            }

            if (_activeVersion == 13 || _activeVersion == -1)
            {
                try
                {
                    int res = XInput13.XInputGetState(dwUserIndex, out state);
                    _activeVersion = 13;
                    return res;
                }
                catch (DllNotFoundException)
                {
                    if (_activeVersion == 13) _activeVersion = -1;
                }
                catch (EntryPointNotFoundException)
                {
                    if (_activeVersion == 13) _activeVersion = -1;
                }
            }

            if (_activeVersion == 91 || _activeVersion == -1)
            {
                try
                {
                    int res = XInput910.XInputGetState(dwUserIndex, out state);
                    _activeVersion = 91;
                    return res;
                }
                catch (DllNotFoundException)
                {
                    _activeVersion = 0;
                }
                catch (EntryPointNotFoundException)
                {
                    _activeVersion = 0;
                }
            }

            return ERROR_DEVICE_NOT_CONNECTED;
        }
        catch
        {
            return ERROR_DEVICE_NOT_CONNECTED;
        }
    }

    public static int SetState(int dwUserIndex, ref XINPUT_VIBRATION vibration)
    {
        try
        {
            if (_activeVersion == 14)
                return XInput14.XInputSetState(dwUserIndex, ref vibration);
            if (_activeVersion == 13)
                return XInput13.XInputSetState(dwUserIndex, ref vibration);
            if (_activeVersion == 91)
                return XInput910.XInputSetState(dwUserIndex, ref vibration);

            return ERROR_DEVICE_NOT_CONNECTED;
        }
        catch
        {
            return ERROR_DEVICE_NOT_CONNECTED;
        }
    }
}

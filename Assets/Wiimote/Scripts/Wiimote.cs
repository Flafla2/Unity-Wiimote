namespace WiimoteApi {
[System.Serializable]
public class Wiimote
{
    public Wiimote()
    {
        led = new bool[4];
        accel = new int[3];
        ir = new int[4,3];
    }

    public RegisterReadData CurrentReadData = null;
    public IntPtr hidapi_handle = IntPtr.Zero;
    public string hidapi_path;
    public bool wiimoteplus = false;
    public bool RumbleOn = false;

    public bool[] led;
    // Current wiimote-space accelration, in wiimote coordinate system.
    // These are RAW values, so they may be off.  See CalibrateAccel().
    // This is only updated if the Wiimote has a report mode with Accel
    // Range:            -128 to 128
    // Up/Down:          +Z/-Z
    // Left/Right:       +X/-Z
    // Forward/Backward: -Y/+Y
    public int[] accel;
    
    // Current wiimote RAW IR data.  Size = [4,3].  Wiimote IR data can
    // detect up to four IR dots.  Data = -1 if it is inapplicable (for
    // example, if there are less than four dots, or if size data is
    // unavailable).
    // This is only updated if the Wiimote has a report mode with IR
    //
    //        | Position X | Position Y |  Size  |
    // Range: |  0 - 1023  |  0 - 767   | 0 - 15 |
    // Index: |     0      |      1     |   2    |
    //
    // int[dot index, x (0) / y (1) / size (2)]
    public int[,] ir;

    // Raw data for any extension controllers connected to the wiimote
    // This is only updated if the Wiimote has a report mode with Extensions.
    public byte[] extension;

    // True if the Wiimote's batteries are low, as reported by the Wiimote.
    // This is only updated when the Wiimote sends status reports.
    // See also: battery_level
    public bool battery_low;
    // True if an extension controller is connected, as reported by the Wiimote.
    // This is only updated when the Wiimote sends status reports.
    public bool ext_connected;
    // True if the speaker is currently enabled, as reported by the Wiimote.
    // This is only updated when the Wiimote sends status reports.
    public bool speaker_enabled;
    // True if IR is currently enabled, as reported by the Wiimote.
    // This is only updated when the Wiimote sends status reports.
    public bool ir_enabled;

    // The current battery level, as reported by the Wiimote.
    // This is only updated when the Wiimote sends status reports.
    // See also: battery_low
    public byte battery_level;

    // Button: D-Pad Left
    public bool d_left;
    // Button: D-Pad Right
    public bool d_right;
    // Button: D-Pad Up
    public bool d_up;
    // Button: D-Pad Down
    public bool d_down;
    // Button: A
    public bool a;
    // Button: B
    public bool b;
    // Button: 1 (one)
    public bool one;
    // Button: 2 (two)
    public bool two;
    // Button: + (plus)
    public bool plus;
    // Button: - (minus)
    public bool minus;
    // Button: Home
    public bool home;

    // Calibration data for the accelerometer.  This is not reported
    // by the wiimote directly - it is instead collected from normal
    // Wiimote accelerometer data.  Here are the 3 calibration steps:
    //
    // 1. Horizontal with the A button facing up
    // 2. IR sensor down on the table so the expansion port is facing up
    // 3. Laying on its side, so the left side is facing up
    // 
    // By default it is set to experimental calibration data.
    // 
    // int[calibration step,calibration data] (size 3x3)
    public int[,] accel_calib = {
                                    { -20, -16,  83 },
                                    { -20,  80, -20 },
                                    {  84, -20, -12 }
                                };

    // True if a Wii Motion Plus is attached to the Wiimote, and it
    // has NOT BEEN ACTIVATED.  When the WMP is activated this value is
    // false.  This is only updated when the remote is requested from
    // Wiimote registers (see: RequestIdentifyWiiMotionPlus())
    public bool wmp_attached = false;
    public ExtensionController current_ext = ExtensionController.NONE;

    public void CalibrateAccel(AccelCalibrationStep step)
    {
        for (int x = 0; x < 3; x++)
            accel_calib[(int)step, x] = accel[x];
    }

    public float[] GetAccelZeroPoints()
    {
        float[] ret = new float[3];
        ret[0] = ((float)accel_calib[0, 0] / (float)accel_calib[1, 0]) / 2f;
        ret[1] = ((float)accel_calib[0, 1] / (float)accel_calib[2, 1]) / 2f;
        ret[2] = ((float)accel_calib[1, 2] / (float)accel_calib[2, 2]) / 2f;
        return ret;
    }

    // Returns the position at which the wiimote is pointing to.  This is a value from 0-1
    // representing the screen-space pointing position in X and Y.  Assume a 4x3 aspect ratio.
    public float[] GetPointingPosition()
    {
        float[] ret = new float[2];
        float[] midpoint = GetIRMidpoint();
        if (midpoint[0] < 0 || midpoint[1] < 0)
            return new float[] { -1, -1 };
        midpoint[0] = 1 - midpoint[0] - 0.5f;
        midpoint[1] = midpoint[1] - 0.5f;

        float rotation = Mathf.Atan2(accel[2], accel[0]) - (float)(Mathf.PI / 2.0f);
        float cos = Mathf.Cos(rotation);
        float sin = Mathf.Sin(rotation);
        ret[0] =  midpoint[0] * cos + midpoint[1] * sin;
        ret[1] = -midpoint[0] * sin + midpoint[1] * cos;
        ret[0] += 0.5f;
        ret[1] += 0.5f;

        ret[1] = 1 - ret[1];

        return ret;
    }

    // Returns the midpoint of all IR dots, or [0, 0] if none are found.  This is a value from 0-1
    // representing the screen-space position in X and Y.
    public float[] GetIRMidpoint()
    {
        float[] ret = new float[2];
        float[,] sensorIR = GetProbableSensorBarIR();
        ret[0] = sensorIR[0, 0] + sensorIR[1, 0];
        ret[1] = sensorIR[0, 1] + sensorIR[1, 1];
        ret[0] /= 2f * 1023f;
        ret[1] /= 2f * 767f;
        
        return ret;
    }

    private float[] LastIRSeparation = new float[] {0,0};
    public float[,] GetProbableSensorBarIR()
    {
        int count = 0;
        int[] ind = new int[2];
        for (int x = 0; x < 4; x++)
        {
            if (count > 1 || ir[x, 0] == -1 || ir[x, 1] == -1)
                continue;

            ind[count] = x;
            if (count == 1 && ir[ind[0], 0] > ir[x, 0])
            {
                ind[1] = ind[0];
                ind[0] = x;
            }
            count++;
        }

        if (count < 2)
            return new float[,] { { -1, -1, -1 }, { -1, -1, -1 } };

        float[,] ret = new float[2, 2];
        for (int x = 0; x < count; x++)
        {
            for(int y = 0; y < 2; y++)
                ret[x, y] = ir[ind[x], y];
        }

        if (count == 1) // one of the dots are outside of the wiimote FOV
        {
            
            if (ret[0,0] < 1023 / 2) // Left side of the screen, means that it's the right dot
            {
                for (int x = 0; x < 2; x++)
                {
                    ret[1, x] = ret[0, x];
                    ret[0, x] -= LastIRSeparation[x];
                }
            }
            else
            {
                for (int x = 0; x < 2; x++)
                {
                    ret[1, x] = ret[0, x];
                    ret[1, x] += LastIRSeparation[x];
                }
            }
        }
        else if (count == 2)
        {
            LastIRSeparation[0] = Mathf.Abs(ret[0, 0] - ret[1, 0]);
            LastIRSeparation[1] = Mathf.Abs(ret[0, 1] - ret[1, 1]);
        }

        return ret;
    }

    // Calibrated Accelerometer Data using experimental calibration points.
    // These values are in Wiimote coordinates (in the direction of gravity)
    // Range: -1 to 1
    // Up/Down:          +Z/-Z
    // Left/Right:       +X/-Z
    // Forward/Backward: -Y/+Y
    // See Also: CalibrateAccel(), GetAccelZeroPoints(), accel[]
    public float[] GetCalibratedAccelData()
    {
        float[] o = GetAccelZeroPoints();

        float x_raw = accel[0];
        float y_raw = accel[1];
        float z_raw = accel[2];

        float[] ret = new float[3];
        ret[0] = (x_raw - o[0]) / (accel_calib[2, 0] - o[0]);
        ret[1] = (y_raw - o[1]) / (accel_calib[1, 1] - o[1]);
        ret[2] = (z_raw - o[2]) / (accel_calib[0, 2] - o[2]);
        return ret;
    }

    public static byte[] ID_InactiveMotionPlus = new byte[] {0x00, 0x00, 0xA6, 0x20, 0x00, 0x05};

    public void RespondIdentifyWiiMotionPlus(byte[] data)
    {
        if (data.Length != ID_InactiveMotionPlus.Length)
        {
            wmp_attached = false;
            return;
        }
        for (int x = 0; x < data.Length; x++)
        {
            if (data[x] != ID_InactiveMotionPlus[x])
            {
                wmp_attached = false;
                return;
            }
        }
        wmp_attached = true;
    }

    public const long ID_ActiveMotionPlus           = 0x0000A4200405;
    public const long ID_ActiveMotionPlus_Nunchuck  = 0x0000A4200505;
    public const long ID_ActiveMotionPlus_Classic   = 0x0000A4200705;
    public const long ID_Nunchuck                   = 0x0000A4200000;
    public const long ID_Classic                    = 0x0000A4200101;
    public const long ID_ClassicPro                 = 0x0100A4200101;


    public void RespondIdentifyExtension(byte[] data)
    {
        if (data.Length != 6)
            return;

        byte[] resized = new byte[8];
        for (int x = 0; x < 6; x++) resized[x] = data[5-x];
        long val = BitConverter.ToInt64(resized, 0);

        if (val == ID_ActiveMotionPlus)
            current_ext = ExtensionController.MOTIONPLUS;
        else if (val == ID_ActiveMotionPlus_Nunchuck)
            current_ext = ExtensionController.MOTIONPLUS_NUNCHUCK;
        else if (val == ID_ActiveMotionPlus_Classic)
            current_ext = ExtensionController.MOTIONPLUS_CLASSIC;
        else if (val == ID_ClassicPro)
            current_ext = ExtensionController.CLASSIC_PRO;
        else if (val == ID_Nunchuck)
            current_ext = ExtensionController.NUNCHUCK;
        else if (val == ID_Classic)
            current_ext = ExtensionController.CLASSIC;
        else
            current_ext = ExtensionController.NONE;
    }
}


public class RegisterReadData
{
    public RegisterReadData(int Offset, int Size, ReadResponder Responder)
    {
        _Offset = Offset;
        _Size = Size;
        _Buffer = new byte[Size];
        _ExpectedOffset = Offset;
        _Responder = Responder;
    }

    public int ExpectedOffset
    {
        get { return _ExpectedOffset; }
    }
    private int _ExpectedOffset;

    public byte[] Buffer
    {
        get { return _Buffer; }
    }
    private byte[] _Buffer;

    public int Offset
    {
        get { return _Offset; }
    }
    private int _Offset;

    public int Size
    {
        get { return _Size; }
    }
    private int _Size;

    private ReadResponder _Responder;

    public bool AppendData(byte[] data)
    {
        int start = _ExpectedOffset - _Offset;
        int end = start + data.Length;

        if (end > _Buffer.Length)
            return false;

        for (int x = start; x < end; x++)
        {
            _Buffer[x] = data[x - start];
        }

        _ExpectedOffset += data.Length;

        if (_ExpectedOffset >= _Offset + _Size)
            _Responder(_Buffer);

        return true;
    }

}
}
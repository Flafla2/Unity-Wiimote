namespace WiimoteApi {
public class NunchuckData
{
    // Nunchuck Acceleration values.  These are in the same (RAW) format
    // as Wiimote.accel[].
    public int[] accel;
    // Nunchuck Analog Stick values.  This is a size 2 Array [X, Y] of
    // RAW (unprocessed) stick data.  Generally the analog stick returns
    // values in the range 35-228 for X and 27-220 for Y.  The center for
    // both is around 128.
    public byte[] stick;
    // If the C button has been pressed
    public bool c;
    // If the Z button has been pressed
    public bool z;

    public NunchuckData()
    {
        accel = new int[3];
        stick = new byte[2];
    }

    public void InterpretExtensionData(byte[] data) {
        if(data == null || data.Length < 6) {
            accel[0] = 0; accel[1] = 0; accel[2] = 0;
            stick[0] = 128; stick[1] = 128;
            c = false;
            z = false;
            return;
        }

        stick[0] = data[0];
        stick[1] = data[1];

        accel[0] = (int)data[2] << 2; accel[0] |= (data[5] & 0xc0) >> 6;
        accel[1] = (int)data[3] << 2; accel[1] |= (data[5] & 0x30) >> 4;
        accel[2] = (int)data[4] << 2; accel[2] |= (data[5] & 0x0c) >> 2;

        c = (data[5] & 0x02) == 0x02;
        z = (data[5] & 0x01) == 0x01;
    }

    public float[] GetStick01() {
        float[] ret = new float[2];
        ret[0] = stick[0];
        ret[0] -= 35;
        ret[1] = stick[1];
        ret[1] -= 27;
        for(int x=0;x<2;x++) {
            ret[x] /= 193f;
        }
        return ret;
    }
}

public class WiimotePlusData
{
    public int PitchSpeed = 0;
    public int YawSpeed = 0;
    public int RollSpeed = 0;
    public bool PitchSlow = false;
    public bool YawSlow = false;
    public bool RollSlow = false;
    public bool ExtensionConnected = false;

    public void InterpretExtensionData(byte[] data)
    {
        if (data.Length < 6)
        {
            PitchSpeed = 0;
            YawSpeed = 0;
            RollSpeed = 0;
            PitchSlow = false;
            YawSlow = false;
            RollSlow = false;
            ExtensionConnected = false;
            return;
        }

        YawSpeed = data[0];
        YawSpeed |= (int)(data[3] & 0xfc) << 6;
        RollSpeed = data[1];
        RollSpeed |= (int)(data[4] & 0xfc) << 6;
        PitchSpeed = data[2];
        PitchSpeed |= (int)(data[5] & 0xfc) << 6;

        YawSlow = (data[3] & 0x02) == 0x02;
        PitchSlow = (data[3] & 0x01) == 0x01;
        RollSlow = (data[4] & 0x02) == 0x02;
        ExtensionConnected = (data[4] & 0x01) == 0x01;
    }
}
}
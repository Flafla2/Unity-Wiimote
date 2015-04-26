using UnityEngine;
using WiimoteApi.Util;

namespace WiimoteApi
{
    public class AccelData : WiimoteData
    {
        // Current wiimote-space accelration, in wiimote coordinate system.
        // These are RAW values, so they may be off.  See CalibrateAccel().
        // This is only updated if the Wiimote has a report mode with Accel
        // Range:            -128 to 128
        // Up/Down:          +Z/-Z
        // Left/Right:       +X/-Z
        // Forward/Backward: -Y/+Y
        public ReadOnlyArray<int> accel { get { return _accel_readonly; } }
        private ReadOnlyArray<int> _accel_readonly;
        private int[] _accel;
        
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

        public AccelData(Wiimote Owner)
            : base(Owner)
        {
            _accel = new int[3];
            _accel_readonly = new ReadOnlyArray<int>(_accel);
        }

        public override bool InterpretData(byte[] data)
        {
            if (data == null || data.Length != 5) return false;

            // Note: data[0 - 1] is the buttons data.  data[2 - 4] is the accel data.
            // Accel data and buttons data is interleaved to reduce packet size.
            _accel[0] = ((int)data[2] << 2) | ((data[0] >> 5) & 0xff);
            _accel[1] = ((int)data[3] << 2) | ((data[1] >> 4) & 0xf0);
            _accel[2] = ((int)data[4] << 2) | ((data[1] >> 5) & 0xf0);

            for (int x = 0; x < 3; x++) _accel[x] -= 0x200; // center around zero.
            return true;
        }

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
    }
}
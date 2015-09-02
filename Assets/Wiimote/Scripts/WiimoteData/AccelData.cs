using UnityEngine;
using WiimoteApi.Util;

namespace WiimoteApi
{
    public class AccelData : WiimoteData
    {
        /// \brief Current remote-space acceleration, in the Wii Remote's coordinate system.
        ///        These are RAW values, so they are not with respect to a zero point.  See CalibrateAccel().
        ///        This is only updated if the Wii Remote has a report mode that supports
        ///        the Accelerometer.
        ///
        /// \warning This should not be used unless if you want to calibrate the accelerometer manually.  Use
        ///          CalibrateAccel() instead.
        ///
        /// Range:            0 - 1024\n
        /// *The sign of the directions below are with respect to the zero point of the accelerometer:*\n
        /// Up/Down:          +Z/-Z\n
        /// Left/Right:       +X/-X\n
        /// Forward/Backward: -Y/+Y\n
        public ReadOnlyArray<int> accel { get { return _accel_readonly; } }
        private ReadOnlyArray<int> _accel_readonly;
        private int[] _accel;
        
        /// \brief Size: 3x3. Calibration data for the accelerometer. This is not reported
        ///        by the Wii Remote directly - it is instead collected from normal
        ///        Wii Remote accelerometer data.
        /// \sa  AccelCalibrationStep,  CalibrateAccel(AccelCalibrationStep)
        ///
        /// Here are the 3 calibration steps:
        /// 1. Horizontal with the A button facing up
        /// 2. IR sensor down on the table so the expansion port is facing up
        /// 3. Laying on its side, so the left side is facing up
        /// 
        /// By default this is set to experimental calibration data.
        /// 
        /// int[calibration step,calibration data] (size 3x3)
        public int[,] accel_calib = {
                                    { 479, 478, 569 },
                                    { 472, 568, 476 },
                                    { 569, 469, 476 }
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
            _accel[0] = ((int)data[2] << 2) | ((data[0] >> 5) & 0x03);
            _accel[1] = ((int)data[3] << 2) | ((data[1] >> 5) & 0x01);
            _accel[2] = ((int)data[4] << 2) | ((data[1] >> 6) & 0x01);

            //for (int x = 0; x < 3; x++) _accel[x] -= 0x200; // center around zero.
            
            return true;
        }

        /// \brief Interprets raw byte data reported by the Wii Remote when in interleaved data reporting mode.
        ///        The format of the actual bytes passed to this depends on the Wii Remote's current data report
        ///        mode and the type of data being passed.
        /// 
        /// \sa Wiimote::ReadWiimoteData()
        public bool InterpretDataInterleaved(byte[] data1, byte[] data2)
        {
            if (data1 == null || data2 == null || data1.Length != 21 || data2.Length != 21)
                return false;

            _accel[0] = (int)data1[2] << 2;
            _accel[1] = (int)data2[2] << 2;
            _accel[2] =   (int)(((data1[0] & 0x60) >> 1) | 
                                ((data1[1] & 0x60) << 1) | 
                                ((data2[0] & 0x60) >> 5) | 
                                ((data2[1] & 0x60) >> 3)) << 2;

            //for (int x = 0; x < 3; x++) _accel[x] -= 0x200; // center around zero.

            return true;
        }

        /// \brief Use current accelerometer values to update calibration data.  Use this when
        ///        the user reports that the Wii Remote is in a calibration position.
        /// \param step The calibration step to perform.
        /// \sa  accel_calib,  AccelCalibrationStep
        public void CalibrateAccel(AccelCalibrationStep step)
        {
            for (int x = 0; x < 3; x++)
                accel_calib[(int)step, x] = accel[x];
        }

        public float[] GetAccelZeroPoints()
        {
            float[] ret = new float[3];
            // For each axis, find the two steps that are not affected by gravity on that axis.
            // average these values together to get a final zero point.
            ret[0] = ((float)accel_calib[0, 0] + (float)accel_calib[1, 0]) / 2f;
            ret[1] = ((float)accel_calib[0, 1] + (float)accel_calib[2, 1]) / 2f;
            ret[2] = ((float)accel_calib[1, 2] + (float)accel_calib[2, 2]) / 2f;
            return ret;
        }

        /// \brief Calibrated Accelerometer Data using experimental calibration points.
        ///        These values are in Wii Remote coordinates (in the direction of gravity)
        /// \sa  CalibrateAccel(),  GetAccelZeroPoints(),  accel,  accel_calib
        ///
        /// Range: -1 to 1\n
        /// Up/Down:          +Z/-Z\n
        /// Left/Right:       +X/-X\n
        /// Forward/Backward: -Y/+Y
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
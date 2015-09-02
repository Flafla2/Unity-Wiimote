using UnityEngine;
using WiimoteApi.Util;

namespace WiimoteApi
{
    public class IRData : WiimoteData
    {
        /// \brief Size: 4x3.  Current Wii Remote RAW IR data.  Wii Remote IR data can
        ///        detect up to four IR dots.  Data = -1 if it is inapplicable (for
        ///        example, if there are less than four dots, or if size data is
        ///        unavailable due to the selected IRDataType).
        ///
        /// This is only updated if the Wii Remote has a report mode with IR
        ///
        /// |       | Position X | Position Y |  Size  |  X min  |  Y min  |  X max  |  Y max  | Intensity |
        /// |------ | ---------- | ---------- | ------ | ------- | ------- | ------- | ------- | --------- |
        /// |Range: |  0 - 1023  |  0 - 767   | 0 - 15 | 0 - 127 | 0 - 127 | 0 - 127 | 0 - 127 |  0 - 256  |
        /// |Index: |     0      |      1     |   2    |    3    |    4    |    5    |    6    |     7     |
        ///
        /// \code int[dot index, x (0) / y (1) / size (2) / xmin (3) / ymin (4) / xmax (5) / ymax (6) / intensity (7)] \endcode
        /// 
        /// \sa IRDataType, Wiimote::SetupIRCamera(IRDataType)
        public ReadOnlyMatrix<int> ir { get { return _ir_readonly; } }
        private ReadOnlyMatrix<int> _ir_readonly;
        private int[,] _ir;

        public IRData(Wiimote Owner)
            : base(Owner)
        {
            _ir = new int[4, 8];
            _ir_readonly = new ReadOnlyMatrix<int>(_ir);
        }

        public override bool InterpretData(byte[] data)
        {
            switch (data.Length)
            {
                case 10:
                    InterpretIRData10(data);
                    return true;
                case 12:
                    InterpretIRData12(data);
                    return true;
                default:
                    return false;
            }
        }

        /// \brief Interprets raw byte data reported by the Wii Remote when in interleaved data reporting mode.
        ///        The format of the actual bytes passed to this depends on the Wii Remote's current data report
        ///        mode and the type of data being passed.
        /// 
        /// \sa Wiimote::ReadWiimoteData()
        public bool InterpretDataInterleaved(byte[] data1, byte[] data2)
        {
            if (data1 == null || data2 == null || data1.Length != 18 || data2.Length != 18)
                return false;

            byte[] subset = new byte[9];
            int[] res;

            for (int x = 0; x < 4; x++) {
                int index = x * 9;
                byte[] data = index >= 18 ? data1 : data2;
                index %= 18;

                for (int y = index; y < index + 9; y++)
                    subset[y - index] = data[y];

                res = InterpretDataInterleaved_Subset(subset);

                for (int y = 0; y < 8; y++)
                    _ir[x, y] = res[y];
            }

            return true;
        }

        private int[] InterpretDataInterleaved_Subset(byte[] data)
        {
            if (data.Length != 9) return new int[] { -1, -1, -1, -1, -1, -1, -1, -1 };
            if (data[0] == 0xff && data[1] == 0xff && data[2] == 0xff) return new int[] { -1, -1, -1, -1, -1, -1, -1, -1 };

            int x = data[0];
            x |= ((int)(data[2] & 0x30)) << 4;
            int y = data[1];
            y |= ((int)(data[2] & 0xc0)) << 2;
            int size = data[2] & 0x0f;
            int xmin = data[3];
            int ymin = data[4];
            int xmax = data[5];
            int ymax = data[6];
            int inten = data[7];

            return new int[] { x, y, size, xmin, ymin, xmax, ymax, inten };
        }

        private void InterpretIRData10(byte[] data)
        {
            if (data.Length != 10) return;

            byte[] half = new byte[5];
            for (int x = 0; x < 5; x++) half[x] = data[x];
            int[,] subset = InterperetIRData10_Subset(half);
            for (int x = 0; x < 2; x++)
                for (int y = 0; y < 8; y++)
                    _ir[x, y] = subset[x, y];

            for (int x = 0; x < 5; x++) half[x] = data[x + 5];
            subset = InterperetIRData10_Subset(half);
            for (int x = 0; x < 2; x++)
                for (int y = 0; y < 8; y++)
                    _ir[x + 2, y] = subset[x, y];
        }

        private int[,] InterperetIRData10_Subset(byte[] data)
        {
            if (data.Length != 5) return new int[,] {{-1, -1, -1, -1, -1, -1, -1, -1},
                                                     {-1, -1, -1, -1, -1, -1, -1, -1}};

            int x1 = data[0];
            x1 |= ((int)(data[2] & 0x30)) << 4;
            int y1 = data[1];
            y1 |= ((int)(data[2] & 0xc0)) << 2;

            if (data[0] == 0xff && data[1] == 0xff && (data[2] & 0xf0) == 0xf0)
            {
                x1 = -1;
                y1 = -1;
            }

            int x2 = data[3];
            x2 |= ((int)(data[2] & 0x03)) << 8;
            int y2 = data[4];
            y2 |= ((int)(data[2] & 0x0c)) << 6;

            if (data[3] == 0xff && data[4] == 0xff && (data[2] & 0x0f) == 0x0f)
            {
                x2 = -1;
                y2 = -1;
            }

            return new int[,] { { x1, y1, -1, -1, -1, -1, -1, -1 },
                                { x2, y2, -1, -1, -1, -1, -1, -1 }};
        }

        private void InterpretIRData12(byte[] data)
        {
            if (data.Length != 12) return;
            for (int x = 0; x < 4; x++)
            {
                int i = x * 3; // starting index of data
                byte[] subset = new byte[] { data[i], data[i + 1], data[i + 2] };
                int[] calc = InterpretIRData12_Subset(subset);

                for (int y = 0; y < 8; y++)
                    _ir[x, y] = calc[y];
            }
        }

        private int[] InterpretIRData12_Subset(byte[] data)
        {
            if (data.Length != 3) return new int[] { -1, -1, -1, -1, -1, -1, -1, -1 };
            if (data[0] == 0xff && data[1] == 0xff && data[2] == 0xff) return new int[] { -1, -1, -1, -1, -1, -1, -1, -1 };

            int x = data[0];
            x |= ((int)(data[2] & 0x30)) << 4;
            int y = data[1];
            y |= ((int)(data[2] & 0xc0)) << 2;
            int size = data[2] & 0x0f;

            return new int[] { x, y, size, -1, -1, -1, -1, -1 };
        }

        /// \brief Size: 2.  Returns the position at which the Wii Remote is pointing to.  This is a value from 0-1
        ///        representing the camera-space pointing position in X and Y.  Assume a 4x3 aspect ratio.
        ///
        /// This takes into account the rotation of the remote (using the Wii Remote's Accelerometer) to correct for
        /// rotational distortion.
        public float[] GetPointingPosition()
        {
            float[] ret = new float[2];
            float[] midpoint = GetIRMidpoint();
            if (midpoint[0] < 0 || midpoint[1] < 0)
                return new float[] { -1, -1 };
            midpoint[0] = 1 - midpoint[0] - 0.5f;
            midpoint[1] = midpoint[1] - 0.5f;

            float[] accel = Owner.Accel.GetCalibratedAccelData();

            float rotation = Mathf.Atan2(accel[2], accel[0]) - (float)(Mathf.PI / 2.0f);
            float cos = Mathf.Cos(rotation);
            float sin = Mathf.Sin(rotation);
            ret[0] = midpoint[0] * cos + midpoint[1] * sin;
            ret[1] = -midpoint[0] * sin + midpoint[1] * cos;
            ret[0] += 0.5f;
            ret[1] += 0.5f;

            ret[1] = 1 - ret[1];

            return ret;
        }

        /// \brief Size: 2.  Returns the midpoint of all IR dots, or [0, 0] if none are found.  This is a value from 0-1
        ///        representing the camera-space position in X and Y.
        /// \param predict If true, and one of the IR "dots" from the sensor bar is outside of the IR camera field of view,
        ///                WiimoteApi will attempt to predict the other dot's position outside of the camera (default true).
        public float[] GetIRMidpoint(bool predict = true)
        {
            float[] ret = new float[2];
            float[,] sensorIR = GetProbableSensorBarIR(predict);
            ret[0] = sensorIR[0, 0] + sensorIR[1, 0];
            ret[1] = sensorIR[0, 1] + sensorIR[1, 1];
            ret[0] /= 2f * 1023f;
            ret[1] /= 2f * 767f;

            return ret;
        }

        private float[] LastIRSeparation = new float[] { 0, 0 };
        private int[] SensorBarIndices = new int[] { -1, -1 };

        /// \brief Attempts to identify which of the four IR "dots" reported by the Wii Remote are from the Wii sensor bar.
        /// \param predict If true, and one of the dots is outside of the Wii Remote's field of view,
        ///                WiimoteApi will attempt to predict the other dot's position outside of the screen (default true).
        ///
        /// \returns First Dimension: Index of detected IR dot.\n
        /// Second Dimension: 0: X, 1: Y, 2: Index in \link ir \endlink (or -1 if predicted)\n
        /// Size: 2x3\n
        /// Range: 0-1 with respect to the Wii Remote Camera dimensions.  If \c predict is true this may be outside of that range.
        public float[,] GetProbableSensorBarIR(bool predict = true)
        {
            // If necessary, change the current "sensor bar" IR indices to new ones.  This happens if one of the dots went out of focus and a new one took its place.
            // We do this because the Wii Remote reports "consistent" IR dot indices - that is, it tracks the IR dots and doesn't change their index in the IR report.
            // This way we can rule out extraneous dots that pop in and out randomly as they aren't being tracked.
            for (int x = 0; x < 2; x++) {
                if (SensorBarIndices[x] == -1 || _ir[SensorBarIndices[x], 0] == -1) {
                    SensorBarIndices[x] = -1;
                    for (int y = 0; y < 4; y++) {
                        if (SensorBarIndices[(x + 1) % 2] == y) continue; // If the other sensor bar index is this one, ignore it.

                        if (_ir[y, 0] != -1) { // If this index is valid, use it.
                            SensorBarIndices[x] = y;
                            y = 4; // end loop
                        }
                    }
                }
            }

            // The first index is the "primary" index (from which the IR separation is derived) so if it goes out of focus
            // the other dot becomes primary, and the IR separation is negated.
            if (SensorBarIndices[0] == -1 && SensorBarIndices[1] != -1)
            {
                SensorBarIndices[0] = SensorBarIndices[1];
                SensorBarIndices[1] = -1;

                LastIRSeparation[0] *= -1;
                LastIRSeparation[1] *= -1;
            }

            if (SensorBarIndices[0] != -1 && SensorBarIndices[1] != -1)
            {
                float[,] ret = new float[2, 3];
                for (int x = 0; x < 2; x++)
                {
                    for (int y = 0; y < 2; y++)
                    {
                        ret[x, y] = _ir[SensorBarIndices[x], y];
                    }
                }

                ret[0, 2] = SensorBarIndices[0];
                ret[1, 2] = SensorBarIndices[1];

                LastIRSeparation[0] = ret[1, 0] - ret[0, 0];
                LastIRSeparation[1] = ret[1, 1] - ret[0, 1];

                return ret;
            } else if (predict && SensorBarIndices[0] != -1) // We have enought data to predict (1 dot) and predicting was requested
            {
                float[,] ret = new float[2, 3];
                ret[0, 0] = _ir[SensorBarIndices[0], 0];
                ret[0, 1] = _ir[SensorBarIndices[0], 1];
                ret[0, 2] = SensorBarIndices[0];

                ret[1, 0] = ret[0, 0] + LastIRSeparation[0];
                ret[1, 1] = ret[0, 1] + LastIRSeparation[1];
                ret[1, 2] = -1;

                return ret;
            } else // We don't have enough data
            {
                LastIRSeparation[0] = 0;
                LastIRSeparation[1] = 0;

                return new float[,] { { -1, -1, -1 }, { -1, -1, -1 } };
            }
        }
    }
}
using UnityEngine;
using WiimoteApi.Util;

namespace WiimoteApi
{
    public class IRData : WiimoteData
    {
        /// \brief Size: 4x3.  Current wiimote RAW IR data.  Wiimote IR data can
        ///        detect up to four IR dots.  Data = -1 if it is inapplicable (for
        ///        example, if there are less than four dots, or if size data is
        ///        unavailable).
        ///
        /// This is only updated if the Wiimote has a report mode with IR
        ///
        ///        | Position X | Position Y |  Size  |
        /// Range: |  0 - 1023  |  0 - 767   | 0 - 15 |
        /// Index: |     0      |      1     |   2    |
        ///
        /// int[dot index, x (0) / y (1) / size (2)]
        public ReadOnlyMatrix<int> ir { get { return _ir_readonly; } }
        public ReadOnlyMatrix<int> _ir_readonly;
        private int[,] _ir;

        public IRData(Wiimote Owner)
            : base(Owner)
        {
            _ir = new int[4, 3];
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

        private void InterpretIRData10(byte[] data)
        {
            if (data.Length != 10) return;

            byte[] half = new byte[5];
            for (int x = 0; x < 5; x++) half[x] = data[x];
            int[,] subset = InterperetIRData10_Subset(half);
            for (int x = 0; x < 2; x++)
                for (int y = 0; y < 3; y++)
                    _ir[x, y] = subset[x, y];

            for (int x = 0; x < 5; x++) half[x] = data[x + 5];
            subset = InterperetIRData10_Subset(half);
            for (int x = 0; x < 2; x++)
                for (int y = 0; y < 3; y++)
                    _ir[x + 2, y] = subset[x, y];
        }

        private int[,] InterperetIRData10_Subset(byte[] data)
        {
            if (data.Length != 5) return new int[,] { { -1, -1, -1 }, { -1, -1, -1 } };

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

            return new int[,] { { x1, y1, -1 }, { x2, y2, -1 } };
        }

        private void InterpretIRData12(byte[] data)
        {
            if (data.Length != 12) return;
            for (int x = 0; x < 4; x++)
            {
                int i = x * 3; // starting index of data
                byte[] subset = new byte[] { data[i], data[i + 1], data[i + 2] };
                int[] calc = InterpretIRData12_Subset(subset);

                _ir[x, 0] = calc[0];
                _ir[x, 1] = calc[1];
                _ir[x, 2] = calc[2];
            }
        }

        private int[] InterpretIRData12_Subset(byte[] data)
        {
            if (data.Length != 3) return new int[] { -1, -1, -1 };
            if (data[0] == 0xff && data[1] == 0xff && data[2] == 0xff) return new int[] { -1, -1, -1 };

            int x = data[0];
            x |= ((int)(data[2] & 0x30)) << 4;
            int y = data[1];
            y |= ((int)(data[2] & 0xc0)) << 2;
            int size = data[2] & 0x0f;

            return new int[] { x, y, size };
        }

        /// \brief Size: 2.  Returns the position at which the wiimote is pointing to.  This is a value from 0-1
        ///        representing the screen-space pointing position in X and Y.  Assume a 4x3 aspect ratio.
        public float[] GetPointingPosition()
        {
            float[] ret = new float[2];
            float[] midpoint = GetIRMidpoint();
            if (midpoint[0] < 0 || midpoint[1] < 0)
                return new float[] { -1, -1 };
            midpoint[0] = 1 - midpoint[0] - 0.5f;
            midpoint[1] = midpoint[1] - 0.5f;

            float rotation = Mathf.Atan2(Owner.Accel.accel[2], Owner.Accel.accel[0]) - (float)(Mathf.PI / 2.0f);
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
        ///        representing the screen-space position in X and Y.
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

        private float[] LastIRSeparation = new float[] { 0, 0 };
        /// \brief Size: 2x2.  Returns the most probable positions of the two sensor bar dots.
        ///        If less than two dots are found, returns -1 for all data.
        /// \param predict If true, and one of the dots is outside of the Wiimote's field of view,
        ///                WiimoteApi will attempt to predict the other dot's position outside of the screen.
        ///
        /// Range: 0 - 1 with respect to Wiimote camera dimensions.  If \c predict is true this may be outside of that range.
        public float[,] GetProbableSensorBarIR(bool predict = false)
        {
            int count = 0;
            int[] ind = new int[2];
            for (int x = 0; x < 4; x++)
            {
                if (count > 1 || _ir[x, 0] == -1 || _ir[x, 1] == -1)
                    continue;

                ind[count] = x;
                if (count == 1 && _ir[ind[0], 0] > _ir[x, 0])
                {
                    ind[1] = ind[0];
                    ind[0] = x;
                }
                count++;
            }

            if (count == 0 || (count == 1 && !predict))
                return new float[,] { { -1, -1 }, { -1, -1 } };

            float[,] ret = new float[2, 2];
            for (int x = 0; x < count; x++)
            {
                for (int y = 0; y < 2; y++)
                    ret[x, y] = _ir[ind[x], y];
            }

            if (count == 1) // one of the dots are outside of the wiimote FOV
            {

                if (ret[0, 0] < 1023 / 2) // Left side of the screen, means that it's the right dot
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
    }
}
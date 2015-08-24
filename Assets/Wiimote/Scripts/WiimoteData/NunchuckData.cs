using WiimoteApi.Util;

namespace WiimoteApi {
    public class NunchuckData : WiimoteData
    {
        /// Nunchuck accelerometer values.  These are in the same (RAW) format
        /// as Wiimote::accel.
        public ReadOnlyArray<int> accel { get { return _accel_readonly; } }
        private ReadOnlyArray<int> _accel_readonly;
        private int[] _accel;

        /// Nunchuck Analog Stick values.  This is a size 2 Array [X, Y] of
        /// RAW (unprocessed) stick data.  Generally the analog stick returns
        /// values in the range 35-228 for X and 27-220 for Y.  The center for
        /// both is around 128.
        public ReadOnlyArray<byte> stick { get { return _stick_readonly; } }
        private ReadOnlyArray<byte> _stick_readonly;
        private byte[] _stick;

        /// Button: C
        public bool c { get { return _c; } }
        private bool _c;
        /// Button: Z
        public bool z { get { return _z; } }
        private bool _z;

        public NunchuckData(Wiimote Owner)
            : base(Owner)
        {
            _accel = new int[3];
            _accel_readonly = new ReadOnlyArray<int>(_accel);

            _stick = new byte[2];
            _stick_readonly = new ReadOnlyArray<byte>(_stick);
        }

        public override bool InterpretData(byte[] data) {
            if(data == null || data.Length < 6) {
                _accel[0] = 0; _accel[1] = 0; _accel[2] = 0;
                _stick[0] = 128; _stick[1] = 128;
                _c = false;
                _z = false;
                return false;
            }

            _stick[0] = data[0];
            _stick[1] = data[1];

            _accel[0] = (int)data[2] << 2; _accel[0] |= (data[5] & 0xc0) >> 6;
            _accel[1] = (int)data[3] << 2; _accel[1] |= (data[5] & 0x30) >> 4;
            _accel[2] = (int)data[4] << 2; _accel[2] |= (data[5] & 0x0c) >> 2;

            _c = (data[5] & 0x02) != 0x02;
            _z = (data[5] & 0x01) != 0x01;
            return true;
        }

        /// Returns a size 2 [X, Y] array of the analog stick's position, in the range
        /// 0 - 1.  This takes into account typical Nunchuck data ranges and zero points.
        public float[] GetStick01() {
            float[] ret = new float[2];
            ret[0] = _stick[0];
            ret[0] -= 35;
            ret[1] = stick[1];
            ret[1] -= 27;
            for(int x=0;x<2;x++) {
                ret[x] /= 193f;
            }
            return ret;
        }
    }
}
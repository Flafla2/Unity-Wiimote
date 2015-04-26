namespace WiimoteApi {
    public class MotionPlusData : WiimoteData
    {
        public int PitchSpeed { get { return _PitchSpeed; } }
        private int _PitchSpeed = 0;

        public int YawSpeed { get { return _YawSpeed; } }
        private int _YawSpeed = 0;

        public int RollSpeed { get { return _RollSpeed; } }
        private int _RollSpeed = 0;

        public bool PitchSlow { get { return _PitchSlow; } }
        private bool _PitchSlow = false;

        public bool YawSlow { get { return _YawSlow; } }
        private bool _YawSlow = false;

        public bool RollSlow { get { return _RollSlow; } }
        private bool _RollSlow = false;

        public bool ExtensionConnected { get { return _ExtensionConnected; } }
        private bool _ExtensionConnected = false;

        public MotionPlusData(Wiimote Owner) : base(Owner) { }

        public override bool InterpretData(byte[] data)
        {
            if (data == null || data.Length < 6)
                return false;

            _YawSpeed = data[0];
            _YawSpeed |= (int)(data[3] & 0xfc) << 6;
            _RollSpeed = data[1];
            _RollSpeed |= (int)(data[4] & 0xfc) << 6;
            _PitchSpeed = data[2];
            _PitchSpeed |= (int)(data[5] & 0xfc) << 6;

            _YawSlow = (data[3] & 0x02) == 0x02;
            _PitchSlow = (data[3] & 0x01) == 0x01;
            _RollSlow = (data[4] & 0x02) == 0x02;
            _ExtensionConnected = (data[4] & 0x01) == 0x01;

            return true;
        }
    }
}
namespace WiimoteApi {
    public class MotionPlusData : WiimoteData
    {
        public int PitchSpeed = 0;
        public int YawSpeed = 0;
        public int RollSpeed = 0;
        public bool PitchSlow = false;
        public bool YawSlow = false;
        public bool RollSlow = false;
        public bool ExtensionConnected = false;

        public override bool InterpretData(byte[] data)
        {
            if (data == null || data.Length < 6)
                return false;

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

            return true;
        }
    }
}
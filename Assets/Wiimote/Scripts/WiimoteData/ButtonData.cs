namespace WiimoteApi
{
    public class ButtonData : WiimoteData
    {
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

        public ButtonData(Wiimote Owner) : base(Owner) { }

        public override bool InterpretData(byte[] data)
        {
            if (data == null || data.Length != 2) return false;

            d_left = (data[0] & 0x01) == 0x01;
            d_right = (data[0] & 0x02) == 0x02;
            d_down = (data[0] & 0x04) == 0x04;
            d_up = (data[0] & 0x08) == 0x08;
            plus = (data[0] & 0x10) == 0x10;

            two = (data[1] & 0x01) == 0x01;
            one = (data[1] & 0x02) == 0x02;
            b = (data[1] & 0x04) == 0x04;
            a = (data[1] & 0x08) == 0x08;
            minus = (data[1] & 0x10) == 0x10;

            home = (data[1] & 0x80) == 0x80;

            return true;
        }
    }
}
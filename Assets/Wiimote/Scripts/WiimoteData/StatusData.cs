using UnityEngine;
using System.Collections;

namespace WiimoteApi
{
    public class StatusData : WiimoteData
    {
        public bool[] led;
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

        public StatusData(Wiimote Owner)
            : base(Owner)
        {
            led = new bool[4];
        }

        public override bool InterpretData(byte[] data)
        {
            if (data == null || data.Length != 2) return false;

            byte flags = data[0];
            battery_low = (flags & 0x01) == 0x01;
            ext_connected = (flags & 0x02) == 0x02;
            speaker_enabled = (flags & 0x04) == 0x04;
            ir_enabled = (flags & 0x08) == 0x08;
            led[0] = (flags & 0x10) == 0x10;
            led[1] = (flags & 0x20) == 0x20;
            led[2] = (flags & 0x40) == 0x40;
            led[3] = (flags & 0x80) == 0x80;

            battery_level = data[1];

            return true;
        }
    }
}
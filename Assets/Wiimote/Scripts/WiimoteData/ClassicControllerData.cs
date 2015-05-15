using WiimoteApi;
using WiimoteApi.Util;

namespace WiimoteApi {
	public class ClassicControllerData : WiimoteData {

		/// Classic Controller left stick analog values.  This is a size-2 array [X,Y]
		/// of RAW (unprocessed) stick data.  These values are in the range 0-63
		/// in both X and Y.
		///
		/// \sa GetLeftStick01()
		public ReadOnlyArray<byte> lstick { get { return _lstick_readonly; } }
		private ReadOnlyArray<byte> _lstick_readonly;
		private byte[] _lstick;

		/// Classic Controller right stick analog values.  This is a size-2 array [X,Y]
		/// of RAW (unprocessed) stick data.  These values are in the range 0-31
		/// in both X and Y.
		/// 
		/// \note The Right analog stick reports one less bit of precision than the left
		///       stick (the left stick is in the range 0-63 while the right is 0-31).
		///
		/// \sa GetRightStick01()
		public ReadOnlyArray<byte> rstick { get { return _rstick_readonly; } }
		private ReadOnlyArray<byte> _rstick_readonly;
		private byte[] _rstick;

		/// Classic Controller left trigger analog value.  This is RAW (unprocessed) analog
		/// data.  It is in the range 0-31 (with 0 being unpressed and 31 being fully pressed).
		///
		/// \sa rtrigger_range, ltrigger_switch, ltrigger_switch
		public byte ltrigger_range { get { return _ltrigger_range; } }
		private byte _ltrigger_range;

		/// Classic Controller right trigger analog value.  This is RAW (unprocessed) analog
		/// data.  It is in the range 0-31 (with 0 being unpressed and 31 being fully pressed).
		///
		/// \sa ltrigger_range, rtrigger_switch, rtrigger_switch
		public byte rtrigger_range { get { return _rtrigger_range; } }
		private byte _rtrigger_range;

		/// Button: Left trigger (bottom out switch)
		/// \sa rtrigger_switch, rtrigger_range, ltrigger_range
		public bool ltrigger_switch { get { return _ltrigger_switch; } }
		private bool _ltrigger_switch;

		/// Button: Right trigger (button out switch)
		/// \sa ltrigger_switch, ltrigger_range, rtrigger_range
		public bool rtrigger_switch { get { return _rtrigger_switch; } }
		private bool _rtrigger_switch;

		/// Button: A
		public bool a { get { return _a; } }
		private bool _a;

		/// Button: B
		public bool b { get { return _b; } }
		private bool _b;

		/// Button: X
		public bool x { get { return _x; } }
		private bool _x;

		/// Button: Y
		public bool y { get { return _y; } }
		private bool _y;

		/// Button: + (plus)
		public bool plus { get { return _plus; } }
		private bool _plus;

		/// Button: - (minus)
		public bool minus { get { return _minus; } }
		private bool _minus;

		/// Button: home
		public bool home { get { return _home; } }
		private bool _home;

		/// Button:  ZL
		public bool zl { get { return _zl; } }
		private bool _zl;

		/// Button: ZR
		public bool zr { get { return _zr; } }
		private bool _zr;

		/// Button: D-Pad Up
		public bool dpad_up { get { return _dpad_up; } }
		private bool _dpad_up;

		/// Button: D-Pad Down
		public bool dpad_down { get { return _dpad_down; } }
		private bool _dpad_down;

		/// Button: D-Pad Left
		public bool dpad_left { get { return _dpad_left; } }
		private bool _dpad_left;

		/// Button: D-Pad Right
		public bool dpad_right { get { return _dpad_right; } }
		private bool _dpad_right;

		public ClassicControllerData(Wiimote owner) : base(owner) {
			_lstick = new byte[2];
			_lstick_readonly = new ReadOnlyArray<byte>(_lstick);

			_rstick = new byte[2];
			_rstick_readonly = new ReadOnlyArray<byte>(_rstick);
		}

		public override bool InterpretData(byte[] data) {
			if(data == null || data.Length < 6)
				return false;

			_lstick[0] = (byte)(data[0] & 0x3f);
			_lstick[1] = (byte)(data[1] & 0x3f);

			_rstick[0] = (byte)(((data[0] & 0xc0) >> 3) |
								((data[1] & 0xc0) >> 5) |
								((data[2] & 0x80) >> 7));
			_rstick[1] = (byte)(data[2] & 0x1f);

			_ltrigger_range = (byte)(((data[2] & 0x60) >> 2) |
								((data[3] & 0xe0) >> 5));

			_rtrigger_range = (byte)(data[3] & 0x1f);

			// Bit is zero when pressed, one when up.  This is really weird so I reverse
			// the bit with !=
			_dpad_right 	 = (data[4] & 0x80) != 0x80;
			_dpad_down  	 = (data[4] & 0x40) != 0x40;
			_ltrigger_switch = (data[4] & 0x20) != 0x20;
			_minus 			 = (data[4] & 0x10) != 0x10;
			_home 			 = (data[4] & 0x08) != 0x08;
			_plus			 = (data[4] & 0x04) != 0x04;
			_rtrigger_switch = (data[4] & 0x02) != 0x02;

			_zl 			 = (data[5] & 0x80) != 0x80;
			_b 				 = (data[5] & 0x40) != 0x40;
			_y 				 = (data[5] & 0x20) != 0x20;
			_a 				 = (data[5] & 0x10) != 0x10;
			_x 				 = (data[5] & 0x08) != 0x08;
			_zr 			 = (data[5] & 0x04) != 0x04;
			_dpad_left		 = (data[5] & 0x02) != 0x02;
			_dpad_up		 = (data[5] & 0x01) != 0x01;

			return true;
		}

		/// Returns the left stick analog values in the range 0-1.
		///
		/// \warning This does not take into account zero points or deadzones.  Likewise it does not guaruntee that 0.5f
		///			 is the zero point.  You must do these calibrations yourself.
		public float[] GetLeftStick01() {
			float[] ret = new float[2];
			for(int x=0;x<2;x++) {
				ret[x] = lstick[x];
				ret[x] /= 63;
			}
			return ret;
		}

		/// Returns the right stick analog values in the range 0-1.
		///
		/// \note The Right stick has half of the precision of the left stick due to how the Wiimote reports data.  The
		/// 	  right stick is therefore better for less precise input.
		///
		/// \warning This does not take into account zero points or deadzones.  Likewise it does not guaruntee that 0.5f
		///			 is the zero point.  You must do these calibrations yourself.
		public float[] GetRightStick01() {
			float[] ret = new float[2];
			for(int x=0;x<2;x++) {
				ret[x] = rstick[x];
				ret[x] /= 31;
			}
			return ret;
		}
	}
}
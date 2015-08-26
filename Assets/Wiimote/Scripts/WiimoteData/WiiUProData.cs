using WiimoteApi;
using WiimoteApi.Util;

namespace WiimoteApi {
	public class WiiUProData : WiimoteData {

		/// Pro Controller left stick analog values.  This is a size-2 array [X,Y]
		/// of RAW (unprocessed) stick data.  These values are in the range 803-3225
		/// in the X direction and 843-3291 in the Y direction.
		///
		/// \note Min/Max values may vary between controllers (untested).  One way to calibrate
		///		  is to prompt the user to spin the control sticks in circles and record the min/max values.
		///
		/// \sa GetLeftStick01()
		public ReadOnlyArray<ushort> lstick { get { return _lstick_readonly; } }
		private ReadOnlyArray<ushort> _lstick_readonly;
		private ushort[] _lstick;

		/// Pro Controller right stick analog values.  This is a size-2 array [X,Y]
		/// of RAW (unprocessed) stick data.  These values are in the range 852-3169
		/// in the X direction and 810-3315 in the Y direction.
		///
		/// \note Min/Max values may vary between controllers (untested).  One way to calibrate
		///		  is to prompt the user to spin the control sticks in circles and record the min/max values.
		///
		/// \sa GetRightStick01()
		public ReadOnlyArray<ushort> rstick { get { return _rstick_readonly; } }
		private ReadOnlyArray<ushort> _rstick_readonly;
		private ushort[] _rstick;

		/// Button: Left Stick Button (push down switch)
		public bool lstick_button { get { return _lstick_button; } }
		private bool _lstick_button;

		/// Button: Right Stick Button (push down switch)
		public bool rstick_button { get { return _rstick_button; } }
		private bool _rstick_button;

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

		/// Button:  L
		public bool l { get { return _l; } }
		private bool _l;

		/// Button: R
		public bool r { get { return _r; } }
		private bool _r;

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

		private ushort[] lmax = {3225,3291};
		private ushort[] lmin = {803,843};
		private ushort[] rmax = {3169,3315};
		private ushort[] rmin = {852,810};

		public WiiUProData(Wiimote owner) : base(owner) {
			_lstick = new ushort[2];
			_lstick_readonly = new ReadOnlyArray<ushort>(_lstick);

			_rstick = new ushort[2];
			_rstick_readonly = new ReadOnlyArray<ushort>(_rstick);
		}

		public override bool InterpretData(byte[] data)
	    {
	        if(data == null || data.Length < 11)
	       		return false;

	       	_lstick[0] = (ushort)((ushort)data[0] | ((ushort)(data[1] & 0x0f) << 8));
	       	_lstick[1] = (ushort)((ushort)data[4] | ((ushort)(data[5] & 0x0f) << 8));

	       	_rstick[0] = (ushort)((ushort)data[2] | ((ushort)(data[3] & 0x0f) << 8));
	       	_rstick[1] = (ushort)((ushort)data[6] | ((ushort)(data[7] & 0x0f) << 8));

	       	_dpad_right	= (data[8] & 0x80) != 0x80;
	       	_dpad_down 	= (data[8] & 0x40) != 0x40;
	       	_l 			= (data[8] & 0x20) != 0x20;
	       	_minus	 	= (data[8] & 0x10) != 0x10;
	       	_home	 	= (data[8] & 0x08) != 0x08;
	       	_plus	 	= (data[8] & 0x04) != 0x04;
	       	_r		 	= (data[8] & 0x02) != 0x02;

	       	_zl 		= (data[9] & 0x80) != 0x80;
	       	_b	 		= (data[9] & 0x40) != 0x40;
	       	_y 			= (data[9] & 0x20) != 0x20;
	       	_a	 		= (data[9] & 0x10) != 0x10;
	       	_x		 	= (data[9] & 0x08) != 0x08;
	       	_zr		 	= (data[9] & 0x04) != 0x04;
	       	_dpad_left 	= (data[9] & 0x02) != 0x02;
	       	_dpad_up 	= (data[9] & 0x01) != 0x01;

	       	_lstick_button = (data[10] & 0x02) != 0x02;
	       	_rstick_button = (data[10] & 0x01) != 0x01;

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
				ret[x] -= lmin[x];
				ret[x] /= lmax[x]-lmin[x];
			}
			return ret;
		}

		/// Returns the right stick analog values in the range 0-1.
		///
		/// \warning This does not take into account zero points or deadzones.  Likewise it does not guaruntee that 0.5f
		///			 is the zero point.  You must do these calibrations yourself.
		public float[] GetRightStick01() {
			float[] ret = new float[2];
			for(int x=0;x<2;x++) {
				ret[x] = rstick[x];
				ret[x] -= rmin[x];
				ret[x] /= rmax[x]-rmin[x];
			}
			return ret;
		}
	}
}
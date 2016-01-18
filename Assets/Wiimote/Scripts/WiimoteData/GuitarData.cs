using WiimoteApi.Util;

namespace WiimoteApi {
	public class GuitarData : WiimoteData
	{
		/// Guitar Analog Stick values.  This is a size 2 Array [X, Y] of
		/// RAW (unprocessed) stick data.  Generally the analog stick returns
		/// values in the range 4-61, the center being at 32.
		public ReadOnlyArray<byte> stick { get { return _stick_readonly; } }
		private ReadOnlyArray<byte> _stick_readonly;
		private byte[] _stick;

		/// Button: Green
		public bool green { get { return _green; } }
		private bool _green;
		/// Button: Red
		public bool red { get { return _red; } }
		private bool _red;
		/// Button: Yellow
		public bool yellow { get { return _yellow; } }
		private bool _yellow;
		/// Button: Blue
		public bool blue { get { return _blue; } }
		private bool _blue;
		/// Button: Orange
		public bool orange { get { return _orange; } }
		private bool _orange;

		/// Button: Plus
		public bool plus { get { return _plus; } }
		private bool _plus;
		/// Button: Minus
		public bool minus { get { return _minus; } }
		private bool _minus;

		/// Strum Up
		public bool strum_up { get { return _strum_up; } }
		private bool _strum_up;
		/// Strum Down
		public bool strum_down { get { return _strum_down; } }
		private bool _strum_down;
		public bool strum {get{ return _strum_down || _strum_up; }}

		/// Whammy Bar, typically rests somewhere between 14-16
		/// and maxes out at 26.
		public byte whammy { get { return _whammy; } }
		private byte _whammy;


		public GuitarData(Wiimote Owner)
			: base(Owner)
		{

			_stick = new byte[2];
			_stick_readonly = new ReadOnlyArray<byte>(_stick);
		}

		public override bool InterpretData(byte[] data) {
			if(data == null || data.Length < 6) {
				_stick[0] = 128; _stick[1] = 128;
				_whammy = 0x0F;
				_green = _red = _yellow = _blue = _orange = false;
				_strum_up = _strum_down = _minus = _plus = false;
				return false;
			}

			_stick[0] = (byte)(data[0] & 0x3F); // because the first 2 bits differ by model
			_stick[1] = (byte)(data[1] & 0x3F); // because the first 2 bits differ by model

			_whammy = (byte)(data[3] & 0x1F); // because the first 3 bits differ by model

			_green = (data[5] & 0x10) != 0x10;
			_red = (data[5] & 0x40) != 0x40;
			_yellow = (data[5] & 0x08) != 0x08;
			_blue = (data[5] & 0x20) != 0x20;
			_orange = (data[5] & 0x80) != 0x80;

			_minus = (data[4] & 0x10) != 0x10;
			_plus = (data[4] & 0x04) != 0x04;

			_strum_up = (data[5] & 0x01) != 0x01;
			_strum_down = (data[4] & 0x40) != 0x40;

			return true;
		}

		/// Returns a size 2 [X, Y] array of the analog stick's position, in the range
		/// (-1, 1)
		public float[] GetStick01() {
			float[] ret = new float[2];
			for(int x=0;x<2;x++) {
				ret[x] = (_stick[x] - 32) / 32f;
			}
			return ret;
		}

		/// Returns a the whammy bar's value in the range (0, 1), where 0 is resting 
		/// position and 1 is fully depressed
		public float GetWhammy01() {
			float ret = (_whammy - 16) / 10f;
			return ret < 0 ? 0 : ret > 1 ? 1 : ret;
		}
	}
}
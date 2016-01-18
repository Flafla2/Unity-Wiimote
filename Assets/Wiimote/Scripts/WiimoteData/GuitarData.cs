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

		/// Guitar frets where element 0 is green and element 4 is orange
		public ReadOnlyArray<bool> frets { get { return _frets_readonly; } }
		private ReadOnlyArray<bool> _frets_readonly;
		private bool[] _frets;

		/// Guitar slider (supported in some models) - the strip below the
		/// regular frets on Guitar Hero IV+ controllers. Analog data in
		/// range (4, 31), snaps to 15 when untouched.
		public byte slider { get { return _slider; } }
		private byte _slider;

		/// True if the model has a slider
		public bool has_slider { get { return _has_slider; } }
		private bool _has_slider;

		/// Get active frets, ignoring slider value
		public bool green_fret { get { return _frets[0]; } }
		public bool red_fret { get { return _frets[1]; } }
		public bool yellow_fret { get { return _frets[2]; } }
		public bool blue_fret { get { return _frets[3]; } }
		public bool orange_fret { get { return _frets[4]; } }

		/// Get the slider value as a fret
		public bool green_slider { get { return _slider < 0x08; } }
		public bool red_slider { get { return _slider > 0x06 && _slider < 0x0E; } }
		public bool yellow_slider { get { return _slider > 0x0B && _slider < 0x16 && _slider != 0x0F; } }
		public bool blue_slider { get { return _slider > 0x13 && _slider < 0x1B; } }
		public bool orange_slider { get { return _slider > 0x19 && _slider < 0x20; } }

		/// Get active fret, whether player is using frets or slider
		public bool green { get { return _frets[0] || green_slider; } }
		public bool red { get { return _frets[1] || red_slider; } }
		public bool yellow { get { return _frets[2] || yellow_slider; } }
		public bool blue { get { return _frets[3] || blue_slider; } }
		public bool orange { get { return _frets[4] || orange_slider; } }

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
			_frets = new bool[5];
			_frets_readonly = new ReadOnlyArray<bool> (_frets);
		}

		public override bool InterpretData(byte[] data) {
			if(data == null || data.Length < 6) {
				_stick[0] = 32; _stick[1] = 32;
				_whammy = 0x10;
				for (int i = 0; i < _frets.Length; i++) {
					_frets [i] = false;
				}
				_slider = 0x0F;
				_strum_up = _strum_down = _minus = _plus = false;
				return false;
			}

			_stick[0] = (byte)(data[0] & 0x3F); // because the first 2 bits differ by model
			_stick[1] = (byte)(data[1] & 0x3F); // because the first 2 bits differ by model

			_whammy = (byte)(data[3] & 0x1F); // because the first 3 bits differ by model

			_frets[0] = (data[5] & 0x10) != 0x10;
			_frets[1] = (data[5] & 0x40) != 0x40;
			_frets[2] = (data[5] & 0x08) != 0x08;
			_frets[3] = (data[5] & 0x20) != 0x20;
			_frets[4] = (data[5] & 0x80) != 0x80;

			_has_slider = data [2] != 0xFF;
			_slider = data [2];

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

		/// Returns a the slider's value in the range (0, 1), where 0 is green and
		/// 1 is orange. If the slider is not supported or not actively being used,
		/// returns -1.
		public float GetSlider01() {
			if (!_has_slider || _slider == 0x0F) {
				return -1f;
			}
			return (_slider-4) / 27f;
		}
	}
}
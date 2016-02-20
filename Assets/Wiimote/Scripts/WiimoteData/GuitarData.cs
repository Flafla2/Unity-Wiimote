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

		/// Get active green fret, ignoring slider value
		public bool green_fret { get { return _frets[0]; } }
		/// Get active red fret, ignoring slider value
		public bool red_fret { get { return _frets[1]; } }
		/// Get active yellow fret, ignoring slider value
		public bool yellow_fret { get { return _frets[2]; } }
		/// Get active blue fret, ignoring slider value
		public bool blue_fret { get { return _frets[3]; } }
		/// Get active orange fret, ignoring slider value
		public bool orange_fret { get { return _frets[4]; } }

		/// True if player's finger is touching green segment of slider
		public bool green_slider { get { return _has_slider && _slider > 0 && _slider < 0x08; } }
		/// True if player's finger is touching red segment of slider
		public bool red_slider { get { return _has_slider && _slider > 0x06 && _slider < 0x0E; } }
		/// True if player's finger is touching yellow segment of slider
		public bool yellow_slider { get { return _has_slider && _slider > 0x0B && _slider < 0x16 && _slider != 0x0F; } }
		/// True if player's finger is touching blue segment of slider
		public bool blue_slider { get { return _has_slider && _slider > 0x13 && _slider < 0x1B; } }
		/// True if player's finger is touching orange segment of slider
		public bool orange_slider { get { return _has_slider && _slider > 0x19 && _slider < 0x20; } }

		/// True if player is touching EITHER green fret or green slider (if supported)
		public bool green { get { return _frets[0] || green_slider; } }
		/// True if player is touching EITHER red fret or red slider (if supported)
		public bool red { get { return _frets[1] || red_slider; } }
		/// True if player is touching EITHER yellow fret or yellow slider (if supported)
		public bool yellow { get { return _frets[2] || yellow_slider; } }
		/// True if player is touching EITHER blue fret or blue slider (if supported)
		public bool blue { get { return _frets[3] || blue_slider; } }
		/// True if player is touching EITHER orange fret or orange slider (if supported)
		public bool orange { get { return _frets[4] || orange_slider; } }

		/// Button: Plus (start/pause)
		public bool plus { get { return _plus; } }
		private bool _plus;
		/// Button: Minus (star power)
		public bool minus { get { return _minus; } }
		private bool _minus;

		/// Strum Up
		public bool strum_up { get { return _strum_up; } }
		private bool _strum_up;
		/// Strum Down
		public bool strum_down { get { return _strum_down; } }
		private bool _strum_down;
		/// Strum Up OR Down
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

			_stick[0] = (byte)(data[0] & 0x3F); // because the last 2 bits differ by model
			_stick[1] = (byte)(data[1] & 0x3F); // because the last 2 bits differ by model

			_whammy = (byte)(data[3] & 0x1F); // only first 5 bits used

			_frets[0] = (data[5] & 0x10) == 0;
			_frets[1] = (data[5] & 0x40) == 0;
			_frets[2] = (data[5] & 0x08) == 0;
			_frets[3] = (data[5] & 0x20) == 0;
			_frets[4] = (data[5] & 0x80) == 0;

			_has_slider = data [2] != 0xFF;
			_slider = (byte)(data [2] & 0x1F); // only first 5 bits used

			_minus = (data[4] & 0x10) == 0;
			_plus = (data[4] & 0x04) == 0;

			_strum_up = (data[5] & 0x01) == 0;
			_strum_down = (data[4] & 0x40) == 0;

			return true;
		}

		/// Returns a size 2 [X, Y] array of the analog stick's position, in the range
		/// (0, 1). The stick typically rests somewhere NEAR [0.5, 0.5], and the actual
		/// range ends up being somewhere in the neighborhood of (0.07, 0.93).
		public float[] GetStick01() {
			float[] ret = new float[2];
			for(int x=0;x<2;x++) {
				ret[x] = _stick[x] / 63f;
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
			if (!_has_slider || _slider == 0x0F || _slider == 0) {
				return -1f;
			}
			return (_slider-4) / 27f;
		}
	}
}

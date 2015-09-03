namespace WiimoteApi {
    public class MotionPlusData : WiimoteData
    {
        /// The rotational velocity in the Pitch direction of the Wii Remote, as
        /// reported by the Wii Motion Plus.  Measured in degrees per second.
        ///
        /// \note The Wii Remote sends updates at a frequency of 95Hz.  So, one way
        ///       of finding the change in degrees over the previous report is to divide
        ///       this value by 95.
        public float PitchSpeed { get { return _PitchSpeed; } }
        private float _PitchSpeed = 0;

        private int _PitchSpeedRaw = 0;

        /// The rotational velocity in the Yaw direction of the Wii Remote, as
        /// reported by the Wii Motion Plus.  Measured in degrees per second.
        ///
        /// \note The Wii Remote sends updates at a frequency of 95Hz.  So, one way
        ///       of finding the change in degrees over the previous report is to divide
        ///       this value by 95.
        public float YawSpeed { get { return _YawSpeed; } }
        private float _YawSpeed = 0;

        private int _YawSpeedRaw = 0;

        /// The rotational velocity in the Roll direction of the Wii Remote, as
        /// reported by the Wii Motion Plus.  Measured in degrees per second.
        ///
        /// \note The Wii Remote sends updates at a frequency of 95Hz.  So, one way
        ///       of finding the change in degrees over the previous report is to divide
        ///       this value by 95.
        public float RollSpeed { get { return _RollSpeed; } }
        private float _RollSpeed;

        private int _RollSpeedRaw = 0;

        /// If true, the Wii Motion Plus reports that it is in "slow" mode in the
        /// Pitch direction.  This means that it is more precise as it doesn't have
        /// to report higher values.  If false often, it is more likely that the Wii Motion
        /// Plus will "fall out of sync" with the real world.
        public bool PitchSlow { get { return _PitchSlow; } }
        private bool _PitchSlow = false;

        /// If true, the Wii Motion Plus reports that it is in "slow" mode in the
        /// Yaw direction.  This means that it is more precise as it doesn't have
        /// to report higher values.  If false often, it is more likely that the Wii Motion
        /// Plus will "fall out of sync" with the real world.
        public bool YawSlow { get { return _YawSlow; } }
        private bool _YawSlow = false;

        /// If true, the Wii Motion Plus reports that it is in "slow" mode in the
        /// Roll direction.  This means that it is more precise as it doesn't have
        /// to report higher values.  If false often, it is more likely that the Wii Motion
        /// Plus will "fall out of sync" with the real world.
        public bool RollSlow { get { return _RollSlow; } }
        private bool _RollSlow = false;

        /// If true, the Wii Motion Plus reports that an extension is connected in its
        /// extension port.
        public bool ExtensionConnected { get { return _ExtensionConnected; } }
        private bool _ExtensionConnected = false;

        private int _PitchZero = 8063;
        private int _YawZero = 8063;
        private int _RollZero = 8063;

        // I would like to say that this was calculated or something, but honestly this was created
        // simply through trial and error.  I am going to tweak this constant to see if I can get it
        // any better in the future.  Realistically this value is the result of the Analog/Digital converter
        // in the Wii Motion Plus along with the analog output of the gyros, but the documentation is so
        // shitty that I don't even care anymore.
        private const float MagicCalibrationConstant = 0.05f;

        public MotionPlusData(Wiimote Owner) : base(Owner) { }

        public override bool InterpretData(byte[] data)
        {
            if (data == null || data.Length < 6)
                return false;

            _YawSpeedRaw    = data[0];
            _YawSpeedRaw   |= (data[3] & 0xfc) << 6;
            _RollSpeedRaw   = data[1];
            _RollSpeedRaw  |= (data[4] & 0xfc) << 6;
            _PitchSpeedRaw  = data[2];
            _PitchSpeedRaw |= (data[5] & 0xfc) << 6;

            _YawSlow = (data[3] & 0x02) == 0x02;
            _PitchSlow = (data[3] & 0x01) == 0x01;
            _RollSlow = (data[4] & 0x02) == 0x02;
            _ExtensionConnected = (data[4] & 0x01) == 0x01;

            _PitchSpeed = (float)(_PitchSpeedRaw - _PitchZero) * MagicCalibrationConstant;
            _YawSpeed = (float)(_YawSpeedRaw - _YawZero) * MagicCalibrationConstant;
            _RollSpeed = (float)(_RollSpeedRaw - _RollZero) * MagicCalibrationConstant;

            // At high speeds, the Wii Remote Reports with less precision to reach higher values.
            // The multiplier is 2000 / 440 when in fast mode.
            // http://wiibrew.org/wiki/Wiimote/Extension_Controllers/Wii_Motion_Plus
            if (!PitchSlow)
                _PitchSpeed *= 2000f / 440f;
            if (!YawSlow)
                _YawSpeed *= 2000f / 440f;
            if (!RollSlow)
                _RollSpeed *= 2000f / 440f;

            return true;
        }

        /// Calibrates the zero values of the Wii Motion Plus in the Pitch, Yaw, and Roll directions.
        /// The Wii Remote should be in a flat, motionless position when calibrating (for example, face
        /// down on a flat surface).
        ///
        /// A good idea here is to reference the Accelerometer values of the Wii Remote to make sure that
        /// your simulated rotation is consistent with the actual rotation of the remote.
        public void SetZeroValues()
        {
            _PitchZero = _PitchSpeedRaw;
            _YawZero = _YawSpeedRaw;
            _RollZero = _RollSpeedRaw;

            _PitchSpeedRaw = 0;
            _YawSpeedRaw = 0;
            _RollSpeedRaw = 0;
            _PitchSpeed = 0;
            _YawSpeed = 0;
            _RollSpeed = 0;
        }
    }
}
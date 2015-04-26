namespace WiimoteApi {
/// \brief A type of data storage register that can be read from / written to.
/// \sa ::Wiimote::SendRegisterWriteRequest(RegisterType, int, byte[]), ::Wiimote::SendRegisterReadRequest(RegisterType, int, int, ReadResponder)
public enum RegisterType
{
    /// The Wiimote's 16kB generic EEPROM memory module.  This is used to store calubration data
    /// as well as Mii block data from the Mii channel.
    EEPROM = 0x00,
    /// The Wiimote's control registers, used for managing the Wiimote's peripherals (such as extension
    /// controllers, the speakers, and the IR camera).
    CONTROL = 0x04
}

/// A so-called output data type represents all data that can be sent from the host to the wiimote.
/// This information is used by the remote to change its internal read/write remote.
public enum OutputDataType
{
    LED = 0x11,
    DATA_REPORT_MODE = 0x12,
    IR_CAMERA_ENABLE = 0x13,
    SPEAKER_ENABLE = 0x14,
    STATUS_INFO_REQUEST = 0x15,
    WRITE_MEMORY_REGISTERS = 0x16,
    READ_MEMORY_REGISTERS = 0x17,
    SPEAKER_DATA = 0x18,
    SPEAKER_MUTE = 0x19,
    IR_CAMERA_ENABLE_2 = 0x1A
}

/// \brief A so-called input data type represents all data that can be sent from the wiimote to the host.
///        This information is used by the host as basic controller data from the wiimote.
/// \note All REPORT_ types represent the actual data types that can be sent from the contoller.
public enum InputDataType
{
    STATUS_INFO = 0x20,
    READ_MEMORY_REGISTERS = 0x21,
    ACKNOWLEDGE_OUTPUT_REPORT = 0x22,
    REPORT_BUTTONS = 0x30,
    REPORT_BUTTONS_ACCEL = 0x31,
    REPORT_BUTTONS_EXT8 = 0x32,
    REPORT_BUTTONS_ACCEL_IR12 = 0x33,
    REPORT_BUTTONS_EXT19 = 0x34,
    REPORT_BUTTONS_ACCEL_EXT16 = 0x35,
    REPORT_BUTTONS_IR10_EXT9 = 0x36,
    REPORT_BUTTONS_ACCEL_IR10_EXT6 = 0x37,
    REPORT_EXT21 = 0x3d,
    REPORT_INTERLEAVED = 0x3e,
    REPORT_INTERLEAVED_ALT = 0x3f
}

/// These are the 3 types of IR data accepted by the Wiimote.  They offer more
/// or less IR data in exchange for space for other data (such as extension
/// controllers or accelerometer data).
///
/// For each IR data type you can only use certain ::InputDataType reports in
/// order to recieve the data.
public enum IRDataType
{
    /// \brief 10 bytes of data.  Contains position data for each dot only.
    /// 
    /// Works with reports ::InputDataType::REPORT_BUTTONS_IR10_EXT9 and ::InputDataType::REPORT_BUTTONS_ACCEL_IR10_EXT6.
    BASIC = 1,
    /// \brief 12 bytes of data.  Contains position and size data for each dot.
    /// 
    /// Works with report ::InputDataType::REPORT_BUTTONS_ACCEL_IR12 only.
    EXTENDED = 3,
    /// \brief 36 bytes of data.  Contains position, size, bounding box, and intensity data for each dot.
    ///
    /// Works with interleaved report ::InputDataType::REPORT_INTERLEAVED / ::InputDataType::REPORT_INTERLEAVED_ALT only.
    FULL = 5
}

public enum ExtensionController
{
    NONE, NUNCHUCK, CLASSIC, CLASSIC_PRO, MOTIONPLUS, MOTIONPLUS_NUNCHUCK, MOTIONPLUS_CLASSIC
}

public enum AccelCalibrationStep {
    A_BUTTON_UP = 0,
    EXPANSION_UP = 1,
    LEFT_SIDE_UP = 2
}

} // namespace WiimoteApi
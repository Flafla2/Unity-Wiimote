namespace WiimoteApi {
/// \brief A type of data storage register that can be read from / written to.
/// \sa Wiimote::SendRegisterWriteRequest(RegisterType, int, byte[]), Wiimote::SendRegisterReadRequest(RegisterType, int, int, ReadResponder)
public enum RegisterType
{
    /// The Wii Remote's 16kB generic EEPROM memory module.  This is used to store calubration data
    /// as well as Mii block data from the Mii channel.
    EEPROM = 0x00,
    /// The Wii Remote's control registers, used for managing the Wii Remote's peripherals (such as extension
    /// controllers, the speakers, and the IR camera).
    CONTROL = 0x04
}

/// A so-called output data type represents all data that can be sent from the host to the Wii Remote.
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

/// \brief A so-called input data type represents all data that can be sent from the Wii Remote to the host.
///        This information is used by the host as basic controller data from the Wii Remote.
/// \note All REPORT_ types represent the actual data types that can be sent from the contoller.
public enum InputDataType
{
    STATUS_INFO = 0x20,
    READ_MEMORY_REGISTERS = 0x21,
    ACKNOWLEDGE_OUTPUT_REPORT = 0x22,
    /// Data Report Mode: Buttons
    REPORT_BUTTONS = 0x30,
    /// Data Report Mode: Buttons, Accelerometer
    REPORT_BUTTONS_ACCEL = 0x31,
    /// Data Report Mode: Buttons, 8 Extension bytes
    REPORT_BUTTONS_EXT8 = 0x32,
    /// Data Report Mode: Buttons, Accelerometer, 12 IR bytes (IRDataType::EXTENDED)
    REPORT_BUTTONS_ACCEL_IR12 = 0x33,
    /// Data Report Mode: Buttons, 19 Extension Bytes
    REPORT_BUTTONS_EXT19 = 0x34,
    /// Data Report Mode: Buttons, Acceleromter, 16 Extension Bytes
    REPORT_BUTTONS_ACCEL_EXT16 = 0x35,
    /// Data Report Mode: Buttons, 10 IR Bytes (IRDataType::BASIC), 9 Extension Bytes
    REPORT_BUTTONS_IR10_EXT9 = 0x36,
    /// Data Report Mode: Buttons, Accelerometer, 10 IR Bytes (IRDataType::BASIC), 6 Extension Bytes
    REPORT_BUTTONS_ACCEL_IR10_EXT6 = 0x37,
    /// Data Report Mode: 21 Extension Bytes
    REPORT_EXT21 = 0x3d,
    /// Data Report Mode: (Interleaved) Buttons, Accelerometer, 36 IR Bytes (IRDataType::FULL)
    REPORT_INTERLEAVED = 0x3e,
    /// Data Report Mode: (Interleaved) Buttons, Accelerometer, 36 IR Bytes (IRDataType::FULL) Alternate
    /// 
    /// \note This is functionally identical to REPORT_INTERLEAVED.
    REPORT_INTERLEAVED_ALT = 0x3f
}

/// These are the 3 types of IR data accepted by the Wii Remote.  They offer more
/// or less IR data in exchange for space for other data (such as extension
/// controllers or accelerometer data).
///
/// For each IR data type you can only use certain InputDataType reports in
/// order to recieve the data.
public enum IRDataType
{
    /// \brief 10 bytes of data.  Contains position data for each dot only.
    /// 
    /// Works with reports InputDataType::REPORT_BUTTONS_IR10_EXT9 and InputDataType::REPORT_BUTTONS_ACCEL_IR10_EXT6.
    BASIC = 1,
    /// \brief 12 bytes of data.  Contains position and size data for each dot.
    /// 
    /// Works with report InputDataType::REPORT_BUTTONS_ACCEL_IR12 only.
    EXTENDED = 3,
    /// \brief 36 bytes of data.  Contains position, size, bounding box, and intensity data for each dot.
    ///
    /// Works with interleaved report InputDataType::REPORT_INTERLEAVED / InputDataType::REPORT_INTERLEAVED_ALT only.
    FULL = 5
}

public enum ExtensionController
{
    /// No Extension Controller is connected.
    NONE, 
    /// A Nunchuck Controller
    NUNCHUCK, 
    /// A Classic Controller
    CLASSIC, 
    /// A Classic Controller Pro.
    CLASSIC_PRO,
    /// A Wii U Pro Controller.  Although a Wii U Pro Controller is not technically an extension controller it is treated
    /// like one when communicating to a bluetooth host.
    WIIU_PRO, 
    /// An activated Wii Motion Plus with no extension controllers in passthrough mode.
    MOTIONPLUS,
    /// An activated Wii Motion Plus with a Nunchuck in passthrough mode.
    /// \warning Nunchuck passthrough is currently not supported.
    MOTIONPLUS_NUNCHUCK, 
    /// An activated Wii Motion Plus with a Classic Controller in passthrough mode. 
    /// \warning Classic Controller passthrough is currently not supported
    MOTIONPLUS_CLASSIC
}

public enum AccelCalibrationStep {
    A_BUTTON_UP = 0,
    EXPANSION_UP = 1,
    LEFT_SIDE_UP = 2
}

/// These different Wii Remote Types are used to differentiate between different devices that behave like the Wii Remote.
public enum WiimoteType {
    /// The original Wii Remote (Name: RVL-CNT-01).  This includes all Wii Remotes manufactured for the original Wii.
    WIIMOTE, 
    /// The new Wii Remote Plus (Name: RVL-CNT-01-TR).  Wii Remote Pluses are now standard with Wii U consoles and come
    /// with a built-in Wii Motion Plus extension.
    WIIMOTEPLUS, 
    /// The Wii U Pro Controller (Name: RVL-CNT-01-UC) behaves identically to a Wii Remote with a Classic Controller
    /// attached.  Obviously the Pro Controller does not support IR so those features will not work.
    PROCONTROLLER
}

} // namespace WiimoteApi
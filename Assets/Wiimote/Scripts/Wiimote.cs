using UnityEngine;
using System;
using WiimoteApi.Internal;
using WiimoteApi.Util;

namespace WiimoteApi {

    public delegate void ReadResponder(byte[] data);

public class Wiimote
{
    /// Represents whether or not to turn on rumble when sending reports to
    /// the Wii Remote.  This will only be applied when a data report is sent.
    /// That is, simply setting this flag will not instantly enable rumble.
    public bool RumbleOn = false;

    /// Accelerometer data component
    public AccelData     Accel      { get { return _Accel; } }
    private AccelData   _Accel;

    /// If a Nunchuck is currently connected to the Wii Remote's extension port,
    /// this contains all relevant Nunchuck controller data as it is reported by
    /// the Wiimote.  If no Nunchuck is connected, this is \c null.
    ///
    /// \sa current_ext
    public NunchuckData Nunchuck {
        get {
            if(current_ext == ExtensionController.NUNCHUCK)
                return (NunchuckData)_Extension;
            return null;
        }
    }

    /// If a Classic Controller is currently connected to the Wii Remote's extension port,
    /// this contains all relevant Classic Controller data as it is reported by
    /// the Wiimote.  If no Classic Controller is connected, this is \c null.
    ///
    /// \sa current_ext
    public ClassicControllerData ClassicController {
        get {
            if(current_ext == ExtensionController.CLASSIC)
                return (ClassicControllerData)_Extension;
            return null;
        }
    }

    /// If a Wii Motion Plus is currently connected to the Wii Remote's extension port,
    /// and has been activated by ActivateWiiMotionPlus(), this contains all relevant 
    /// Wii Motion Plus controller data as it is reported by the Wiimote.  If no
    /// WMP is connected, this is \c null.
    ///
    /// \sa current_ext, wmp_attached, ActivateWiiMotionPlus()
    public MotionPlusData MotionPlus {
        get {
            if(current_ext == ExtensionController.MOTIONPLUS)
                return (MotionPlusData)_Extension;
            return null;
        }
    }

    /// If this Wiimote is a Wii U Pro Controller,
    /// this contains all relevant Pro Controller data as it is reported by
    /// the Controller.  If this Wiimote is not a Wii U Pro Controller, this is \c null.
    ///
    /// \sa current_ext
    public WiiUProData WiiUPro {
        get {
            if(current_ext == ExtensionController.WIIU_PRO)
                return (WiiUProData)_Extension;
            return null;
        }
    }

    private WiimoteData _Extension;

    /// Button data component.
    public ButtonData    Button     { get { return _Button; } }
    private ButtonData  _Button;
    /// IR data component.
    public IRData        Ir         { get { return _Ir; } }
    private IRData      _Ir;
    /// Status info data component.
    public StatusData    Status     { get { return _Status; } }
    private StatusData  _Status;

    /// A pointer representing HIDApi's low-level device handle to this
    /// Wii Remote.  Use this when interfacing directly with HIDApi.
    public IntPtr hidapi_handle { get { return _hidapi_handle; } }
    private IntPtr _hidapi_handle = IntPtr.Zero;

    /// The RAW (unprocessesed) extension data reported by the Wii Remote.  This could
    /// be used for debugging new / undocumented extension controllers.
    public ReadOnlyArray<byte> RawExtension { get { return _RawExtension; } }
    private ReadOnlyArray<byte> _RawExtension = null;

    /// The low-level bluetooth HID path of this Wii Remote.  Use this
    /// when interfacing directly with HIDApi.
    public string hidapi_path { get { return _hidapi_path; } }
    private string _hidapi_path;

    public WiimoteType Type { get { return _Type; } }
    private WiimoteType _Type;

    private RegisterReadData CurrentReadData = null;

    private InputDataType last_report_type = InputDataType.REPORT_BUTTONS;
    private bool expecting_status_report = false;

    /// True if a Wii Motion Plus is attached to the Wii Remote, and it
    /// has NOT BEEN ACTIVATED.  When the WMP is activated this value is
    /// false.  This is only updated when WMP state is requested from
    /// Wii Remote registers (see: RequestIdentifyWiiMotionPlus())
    public bool wmp_attached { get { return _wmp_attached; } }
    private bool _wmp_attached = false;

    /// The current extension connected to the Wii Remote.  This is only updated
    /// when the Wii Remote reports an extension change (this should update
    /// automatically).
    public ExtensionController current_ext { get { return _current_ext; } }
    private ExtensionController _current_ext = ExtensionController.NONE;


    private byte[] InterleavedDataBuffer = new byte[18];
    private bool ExpectingSecondInterleavedPacket = false;

    private bool ExpectingWiiMotionPlusSwitch = false;

    public Wiimote(IntPtr hidapi_handle, string hidapi_path, WiimoteType Type)
    {
        _hidapi_handle  = hidapi_handle;
        _hidapi_path    = hidapi_path;
        _Type    = Type;

        _Accel  = new AccelData(this);
        _Button = new ButtonData(this);
        _Ir     = new IRData(this);
        _Status = new StatusData(this);
        _Extension = null;

        //RequestIdentifyWiiMotionPlus(); // why not?
    }

    private static byte[] ID_InactiveMotionPlus  = new byte[] {0x00, 0x00, 0xA6, 0x20, 0x00, 0x05};

    private void RespondIdentifyWiiMotionPlus(byte[] data)
    {
        if (data.Length != ID_InactiveMotionPlus.Length)
        {
            _wmp_attached = false;
            return;
        }

        if (data[0] == 0x01)                    // This is a weird inconsistency with some Wii Remote Pluses.  They don't have the -TR suffix
            _Type = WiimoteType.WIIMOTEPLUS;    // or a different PID as an identifier.  Instead they have a different WMP extension identifier.
                                                // It occurs on some of the oldest Wii Remote Pluses available (pre-2012).

        for (int x = 0; x < data.Length; x++)
        {
            // [x != 4] is necessary because byte 5 of the identifier changes based on the state of the remote
            // It is 0x00 on startup, 0x04 when deactivated, 0x05 when deactivated nunchuck passthrough,
            // and 0x07 when deactivated classic passthrough
            //
            // [x != 0] is necessary due to the inconsistency noted above.
            if (x != 4 && x != 0 && data[x] != ID_InactiveMotionPlus[x])
            {
                _wmp_attached = false;
                return;
            }
        }
        _wmp_attached = true;
    }

    private const long ID_ActiveMotionPlus          = 0x0000A4200405;
    private const long ID_ActiveMotionPlus_Nunchuck = 0x0000A4200505;
    private const long ID_ActiveMotionPlus_Classic  = 0x0000A4200705;
    private const long ID_Nunchuck                  = 0x0000A4200000;
    private const long ID_Classic                   = 0x0000A4200101;
    private const long ID_ClassicPro                = 0x0100A4200101;
    private const long ID_WiiUPro                   = 0x0000A4200120;


    private void RespondIdentifyExtension(byte[] data)
    {
        if (data.Length != 6)
            return;

        byte[] resized = new byte[8];
        for (int x = 0; x < 6; x++) resized[x] = data[5-x];
        long val = BitConverter.ToInt64(resized, 0);

        // Disregard bytes 0 and 5 - see RespondIdentifyWiiMotionPlus()
        if ((val | 0xff000000ff00) == (ID_ActiveMotionPlus | 0xff000000ff00))
        {
            _current_ext = ExtensionController.MOTIONPLUS;
            if(_Extension == null || _Extension.GetType() != typeof(MotionPlusData))
                _Extension = new MotionPlusData(this);
        }
        else if (val == ID_ActiveMotionPlus_Nunchuck)
        {
            _current_ext = ExtensionController.MOTIONPLUS_NUNCHUCK;
            _Extension = null;
        }
        else if (val == ID_ActiveMotionPlus_Classic)
        {
            _current_ext = ExtensionController.MOTIONPLUS_CLASSIC;
            _Extension = null;
        }
        else if (val == ID_ClassicPro)
        {
            _current_ext = ExtensionController.CLASSIC_PRO;
            _Extension = null;
        }
        else if (val == ID_Nunchuck)
        {
            _current_ext = ExtensionController.NUNCHUCK;
            if (_Extension == null || _Extension.GetType() != typeof(NunchuckData))
                _Extension = new NunchuckData(this);
        }
        else if (val == ID_Classic)
        {
            _current_ext = ExtensionController.CLASSIC;
            if (_Extension == null || _Extension.GetType() != typeof(ClassicControllerData))
                _Extension = new ClassicControllerData(this);
        }
        else if (val == ID_WiiUPro)
        {
            _current_ext = ExtensionController.WIIU_PRO;
            _Type = WiimoteType.PROCONTROLLER;
            if (_Extension == null || _Extension.GetType() != typeof(WiiUProData))
                _Extension = new WiiUProData(this);
        }
        else
        {
            _current_ext = ExtensionController.NONE;
            _Extension = null;
        }
    }

    #region Setups

    /// \brief Performs a series of coperations to initialize the IR camera.
    /// \param type The IR Report type you want to use.
    /// \return If all IR setup commands were successfully sent to the Wii Remote.
    /// 
    /// This performs the following steps in order to set up the IR camera:
    /// 1. Enable IR Camera (Send \c 0x04 to Output Report \c 0x13)
    /// 2. Enable IR Camera 2 (Send \c 0x04 to Output Report \c 0x1a)
    /// 3. Write 0x08 to register \c 0xb00030
    /// 4. Write Sensitivity Block 1 to registers at \c 0xb00000
    /// 5. Write Sensitivity Block 2 to registers at \c 0xb0001a
    /// 6. Write Mode Number to register \c 0xb00033
    /// 7. Write 0x08 to register \c 0xb00030 (again)
    /// 8. Update the Wii Remote's data reporting mode based on \c type
    public bool SetupIRCamera(IRDataType type = IRDataType.EXTENDED)
    {
        int res;
        // 1. Enable IR Camera (Send 0x04 to Output Report 0x13)
        // 2. Enable IR Camera 2 (Send 0x04 to Output Report 0x1a)
        res = SendIRCameraEnable(true);
        if (res < 0) return false;
        // 3. Write 0x08 to register 0xb00030
        res = SendRegisterWriteRequest(RegisterType.CONTROL, 0xb00030, new byte[] { 0x08 });
        if (res < 0) return false;
        // 4. Write Sensitivity Block 1 to registers at 0xb00000
        // Wii sensitivity level 3:
        // 02 00 00 71 01 00 aa 00 64
        // High Sensitivity:
        // 00 00 00 00 00 00 90 00 41
        res = SendRegisterWriteRequest(RegisterType.CONTROL, 0xb00000,
            new byte[] { 0x02, 0x00, 0x00, 0x71, 0x01, 0x00, 0xaa, 0x00, 0x64 });
        //new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x90, 0x00, 0x41});
        if (res < 0) return false;
        // 5. Write Sensitivity Block 2 to registers at 0xb0001a
        // Wii sensitivity level 3: 
        // 63 03
        // High Sensitivity:
        // 40 00
        res = SendRegisterWriteRequest(RegisterType.CONTROL, 0xb0001a, new byte[] { 0x63, 0x03 });
        if (res < 0) return false;
        // 6. Write Mode Number to register 0xb00033
        res = SendRegisterWriteRequest(RegisterType.CONTROL, 0xb00033, new byte[] { (byte)type });
        if (res < 0) return false;
        // 7. Write 0x08 to register 0xb00030 (again)
        res = SendRegisterWriteRequest(RegisterType.CONTROL, 0xb00030, new byte[] { 0x08 });
        if (res < 0) return false;

        switch (type)
        {
            case IRDataType.BASIC:
                res = SendDataReportMode(InputDataType.REPORT_BUTTONS_ACCEL_IR10_EXT6);
                break;
            case IRDataType.EXTENDED:
                res = SendDataReportMode(InputDataType.REPORT_BUTTONS_ACCEL_IR12);
                break;
            case IRDataType.FULL:
                res = SendDataReportMode(InputDataType.REPORT_INTERLEAVED);
                break;
        }

        if (res < 0) return false;
        return true;
    }

    /// \brief Attempts to identify whether or not a Wii Motion Plus is connected, but NOT activated.
    /// \sa RequestIdentifyExtension(), ActivateWiiMotionPlus()
    /// \return If the Identification request was successfully sent to the Wii Remote.
    ///
    /// When the Wii Remote reports back if a Wii Motion Plus is connected, wmp_attached will be updated.
    /// \note If the Wii Motion Plus is activated (using ActivateWiiMotionPlus()) the Wii Remote will report false
    public bool RequestIdentifyWiiMotionPlus()
    {
        int res;
        res = SendRegisterReadRequest(RegisterType.CONTROL, 0xA600FA, 6, RespondIdentifyWiiMotionPlus);
        return res > 0;
    }

    /// \brief Attempts to identify what (if any) ACTIVE extension is currently connected to the Wii Remote.
    /// \sa RequestIdentifyWiiMotionPlus(), ActivateExtension()
    /// \return If the identification request was successfully sent to the Wii Remote.
    ///
    /// When the Wii Remote reports back what extension is connected, current_ext will be updated.
    /// \note If the Extension has not been activated yet (using ActivateExtension) the Wii Remote will report ExtensionController::NONE.
    private bool RequestIdentifyExtension()
    {
        int res = SendRegisterReadRequest(RegisterType.CONTROL, 0xA400FA, 6, RespondIdentifyExtension);
        return res > 0;
    }

    /// \brief Attempts to activate the Wii Motion Plus.
    /// \sa RequestIdentifyWiiMotionPlus(), wmp_attached
    /// \return If the activation request was successfully sent to the Wii Remote.
    ///
    /// When the Wii Remote reports that the Wii Motion Plus has been activated, current_ext will be updated to ExtensionController::MOTIONPLUS
    /// If there is no Wii Motion Plus connected, undefined behavior may occur on the Wii Remote.
    public bool ActivateWiiMotionPlus()
    {
        if (!wmp_attached)
            Debug.LogWarning("There is a request to activate the Wii Motion Plus even though it has not been confirmed to exist!  Trying anyway.");

        // Initialize the Wii Motion Plus by writing 0x55 to register 0xA600F0
        int res = SendRegisterWriteRequest(RegisterType.CONTROL, 0xA600F0, new byte[] { 0x55 });
        if (res < 0) return false;

        // Activate the Wii Motion Plus as the active extension by writing 0x04 to register 0xA600FE
        // This does 3 things:
        // 1. A status report (0x20) will be sent, which indicates that an extension has been
        //    plugged in - IF there is no extension plugged into the passthrough port.
        // 2. The standard extension identifier at 0xA400FA now reads 00 00 A4 20 04 05
        // 3. Extension reports now contain Wii Motion Plus data.
        res = SendRegisterWriteRequest(RegisterType.CONTROL, 0xA600FE, new byte[] { 0x04 });
        if (res < 0) return false;

        _current_ext = ExtensionController.MOTIONPLUS;
        if (_Extension == null || _Extension.GetType() != typeof(MotionPlusData))
            _Extension = new MotionPlusData(this);
        ExpectingWiiMotionPlusSwitch = true;

        return true;
    }

    public bool DeactivateWiiMotionPlus()
    {
        if (current_ext != ExtensionController.MOTIONPLUS && current_ext != ExtensionController.MOTIONPLUS_CLASSIC && current_ext != ExtensionController.MOTIONPLUS_NUNCHUCK)
            Debug.LogWarning("There is a request to deactivate the Wii Motion Plus even though it has not been activated!  Trying anyway.");
        int res = SendRegisterWriteRequest(RegisterType.CONTROL, 0xA400F0, new byte[] { 0x55 });
        return res > 0;
    }

    /// \brief Attempts to activate any connected extension controller
    /// \sa RequestIdentifyExtension(), StatusData::ext_connected
    /// \return If the activation request was successfully sent to the Wii Remote.
    ///
    /// If there is no extension connected, undefined behavior may occur on the Wii Remote.
    private bool ActivateExtension()
    {
        if (!Status.ext_connected)
            Debug.LogWarning("There is a request to activate an Extension controller even though it has not been confirmed to exist!  Trying anyway.");

        // 1. Initialize the Extension by writing 0x55 to register 0xA400F0
        int res = SendRegisterWriteRequest(RegisterType.CONTROL, 0xA400F0, new byte[] { 0x55 });
        if (res < 0) return false;

        // 2. Activate the Extension by writing 0x00 to register 0xA400FB
        res = SendRegisterWriteRequest(RegisterType.CONTROL, 0xA400FB, new byte[] { 0x00 });
        if (res < 0) return false;
        return true;
    }

    #endregion

    #region Write
    /// \brief Sends a generic block of data to the Wii Remote using the specified Output Report.
    /// \param type The output report type you would like to send
    /// \param data The raw data you would like to send using the specified \c type.
    /// \return On success, the total size of the data written, -1 if HIDApi reports an error, or < -1 if there is an invalid input.
    /// 
    /// This should only be used to send custom data to the Wii Remote that is currently unimplemented by WiimoteApi.
    /// In any average use case you can use any of the higher-level output functions provided by WiimoteApi.
    ///
    /// The Wii Remote rumble settings are also updated based on RumbleOn.
    public int SendWithType(OutputDataType type, byte[] data)
    {
        byte[] final = new byte[data.Length + 1];
        final[0] = (byte)type;

        for (int x = 0; x < data.Length; x++)
            final[x + 1] = data[x];

        if (RumbleOn)
            final[1] |= 0x01;

        int res = WiimoteManager.SendRaw(hidapi_handle, final);

        if (res < -1) Debug.LogError("Incorrect Input to HIDAPI.  No data has been sent.");


        return res;
    }

    /// \brief Updates the Player LEDs on the bottom of the Wii Remote
    /// \param led1,led2,led3,led4 If this LED should be turned on
    /// \return On success, the total size of the data written (> 0), <= 0 on failure.
    /// \sa SendWithType(OutputDataType, byte[])
    /// \note More than one LED can be on at a time, but this may confuse players.  Use this with caution.
    ///
    /// If you are willing to use up a lot of bluetooth bandwith, pulse-width modulation (PWM) is also possible
    /// to lower the intensity of the LEDs.
    public int SendPlayerLED(bool led1, bool led2, bool led3, bool led4)
    {
        byte mask = 0;
        if (led1) mask |= 0x10;
        if (led2) mask |= 0x20;
        if (led3) mask |= 0x40;
        if (led4) mask |= 0x80;

        return SendWithType(OutputDataType.LED, new byte[] { mask });
    }

    /// \brief Sets the Data Reporting mode of the Wii Remote.
    /// \param mode The data reporting mode desired.  This can be any InputDataType except for
    ///         InputDataType::STATUS_INFO, InputDataType::READ_MEMORY_REGISTERS, or InputDataType::ACKNOWLEDGE_OUTPUT_REPORT.
    ///         Said data types are not data reporting modes so it doesn't make sense to use them here.
    /// \return On success, the total size of the data written (> 0), <= 0 on failure.
    /// \sa SendWithType(OutputDataType, byte[])
    public int SendDataReportMode(InputDataType mode)
    {
        if (mode == InputDataType.STATUS_INFO || mode == InputDataType.READ_MEMORY_REGISTERS || mode == InputDataType.ACKNOWLEDGE_OUTPUT_REPORT)
        {
            Debug.LogError("Passed " + mode.ToString() + " to SendDataReportMode!");
            return -2;
        }

        last_report_type = mode;

        ExpectingSecondInterleavedPacket = false;

        return SendWithType(OutputDataType.DATA_REPORT_MODE, new byte[] { 0x00, (byte)mode });
    }

    private int SendIRCameraEnable(bool enabled)
    {
        byte[] mask = new byte[] { (byte)(enabled ? 0x04 : 0x00) };

        int first = SendWithType(OutputDataType.IR_CAMERA_ENABLE, mask);
        if (first < 0) return first;

        int second = SendWithType(OutputDataType.IR_CAMERA_ENABLE_2, mask);
        if (second < 0) return second;

        return first + second; // success
    }

    private int SendSpeakerEnabled(bool enabled)
    {
        byte[] mask = new byte[] { (byte)(enabled ? 0x04 : 0x00) };

        return SendWithType(OutputDataType.SPEAKER_ENABLE, mask);
    }

    private int SendSpeakerMuted(bool muted)
    {
        byte[] mask = new byte[] { (byte)(muted ? 0x04 : 0x00) };

        return SendWithType(OutputDataType.SPEAKER_MUTE, mask);
    }

    /// \brief Request a Wii Remote Status update.
    /// \return On success > 0, <= 0 on failure.
    /// \sa Status, StatusData
    ///
    /// This will update the data in Status when the Wii Remote reports back.
    public int SendStatusInfoRequest()
    {
        expecting_status_report = true;
        return SendWithType(OutputDataType.STATUS_INFO_REQUEST, new byte[] { 0x00 });
    }

    /// \brief Requests the Wii Remote to report data from its internal registers.
    /// \param type The type of register you would like to read from
    /// \param offset The starting offset of the block of data you would like to read
    /// \param size The size of the block of data you would like to read
    /// \param Responder This will be called when the Wii Remote finishes reporting the requested data.
    /// \return On success, > 0, <= 0 on failure.
    /// \sa SendRegisterWriteRequest(RegisterType, int, byte[])
    ///
    /// \warning Do not attempt to read from the registers when another read is pending (that is, data is being
    ///          recieved by the Wii Remote).  If you attempt to do this, the new read request will be ignored.
    /// 
    /// Reading from the Wii Remote's internal registers can give important data not available through normal output reports.
    /// This can, for example, be used to read saved Mii data from the Wii Remote's EEPROM registers.  It is also used by some
    /// of WiimoteApi's setup functions.
    /// 
    /// If you use this incorrectly (for example, if you attempt to read from an invalid block of data), \c Responder will not be called.
    public int SendRegisterReadRequest(RegisterType type, int offset, int size, ReadResponder Responder)
    {
        if (CurrentReadData != null)
        {
            Debug.LogWarning("Aborting read request; There is already a read request pending!");
            return -2;
        }


        CurrentReadData = new RegisterReadData(offset, size, Responder);

        byte address_select = (byte)type;
        byte[] offsetArr = IntToBigEndian(offset, 3);
        byte[] sizeArr = IntToBigEndian(size, 2);

        byte[] total = new byte[] { address_select, offsetArr[0], offsetArr[1], offsetArr[2], 
            sizeArr[0], sizeArr[1] };

        return SendWithType(OutputDataType.READ_MEMORY_REGISTERS, total);
    }

    /// \brief Attempts to write a block of data to the Wii Remote's internal registers.
    /// \param type The type of register you would like to write to
    /// \param offset The starting offset of the block of data you would like to write
    /// \param data Data to write to registers at \c offset.  This must have a maximum length of 16.
    /// \return On success, > 0, <= 0 on failure.
    /// \warning If data.Length > 16 the write request will be ignored.
    /// 
    /// Writing to the Wii Remote's internal registers allows you to access advanced functions of the remote, such as
    /// the speakers or the IR camera.  It is used by some of WiimoteApi's setup functions (SetupIRCamera()
    /// for example).
    /// 
    /// If you use this incorrectly (for example, if you attempt to write to a read-only register) the Wii Remote handles this gracefully
    /// and nothing happens.
    public int SendRegisterWriteRequest(RegisterType type, int offset, byte[] data)
    {
        if (data.Length > 16) return -2;


        byte address_select = (byte)type;
        byte[] offsetArr = IntToBigEndian(offset, 3);

        byte[] total = new byte[21];
        total[0] = address_select;
        for (int x = 0; x < 3; x++) total[x + 1] = offsetArr[x];
        total[4] = (byte)data.Length;
        for (int x = 0; x < data.Length; x++) total[x + 5] = data[x];

        return SendWithType(OutputDataType.WRITE_MEMORY_REGISTERS, total);
    }
    #endregion

    #region Read
    /// \brief Reads and interprets data reported by the Wii Remote.
    /// \return On success, > 0, < 0 on failure, 0 if nothing has been recieved.
    /// 
    /// Wii Remote reads function similarly to a Queue, in FIFO (first in, first out) order.
    /// For example, if two reports were sent since the last \c ReadWiimoteData() call,
    /// this call will only read and interpret the first of those two (and "pop" it off
    /// of the queue).  So, in order to make sure you don't fall behind the Wiimote's update
    /// frequency, you can do something like this (in a game loop for example):
    ///
    /// \code
    /// Wii Remote wiimote;
    /// int ret;
    /// do
    /// {
    ///     ret = wiimote.ReadWiimoteData();
    /// } while (ret > 0);
    /// \endcode
    public int ReadWiimoteData()
    {
        byte[] buf = new byte[22];
        int status = WiimoteManager.RecieveRaw(hidapi_handle, buf);
        if (status <= 0) return status; // Either there is some sort of error or we haven't recieved anything

        int typesize = GetInputDataTypeSize((InputDataType)buf[0]);
        byte[] data = new byte[typesize];
        for (int x = 0; x < data.Length; x++)
            data[x] = buf[x + 1];

        if (WiimoteManager.Debug_Messages)
            Debug.Log("Recieved: [" + buf[0].ToString("X").PadLeft(2, '0') + "] " + BitConverter.ToString(data));

        // Variable names used throughout the switch/case block
        byte[] buttons;
        byte[] accel;
        byte[] ext = null;
        byte[] ir;

        switch ((InputDataType)buf[0]) // buf[0] is the output ID byte
        {
            case InputDataType.STATUS_INFO: // done.
                buttons = new byte[] { data[0], data[1] };
                byte flags = data[2];
                byte battery_level = data[5];

                Button.InterpretData(buttons);

                bool old_ext_connected = Status.ext_connected;

                byte[] total = new byte[] { flags, battery_level };
                Status.InterpretData(total);

                if (expecting_status_report)
                {
                    expecting_status_report = false;
                }
                else                                        // We haven't requested any data report type, meaning a controller has connected.
                {
                    SendDataReportMode(last_report_type);   // If we don't update the data report mode, no updates will be sent
                }

                if (Status.ext_connected != old_ext_connected && Type != WiimoteType.PROCONTROLLER)
                {
                    if (Status.ext_connected)                // The Wii Remote doesn't allow reading from the extension identifier
                    {                                        // when nothing is connected.
                        Debug.Log("An extension has been connected.");
                        if (current_ext != ExtensionController.MOTIONPLUS)
                        {
                            ActivateExtension();
                            RequestIdentifyExtension();         // Identify what extension was connected.
                        }
                        else
                            ExpectingWiiMotionPlusSwitch = false;
                    }
                    else
                    {
                        if (!ExpectingWiiMotionPlusSwitch)
                            _current_ext = ExtensionController.NONE;
                        Debug.Log("An extension has been disconnected.");
                    }
                }
                break;
            case InputDataType.READ_MEMORY_REGISTERS: // done.
                buttons = new byte[] { data[0], data[1] };
                Button.InterpretData(buttons);

                if (CurrentReadData == null)
                {
                    Debug.LogWarning("Recived Register Read Report when none was expected.  Ignoring.");
                    return status;
                }

                byte size = (byte)((data[2] >> 4) + 0x01);
                byte error = (byte)(data[2] & 0x0f);
                // Error 0x07 means reading from a write-only register
                // Offset 0xa600fa is for the Wii Motion Plus.  This error code can be expected behavior in this case.
                if (error == 0x07)
                {
                    if(CurrentReadData.Offset != 0xa600fa)
                        Debug.LogError("Wiimote reports Read Register error 7: Attempting to read from a write-only register ("+CurrentReadData.Offset.ToString("x")+").  Aborting read.");

                    CurrentReadData = null;
                    return status;
                }
                // lowOffset is reversed because the Wii Remote reports are in Big Endian order
                ushort lowOffset = BitConverter.ToUInt16(new byte[] { data[4], data[3] }, 0);
                ushort expected = (ushort)CurrentReadData.ExpectedOffset;
                if (expected != lowOffset)
                    Debug.LogWarning("Expected Register Read Offset (" + expected + ") does not match reported offset from Wii Remote (" + lowOffset + ")");
                byte[] read = new byte[size];
                for (int x = 0; x < size; x++)
                    read[x] = data[x + 5];

                CurrentReadData.AppendData(read);
                if (CurrentReadData.ExpectedOffset >= CurrentReadData.Offset + CurrentReadData.Size)
                    CurrentReadData = null;

                break;
            case InputDataType.ACKNOWLEDGE_OUTPUT_REPORT:
                buttons = new byte[] { data[0], data[1] };
                Button.InterpretData(buttons);
                // TODO: doesn't do any actual error handling, or do any special code about acknowledging the output report.
                break;
            case InputDataType.REPORT_BUTTONS: // done.
                buttons = new byte[] { data[0], data[1] };
                Button.InterpretData(buttons);
                break;
            case InputDataType.REPORT_BUTTONS_ACCEL: // done.
                buttons = new byte[] { data[0], data[1] };
                Button.InterpretData(buttons);

                accel = new byte[] { data[0], data[1], data[2], data[3], data[4] };
                Accel.InterpretData(accel);
                break;
            case InputDataType.REPORT_BUTTONS_EXT8: // done.
                buttons = new byte[] { data[0], data[1] };
                Button.InterpretData(buttons);

                ext = new byte[8];
                for (int x = 0; x < ext.Length; x++)
                    ext[x] = data[x + 2];

                if (_Extension != null)
                    _Extension.InterpretData(ext);
                break;
            case InputDataType.REPORT_BUTTONS_ACCEL_IR12: // done.
                buttons = new byte[] { data[0], data[1] };
                Button.InterpretData(buttons);

                accel = new byte[] { data[0], data[1], data[2], data[3], data[4] };
                Accel.InterpretData(accel);

                ir = new byte[12];
                for (int x = 0; x < 12; x++)
                    ir[x] = data[x + 5];
                Ir.InterpretData(ir);
                break;
            case InputDataType.REPORT_BUTTONS_EXT19: // done.
                buttons = new byte[] { data[0], data[1] };
                Button.InterpretData(buttons);

                ext = new byte[19];
                for (int x = 0; x < ext.Length; x++)
                    ext[x] = data[x + 2];

                if (_Extension != null)
                    _Extension.InterpretData(ext);
                break;
            case InputDataType.REPORT_BUTTONS_ACCEL_EXT16: // done.
                buttons = new byte[] { data[0], data[1] };
                Button.InterpretData(buttons);

                accel = new byte[] { data[0], data[1], data[2], data[3], data[4] };
                Accel.InterpretData(accel);

                ext = new byte[16];
                for (int x = 0; x < ext.Length; x++)
                    ext[x] = data[x + 5];

                if (_Extension != null)
                    _Extension.InterpretData(ext);
                break;
            case InputDataType.REPORT_BUTTONS_IR10_EXT9: // done.
                buttons = new byte[] { data[0], data[1] };
                Button.InterpretData(buttons);

                ir = new byte[10];
                for (int x = 0; x < 10; x++)
                    ir[x] = data[x + 2];
                Ir.InterpretData(ir);

                ext = new byte[9];
                for (int x = 0; x < 9; x++)
                    ext[x] = data[x + 12];

                if (_Extension != null)
                    _Extension.InterpretData(ext);
                break;
            case InputDataType.REPORT_BUTTONS_ACCEL_IR10_EXT6: // done.
                buttons = new byte[] { data[0], data[1] };
                Button.InterpretData(buttons);

                accel = new byte[] { data[0], data[1], data[2], data[3], data[4] };
                Accel.InterpretData(accel);

                ir = new byte[10];
                for (int x = 0; x < 10; x++)
                    ir[x] = data[x + 5];
                Ir.InterpretData(ir);

                ext = new byte[6];
                for (int x = 0; x < 6; x++)
                    ext[x] = data[x + 15];

                if (_Extension != null)
                    _Extension.InterpretData(ext);
                break;
            case InputDataType.REPORT_EXT21: // done.
                ext = new byte[21];
                for (int x = 0; x < ext.Length; x++)
                    ext[x] = data[x];

                if (_Extension != null)
                    _Extension.InterpretData(ext);
                break;
            case InputDataType.REPORT_INTERLEAVED:
                if (!ExpectingSecondInterleavedPacket)
                {
                    ExpectingSecondInterleavedPacket = true;
                    InterleavedDataBuffer = data;
                } else if(WiimoteManager.Debug_Messages) {
                    Debug.LogWarning(
                        "Recieved two REPORT_INTERLEAVED ("+InputDataType.REPORT_INTERLEAVED.ToString("x")+") reports in a row!  "
                        + "Expected REPORT_INTERLEAVED_ALT ("+InputDataType.REPORT_INTERLEAVED_ALT.ToString("x")+").  Ignoring!"
                    );
                }
                
                break;
            case InputDataType.REPORT_INTERLEAVED_ALT:
                if (ExpectingSecondInterleavedPacket)
                {
                    ExpectingSecondInterleavedPacket = false;

                    buttons = new byte[] { data[0], data[1] };
                    Button.InterpretData(buttons);

                    byte[] ir1 = new byte[18];
                    byte[] ir2 = new byte[18];

                    for (int x = 0; x < 18; x++)
                    {
                        ir1[x] = InterleavedDataBuffer[x + 3];
                        ir2[x] = data[x + 3];
                    }

                    Ir.InterpretDataInterleaved(ir1, ir2);
                    Accel.InterpretDataInterleaved(InterleavedDataBuffer, data);
                }
                else if(WiimoteManager.Debug_Messages)
                {
                    Debug.LogWarning(
                        "Recieved two REPORT_INTERLEAVED_ALT ("+InputDataType.REPORT_INTERLEAVED_ALT.ToString("x")+") reports in a row!  "
                        + "Expected REPORT_INTERLEAVED ("+InputDataType.REPORT_INTERLEAVED.ToString("x")+").  Ignoring!"
                    );
                }
                break;
        }

        if(ext == null)
            _RawExtension = null;
        else
            _RawExtension = new ReadOnlyArray<byte>(ext);

        return status;
    }

    /// The size, in bytes, of a given Wii Remote InputDataType when reported by the Wiimote.
    ///
    /// This is at most 21 bytes.
    public static int GetInputDataTypeSize(InputDataType type)
    {
        switch (type)
        {
            case InputDataType.STATUS_INFO:
                return 6;
            case InputDataType.READ_MEMORY_REGISTERS:
                return 21;
            case InputDataType.ACKNOWLEDGE_OUTPUT_REPORT:
                return 4;
            case InputDataType.REPORT_BUTTONS:
                return 2;
            case InputDataType.REPORT_BUTTONS_ACCEL:
                return 5;
            case InputDataType.REPORT_BUTTONS_EXT8:
                return 10;
            case InputDataType.REPORT_BUTTONS_ACCEL_IR12:
                return 17;
            case InputDataType.REPORT_BUTTONS_EXT19:
                return 21;
            case InputDataType.REPORT_BUTTONS_ACCEL_EXT16:
                return 21;
            case InputDataType.REPORT_BUTTONS_IR10_EXT9:
                return 21;
            case InputDataType.REPORT_BUTTONS_ACCEL_IR10_EXT6:
                return 21;
            case InputDataType.REPORT_EXT21:
                return 21;
            case InputDataType.REPORT_INTERLEAVED:
                return 21;
            case InputDataType.REPORT_INTERLEAVED_ALT:
                return 21;
        }
        return 0;
    }


    #endregion

    // ------------- UTILITY ------------- //
    public static byte[] IntToBigEndian(int input, int len)
    {
        byte[] intBytes = BitConverter.GetBytes(input);
        Array.Resize(ref intBytes, len);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(intBytes);

        return intBytes;
    }
}
}
using UnityEngine;
using System;
using WiimoteApi.Internal;
using WiimoteApi.Util;

namespace WiimoteApi {

    public delegate void ReadResponder(byte[] data);

[System.Serializable]
public class Wiimote
{
    public bool RumbleOn = false;

    public AccelData     Accel      { get { return _Accel; } }
    private AccelData   _Accel;
    public WiimoteData   Extension  { get { return _Extension; } }
    private WiimoteData _Extension;
    public ButtonData    Button     { get { return _Button; } }
    private ButtonData  _Button;
    public IRData        Ir         { get { return _Ir; } }
    private IRData      _Ir;
    public StatusData    Status     { get { return _Status; } }
    private StatusData  _Status;

    public IntPtr hidapi_handle { get { return _hidapi_handle; } }
    private IntPtr _hidapi_handle = IntPtr.Zero;

    public string hidapi_path { get { return _hidapi_path; } }
    private string _hidapi_path;

    public bool wiimoteplus { get { return _wiimoteplus; } }
    private bool _wiimoteplus = false;

    private RegisterReadData CurrentReadData = null;

    private InputDataType last_report_type = InputDataType.REPORT_BUTTONS;
    private bool expecting_status_report = false;

    // True if a Wii Motion Plus is attached to the Wiimote, and it
    // has NOT BEEN ACTIVATED.  When the WMP is activated this value is
    // false.  This is only updated when the remote is requested from
    // Wiimote registers (see: RequestIdentifyWiiMotionPlus())
    public bool wmp_attached { get { return _wmp_attached; } }
    private bool _wmp_attached = false;

    public ExtensionController current_ext { get { return _current_ext; } }
    private ExtensionController _current_ext = ExtensionController.NONE;

    public Wiimote(IntPtr hidapi_handle, string hidapi_path, bool wiimoteplus)
    {
        _hidapi_handle  = hidapi_handle;
        _hidapi_path    = hidapi_path;
        _wiimoteplus    = wiimoteplus;

        _Accel  = new AccelData(this);
        _Button = new ButtonData(this);
        _Ir     = new IRData(this);
        _Status = new StatusData(this);
        _Extension = null;
    }

    public static byte[] ID_InactiveMotionPlus = new byte[] {0x00, 0x00, 0xA6, 0x20, 0x00, 0x05};

    private void RespondIdentifyWiiMotionPlus(byte[] data)
    {
        if (data.Length != ID_InactiveMotionPlus.Length)
        {
            _wmp_attached = false;
            return;
        }
        for (int x = 0; x < data.Length; x++)
        {
            if (data[x] != ID_InactiveMotionPlus[x])
            {
                _wmp_attached = false;
                return;
            }
        }
        _wmp_attached = true;
    }

    public const long ID_ActiveMotionPlus           = 0x0000A4200405;
    public const long ID_ActiveMotionPlus_Nunchuck  = 0x0000A4200505;
    public const long ID_ActiveMotionPlus_Classic   = 0x0000A4200705;
    public const long ID_Nunchuck                   = 0x0000A4200000;
    public const long ID_Classic                    = 0x0000A4200101;
    public const long ID_ClassicPro                 = 0x0100A4200101;


    private void RespondIdentifyExtension(byte[] data)
    {
        if (data.Length != 6)
            return;

        byte[] resized = new byte[8];
        for (int x = 0; x < 6; x++) resized[x] = data[5-x];
        long val = BitConverter.ToInt64(resized, 0);

        if (val == ID_ActiveMotionPlus)
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
            _Extension = null;
        }
        else
        {
            _current_ext = ExtensionController.NONE;
            _Extension = null;
        }
    }

    #region Setups

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
                Debug.LogWarning("Interleaved data reporting is currently not supported.");
                res = SendDataReportMode(InputDataType.REPORT_INTERLEAVED);
                break;
        }

        if (res < 0) return false;
        return true;
    }

    public bool RequestIdentifyWiiMotionPlus()
    {
        int res;
        res = SendRegisterReadRequest(RegisterType.CONTROL, 0xA600FA, 6, RespondIdentifyWiiMotionPlus);
        return res > 0;
    }

    public bool RequestIdentifyExtension()
    {
        int res = SendRegisterReadRequest(RegisterType.CONTROL, 0xA400FA, 6, RespondIdentifyExtension);
        return res > 0;
    }

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

        return true;
    }

    public bool ActivateExtension()
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

    public int SendPlayerLED(bool led1, bool led2, bool led3, bool led4)
    {
        byte mask = 0;
        if (led1) mask |= 0x10;
        if (led2) mask |= 0x20;
        if (led3) mask |= 0x40;
        if (led4) mask |= 0x80;

        return SendWithType(OutputDataType.LED, new byte[] { mask });
    }

    public int SendDataReportMode(InputDataType mode)
    {
        if (mode == InputDataType.STATUS_INFO || mode == InputDataType.READ_MEMORY_REGISTERS || mode == InputDataType.ACKNOWLEDGE_OUTPUT_REPORT)
        {
            Debug.LogError("Passed " + mode.ToString() + " to SendDataReportMode!");
            return -2;
        }

        last_report_type = mode;

        return SendWithType(OutputDataType.DATA_REPORT_MODE, new byte[] { 0x00, (byte)mode });
    }

    public int SendIRCameraEnable(bool enabled)
    {
        byte[] mask = new byte[] { (byte)(enabled ? 0x04 : 0x00) };

        int first = SendWithType(OutputDataType.IR_CAMERA_ENABLE, mask);
        if (first < 0) return first;

        int second = SendWithType(OutputDataType.IR_CAMERA_ENABLE_2, mask);
        if (second < 0) return second;

        return first + second; // success
    }

    public int SendSpeakerEnabled(bool enabled)
    {
        byte[] mask = new byte[] { (byte)(enabled ? 0x04 : 0x00) };

        return SendWithType(OutputDataType.SPEAKER_ENABLE, mask);
    }

    public int SendSpeakerMuted(bool muted)
    {
        byte[] mask = new byte[] { (byte)(muted ? 0x04 : 0x00) };

        return SendWithType(OutputDataType.SPEAKER_MUTE, mask);
    }

    public int SendStatusInfoRequest()
    {
        expecting_status_report = true;
        return SendWithType(OutputDataType.STATUS_INFO_REQUEST, new byte[] { 0x00 });
    }

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
        byte[] ext;
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
                    Debug.Log("An extension has been connected or disconnected.");
                    SendDataReportMode(last_report_type);   // If we don't update the data report mode, no updates will be sent
                }

                if (Status.ext_connected != old_ext_connected)
                {
                    if (Status.ext_connected)                // The wiimote doesn't allow reading from the extension identifier
                    {                                               // when nothing is connected.
                        ActivateExtension();
                        RequestIdentifyExtension();         // Identify what extension was connected.
                    }
                    else
                    {
                        _current_ext = ExtensionController.NONE;
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
                if (error == 0x07)
                {
                    Debug.LogError("Wiimote reports Read Register error 7: Attempting to read from a write-only register.  Aborting read.");
                    CurrentReadData = null;
                    return status;
                }
                // lowOffset is reversed because the wiimote reports are in Big Endian order
                ushort lowOffset = BitConverter.ToUInt16(new byte[] { data[4], data[3] }, 0);
                ushort expected = (ushort)CurrentReadData.ExpectedOffset;
                if (expected != lowOffset)
                    Debug.LogWarning("Expected Register Read Offset (" + expected + ") does not match reported offset from Wiimote (" + lowOffset + ")");
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

                if (Extension != null)
                    Extension.InterpretData(ext);
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

                if (Extension != null)
                    Extension.InterpretData(ext);
                break;
            case InputDataType.REPORT_BUTTONS_ACCEL_EXT16: // done.
                buttons = new byte[] { data[0], data[1] };
                Button.InterpretData(buttons);

                accel = new byte[] { data[0], data[1], data[2], data[3], data[4] };
                Accel.InterpretData(accel);

                ext = new byte[16];
                for (int x = 0; x < ext.Length; x++)
                    ext[x] = data[x + 5];

                if (Extension != null)
                    Extension.InterpretData(ext);
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

                if (Extension != null)
                    Extension.InterpretData(ext);
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

                if (Extension != null)
                    Extension.InterpretData(ext);
                break;
            case InputDataType.REPORT_EXT21: // done.
                ext = new byte[21];
                for (int x = 0; x < ext.Length; x++)
                    ext[x] = data[x];

                if (Extension != null)
                    Extension.InterpretData(ext);
                break;
            case InputDataType.REPORT_INTERLEAVED:
                // TODO
                break;
            case InputDataType.REPORT_INTERLEAVED_ALT:
                // TODO
                break;
        }
        return status;
    }

    public int GetInputDataTypeSize(InputDataType type)
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
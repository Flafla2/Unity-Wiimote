using UnityEngine;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Runtime.InteropServices;
using WiimoteApi.Internal;

namespace WiimoteApi { 

public class WiimoteManager
{
    public const ushort vendor_id = 0x057e;
    public const ushort product_id_wiimote = 0x0306;
    public const ushort product_id_wiimoteplus = 0x0330;
    
    public static List<Wiimote> Wiimotes = new List<Wiimote>();

    private static InputDataType last_report_type = InputDataType.REPORT_BUTTONS;
    private static bool expecting_status_report = false;

    public static bool Debug_Messages = false;

    public static int MaxWriteFrequency = 20; // In ms
    private static float LastWriteTime = 0;
    private static Queue<WriteQueueData> WriteQueue;

    // ------------- RAW HIDAPI INTERFACE ------------- //

    public static bool FindWiimote(bool wiimoteplus)
    {
        //if (hidapi_wiimote != IntPtr.Zero)
        //    HIDapi.hid_close(hidapi_wiimote);

        IntPtr ptr = HIDapi.hid_enumerate(vendor_id, wiimoteplus ? product_id_wiimoteplus : product_id_wiimote);
        IntPtr cur_ptr = ptr;

        if (ptr == IntPtr.Zero)
            return false;

        hid_device_info enumerate = (hid_device_info)Marshal.PtrToStructure(ptr, typeof(hid_device_info));

        bool found = false;

        while(cur_ptr != IntPtr.Zero)
        {
            Wiimote remote = null;
            bool fin = false;
            foreach (Wiimote r in Wiimotes)
            {
                if (fin)
                    continue;

                if (r.hidapi_path.Equals(enumerate.path))
                {
                    remote = r;
                    fin = true;
                }
            }
            if (remote == null)
            {
                IntPtr handle = HIDapi.hid_open_path(remote.hidapi_path);
                if (Debug_Messages)
                    Debug.Log("Found New Remote: " + remote.hidapi_path);

                remote = new Wiimote(handle, enumerate.path);
                remote.wiimoteplus = wiimoteplus;

                Wiimotes.Add(remote);

                SendDataReportMode(remote, InputDataType.REPORT_BUTTONS);
                SendStatusInfoRequest(remote);
            }

            cur_ptr = enumerate.next;
            if(cur_ptr != IntPtr.Zero)
                enumerate = (hid_device_info)Marshal.PtrToStructure(cur_ptr, typeof(hid_device_info));
        }

        HIDapi.hid_free_enumeration(ptr);

        return found;
    }

    public static void Cleanup(Wiimote remote)
    {
        if (remote.hidapi_handle != IntPtr.Zero)
            HIDapi.hid_close(remote.hidapi_handle);

        Wiimotes.Remove(remote);
    }

    public static bool HasWiimote()
    {
        return !(Wiimotes.Count <= 0 || Wiimotes[0] == null || Wiimotes[0].hidapi_handle == IntPtr.Zero);
    }

    public static int SendRaw(IntPtr hidapi_wiimote, byte[] data)
    {
        if (hidapi_wiimote == IntPtr.Zero) return -2;

        if (WriteQueue == null)
        {
            WriteQueue = new Queue<WriteQueueData>();
            SendThreadObj = new Thread(new ThreadStart(SendThread));
            SendThreadObj.Start();
        }

        WriteQueueData wqd = new WriteQueueData();
        wqd.pointer = hidapi_wiimote;
        wqd.data = data;
        lock(WriteQueue)
            WriteQueue.Enqueue(wqd);

        return 0; // TODO: Better error handling
    }

    private static Thread SendThreadObj;
    private static void SendThread()
    {
        while (true)
        {
            lock (WriteQueue)
            {
                if (WriteQueue.Count != 0)
                {
                    WriteQueueData wqd = WriteQueue.Dequeue();
                    int res = HIDapi.hid_write(wqd.pointer, wqd.data, new UIntPtr(Convert.ToUInt32(wqd.data.Length)));
                    if (res == -1) Debug.LogError("HidAPI reports error " + res + " on write: " + Marshal.PtrToStringUni(HIDapi.hid_error(wqd.pointer)));
                    else if (Debug_Messages) Debug.Log("Sent " + res + "b: [" + wqd.data[0].ToString("X").PadLeft(2, '0') + "] " + BitConverter.ToString(wqd.data, 1));
                }
            }
            Thread.Sleep(MaxWriteFrequency);
        }
    }

    public static int RecieveRaw(IntPtr hidapi_wiimote, byte[] buf)
    {
        if (hidapi_wiimote == IntPtr.Zero) return -2;

        HIDapi.hid_set_nonblocking(hidapi_wiimote, 1);
        int res = HIDapi.hid_read(hidapi_wiimote, buf, new UIntPtr(Convert.ToUInt32(buf.Length)));

        return res;
    }

    // ------------- WIIMOTE SPECIFIC UTILITIES ------------- //

    #region Setups

    public static bool SetupIRCamera(Wiimote remote, IRDataType type = IRDataType.EXTENDED)
    {
        int res;
        // 1. Enable IR Camera (Send 0x04 to Output Report 0x13)
        // 2. Enable IR Camera 2 (Send 0x04 to Output Report 0x1a)
        res = SendIRCameraEnable(remote, true);
        if (res < 0) return false;
        // 3. Write 0x08 to register 0xb00030
        res = SendRegisterWriteRequest(remote, RegisterType.CONTROL, 0xb00030, new byte[] { 0x08 });
        if (res < 0) return false;
        // 4. Write Sensitivity Block 1 to registers at 0xb00000
        // Wii sensitivity level 3:
        // 02 00 00 71 01 00 aa 00 64
        // High Sensitivity:
        // 00 00 00 00 00 00 90 00 41
        res = SendRegisterWriteRequest(remote, RegisterType.CONTROL, 0xb00000, 
            new byte[] { 0x02, 0x00, 0x00, 0x71, 0x01, 0x00, 0xaa, 0x00, 0x64 });
            //new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x90, 0x00, 0x41});
        if (res < 0) return false;
        // 5. Write Sensitivity Block 2 to registers at 0xb0001a
        // Wii sensitivity level 3: 
        // 63 03
        // High Sensitivity:
        // 40 00
        res = SendRegisterWriteRequest(remote, RegisterType.CONTROL, 0xb0001a, new byte[] { 0x63, 0x03 });
        if (res < 0) return false;
        // 6. Write Mode Number to register 0xb00033
        res = SendRegisterWriteRequest(remote, RegisterType.CONTROL, 0xb00033, new byte[] { (byte)type });
        if (res < 0) return false;
        // 7. Write 0x08 to register 0xb00030 (again)
        res = SendRegisterWriteRequest(remote, RegisterType.CONTROL, 0xb00030, new byte[] { 0x08 });
        if (res < 0) return false;

        switch (type)
        {
            case IRDataType.BASIC:
                res = SendDataReportMode(remote, InputDataType.REPORT_BUTTONS_ACCEL_IR10_EXT6);
                break;
            case IRDataType.EXTENDED:
                res = SendDataReportMode(remote, InputDataType.REPORT_BUTTONS_ACCEL_IR12);
                break;
            case IRDataType.FULL:
                Debug.LogWarning("Interleaved data reporting is currently not supported.");
                res = SendDataReportMode(remote, InputDataType.REPORT_INTERLEAVED);
                break;
        }
        
        if (res < 0) return false;
        return true;
    }

    public static bool RequestIdentifyWiiMotionPlus(Wiimote remote)
    {
        int res;
        res = SendRegisterReadRequest(remote, RegisterType.CONTROL, 0xA600FA, 6, remote.RespondIdentifyWiiMotionPlus);
        return res > 0;
    }

    public static bool RequestIdentifyExtension(Wiimote remote)
    {
        int res = SendRegisterReadRequest(remote, RegisterType.CONTROL, 0xA400FA, 6, remote.RespondIdentifyExtension);
        return res > 0;
    }

    public static bool ActivateWiiMotionPlus(Wiimote remote)
    {
        if (!remote.wmp_attached)
            Debug.LogWarning("There is a request to activate the Wii Motion Plus even though it has not been confirmed to exist!  Trying anyway.");

        // Initialize the Wii Motion Plus by writing 0x55 to register 0xA600F0
        int res = SendRegisterWriteRequest(remote, RegisterType.CONTROL, 0xA600F0, new byte[] { 0x55 });
        if (res < 0) return false;

        // Activate the Wii Motion Plus as the active extension by writing 0x04 to register 0xA600FE
        // This does 3 things:
        // 1. A status report (0x20) will be sent, which indicates that an extension has been
        //    plugged in - IF there is no extension plugged into the passthrough port.
        // 2. The standard extension identifier at 0xA400FA now reads 00 00 A4 20 04 05
        // 3. Extension reports now contain Wii Motion Plus data.
        res = SendRegisterWriteRequest(remote, RegisterType.CONTROL, 0xA600FE, new byte[] { 0x04 });
        if (res < 0) return false;

        return true;
    }

    public static bool ActivateExtension(Wiimote remote)
    {
        if (!remote.Status.ext_connected)
            Debug.LogWarning("There is a request to activate an Extension controller even though it has not been confirmed to exist!  Trying anyway.");

        // 1. Initialize the Extension by writing 0x55 to register 0xA400F0
        int res = SendRegisterWriteRequest(remote, RegisterType.CONTROL, 0xA400F0, new byte[] { 0x55 });
        if (res < 0) return false;

        // 2. Activate the Extension by writing 0x00 to register 0xA400FB
        res = SendRegisterWriteRequest(remote, RegisterType.CONTROL, 0xA400FB, new byte[] { 0x00 });
        if (res < 0) return false;
        return true;
    }

    #endregion

    #region Write
    public static int SendWithType(Wiimote remote, OutputDataType type, byte[] data)
    {
        byte[] final = new byte[data.Length + 1];
        final[0] = (byte)type;

        for (int x = 0; x < data.Length; x++)
            final[x + 1] = data[x];

        if (remote.RumbleOn)
            final[1] |= 0x01;

        int res = SendRaw(remote.hidapi_handle, final);

        if (res < -1) Debug.LogError("Incorrect Input to HIDAPI.  No data has been sent.");
        

        return res;
    }

    public static int SendPlayerLED(Wiimote remote, bool led1, bool led2, bool led3, bool led4)
    {
        byte mask = 0;
        if (led1) mask |= 0x10;
        if (led2) mask |= 0x20;
        if (led3) mask |= 0x40;
        if (led4) mask |= 0x80;

        return SendWithType(remote, OutputDataType.LED, new byte[] { mask });
    }

    public static int SendDataReportMode(Wiimote remote, InputDataType mode)
    {
        if (mode == InputDataType.STATUS_INFO || mode == InputDataType.READ_MEMORY_REGISTERS || mode == InputDataType.ACKNOWLEDGE_OUTPUT_REPORT)
        {
            Debug.LogError("Passed " + mode.ToString() + " to SendDataReportMode!");
            return -2;
        }

        last_report_type = mode;

        return SendWithType(remote, OutputDataType.DATA_REPORT_MODE, new byte[] { 0x00, (byte)mode });
    }

    public static int SendIRCameraEnable(Wiimote remote, bool enabled)
    {
        byte[] mask = new byte[] { (byte)(enabled ? 0x04 : 0x00) };

        int first = SendWithType(remote, OutputDataType.IR_CAMERA_ENABLE, mask);
        if (first < 0) return first;

        int second = SendWithType(remote, OutputDataType.IR_CAMERA_ENABLE_2, mask);
        if (second < 0) return second;

        return first + second; // success
    }

    public static int SendSpeakerEnabled(Wiimote remote, bool enabled)
    {
        byte[] mask = new byte[] { (byte)(enabled ? 0x04 : 0x00) };

        return SendWithType(remote, OutputDataType.SPEAKER_ENABLE, mask);
    }

    public static int SendSpeakerMuted(Wiimote remote, bool muted)
    {
        byte[] mask = new byte[] { (byte)(muted ? 0x04 : 0x00) };

        return SendWithType(remote, OutputDataType.SPEAKER_MUTE, mask);
    }

    public static int SendStatusInfoRequest(Wiimote remote)
    {
        expecting_status_report = true;
        return SendWithType(remote, OutputDataType.STATUS_INFO_REQUEST, new byte[] { 0x00 });
    }

    public static int SendRegisterReadRequest(Wiimote remote, RegisterType type, int offset, int size, ReadResponder Responder)
    {
        if (remote.CurrentReadData != null)
        {
            Debug.LogWarning("Aborting read request; There is already a read request pending!");
            return -2;
        }
            

        remote.CurrentReadData = new RegisterReadData(offset, size, Responder);

        byte address_select = (byte)type;
        byte[] offsetArr = IntToBigEndian(offset, 3);
        byte[] sizeArr = IntToBigEndian(size, 2);

        byte[] total = new byte[] { address_select, offsetArr[0], offsetArr[1], offsetArr[2], 
            sizeArr[0], sizeArr[1] };

        return SendWithType(remote, OutputDataType.READ_MEMORY_REGISTERS, total);
    }

    public static int SendRegisterWriteRequest(Wiimote remote, RegisterType type, int offset, byte[] data)
    {
        if (data.Length > 16) return -2;
        

        byte address_select = (byte)type;
        byte[] offsetArr = IntToBigEndian(offset,3);

        byte[] total = new byte[21];
        total[0] = address_select;
        for (int x = 0; x < 3; x++) total[x + 1] = offsetArr[x];
        total[4] = (byte)data.Length;
        for (int x = 0; x < data.Length; x++) total[x + 5] = data[x];

        return SendWithType(remote, OutputDataType.WRITE_MEMORY_REGISTERS, total);
    }
    #endregion

    #region Read
    public static int ReadWiimoteData(Wiimote remote)
    {
        byte[] buf = new byte[22];
        int status = RecieveRaw(remote.hidapi_handle, buf);
        if (status <= 0) return status; // Either there is some sort of error or we haven't recieved anything

        int typesize = GetInputDataTypeSize((InputDataType)buf[0]);
        byte[] data = new byte[typesize];
        for (int x = 0; x < data.Length; x++)
            data[x] = buf[x + 1];

        if (Debug_Messages)
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

                remote.Button.InterpretData(buttons);

                bool old_ext_connected = remote.Status.ext_connected;

                byte[] total = new byte[] { flags, battery_level };
                remote.Status.InterpretData(total);

                if (expecting_status_report)
                {
                    expecting_status_report = false;
                }
                else                                        // We haven't requested any data report type, meaning a controller has connected.
                {
                    Debug.Log("An extension has been connected or disconnected.");
                    SendDataReportMode(remote, last_report_type);   // If we don't update the data report mode, no updates will be sent
                }

                if (remote.Status.ext_connected != old_ext_connected)
                {
                    if (remote.Status.ext_connected)                // The wiimote doesn't allow reading from the extension identifier
                    {                                               // when nothing is connected.
                        ActivateExtension(remote);
                        RequestIdentifyExtension(remote);         // Identify what extension was connected.
                    } else
                    {
                        remote.current_ext = ExtensionController.NONE;
                    }
                }
                break;
            case InputDataType.READ_MEMORY_REGISTERS: // done.
                buttons = new byte[] { data[0], data[1] };
                remote.Button.InterpretData(buttons);

                if (remote.CurrentReadData == null)
                {
                    Debug.LogWarning("Recived Register Read Report when none was expected.  Ignoring.");
                    return status;
                }

                byte size = (byte)((data[2] >> 4) + 0x01);
                byte error = (byte)(data[2] & 0x0f);
                if (error == 0x07)
                {
                    Debug.LogError("Wiimote reports Read Register error 7: Attempting to read from a write-only register.  Aborting read.");
                    remote.CurrentReadData = null;
                    return status;
                }
                // lowOffset is reversed because the wiimote reports are in Big Endian order
                ushort lowOffset = BitConverter.ToUInt16(new byte[] { data[4], data[3] }, 0);
                ushort expected = (ushort)remote.CurrentReadData.ExpectedOffset;
                if (expected != lowOffset)
                    Debug.LogWarning("Expected Register Read Offset (" + expected + ") does not match reported offset from Wiimote (" + lowOffset + ")");
                byte[] read = new byte[size];
                for (int x = 0; x < size; x++)
                    read[x] = data[x + 5];

                remote.CurrentReadData.AppendData(read);
                if (remote.CurrentReadData.ExpectedOffset >= remote.CurrentReadData.Offset + remote.CurrentReadData.Size)
                    remote.CurrentReadData = null;

                break;
            case InputDataType.ACKNOWLEDGE_OUTPUT_REPORT:
                buttons = new byte[] { data[0], data[1] };
                remote.Button.InterpretData(buttons);
                // TODO: doesn't do any actual error handling, or do any special code about acknowledging the output report.
                break;
            case InputDataType.REPORT_BUTTONS: // done.
                buttons = new byte[] { data[0], data[1] };
                remote.Button.InterpretData(buttons);
                break;
            case InputDataType.REPORT_BUTTONS_ACCEL: // done.
                buttons = new byte[] { data[0], data[1] };
                remote.Button.InterpretData(buttons);

                accel = new byte[] { data[0], data[1], data[2], data[3], data[4] };
                remote.Accel.InterpretData(accel);
                break;
            case InputDataType.REPORT_BUTTONS_EXT8: // done.
                buttons = new byte[] { data[0], data[1] };
                remote.Button.InterpretData(buttons);

                ext = new byte[8];
                for (int x = 0; x < ext.Length; x++)
                    ext[x] = data[x + 2];

                remote.extension = ext;
                break;
            case InputDataType.REPORT_BUTTONS_ACCEL_IR12: // done.
                buttons = new byte[] { data[0], data[1] };
                remote.Button.InterpretData(buttons);

                accel = new byte[] { data[0], data[1], data[2], data[3], data[4] };
                remote.Accel.InterpretData(accel);

                ir = new byte[12];
                for (int x = 0; x < 12; x++)
                    ir[x] = data[x + 5];
                remote.Ir.InterpretData(ir);
                break;
            case InputDataType.REPORT_BUTTONS_EXT19: // done.
                buttons = new byte[] { data[0], data[1] };
                remote.Button.InterpretData(buttons);

                ext = new byte[19];
                for (int x = 0; x < ext.Length; x++)
                    ext[x] = data[x + 2];
                remote.extension = ext;
                break;
            case InputDataType.REPORT_BUTTONS_ACCEL_EXT16: // done.
                buttons = new byte[] { data[0], data[1] };
                remote.Button.InterpretData(buttons);

                accel = new byte[] { data[0], data[1], data[2], data[3], data[4] };
                remote.Accel.InterpretData(accel);

                ext = new byte[16];
                for (int x = 0; x < ext.Length; x++)
                    ext[x] = data[x + 5];
                remote.extension = ext;
                break;
            case InputDataType.REPORT_BUTTONS_IR10_EXT9: // done.
                buttons = new byte[] { data[0], data[1] };
                remote.Button.InterpretData(buttons);

                ir = new byte[10];
                for (int x = 0; x < 10; x++)
                    ir[x] = data[x + 2];
                remote.Ir.InterpretData(ir);

                ext = new byte[9];
                for (int x = 0; x < 9; x++)
                    ext[x] = data[x + 12];
                remote.extension = ext;
                break;
            case InputDataType.REPORT_BUTTONS_ACCEL_IR10_EXT6: // done.
                buttons = new byte[] { data[0], data[1] };
                remote.Button.InterpretData(buttons);

                accel = new byte[] { data[0], data[1], data[2], data[3], data[4] };
                remote.Accel.InterpretData(accel);

                ir = new byte[10];
                for (int x = 0; x < 10; x++)
                    ir[x] = data[x + 5];
                remote.Ir.InterpretData(ir);

                ext = new byte[6];
                for (int x = 0; x < 6; x++)
                    ext[x] = data[x + 15];
                remote.extension = ext;
                break;
            case InputDataType.REPORT_EXT21: // done.
                ext = new byte[21];
                for (int x = 0; x < ext.Length; x++)
                    ext[x] = data[x];
                remote.extension = ext;
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

    private class WriteQueueData {
        public IntPtr pointer;
        public byte[] data;
    }
}
} // namespace WiimoteApi
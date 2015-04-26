using UnityEngine;
using System;
using WiimoteApi.Internal;

namespace WiimoteApi {

    public delegate void ReadResponder(byte[] data);

[System.Serializable]
public class Wiimote
{
    public RegisterReadData CurrentReadData = null;
    public IntPtr hidapi_handle = IntPtr.Zero;
    public string hidapi_path;
    public bool wiimoteplus = false;
    public bool RumbleOn = false;

    public AccelData    Accel;
    public WiimoteData  Extension;
    public ButtonData   Button;
    public IRData       Ir;
    public StatusData   Status;

    public Wiimote(IntPtr hidapi_handle, string hidapi_path)
    {
        this.hidapi_handle = hidapi_handle;
        this.hidapi_path = hidapi_path;

        Accel   = new AccelData(this);
        Button  = new ButtonData(this);
        Ir      = new IRData(this);
        Status  = new StatusData(this);
        Extension = null;
    }
    
    // Raw data for any extension controllers connected to the wiimote
    // This is only updated if the Wiimote has a report mode with Extensions.
    public byte[] extension;

    // True if a Wii Motion Plus is attached to the Wiimote, and it
    // has NOT BEEN ACTIVATED.  When the WMP is activated this value is
    // false.  This is only updated when the remote is requested from
    // Wiimote registers (see: RequestIdentifyWiiMotionPlus())
    public bool wmp_attached = false;
    public ExtensionController current_ext = ExtensionController.NONE;

    public static byte[] ID_InactiveMotionPlus = new byte[] {0x00, 0x00, 0xA6, 0x20, 0x00, 0x05};

    public void RespondIdentifyWiiMotionPlus(byte[] data)
    {
        if (data.Length != ID_InactiveMotionPlus.Length)
        {
            wmp_attached = false;
            return;
        }
        for (int x = 0; x < data.Length; x++)
        {
            if (data[x] != ID_InactiveMotionPlus[x])
            {
                wmp_attached = false;
                return;
            }
        }
        wmp_attached = true;
    }

    public const long ID_ActiveMotionPlus           = 0x0000A4200405;
    public const long ID_ActiveMotionPlus_Nunchuck  = 0x0000A4200505;
    public const long ID_ActiveMotionPlus_Classic   = 0x0000A4200705;
    public const long ID_Nunchuck                   = 0x0000A4200000;
    public const long ID_Classic                    = 0x0000A4200101;
    public const long ID_ClassicPro                 = 0x0100A4200101;


    public void RespondIdentifyExtension(byte[] data)
    {
        if (data.Length != 6)
            return;

        byte[] resized = new byte[8];
        for (int x = 0; x < 6; x++) resized[x] = data[5-x];
        long val = BitConverter.ToInt64(resized, 0);

        if (val == ID_ActiveMotionPlus)
            current_ext = ExtensionController.MOTIONPLUS;
        else if (val == ID_ActiveMotionPlus_Nunchuck)
            current_ext = ExtensionController.MOTIONPLUS_NUNCHUCK;
        else if (val == ID_ActiveMotionPlus_Classic)
            current_ext = ExtensionController.MOTIONPLUS_CLASSIC;
        else if (val == ID_ClassicPro)
            current_ext = ExtensionController.CLASSIC_PRO;
        else if (val == ID_Nunchuck)
            current_ext = ExtensionController.NUNCHUCK;
        else if (val == ID_Classic)
            current_ext = ExtensionController.CLASSIC;
        else
            current_ext = ExtensionController.NONE;
    }
}
}
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Text;
using WiimoteApi;

public class WiimoteDemo : MonoBehaviour {

    public WiimoteModel model;
    public RectTransform[] ir_dots;

    public bool UseCalibratedAccel = false;

    private float last_update_time = 0;
    private Wiimote wiimote;

	// Use this for initialization
	void Start () {
	
	}

    private Vector3 velocity = Vector3.zero;

	// Update is called once per frame
	void Update () {
        if (!WiimoteManager.HasWiimote()) { velocity = Vector3.zero; return; }

        int ret;
        do
        {
            ret = WiimoteManager.ReadWiimoteData();
        } while (ret > 0);

        wiimote = WiimoteManager.State;

        model.a.enabled = wiimote.a;
        model.b.enabled = wiimote.b;
        model.one.enabled = wiimote.one;
        model.two.enabled = wiimote.two;
        model.d_up.enabled = wiimote.d_up;
        model.d_down.enabled = wiimote.d_down;
        model.d_left.enabled = wiimote.d_left;
        model.d_right.enabled = wiimote.d_right;
        model.plus.enabled = wiimote.plus;
        model.minus.enabled = wiimote.minus;
        model.home.enabled = wiimote.home;

        if (ir_dots.Length < 4) return;

        for (int i = 0; i < 4; i++)
        {
            float x = (float)wiimote.ir[i, 0] / 1023f;
            float y = (float)wiimote.ir[i, 1] / 767f;
            if (x == -1 || y == -1) {
                ir_dots[i].anchorMin = new Vector2(0, 0);
                ir_dots[i].anchorMax = new Vector2(0, 0);
            }

            float ui_dot_size = (float)wiimote.ir[i, 2] / 15f * 50f;
            if(wiimote.ir[i,2] != -1)
                ir_dots[i].sizeDelta = new Vector2(ui_dot_size, ui_dot_size);
            else
                ir_dots[i].sizeDelta = new Vector2(20, 20);

            ir_dots[i].anchorMin = new Vector2(x, y);
            ir_dots[i].anchorMax = new Vector2(x, y);
        }
	}

    void OnGUI()
    {
        GUILayout.Label("Wiimote Found: " + WiimoteManager.HasWiimote());
        if (GUILayout.Button("Find Wiimote"))
        {
            bool found = WiimoteManager.FindWiimote(false);
            if (found)
            {
                //WiimoteManager.SendRaw(new byte[] { (byte)WiimoteManager.InputDataType.REPORT_BUTTONS_ACCEL_IR12 });
                //int gucci = WiimoteManager.SendPlayerLED(true, false, false, false);
                //WiimoteManager.SetupIRCamera();
                //WiimoteManager.SendDataReportMode(WiimoteManager.InputDataType.REPORT_BUTTONS);
            }
        }

        if (GUILayout.Button("Cleanup"))
            WiimoteManager.Cleanup();

        for (int x = 0; x < 4;x++ )
            if (GUILayout.Button("LED Test "+x))
                WiimoteManager.SendPlayerLED(x == 0, x == 1, x == 2, x == 3);

        if(GUILayout.Button("Set Report: Button/Accel"))
            WiimoteManager.SendDataReportMode(WiimoteManager.InputDataType.REPORT_BUTTONS_ACCEL);

        if (GUILayout.Button("Request Status Report"))
            WiimoteManager.SendStatusInfoRequest();

        if(GUILayout.Button("IR Setup Sequence"))
            WiimoteManager.SetupIRCamera(WiimoteManager.IRDataType.BASIC);

        for (int x = 0; x < 3; x++)
        {
            AccelCalibrationStep step = (AccelCalibrationStep)x;
            if (GUILayout.Button("Calibrate Accel: " + step.ToString()))
                WiimoteManager.State.CalibrateAccel(step);
        }

        if (GUILayout.Button("Print Calibration Data"))
        {
            StringBuilder str = new StringBuilder();
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    str.Append(WiimoteManager.State.accel_calib[y, x]).Append(" ");
                }
                str.Append("\n");
            }
            Debug.Log(str.ToString());
        }

        if (wiimote != null && wiimote.extension != null && wiimote.current_ext == ExtensionController.NUNCHUCK)
        {
            NunchuckData data = new NunchuckData();
            data.InterpretExtensionData(wiimote.extension);
            GUILayout.Label("Nunchuck Stick: " + data.stick[0] + ", " + data.stick[1]);
        }

    }

    void OnDrawGizmos()
    {
        if (wiimote == null) return;

        float accel_x;
        float accel_y;
        float accel_z;

        if (UseCalibratedAccel)
        {
            float[] accel = wiimote.GetCalibratedAccelData();
            accel_x = -accel[0];
            accel_y =  accel[2];
            accel_z = -accel[1];
        }
        else
        {
            accel_x = -(float)wiimote.accel[0] / 128f;
            accel_y =  (float)wiimote.accel[2] / 128f;
            accel_z = -(float)wiimote.accel[1] / 128f;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawLine(model.rot.position, model.rot.position + model.rot.rotation*new Vector3(accel_x,-accel_y,accel_z)*2);

        //Gizmos.color = Color.red;
        //Gizmos.DrawLine(model.rot.position, model.rot.position + Vector3.right * accel_x * 2);
        //Gizmos.color = Color.green;
        //Gizmos.DrawLine(model.rot.position, model.rot.position + Vector3.up * accel_y * 2);
        //Gizmos.color = Color.blue;
        //Gizmos.DrawLine(model.rot.position, model.rot.position + Vector3.forward * accel_z * 2);
    }

    [System.Serializable]
    public class WiimoteModel
    {
        public Transform rot;
        public Renderer a;
        public Renderer b;
        public Renderer one;
        public Renderer two;
        public Renderer d_up;
        public Renderer d_down;
        public Renderer d_left;
        public Renderer d_right;
        public Renderer plus;
        public Renderer minus;
        public Renderer home;
    }

}

using UnityEngine;
using System.Collections;
using WiimoteApi;

public class WiimoteDemo : MonoBehaviour {

    public WiimoteModel model;

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
            WiimoteManager.SetupIRCamera();

    }

    void OnDrawGizmos()
    {
        if (wiimote == null) return;
        
        float accel_x = -(float)wiimote.accel[0] / 128f;
        float accel_y =  (float)wiimote.accel[2] / 128f;
        float accel_z = -(float)wiimote.accel[1] / 128f;

        Gizmos.color = Color.cyan;
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

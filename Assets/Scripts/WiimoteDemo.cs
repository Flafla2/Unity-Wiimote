using UnityEngine;
using System.Collections;
using WiimoteApi;

public class WiimoteDemo : MonoBehaviour {

    public float wiimote_update_interval = 0.005f;

    private float last_update_time = 0;
    private Wiimote wiimote;

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
        if (!WiimoteManager.HasWiimote()) return;

        if (Time.time - last_update_time > wiimote_update_interval)
        {
            last_update_time = Time.time;
            WiimoteManager.ReadWiimoteData();
        }

        wiimote = WiimoteManager.State;
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
                //WiimoteManager.SendDataReportMode(WiimoteManager.InputDataType.REPORT_BUTTONS_ACCEL_IR12);
            }
        }

        if (GUILayout.Button("Cleanup"))
            WiimoteManager.Cleanup();

        if(GUILayout.Button("LED Test"))
            WiimoteManager.SendPlayerLED(true, false, false, false);

        if(GUILayout.Button("Set Report: Button/Accel/IR12"))
            WiimoteManager.SendDataReportMode(WiimoteManager.InputDataType.REPORT_BUTTONS_ACCEL);

        if (GUILayout.Button("Send Status Report"))
            WiimoteManager.SendStatusInfoRequest();

        //if(GUILayout.Button("Read"))
         //   WiimoteManager.ReadWiimoteData();

    }
}

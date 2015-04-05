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
                int gucci = WiimoteManager.SendPlayerLED(true, false, true, false);
                if (gucci < 0) Debug.Log("Shit.");
                WiimoteManager.SetupIRCamera();
            }
        }

    }
}

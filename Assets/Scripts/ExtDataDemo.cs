using UnityEngine;

using WiimoteApi;
using WiimoteApi.Util;

// This demo was created for the purpose of reverse engineering the Wii U Pro Controller.  It dumps the raw extension
// Controller bits onto the screen so that the purpose of each bit can be derived more easily.
public class ExtDataDemo : MonoBehaviour {

	private Wiimote wiimote;

	private Rect windowRect = new Rect(300,20,470,300);
	
	void Update () {
		if (!WiimoteManager.HasWiimote()) { return; }

		bool n = wiimote == null;
        wiimote = WiimoteManager.Wiimotes[0];
        if(n && wiimote != null) {
        	wiimote.SendPlayerLED(true, false, false, false);
        	wiimote.SendDataReportMode(InputDataType.REPORT_EXT21);
        }

        int ret;
        do
        {
            ret = wiimote.ReadWiimoteData();
        } while (ret > 0);
	}

	void OnGUI() {
		GUI.Box(new Rect(0,0,300,Screen.height), "");

        GUILayout.BeginVertical(GUILayout.Width(300));
        GUILayout.Label("Wiimote Found: " + WiimoteManager.HasWiimote());
        if (GUILayout.Button("Find Wiimote"))
            WiimoteManager.FindWiimotes();

        if (GUILayout.Button("Cleanup"))
        {
            WiimoteManager.Cleanup(wiimote);
            wiimote = null;
        }

        GUILayout.Label("LED Test:");
        GUILayout.BeginHorizontal();
        for (int x = 0; x < 4;x++ )
            if (GUILayout.Button(""+x, GUILayout.Width(300/4)))
                wiimote.SendPlayerLED(x == 0, x == 1, x == 2, x == 3);
        GUILayout.EndHorizontal();

        if(wiimote != null && wiimote.Type == WiimoteType.PROCONTROLLER) {
        	float[] ls = wiimote.WiiUPro.GetLeftStick01();
        	float[] rs = wiimote.WiiUPro.GetRightStick01();
        	GUILayout.Label("LS: "+ls[0]+","+ls[1]);
        	GUILayout.Label("RS: "+rs[0]+","+rs[1]);
        }

        GUILayout.EndVertical();

        if (wiimote != null)
            windowRect = GUI.Window(0, windowRect, DataWindow, "Data");
	}

	private Vector2 scrollPosition = Vector2.zero;

	void DataWindow(int id) {
		ReadOnlyArray<byte> data = wiimote.RawExtension;

		GUILayout.BeginVertical(GUILayout.Width(470), GUILayout.Height(300));
		GUILayout.Space(20);

		GUILayout.BeginHorizontal(GUILayout.Height(25));
		GUILayout.Space(10);
		GUILayout.Label("##", GUILayout.Width(40));
		GUILayout.Label("Val", GUILayout.Width(40));
		for(int x=7;x>=0;x--)
			GUILayout.Label(x.ToString(), GUILayout.Width(40));
		GUILayout.EndHorizontal();

		scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(240));


		for(int x=0;x<data.Length;x++) {
			byte val = data[x];

			GUILayout.BeginHorizontal(GUILayout.Height(25));
			GUILayout.Space(10);
			GUILayout.Label(x.ToString(), GUILayout.Width(40));
			GUILayout.Label(val.ToString("X2"), GUILayout.Width(40));
			byte bit = (byte)0x80;
			for(int i = 0; i < 8; i++) {
				bool flipped = (val & bit) == bit;
				GUILayout.Label(flipped ? "1" : "0", GUILayout.Width(40));

				bit = (byte)(bit >> 1);
			}
			GUILayout.EndHorizontal();
		}

		GUILayout.EndScrollView();

		GUI.DragWindow(new Rect(0, 0, 10000, 10000));
	}
}

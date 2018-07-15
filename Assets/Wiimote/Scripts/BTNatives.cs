using System.Runtime.InteropServices;
using System;
using UnityEngine;

public class BTNatives {

    public delegate void remote_connect_responder(string path, string name);

    [DllImport("hidapi")]
    public static extern IntPtr init_bt_search();

    [DllImport("hidapi")]
    public static extern void begin_wiimote_bt_search(IntPtr searcher, remote_connect_responder resp, int length);

    [DllImport("hidapi")]
    public static extern void stop_wiimote_bt_search(IntPtr searcher);

    [DllImport("hidapi")]
    public static extern void free_bt_search(IntPtr searcher);

    [DllImport("hidapi")]
    public static extern bool is_searching(IntPtr searcher);

    [DllImport("hidapi")]
    public static extern string get_debug_log(IntPtr searcher);


}

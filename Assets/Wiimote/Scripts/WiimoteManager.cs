using UnityEngine;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Runtime.InteropServices;

namespace WiimoteApi
{

    public class WiimoteManager
    {
        private const ushort vendor_id_wiimote = 0x057e;
        private const ushort product_id_wiimote = 0x0306;
        private const ushort product_id_wiimoteplus = 0x0330;

        /// A list of all currently connected Wii Remotes.
        public static List<Wiimote> Wiimotes { get { return _Wiimotes; } }
        private static List<Wiimote> _Wiimotes = new List<Wiimote>();

        /// If true, WiimoteManager and Wiimote will write data reports and other debug
        /// messages to the console.  Any incorrect usages / errors will still be reported.
        public static bool Debug_Messages = false;

        /// The minimum time, in milliseconds, between data report writes.  This prevents
        /// WiimoteApi from attempting to write faster than most bluetooth drivers can handle.
        ///
        /// If you attempt to write at a rate faster than this, the extra write requests will
        /// be queued up and written to the Wii Remote after the delay is up.
        public static int MaxWriteFrequency = 20; // In ms

        /// \brief This event is called whenever WiimoteApi encounters a new Wiimote.  Returns the newly-connected
        ///        Wiimote object.
        /// \warning This is NOT called by the main thread, so be careful about multithreading.
        /// 
        /// The Wiimote connection could be a result of Wiimote::FindWiimotes or Wiimote::BeginWiimoteScan.
        public static event Action<Wiimote> OnWiimoteConnect;

        /// \brief True if WiimoteApi is currently scanning for Wii Remotes.
        /// \sa WiimoteManager::BeginWiimoteScan
        public static bool IsScanningForWiimotes
        {
            get { return WiimoteScanThread != null; }
        }

        /// \brief True if WiimoteApi is currently attempting to pair with new Wii Remotes.
        /// \sa WiimoteManager::BeginPairingWiimotes
        public static bool IsPairingWithWiimotes
        {
            get
            {
#if UNITY_STANDALONE_OSX
                return SyncHandle != IntPtr.Zero && BTNatives.is_searching(SyncHandle);
#else
                return false;
#endif
            }
        }

        private static Queue<WriteQueueData> WriteQueue;

        private static IntPtr SyncHandle = IntPtr.Zero;

        // ------------- RAW HIDAPI INTERFACE ------------- //

        /// \brief Attempts to find connected Wii Remotes, Wii Remote Pluses or Wii U Pro Controllers
        /// \return If any new remotes were found.
        public static bool FindWiimotes()
        {
            bool ret = _FindWiimotes(WiimoteType.WIIMOTE);
            ret = ret || _FindWiimotes(WiimoteType.WIIMOTEPLUS);
            return ret;
        }

        private static bool _FindWiimotes(WiimoteType type)
        {
            //if (hidapi_wiimote != IntPtr.Zero)
            //    HIDapi.hid_close(hidapi_wiimote);

            ushort vendor = 0;
            ushort product = 0;

            if (type == WiimoteType.WIIMOTE)
            {
                vendor = vendor_id_wiimote;
                product = product_id_wiimote;
            }
            else if (type == WiimoteType.WIIMOTEPLUS || type == WiimoteType.PROCONTROLLER)
            {
                vendor = vendor_id_wiimote;
                product = product_id_wiimoteplus;
            }

            IntPtr ptr = HIDapi.hid_enumerate(vendor, product);
            IntPtr cur_ptr = ptr;

            if (ptr == IntPtr.Zero)
                return false;

            hid_device_info enumerate = (hid_device_info)Marshal.PtrToStructure(ptr, typeof(hid_device_info));

            bool found = false;

            while (cur_ptr != IntPtr.Zero)
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
                    IntPtr handle = HIDapi.hid_open_path(enumerate.path);

                    if (handle == IntPtr.Zero)
                    {
                        Debug.LogError("ERROR: hid_open_path returned NULL!");
                        cur_ptr = enumerate.next;
                        if (cur_ptr != IntPtr.Zero)
                            enumerate = (hid_device_info)Marshal.PtrToStructure(cur_ptr, typeof(hid_device_info));
                        continue;
                    }

                    WiimoteType trueType = type;

                    // Wii U Pro Controllers have the same identifiers as the newer Wii Remote Plus except for product
                    // string (WHY nintendo...)
                    if (enumerate.product_string.EndsWith("UC", false, null))
                        trueType = WiimoteType.PROCONTROLLER;

                    remote = new Wiimote(handle, enumerate.path, trueType);

                    if (Debug_Messages)
                        Debug.Log("Found New Remote: " + remote.hidapi_path);

                    Wiimotes.Add(remote);
                    found = true;

                    remote.SendDataReportMode(InputDataType.REPORT_BUTTONS);
                    remote.SendStatusInfoRequest();

                    if(OnWiimoteConnect != null)
                        OnWiimoteConnect(remote);
                }

                cur_ptr = enumerate.next;
                if (cur_ptr != IntPtr.Zero)
                    enumerate = (hid_device_info)Marshal.PtrToStructure(cur_ptr, typeof(hid_device_info));
            }

            HIDapi.hid_free_enumeration(ptr);

            return found;
        }

        private static Thread WiimoteScanThread;
        private static ManualResetEvent ScanTimeoutEvent;
        private static void ScanThread(uint scanInterval)
        {
            while (true)
            {
                FindWiimotes();

#if UNITY_STANDALONE_OSX
                if(Debug_Messages) {
                    if (SyncHandle != IntPtr.Zero)
                    {
                        string log = BTNatives.get_debug_log(SyncHandle);
                        if (log != null && !log.Equals(""))
                            Debug.Log("[Bluetooth Native Log]\n" + log);
                    }
                }
#endif

                if (ScanTimeoutEvent == null || ScanTimeoutEvent.WaitOne((int)scanInterval))
                    break;
            }
        }
        /// \brief Begins scanning for Wii Remotes with the given polling interval.  Wiimote::OnWiimoteConnect is called
        ///        when new Wii Remotes are found.
        /// \param scanInterval Scanning interval in ms
        /// \warning The Wiimote::OnWiimoteConnect callback is NOT called by the caller thread (it is called by a
        ///          separate search thread).
        /// \sa WiimoteManager::EndWiimoteScan()
        /// \sa WiimoteManager::OnWiimoteConnect
        public static bool BeginWiimoteScan(uint scanInterval = 500)
        {
            if (WiimoteScanThread != null)
                return false;

            ScanTimeoutEvent = new ManualResetEvent(false);
            WiimoteScanThread = new Thread(() => ScanThread(scanInterval));
            WiimoteScanThread.Start();

            return true;
        }

        /// \brief Stops searching for Wii Remotes and ends the polling thread.
        /// \sa WiimoteManager::BeginWiimoteScan()
        public static bool EndWiimoteScan()
        {
            if (WiimoteScanThread == null)
                return false;

            ScanTimeoutEvent.Set();
            WiimoteScanThread.Join(100);

            WiimoteScanThread = null;
            ScanTimeoutEvent = null;

            return true;
        }

        /// \brief Calling this is equivalent to pressing the "SYNC" button on a real Wii.  WiimoteApi interfaces with
        ///        native bluetooth drivers to search for discoverable Wiimotes.
        /// \param duration Duration of the search in seconds
        /// \warning Currently Bluetooth Pairing is only available on MacOS.
        /// \warning It is not recommended to have this pair "always on" (i.e. calling \c BeginPairingWiimotes(1000000) )
        ///          because it causes strain on the hardware and operating systems don't like that.  For example,
        ///          macOS will throttle bluetooth performance if the bluetooth API is being abused.
        /// 
        /// Instruct players to press the SYNC button on the back of previously-disconnected Wii Remotes after calling
        /// this.  After pairing a remote, it can be reconnected simply by pressing 1 and 2 simultaneously -- your OS
        /// should automatically find the remote and reconnect.
        public static void BeginPairingWiimotes(int duration = 10)
        {
            //TODO: Use something more generic than Unity defines
#if UNITY_STANDALONE_OSX
            if (SyncHandle == IntPtr.Zero)
            {
                SyncHandle = BTNatives.init_bt_search();
                if (SyncHandle == IntPtr.Zero)
                    throw new NullReferenceException("Unable to initialize bluetooth search");
            }

            if (BTNatives.is_searching(SyncHandle))
                throw new InvalidOperationException("Tried to begin pairing when pairing is already in progress!");

            BTNatives.begin_wiimote_bt_search(SyncHandle, (a, b) => { }, duration);
#else
            throw new NotImplementedException("Wiimote pairing is only available on MacOS.");
#endif
        }

        /// \brief Ends pairing scan began by Wiimote::BeginPairingWiimotes.
        /// \warning Currently Bluetooth Pairing is only available on MacOS.
        /// \sa WiimoteManager::CancelPairingWiimotes()
        public static void CancelPairingWiimotes()
        {
            //TODO: Use something more generic than Unity defines
#if UNITY_STANDALONE_OSX
            if (SyncHandle == IntPtr.Zero || !BTNatives.is_searching(SyncHandle))
                throw new InvalidOperationException("Tried to cancel pairing when pairing was not in progress!");

            BTNatives.stop_wiimote_bt_search(SyncHandle);
#else
            throw new NotImplementedException("Wiimote pairing is only available on MacOS.");
#endif
        }

        /// \brief Disables the given \c Wiimote by closing its bluetooth HID connection.  Also removes the remote from Wiimotes
        /// \param remote The remote to cleanup
        public static void Cleanup(Wiimote remote)
        {
            if (remote != null)
    		{
    			if (remote.hidapi_handle != IntPtr.Zero)
    				HIDapi.hid_close (remote.hidapi_handle);

    			Wiimotes.Remove (remote);
    		}
        }

        /// \return If any Wii Remotes are connected and found by FindWiimote
        public static bool HasWiimote()
        {
            return !(Wiimotes.Count <= 0 || Wiimotes[0] == null || Wiimotes[0].hidapi_handle == IntPtr.Zero);
        }

        /// \brief Sends RAW DATA to the given bluetooth HID device.  This is essentially a wrapper around HIDApi.
        /// \param hidapi_wiimote The HIDApi device handle to write to.
        /// \param data The data to write.
        /// \sa Wiimote::SendWithType(OutputDataType, byte[])
        /// \warning DO NOT use this unless you absolutely need to bypass the given Wiimote communication functions.
        ///          Use the functionality provided by Wiimote instead.
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
                        if (res == -1) 
                            Debug.LogError("HidAPI reports error " + res + " on write: " + Marshal.PtrToStringUni(HIDapi.hid_error(wqd.pointer)));
                        else if (Debug_Messages) 
                            Debug.Log("Sent " + res + "b: [" + wqd.data[0].ToString("X").PadLeft(2, '0') + "] " + BitConverter.ToString(wqd.data, 1));
                    }
                }
                Thread.Sleep(MaxWriteFrequency);
            }
        }

        /// \brief Attempts to recieve RAW DATA to the given bluetooth HID device.  This is essentially a wrapper around HIDApi.
        /// \param hidapi_wiimote The HIDApi device handle to write to.
        /// \param buf The data to write.
        /// \sa Wiimote::ReadWiimoteData()
        /// \warning DO NOT use this unless you absolutely need to bypass the given Wiimote communication functions.
        ///          Use the functionality provided by Wiimote instead.
        public static int RecieveRaw(IntPtr hidapi_wiimote, byte[] buf)
        {
            if (hidapi_wiimote == IntPtr.Zero) return -2;

            HIDapi.hid_set_nonblocking(hidapi_wiimote, 1);
            int res = HIDapi.hid_read(hidapi_wiimote, buf, new UIntPtr(Convert.ToUInt32(buf.Length)));

            return res;
        }

        private class WriteQueueData {
            public IntPtr pointer;
            public byte[] data;
        }
    }
} // namespace WiimoteApi
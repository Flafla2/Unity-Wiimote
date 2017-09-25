using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace WiimoteApi
{
    public class SpeakerData : WiimoteData
    {
        private Thread audioThread;

        public SpeakerData(Wiimote Owner) : base(Owner)
        {
        }

        public void Init()
        {
            if (_Initialized)
                return;
            Enabled = true;
            Muted = true;
            Owner.SendRegisterWriteRequest(RegisterType.CONTROL, 0xa20009, new byte[] { 0x01 });
            Owner.SendRegisterWriteRequest(RegisterType.CONTROL, 0xa20001, new byte[] { 0x08 });
            SendConfigData();
            Owner.SendRegisterWriteRequest(RegisterType.CONTROL, 0xa20008, new byte[] { 0x01 });
            Muted = false;
            _Initialized = true;
            Debug.Log("Speaker Init");
        }

        public void SendConfigData()
        {
            //Is this even right?
            byte[] config = new byte[] { 0x00, 0x40, 0x70, 0x17, 0x60, 0x00, 0x00 };
            for (int i = 0; i < 7; i++)
            {
                Owner.SendRegisterWriteRequest(RegisterType.CONTROL, 0xa20001+i, new byte[] { config[i] });
            }
        }

        private bool _Initialized;
        public bool Initialized
        {
            get
            {
                return Initialized;
            }
        }

        private bool _Enabled;
        public bool Enabled
        {
            get
            {
                return _Enabled;
            }
            set
            {
                _Enabled = value;
                byte[] mask = new byte[] { (byte)(_Enabled ? 0x04 : 0x00) };
                lock (this)
                {
                    Owner.SendWithType(OutputDataType.SPEAKER_ENABLE, mask);
                }

            }
        }

        private bool _Muted;
        public bool Muted
        {
            get
            {
                return _Muted;
            }
            set
            {
                _Muted = value;
                byte[] mask = new byte[] { (byte)(_Muted ? 0x04 : 0x00) };
                lock (this)
                {
                    Owner.SendWithType(OutputDataType.SPEAKER_MUTE, mask);
                }
            }
        }

        public int Play(AudioClip audioClip)
        {
            Init();

            if (IsPlaying)
                return 0;
            byte[] buffer = GetAudioClipData(audioClip);
            return Play(buffer);
        }

        public int Play(byte[] buffer)
        {
            Init();

            if (IsPlaying)
                return 0;

            audioThread = new Thread(AudioThreadFunc);
            audioThread.IsBackground = true;
            audioThread.Start(buffer);
            return 0;
        }

        private void AudioThreadFunc(object buffObj)
        {
            byte[] buffer = (byte[])buffObj;
            MemoryStream stream = new MemoryStream(buffer);
            byte[] chuck = new byte[21];
            int readBytes = 0;
            while ((readBytes = stream.Read(chuck, 1, chuck.Length - 1)) > 0)
            {
                //length
                chuck[0] = (byte)(readBytes << 3);
                //padding
                if (readBytes < chuck.Length - 1)
                {
                    for (int i = readBytes + 1; i < chuck.Length; i++)
                    {
                        chuck[i] = 127;
                    }
                }
                //send
                lock (this)
                {
                    Owner.SendWithType(OutputDataType.SPEAKER_DATA, chuck);
                }
                Thread.Sleep(10);
            }
        }

        private bool IsPlaying
        {
            get
            {
                return audioThread != null && audioThread.IsAlive;
            }
        }

        //Converts audioClip data to mono/8bits/2000hz data
        //TODO: actually converts the data
        private static byte[] GetAudioClipData(AudioClip audioClip)
        {
            if (audioClip.channels != 1 || audioClip.frequency != 2000)
            {
                throw new NotSupportedException(string.Format("Only 2000hz mono audio.(channels:{0};frequency:{1};)", audioClip.channels, audioClip.frequency));
            }
            float[] samples = new float[audioClip.samples];
            audioClip.GetData(samples, 0);

            //float to byte convertion : 16 to 8 bits
            //just for the sake of testing
            byte[] buffer = new byte[samples.Length];
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (byte)(((samples[i] + 1) * 255) / 2);

            }
            return buffer;
        }

        public override bool InterpretData(byte[] data)
        {
            return false;
        }
    }
}



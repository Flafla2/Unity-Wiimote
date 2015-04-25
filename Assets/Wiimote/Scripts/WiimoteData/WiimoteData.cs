using UnityEngine;
using System.Collections;

namespace WiimoteApi
{
    public abstract class WiimoteData
    {
        protected Wiimote Owner;

        public WiimoteData(Wiimote Owner)
        {
            this.Owner = Owner;
        }

        public abstract bool InterpretData(byte[] data);
        
    }
}
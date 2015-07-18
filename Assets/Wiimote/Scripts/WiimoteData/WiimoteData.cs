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

        /// \brief Interprets raw byte data reported by the Wii Remote.  The format of the actual bytes
        ///        passed to this depends on the Wii Remote's current data report mode and the type
        ///        of data being passed.
        /// \sa Wiimote::ReadWiimoteData()
        public abstract bool InterpretData(byte[] data);
        
    }
}
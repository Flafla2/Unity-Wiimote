using WiimoteApi;

namespace WiimoteApi.Internal {
    public class RegisterReadData
    {
        public RegisterReadData(int Offset, int Size, ReadResponder Responder)
        {
            _Offset = Offset;
            _Size = Size;
            _Buffer = new byte[Size];
            _ExpectedOffset = Offset;
            _Responder = Responder;
        }

        public int ExpectedOffset
        {
            get { return _ExpectedOffset; }
        }
        private int _ExpectedOffset;

        public byte[] Buffer
        {
            get { return _Buffer; }
        }
        private byte[] _Buffer;

        public int Offset
        {
            get { return _Offset; }
        }
        private int _Offset;

        public int Size
        {
            get { return _Size; }
        }
        private int _Size;

        private ReadResponder _Responder;

        public bool AppendData(byte[] data)
        {
            int start = _ExpectedOffset - _Offset;
            int end = start + data.Length;

            if (end > _Buffer.Length)
                return false;

            for (int x = start; x < end; x++)
            {
                _Buffer[x] = data[x - start];
            }

            _ExpectedOffset += data.Length;

            if (_ExpectedOffset >= _Offset + _Size)
                _Responder(_Buffer);

            return true;
        }
    }
}

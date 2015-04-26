namespace WiimoteApi.Util
{
    public class ReadOnlyArray<T>
    {
        private T[] _data;

        public ReadOnlyArray(T[] data)
        {
            _data = data;
        }

        public T this[int x]
        {
            get
            {
                return _data[x];
            }
        }
    }

    public class ReadOnlyMatrix<T>
    {
        private T[,] _data;

        public ReadOnlyMatrix(T[,] data)
        {
            _data = data;
        }

        public T this[int x, int y]
        {
            get
            {
                return _data[x, y];
            }
        }
    }
}
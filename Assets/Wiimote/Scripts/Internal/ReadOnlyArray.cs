namespace WiimoteApi.Util
{
    /// A simple, immutable, read only array.  This is used for basic
    /// data encapsulation in many of the WiimoteData subclasses.
    public class ReadOnlyArray<T>
    {
        private T[] _data;

        public int Length
        {
            get { return _data.Length; }
        }

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

    /// A simple, immutable, read only matrix (2-D array).  This is used for basic
    /// data encapsulation in many of the WiimoteData subclasses.
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

        /// Returns the length of this array in the given dimension.
        public int GetLength(int dim) {
            return _data.GetLength(dim);
        }
    }
}
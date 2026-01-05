namespace Networking.Utils
{
    public class IdIterator
    {
        public int Id { get; private set; }
        private readonly int _startId;

        /// <summary>
        /// Starting from 1 as 0 is bad for definition as it can be fallback value especially in serialization
        /// </summary>
        /// <param name="startId"></param>
        public IdIterator(int startId = 1)
        {
            _startId = startId;
            Id = startId;
        }

        public int Next()
        {
            return Id++;
        }

        public void Reset()
        {
            Id = _startId;
        }
    }
}
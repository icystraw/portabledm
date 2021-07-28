using System;

namespace partialdownloadgui.Components
{
    public class SpeedCalculator
    {
        private DateTime dt1 = DateTime.MinValue;
        private DateTime dt2 = DateTime.MinValue;
        private long bytes1 = (-1);
        private long bytes2 = (-1);

        public void RegisterBytes(long bytes)
        {
            if (bytes < 0) throw new ArgumentException("bytes cannot be less than zero.", nameof(bytes));
            if (dt1 == DateTime.MinValue && dt2 == DateTime.MinValue)
            {
                dt1 = DateTime.Now;
                bytes1 = bytes;
            }
            else if (dt2 == DateTime.MinValue)
            {
                dt2 = DateTime.Now;
                bytes2 = bytes;
            }
            else
            {
                dt1 = dt2;
                dt2 = DateTime.Now;
                bytes1 = bytes2;
                bytes2 = bytes;
            }
        }

        public long GetSpeed()
        {
            if (dt1 == DateTime.MinValue || dt2 == DateTime.MinValue) return 0;
            return (long)(((decimal)bytes2 - (decimal)bytes1) / (((decimal)dt2.Ticks - (decimal)dt1.Ticks) / 10000000m));
        }
    }
}

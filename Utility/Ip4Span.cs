using System;

namespace mktool.Utility
{
    class Ip4Span
    {
        public uint Start { get; }
        public uint End { get; }

        private Ip4Span(uint start, uint end)
        {
            Start = start;
            End = end;
        }

        private static readonly Ip4Span _default = new Ip4Span(0, 0);

        public static Ip4Span Create(uint start, uint end)
        {
            if (start > end) throw new ArgumentOutOfRangeException(nameof(start), "Begin cannot me more than end");
            return new Ip4Span(start, end);
        }

        public bool IsOverlapped(Ip4Span other)
        {
            return Start <= other.End && End >= other.Start;
        }

        public Ip4Span Merge(Ip4Span other)
        {
            if (!IsOverlapped(other)) throw new ArgumentOutOfRangeException(nameof(other), "Spans must overlap");
            return new Ip4Span(Math.Min(Start, other.Start), Math.Max(End, other.End));
        }
        
        public bool TryMerge(Ip4Span other, out Ip4Span mergedItem)
        {
            mergedItem = _default;
            if (!IsOverlapped(other)) return false;
            mergedItem =  new Ip4Span(Math.Min(Start, other.Start), Math.Max(End, other.End));
            return true;
        }
    }
}

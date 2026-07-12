namespace Pug.Sqetch;

public class Range<T> where T : IComparable<T>
{
	public T Start { get; init; }
	public T End { get; init; }

	public bool IsSmallerThan( T other ) => End.CompareTo( other ) < 0;
	public bool IsLargerThan( T other ) => Start.CompareTo( other ) > 0;
}

public static class RangeExtensions
{
	public static bool IsWithin<T>( this T other, Range<T> range ) where T : IComparable<T>
	{
		return range.Start.CompareTo( other ) > -1 &&  range.End.CompareTo( other ) < 1;
	}
}
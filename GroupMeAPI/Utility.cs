using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GroupMeAPI
{
	internal static class Utility
	{
		public static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
		{
			// Unix timestamp is seconds past epoch
			DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
			dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
			return dateTime;
		}
		/// <summary> Converts the date component of a <see cref="DateTime"/> object to <see cref="string"/> </summary>
		public static string DateToString(DateTime dateTime) => $"{dateTime.Month}/{dateTime.Day}/{dateTime.Year}";
		
		public static TSource? SafeMaxBy<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector, TSource? defaultValue = default)
		{
			if (source is null || !source.Any()) return defaultValue;
			else return source.MaxBy(selector);
		}
		public static TSource? SafeMinBy<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector, TSource? defaultValue = default)
		{
			if (source is null || !source.Any()) return defaultValue;
			else return source.MinBy(selector);
		}
		public static TResult? SafeMax<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector, TResult? defaultValue = default)
		{
			if (!source.Any()) return defaultValue;
			else return source.Max(selector);
		}
		public static TResult? SafeMin<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector, TResult? defaultValue = default)
		{
			if (!source.Any()) return defaultValue;
			else return source.Min(selector);
		}

	}
}

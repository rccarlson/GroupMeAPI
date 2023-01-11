using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GroupMeAPI
{
	public static class TableHelpers
	{
		public static string LeaderboardByUser<T>(string title, Group group, Message[] messages, Func<GroupMember, Message[], T> score, PadType scorePadType, int leaderboardSize = 10)
		{
			//var users = group.members;
			//var messagesByUser = users.Select(user => (user, messages: messages.Where(message => message.sender_id == user.user_id)));
			//var userAndScore = messagesByUser.Select(tuple => (tuple.user.name, score: score(tuple.user, messages)));
			//var leaderboard = userAndScore.OrderByDescending(pair => pair.score).Take(leaderboardSize);
			//return WriteLeaderboard(title, leaderboard);

			return WriteTable(title,
				group.members
				.OrderByDescending(user => score(user, messages))
				.Take(leaderboardSize)
				.Select(user => new object[]
				{
					user.name,
					score(user, messages)
				}),
				new[] { PadType.Left, scorePadType });
		}

		public static string LeaderboardByMessage<T>(string title, Group group, Message[] messages, Func<Message, T> score, int leaderboardSize = 10)
		{
			var leaderboard = messages
				.OrderByDescending(m => score(m))
				.Take(leaderboardSize);
			var tablated = leaderboard.Select((m, i) => {
				var sender = group.members.FirstOrDefault(u => u.user_id == m.user_id);
				return new object[]
				{
					$"#{i+1}",
					Utility.DateToString(m.CreatedAt),
					string.IsNullOrWhiteSpace(sender.name) ? "<User not in group>" : sender.name,
					score(m)?.ToString() ?? string.Empty,
					m.text
				};
			});

			return WriteTable(title, tablated, new[] { PadType.Left, PadType.Left, PadType.Right, PadType.Left, PadType.Right });
		}

		public enum PadType { Left, Right }
		/// <summary>
		/// Given an
		/// </summary>
		/// <param name="source">List of rows</param>
		public static string WriteTable<T>(string title, IEnumerable<IEnumerable<T>> source, IEnumerable<PadType> padTypes = null, int margin = 1, int maxWidth = 80)
		{
			StringBuilder result = new();

			// validate data completeness
			if (source is null) throw new ArgumentNullException(nameof(source));
			var numColumns = source.FirstOrDefault()?.Count() ?? 0;
			if (source.Any(row => row.Count() != numColumns)) throw new ArgumentException($"Not all rows {numColumns} elements long");
			// add padding if it is missing
			if (padTypes is null)
				padTypes = Array.Empty<PadType>();
			for (int i = padTypes.Count(); i < numColumns; i++)
				padTypes = padTypes.Append(PadType.Right);
			var padTypeArr = padTypes.ToArray();

			// calculate size of columns
			var arr = source.Select(row => row.Select(obj => obj?.ToString()).ToArray()).ToArray();
			var colWidths = new int[numColumns];
			for (int col = 0; col < numColumns; col++)
			{
				var columnMaxWidth = arr.Max(row => row[col]?.Length ?? 0);
				colWidths[col] = columnMaxWidth > maxWidth ? maxWidth : columnMaxWidth;
			}

			result.AppendLine($"---{title}---");

			foreach (var row in arr)
			{
				StringBuilder rowOutput = new();
				for (int colNum = 0; colNum < numColumns; colNum++)
				{
					var entry = row[colNum] ?? string.Empty;
					string output = entry;
					// replace newlines
					output = output.ReplaceLineEndings(" ");
					// apply trimming if needed
					if (output.Length > maxWidth) output = output[..(maxWidth - 3)] + "...";
					// apply padding
					output = padTypeArr[colNum] switch
					{
						PadType.Right => output.PadRight(colWidths[colNum]),
						PadType.Left => output.PadLeft(colWidths[colNum]),
						_ => throw new NotImplementedException(padTypeArr[colNum].ToString())
					};
					rowOutput.Append(output);
					rowOutput.Append(' ', margin);
				}
				result.AppendLine(rowOutput.ToString());
			}
			return result.ToString();
		}
		public static string WriteList(string title, IEnumerable<string> list)
		{
			StringBuilder result = new();
			result.AppendLine($"---{title}---");
			foreach (var item in list)
				result.AppendLine(item);
			return result.ToString();
		}
	}
}

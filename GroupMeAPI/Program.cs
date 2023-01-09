using System.Text;

namespace GroupMeAPI
{
	internal class Program
	{
		const string Extension = ".gm";

		static void Main(string[] args)
		{
			var api = new API();

			WriteList("Groups", api.GetGroups().Select(g=>$"{g.name}: {g.id}").Take(5));

			var mostRecentGroup = api.GetGroups().First().group_id;

			var allMessages = api.UpdateFile($"{mostRecentGroup}{Extension}", mostRecentGroup);
			var totalMessages = api.GetGroup(mostRecentGroup).messages.count;
			if (allMessages.Length != totalMessages + 8)
				Console.WriteLine($"{totalMessages - allMessages.Length} MISSING MESSAGES!!");
			else
				Console.WriteLine("All messages accounted for");
			var group = api.GetGroup(mostRecentGroup);

			var distinctEventTypes = allMessages
				.DistinctBy(m => m.@event.type)
				.Where(m=>m.@event.type is not null)
				.OrderBy(m=>m.@event.type)
				.Skip(4)
				.Select(m=>(m.@event.type, m.@event, API.GetMessageProceeding(allMessages, m).id))
				.ToList();

			Console.WriteLine($"--TOTALS--");
			Console.WriteLine($"All messages: {allMessages.Length}");
			Console.WriteLine($"System Messages: {allMessages.Where(msg => msg.system).Count()}");
			Console.WriteLine($"Polls: {allMessages.Where(m => m.attachments.Any(attachment => attachment.type == "poll")).Count()}");
			Console.WriteLine($"Platforms: "+string.Join(", ", allMessages.Select(m=>$"'{m.platform}'").Distinct()));
			Console.WriteLine($"Deleted messages: {allMessages.Count(m => m.@event.data.deleter_id is not null)}");
			Console.WriteLine();

			LeaderboardByUser("Top posters", group, allMessages, (user, messages) => messages.Count(m => m.sender_id == user.user_id));
			LeaderboardByUser("Most likes received", group, allMessages, (user, messages) =>
				messages.Where(m => m.sender_id == user.user_id).Sum(m => m.favorited_by.Length)
				);
			LeaderboardByUser("Most likes given", group, allMessages, (user, messages) =>
				messages.Count(m => m.favorited_by.Contains(user.user_id)));
			LeaderboardByUser("Likes received/given ratio", group, allMessages,
				(user, messages) => {
					float received = messages.Where(m => m.sender_id == user.user_id).Sum(m => m.favorited_by.Length);
					float given = messages.Count(m => m.favorited_by.Contains(user.user_id)); // likes given
					if (given == 0) return received;
					else return MathF.Round(received / given, 3);
				}
				);
			LeaderboardByUser("Average likes received per message", group, allMessages,
			 (user, messages) => {
				 var sentByUser = messages.Where(m => m.sender_id == user.user_id).ToArray();
				 float likesReceived = sentByUser.Sum(m => m.favorited_by.Length);
				 return MathF.Round(likesReceived/sentByUser.Length,3);
				 }
			 );
			LeaderboardByUser("Longest inactive", group, allMessages, (user, messages) =>
				(int)SafeMin(messages.Where(m => RepresentsActivity(user, m)), m => (DateTime.Now - m.CreatedAt).TotalDays)
			);
			LeaderboardByUser("Most mentioned", group, allMessages, (user, messages) =>
				messages.Count(m => m.attachments.Any(a => a.type == "mentions" && a.user_ids.Contains(user.user_id)))
			);
			LeaderboardByUser("Most distinct usernames", group, allMessages, (user, messages) =>
				messages
				.Where(m=>m.@event.type == "membership.nickname_changed")
				.Where(m=>m.@event.data.user.id.ToString() == user.user_id)
				.Select(m=>m.@event.data.name)
				.Distinct()
				.Count()
			);

			var jackNames = allMessages
				.Where(m => m.@event.type == "membership.nickname_changed")
				.Where(m => m.@event.data.user.id == 13417292)
				.Select(m => m.@event.data.name)
				.Distinct();

			var shawnInteractions = allMessages.Where(m => RepresentsActivity(group.members.First(m => m.name.Contains("Shawn")), m)).ToArray();

			LeaderboardByMessage("Most liked messages", group, allMessages, m => m.favorited_by.Length, 20);
		}

		/// <summary>
		/// Returns true if the <paramref name="message"/> represents an interaction by the <paramref name="user"/>
		/// </summary>
		static bool RepresentsActivity(GroupMember user, Message message)
		{
			var id = user.user_id;
			if(message.sender_id == id) return true; // sent by user
			if(message.favorited_by.Contains(id)) return true; // liked by user
			if (message.@event.data.added_users?.Any(u => u.id.ToString() == user.id) ?? false) return true; // user joined
			// todo: user rejoined
			return false;
		}
		static void LeaderboardByUser<T>(string title, Group group, Message[] messages, Func<GroupMember, Message[], T> score, int leaderboardSize = 10)
		{
			var users = group.members;
			var messagesByUser = users.Select(user => (user, messages: messages.Where(message => message.sender_id == user.user_id)));
			var userAndScore = messagesByUser.Select(tuple => (tuple.user.name, score: score(tuple.user, messages)));
			var leaderboard = userAndScore.OrderByDescending(pair => pair.score).Take(leaderboardSize);
			WriteLeaderboard(title, leaderboard);

		}

		static void LeaderboardByMessage<T>(string title, Group group, Message[] messages, Func<Message, T> score, int leaderboardSize = 10)
		{
			var leaderboard = messages
				.OrderByDescending(m => score(m))
				.Take(leaderboardSize);
			var tablated = leaderboard.Select((m,i) => {
				var sender = group.members.FirstOrDefault(u => u.user_id == m.user_id);
				return new object[]
				{
					$"#{i+1}",
					m.CreatedAt,
					string.IsNullOrWhiteSpace(sender.name) ? "<User not in group>" : sender.name,
					score(m)?.ToString() ?? string.Empty,
					m.text
				};
			});

			WriteTable(title, tablated, new[] { PadType.Left, PadType.Left, PadType.Right, PadType.Left, PadType.Right });
		}

		enum PadType { Left, Right }
		/// <summary>
		/// Given an
		/// </summary>
		/// <param name="source">List of rows</param>
		static void WriteTable<T>(string title, IEnumerable<IEnumerable<T>> source, IEnumerable<PadType> padTypes = null, int margin = 1, int maxWidth = 80)
		{
			// validate data completeness
			if (source is null) throw new ArgumentNullException(nameof(source));
			var numColumns = source.FirstOrDefault()?.Count() ?? 0;
			if (source.Any(row => row.Count() != numColumns)) throw new ArgumentException($"Not all rows {numColumns} elements long");
			// add padding if it is missing
			if (padTypes is null)
				padTypes = Array.Empty<PadType>();
			for(int i = padTypes.Count(); i<numColumns; i++)
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

			Console.WriteLine($"---{title}---");

			foreach(var row in arr)
			{
				StringBuilder rowOutput = new();
				for(int colNum = 0; colNum < numColumns; colNum++)
				{
					var entry = row[colNum] ?? string.Empty;
					string output = entry;
					// replace newlines
					output = output.ReplaceLineEndings(" ");
					// apply trimming if needed
					if (output.Length > maxWidth) output = output[..(maxWidth - 3)]+"...";
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
				Console.WriteLine(rowOutput);
			}

		}
		static void WriteLeaderboard<T>(string title, IEnumerable<(string, T)> leaderboard)
		{
			var col1Width = leaderboard.Max(pair => pair.Item1.Length);
#pragma warning disable CS8602 // Dereference of a possibly null reference.
			var col2Width = leaderboard.Max(pair => pair.Item2?.ToString().Length ?? 0);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
			Type[] padLeftTypes = new[] { typeof(int) };
			Console.WriteLine($"---{title}---");
			foreach (var (user, score) in leaderboard)
			{
				StringBuilder sb = new();
				sb.Append(user.PadLeft(col1Width));
				sb.Append(": ");
#pragma warning disable CS8602 // Dereference of a possibly null reference.
				if (score is null)
					sb.Append(' ', col2Width);
				else if(padLeftTypes.Contains(typeof(T)))
					sb.Append(score.ToString().PadLeft(col2Width));
				else
					sb.Append(score.ToString().PadRight(col2Width));
#pragma warning restore CS8602 // Dereference of a possibly null reference.
				Console.WriteLine(sb);
			}
			Console.WriteLine();
		}
		static void WriteList(string title, IEnumerable<string> list)
		{
			Console.WriteLine($"---{title}---");
			foreach (var item in list)
				Console.WriteLine(item);
		}
		static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
		{
			// Unix timestamp is seconds past epoch
			DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
			dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
			return dateTime;
		}
		public static TResult? SafeMax<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, TResult> selector, TResult defaultValue = default)
		{
			if (!source.Any()) return defaultValue;
			else return source.Max(selector);
		}
		public static TResult SafeMin<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, TResult> selector, TResult defaultValue = default)
		{
			if (!source.Any()) return defaultValue;
			else return source.Min(selector);
		}

	}
}
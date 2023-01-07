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

			var systemMessages = allMessages.Where(msg => msg.system).ToArray();
			var pollMessages = allMessages.Where(m => m.attachments.Any(attachment => attachment.type == "poll")).ToArray();
			var events = allMessages.Where(msg => msg.@event.type is not null).ToArray();
			var joinTimes = group.members
				.Select(u => (
				user: u, 
				joindate: allMessages
					.Where(msg => msg.@event.type is not null && msg.@event.type.Contains("membership.announce.added"))
					.MaxBy(msg => msg.created_at)
				)
			);
			var eventTypes = allMessages.Select(m => m.@event.type).Where(x => x is not null).Distinct().OrderBy(x=>x).ToArray();
			var membershipAnnounces = allMessages.Where(msg => msg.@event.type is not null && msg.@event.type.Contains("membership.announce"));

			Console.WriteLine($"--TOTALS--");
			Console.WriteLine($"All messages: {allMessages.Length}");
			Console.WriteLine($"System Messages: {systemMessages.Length}");
			Console.WriteLine($"Polls: {pollMessages.Length}");
			Console.WriteLine($"Platforms: "+string.Join(", ", allMessages.Select(m=>$"'{m.platform}'").Distinct()));
			Console.WriteLine();

			LeaderboardByUser("Top posters", group, allMessages, (user, messages) => messages.Count(m => m.sender_id == user.user_id), 10);
			LeaderboardByUser("Most likes received", group, allMessages, (user, messages) =>
				messages.Where(m => m.sender_id == user.user_id).Sum(m => m.favorited_by.Length)
				, 10);
			LeaderboardByUser("Most likes given", group, allMessages, (user, messages) =>
				messages.Count(m => m.favorited_by.Contains(user.user_id))
				, 10);
			LeaderboardByUser("Likes received/given ratio", group, allMessages,
				(user, messages) => {
					float received = messages.Where(m => m.sender_id == user.user_id).Sum(m => m.favorited_by.Length);
					float given = messages.Count(m => m.favorited_by.Contains(user.user_id)); // likes given
					if (given == 0) return received;
					else return MathF.Round(received / given, 3);
				}
				, 10);
			LeaderboardByUser("Average likes received per message", group, allMessages,
			 (user, messages) => {
				 var sentByUser = messages.Where(m => m.sender_id == user.user_id).ToArray();
				 float likesReceived = sentByUser.Sum(m => m.favorited_by.Length);
				 return MathF.Round(likesReceived/sentByUser.Length,3);
				 },
				10);
			LeaderboardByUser("Longest inactive", group, allMessages, (user, messages) =>
			{
				var sentByUser = messages.Where(m => m.sender_id == user.user_id);
				DateTime mostRecentSentMsg = SafeMax(messages.Where(m => m.sender_id == user.user_id), m => UnixTimeStampToDateTime(m.created_at));
				//if(sentByUser.Any())
				//	mostRecentSentMsg = sentByUser.Max(m => UnixTimeStampToDateTime(m.created_at));
				//else
				//	mostRecentSentMsg = DateTime.MinValue;
				//var mostRecentSentLike = messages.Where(m => m.favorited_by.Contains(user.user_id)).Max(m => UnixTimeStampToDateTime(m.created_at));

				var mostRecentActivity = new[] { /*mostRecentSentLike,*/ mostRecentSentMsg }.Min(dt => (DateTime.Now - dt).TotalDays);
				return (int)mostRecentActivity;
			});
		}
		static void LeaderboardByUser<T>(string title, Group group, Message[] messages, Func<User, Message[], T> score, int leaderboardSize = 10)
		{
			var users = group.members;
			var messagesByUser = users.Select(user => (user, messages: messages.Where(message => message.sender_id == user.user_id)));
			var userAndScore = messagesByUser.Select(tuple => (tuple.user.name, score: score(tuple.user, messages)));
			var leaderboard = userAndScore.OrderByDescending(pair => pair.score).Take(leaderboardSize);
			WriteLeaderboard(title, leaderboard);

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

	}
}
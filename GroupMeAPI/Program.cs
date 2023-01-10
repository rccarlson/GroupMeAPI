using System.Diagnostics;
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
			var group = api.GetGroup(mostRecentGroup);

			var distinctEventTypes = allMessages
				.DistinctBy(m => m.@event.type)
				.Where(m=>m.@event.type is not null)
				.OrderBy(m=>m.@event.type)
				.Skip(4)
				.Select(m=>(m.@event.type, m.@event, API.GetMessageProceeding(allMessages, m).id))
				.ToList();

			PrintStats(group, allMessages, 6);

		}

		static void PrintStats(Group group, Message[] messages, int parallelism)
		{
			//int parallelism = 6; //new Random().Next(1, Environment.ProcessorCount);
			PrintParallel(
				new Func<string>[]
				{
					()=>WriteTable("User IDs", group.members.OrderBy(m=>m.name).Select(member => new string[] { member.nickname, member.name, member.user_id })),
					()=>$"--TOTALS--",
					()=>$"All messages: {messages.Length}",
					()=>$"System Messages: {messages.Where(msg => msg.system).Count()}",
					()=>$"Polls: {messages.Where(m => m.attachments.Any(attachment => attachment.type == "poll")).Count()}",
					()=>$"Platforms: "+string.Join(", ", messages.Select(m=>$"'{m.platform}'").Distinct()),
					()=>$"Deleted messages: {messages.Count(m => m.@event.data.deleter_id is not null)}",
					()=>"",
					()=>LeaderboardByUser("Top posters", group, messages, (user, messages) => messages.Count(m => m.sender_id == user.user_id), PadType.Left),
					()=>LeaderboardByUser("Most likes received", group, messages, (user, messages) =>
								messages.Where(m => m.sender_id == user.user_id).Sum(m => m.favorited_by.Length)
							, PadType.Left),
					()=>LeaderboardByUser("Most likes given", group, messages, (user, messages) =>
								messages.Count(m => m.favorited_by.Contains(user.user_id))
							, PadType.Left),
					()=>LeaderboardByUser("Likes received/given ratio", group, messages, (user, messages) => {
								float received = messages.Where(m => m.sender_id == user.user_id).Sum(m => m.favorited_by.Length);
								float given = messages.Count(m => m.favorited_by.Contains(user.user_id)); // likes given
								if (given == 0) return received;
								else return MathF.Round(received / given, 3);
							}, PadType.Right),
					()=>LeaderboardByUser("Average likes received per message", group, messages, (user, messages) => {
							 var sentByUser = messages.Where(m => m.sender_id == user.user_id).ToArray();
							 float likesReceived = sentByUser.Sum(m => m.favorited_by.Length);
							 return MathF.Round(likesReceived/sentByUser.Length,3);
						 }, PadType.Right),
					()=>LeaderboardByUser("Longest inactive", group, messages, (user, messages) =>
								(int)SafeMin(messages.Where(m => RepresentsActivity(user, m)), m => (DateTime.Now - m.CreatedAt).TotalDays)
							, PadType.Left),
					()=>LeaderboardByUser("Most mentioned", group, messages, (user, messages) =>
								messages.Count(m => m.attachments.Any(a => a.type == "mentions" && a.user_ids.Contains(user.user_id)))
							, PadType.Left),
					()=>LeaderboardByUser("Most distinct usernames", group, messages, (user, messages) =>
								messages
								.Where(m=>m.@event.type == "membership.nickname_changed")
								.Where(m=>m.@event.data.user.id.ToString() == user.user_id)
								.Select(m=>m.@event.data.name)
								.Distinct()
								.Count()
							, PadType.Left),
					()=>LeaderboardByUser("Created most polls", group, messages, (user, messages) =>
								messages.Count(m=>m.attachments.Any(a=>a.IsPoll) && m.sender_id == user.user_id)
							, PadType.Left),
					()=>LeaderboardByUser("Responded to most polls", group, messages, (user, messages) =>
								messages.Count(m=>m.@event.data.options?.Any(opt => opt.voter_ids?.Contains(user.user_id)??false)??false)
							, PadType.Left),
					()=>LeaderboardByMessage("Most liked messages", group, messages, m => m.favorited_by.Length, 20),
				}
				, parallelism);
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
		static string LeaderboardByUser<T>(string title, Group group, Message[] messages, Func<GroupMember, Message[], T> score, PadType scorePadType, int leaderboardSize = 10)
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

		static string LeaderboardByMessage<T>(string title, Group group, Message[] messages, Func<Message, T> score, int leaderboardSize = 10)
		{
			var leaderboard = messages
				.OrderByDescending(m => score(m))
				.Take(leaderboardSize);
			var tablated = leaderboard.Select((m,i) => {
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

		enum PadType { Left, Right }
		/// <summary>
		/// Given an
		/// </summary>
		/// <param name="source">List of rows</param>
		static string WriteTable<T>(string title, IEnumerable<IEnumerable<T>> source, IEnumerable<PadType> padTypes = null, int margin = 1, int maxWidth = 80)
		{
			StringBuilder result = new();

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

			result.AppendLine($"---{title}---");

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
				result.AppendLine(rowOutput.ToString());
			}
			return result.ToString();
		}
		static string WriteList(string title, IEnumerable<string> list)
		{
			StringBuilder result = new();
			result.AppendLine($"---{title}---");
			foreach (var item in list)
				result.AppendLine(item);
			return result.ToString();
		}
		/// <summary>
		/// Runs the provided <paramref name="actions"/> in parallel, up to the given maximum simultaneously.
		/// </summary>
		public static void PrintParallel(IEnumerable<Func<string>> actions, int parallelism)
		{
			//foreach(var action in actions) Console.WriteLine(action());
			//return;

			object _queueLock = new object();
			Queue<Func<string>> actionQueue = new(actions);
			Queue<Task<string>> runningQueue = new(parallelism);

			void attemptQueue()
			{
				lock(_queueLock)
				{
					if (!actionQueue.Any()) return;
					var action = actionQueue.Dequeue();
					var task = Task.Run(() => {
						var result = action();
						attemptQueue();
						return result;
					});
					runningQueue.Enqueue(task);
				}
			}

			// queue
			for(int i = 0; i < parallelism; i++)
				attemptQueue();
			// consume
			while(runningQueue.Any())
			{
				var action = runningQueue.Dequeue();
				action.Wait();
				var result = action.Result;
				Console.WriteLine(result);
			}
		}
	}
}
using GroupMeAPI;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace TSKBot
{
	internal class Program
	{
		/// <summary> If true, do not allow posting </summary>
		public const bool DryRun = true;
		public const string Prefix = "!";

		static void Main(string[] args)
		{

			//(string botToken, string group) = (testToken, testID);
			(string botToken, string group) = (TSKToken, TSKID);

			var filename = $"{group}.grp";

			//File.Delete(filename);

			API api = new();
			Console.WriteLine($"Loading from file '{filename}'");
			var file = SaveFile.Load(filename, group, api);
			var lastUpdated = file.LastUpdated;
			Console.WriteLine($"{file.Messages.Length} messages loaded from file");
			if (lastUpdated is null) Console.WriteLine("Never updated");
			else Console.WriteLine($"Last updated {lastUpdated}");

			//var idToUser = file.Group?.members.ToDictionary(m => m.user_id, m => m);
			//var scores = CalculateSocialCreditScores(file);

			Stopwatch runtime = Stopwatch.StartNew();
			do
			{
				var newMsgs = file.Update(forceUpdateMessages: 200, searchOlderMessages: false);
				foreach (var msg in newMsgs.Where(m=>m.sender_type is not "bot").OrderBy(m=>m.created_at))
				{
					Console.WriteLine(msg);
					// process messages here
					ProcessMessage(msg, file, botToken);
				}
				if(!DryRun) file.Save(filename);

				Thread.Sleep(10_000);
			} while (runtime.Elapsed < TimeSpan.FromHours(1));
			Console.WriteLine($"Exiting...");


		}
		static void ProcessMessage(Message message, SaveFile file, string botKey)
		{
			var group = file.Group.Value;

			var idToUser = file.Group?.members.ToDictionary(m => m.user_id, m => m);

			if (!string.IsNullOrWhiteSpace(message.text))
			{
				var text = message.text.Trim();
				var user = GetMemberByID(file, message.sender_id);

				// social credit score
				if (text == $"{Prefix}sc")
				{
					var result = $"{{0}}, your social credit score is {CalculateSocialCreditScores(file)[message.sender_id]}";
					if (DryRun) Console.WriteLine($"Posting social credit scores for {GetMemberByID(file, message.sender_id)?.name}: {result}");
					else API.SendBotMessage(group, botKey, result, mentionIds: new[] { message.sender_id });
				}
				else if (text == $"{Prefix}sc all")
				{
					var scores = CalculateSocialCreditScores(file)
						.Select(kv => (userId: kv.Key, score: kv.Value))
						.Where(pair => pair.score != 0)
						.OrderByDescending(pair => pair.score);
					string result = TableHelpers.WriteTable(
						"Social Credit Scores",
						scores.Select(pair => new string[] { GetMemberByID(file, pair.userId)?.name ?? pair.userId, pair.score.ToString() }),
						new[] { TableHelpers.PadType.Right, TableHelpers.PadType.Left }
						);
					if (DryRun) Console.WriteLine($"Posting all social credit scores:\n{result}");
					else API.SendBotMessage(group, botKey, result);
				}
				else if (text == $"{Prefix}stats")
				{
					var result = Utility.ParallelStringBuild(new[]
					{
						()=> "Statistics for {0}:",
						()=> $"Total posts: {file.Messages.Count(m=>m.sender_id == message.sender_id)}",
						()=> $"Likes given: {file.Messages.Count(m=>m.favorited_by?.Contains(message.sender_id) ?? false)}",
						()=> $"Like received: {file.Messages.Where(m=>m.sender_id == message.sender_id).Sum(m=>m.favorited_by.Length)}",
					}, Math.Max(Environment.ProcessorCount - 1, 1));
					if (DryRun) Console.WriteLine($"Posting statistics for {GetMemberByID(file, message.sender_id)?.name}");
					else API.SendBotMessage(group, botKey, result, mentionIds: new[] { message.sender_id });
				}
				// sexist language callout
				if (new string[] { /*sexism goes here*/ }.Any(text.Contains))
				{
					var exceptions = new string[] { "40888614" };
					StringBuilder sb = new();
					sb.Append($"!!! SEXIST LANGUAGE DETECTED !!! {0} 😡😡 WE BE RESPECTING WOMEN AROUND THESE PARTS 💪💪");
					if(exceptions.Contains(user?.user_id))
						sb.Append(" (i know you didn't mean it tho)");

					if (DryRun) Console.WriteLine($"Detected sexist language");
					else API.SendBotMessage(group, botKey,
						sb.ToString(),
						replyToMessageID: message.id,
						mentionIds: new string[] { message.sender_id });
				}
			}
		}

		static Dictionary<string, int> CalculateSocialCreditScores(SaveFile file/*Message[] messages*/)
		{
			var regexes = new string[] {
				@"([+-])\s*([\d,]+)\s*sc", // + 200 sc
				@"([+-])\s*([\d,]+)\s*social\s*credit", // + 200 social credit
			};
			Match getFirstMatch(Message message)
			{
				foreach(var regex in regexes)
				{
					var match = Regex.Match(message.text, regex);
					if (match.Success) return match;
				}
				return Match.Empty;
			}

			var messages = file.Messages;

			var scoreAdjustments = messages
				.Where(m => !string.IsNullOrWhiteSpace(m.text)) // must have text
				.Where(m => m.attachments.Any(a => a.IsReply || a.IsMention)) // must be a reply
				.Select(m => (message: m, match: getFirstMatch(m)))
				.Where(pair => pair.match.Success) // must be a successful match
				.ToArray();

			var userIds = messages.Select(m => m.sender_id).Distinct();

			var scoreDict = userIds.ToDictionary(id => id, id => 0);

			foreach(var (message, match) in scoreAdjustments)
			{
				if (int.TryParse(match.Groups[2].Value, out int intValue))
				{
					List<string> judgedUsers = new();
					var replies = message.attachments.Where(a => a.IsReply);
					var mentions = message.attachments.Where(a => a.IsMention);

					foreach (var reply in replies)
					{
						var repliedMessage = GetMessageByID(file, reply.reply_id);
						if (repliedMessage is null) continue;
						if (repliedMessage?.sender_type is "bot") continue;
						judgedUsers.Add(repliedMessage?.user_id);
					}
					foreach(var mention in mentions) judgedUsers.AddRange(mention.user_ids);

					foreach(var judgedUser in judgedUsers.Distinct())
					{
						switch (match.Groups[1].Value)
						{
							case "+": scoreDict[judgedUser] += intValue; break;
							case "-": scoreDict[judgedUser] -= intValue; break;
						}
					}
				}
			}

			return scoreDict;
		}

		static Message? GetMessageByID(SaveFile file, string id) => file.Messages.FirstOrDefault(m => m.id == id);
		static GroupMember? GetMemberByID(SaveFile file, string id) => file.Group?.members.FirstOrDefault(m => m.user_id == id);
	}
}
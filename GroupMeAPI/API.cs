using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace GroupMeAPI
{
	public class API
	{
		private const string BaseURL = "https://api.groupme.com/v3";

		private readonly string token;
		public API(string token)
		{
			this.token = token;
		}
		public API() {
			var assembly = System.Reflection.Assembly.GetAssembly(typeof(API));
			using Stream stream = assembly.GetManifestResourceStream("GroupMeAPI.Token.txt");
			using StreamReader reader = new(stream);
			token = reader.ReadToEnd().Trim();
		}

		private static bool IsNumeric(string str)
		{
			if (str is null) return false;
			else return str.All(c => "0123456789".Contains(c));
		}

		private struct Response<T>
		{
			public Meta meta;
			public T response;
		}
		public Group[] GetGroups()
		{
			List<Group> groups = new();
			Group[] lastBatch;
			int page = 1;
			do
			{
				lastBatch = GetGroups(page, 100);
				groups.AddRange(lastBatch);
				page++;
			} while (lastBatch.Length > 0);
			return groups.ToArray();
		}
		public Group[] GetGroups(int page, int per_page)
		{
			var json = WebRequester.Get($"{BaseURL}/groups", new Dictionary<string, object>()
			{
				{ "token", token },
				{ "page", page },
				{ "per_page", per_page },
			}).Response;
			var result = Newtonsoft.Json.JsonConvert.DeserializeObject<Response<Group[]>>(json);
			return result.response;
		}
		public Group[] GetFormerGroups()
		{
			var json = WebRequester.Get($"{BaseURL}/groups/former", new Dictionary<string, object>()
			{
				{ "token", token },
			});
			var result = Newtonsoft.Json.JsonConvert.DeserializeObject<Response<Group[]>>(json.Response);
			return result.response;
		}
		public Group GetGroup(string id)
		{
			if (!IsNumeric(id)) throw new ArgumentException($"{nameof(id)} '{id}' contained non-numeric character");

			var json = WebRequester.Get($"{BaseURL}/groups/{id}", new Dictionary<string, object>()
			{
				{ "token", token },
			});
			var result = Newtonsoft.Json.JsonConvert.DeserializeObject<Response<Group>>(json.Response);
			return result.response;
		}

		public Poll GetPoll(string group_id, string poll_id)
		{
			if (!IsNumeric(group_id)) throw new ArgumentException($"{nameof(group_id)} '{group_id}' contained non-numeric character");
			if (!IsNumeric(poll_id)) throw new ArgumentException($"{nameof(poll_id)} '{poll_id}' contained non-numeric character");

			var json = WebRequester.Get($"{BaseURL}/poll/{group_id}/{poll_id}", new Dictionary<string, object>()
			{
				{ "token", token },
			});
			var result = Newtonsoft.Json.JsonConvert.DeserializeObject<Response<PollResponse>>(json.Response);
			return result.response.poll;
		}

		public Poll[] GetPolls(string group_id)
		{
			if (!IsNumeric(group_id)) throw new ArgumentException($"{nameof(group_id)} '{group_id}' contained non-numeric character");

			var json = WebRequester.Get($"{BaseURL}/poll/{group_id}", new Dictionary<string, object>()
			{
				{ "token", token },
			});
			var result = Newtonsoft.Json.JsonConvert.DeserializeObject<Response<PollCollectionResponse>>(json.Response);
			return result.response.polls;
		}

		public struct MessageResult
		{
			public int count;
			public Message[] messages;
		}
		private MessageResult GetMessages(string group_id, Dictionary<string, object> arguments)
		{
			if (!IsNumeric(group_id)) throw new ArgumentException($"{nameof(group_id)} '{group_id}' contained non-numeric character");

			var json = WebRequester.Get($"{BaseURL}/groups/{group_id}/messages", arguments);
			if (string.IsNullOrWhiteSpace(json.Response))
			{
				return default;
			}
			else
			{
				var result = Newtonsoft.Json.JsonConvert.DeserializeObject<Response<MessageResult>>(json.Response);
				return result.response;
			}
		}
		public MessageResult GetMessagesBefore(string group_id, string? before_id, int limit)
		{
			if (before_id is not null && !IsNumeric(before_id)) throw new ArgumentException($"{nameof(before_id)} '{before_id}' contained non-numeric character");
			if (limit is < 1 or > 100) throw new ArgumentException($"{nameof(limit)} must be between 1 and 100");
			return GetMessages(group_id, new Dictionary<string, object>()
			{
				{ "token", token },
				{ "before_id", before_id },
				{ "limit", limit },
			});
		}
		public Message[] GetAllMessagesBefore(string group_id, string? before_id = null, int limit = -1)
		{
			var pullSize = limit > 0 ? Math.Min(100, limit) : 100;
			return BuildMessageList(
				last => GetMessagesBefore(group_id, last, pullSize).messages,
				before_id,
				limit
			);
		}
		public MessageResult GetMessagesAfter(string group_id, string after_id, int limit)
		{
			if (after_id is not null && !IsNumeric(after_id)) throw new ArgumentException($"{nameof(after_id)} '{after_id}' contained non-numeric character");
			if (limit is < 1 or > 100) throw new ArgumentException($"{nameof(limit)} must be between 1 and 100");

			return GetMessages(group_id, new Dictionary<string, object>()
			{
				{ "token", token },
				{ "after_id", after_id },
				{ "limit", limit },
			});
		}
		public Message[] GetAllMessagesAfter(string group_id, string? before_id = null, int limit = -1)
		{
			var pullSize = limit > 0 ? Math.Min(100, limit) : 100;
			return BuildMessageList(
				last => GetMessagesAfter(group_id, last, pullSize).messages,
				before_id,
				limit
			);
		}

		/// <summary>
		/// Uses the provided generator to build an array of <see cref="Message"/>s
		/// </summary>
		/// <param name="messageGenerator"></param>
		/// <param name="seed"></param>
		/// <returns></returns>
		private static Message[] BuildMessageList(Func<string?, Message[]> messageGenerator, string? seed = null, int limit = -1)
		{
			List<Message> messages = new();
			var lastMessage = seed;
			Message[] newMessages;
			do
			{
				newMessages = messageGenerator(lastMessage);
				if (newMessages is null) return messages.ToArray();
				lastMessage = newMessages?.LastOrDefault().id;
				messages.AddRange(newMessages);
			} while (newMessages.Any() && (limit < 0 || messages.Count < limit));
			return messages.ToArray();
		}

		static readonly System.Runtime.Serialization.Formatters.Binary.BinaryFormatter formatter = new();
		public static Message[] GetMessagesFromFile(string file)
		{
			if (!File.Exists(file)) return Array.Empty<Message>();
			using Stream stream = File.Open(file, FileMode.Open);
#pragma warning disable SYSLIB0011 // Type or member is obsolete
			return (Message[])formatter.Deserialize(stream);
#pragma warning restore SYSLIB0011 // Type or member is obsolete
		}

		public static void WriteMessagesToFile(string file, Message[] messages)
		{
			using Stream stream = File.Open(file, FileMode.Create);
#pragma warning disable SYSLIB0011 // Type or member is obsolete
			formatter.Serialize(stream, messages);
#pragma warning restore SYSLIB0011 // Type or member is obsolete
		}
		public static Message[] MergeMessageLists(params Message[][] messages)
		{
			return messages
				.SelectMany(m => m)
				.DistinctBy(m => m.id)
				.OrderByDescending(m => m.created_at)
				.ToArray();
		}

		public Message[] UpdateFile(string file, string group_id, int pullLimit = -1)
		{
			// load from file
			var existingMessages = GetMessagesFromFile(file) ?? Array.Empty<Message>();

			if(existingMessages.Any(m => m.group_id != group_id)) {
				throw new InvalidDataException($"Messages in file are for group '{existingMessages.FirstOrDefault().group_id}', while trying to update for group '{group_id}'");
			}

			// Get messages predating list
			var minID = existingMessages.Min(m => m.id);
			var olderMessages = GetAllMessagesBefore(group_id, minID, pullLimit);

			// Get messages after list
			var maxID = existingMessages.Max(m => m.id);
			var newerMessages = GetAllMessagesAfter(group_id, maxID, pullLimit);

			// combine and save
			var allMessages = MergeMessageLists(olderMessages, newerMessages, existingMessages);
			WriteMessagesToFile(file, allMessages);

			return allMessages;
		}
	}
}

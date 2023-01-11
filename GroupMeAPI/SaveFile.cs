using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using static GroupMeAPI.Utility;

namespace GroupMeAPI
{
	public class SaveFile
	{
		const string Version = "1.0.0";

		public string? GroupID { get; init; }
		public Message[] Messages { get; set; }
		public Group? Group { get; set; }
		public DateTime? LastUpdated { get; private set; }

		public Dictionary<string, object> Properties { get; set; }

		private API api;

		private SaveFile(API api) {
			this.api = api;
		}
		private SaveFile(string groupID, API api, bool runUpdate) : this(api)
		{
			GroupID = groupID;
			Messages = Array.Empty<Message>();
			Group = null;
			if(runUpdate) Update(searchOlderMessages: true);
		}

		/// <summary>
		/// Fetches new data for the <see cref="SaveFile"/>
		/// </summary>
		/// <param name="forceUpdateMessages"> Number of messages to fetch, regardless of when they were pulled </param>
		/// <param name="searchOlderMessages">If true, will try to populate messages at the earliest end of history</param>
		public Message[] Update(int forceUpdateMessages = 200, bool searchOlderMessages = false)
		{
			UpdateGroup();
			var newMessages = UpdateMessages(forceUpdateMessages, searchOlderMessages);
			LastUpdated = DateTime.Now;
			return newMessages;
		}

		private Message[] UpdateMessages(int forceUpdateMessages = 200, bool searchOlderMessages = false)
		{
			if (GroupID is null) throw new ArgumentNullException(nameof(GroupID));

			IEnumerable<Message> originalMessages = Messages;

			Message[] olderMessages = Array.Empty<Message>();
			if (searchOlderMessages)
			{
				// Get messages predating list
				Stopwatch oldMsgSw = Stopwatch.StartNew();
				var minID = originalMessages.Min(m => m.id);
				olderMessages = api.GetAllMessagesBefore(GroupID, minID);
				oldMsgSw.Stop();
				//Console.WriteLine($"Pulled {olderMessages.Length} old messages in {oldMsgSw.Elapsed}");
			}

			// Get messages after list
			Stopwatch newMsgSw = Stopwatch.StartNew();
			var maxID = originalMessages.Concat(olderMessages).Max(m => m.id);
			var newerMessages = api.GetAllMessagesAfter(GroupID, maxID);
			newMsgSw.Stop();
			//Console.WriteLine($"Pulled {newerMessages.Length} new messages in {newMsgSw.Elapsed}");

			// force update
			Message[] forcedUpdateMessages = Array.Empty<Message>();
			if (forceUpdateMessages > 0)
			{
				Stopwatch forcedMsgSw = Stopwatch.StartNew();
				var messagesToPull = forceUpdateMessages - newerMessages.Length;
				var oldestNewMessage = newerMessages.Min(m => m.id);
				forcedUpdateMessages = api.GetAllMessagesBefore(GroupID, oldestNewMessage, messagesToPull);
				forcedMsgSw.Stop();
				//Console.WriteLine($"Pulled {forcedUpdateMessages.Length} updated messages in {forcedMsgSw.Elapsed}");
			}

			Messages = 
				originalMessages // start with original messages
				.Concat(olderMessages) // add old messages pulled
				.Concat(newerMessages) // add new messages pulled
				.ExceptBy(forcedUpdateMessages.Select(m=>m.id), m=>m.id) // remove messages to be replaced
				.Concat(forcedUpdateMessages) // add messages that were force added
				.DistinctBy(m => m.id) // remove duplicates that may have snuck in
				.OrderByDescending(m => m.created_at) // order everything by created date
				.ToArray();

			return newerMessages;
				//.Concat(forcedUpdateMessages)
				//.OrderByDescending(m => m.id)
				//.ToArray();
		}
		private void UpdateGroup()
		{
			if (GroupID is null) throw new ArgumentNullException(nameof(GroupID));

			Group = api.GetGroup(GroupID);
		}

		public static SaveFile Load(string filename, string groupID, API api)
		{
			if(!File.Exists(filename)) return new(groupID, api, true);

			using Stream fileStream = File.OpenRead(filename);
			using BinaryReader reader = new(fileStream);
			var fileVersion = reader.ReadString();
			if (fileVersion != Version) return new SaveFile(groupID, api, true); // Do not attempt to load files from old versions

			try
			{
				SaveFile result = new(api)
				{
					GroupID = ReadObject<string>(reader),
					Messages = ReadArray<Message>(reader) ?? Array.Empty<Message>(),
					Group = ReadObject<Group>(reader),
					LastUpdated = ReadObject<DateTime>(reader),
				};
				return result;
			}catch(EndOfStreamException ex)
			{
				return new(groupID, api, true);
			}

		}

		public void Save(string filename)
		{
			using Stream fileStream = File.OpenWrite(filename);
			using BinaryWriter writer = new(fileStream);
			writer.Write(Version);

			WriteObject(GroupID, writer);
			WriteArray(Messages, writer);
			WriteObject(Group, writer);
			WriteObject(LastUpdated, writer);

		}

		#region IO HELPERS
		private static T? ReadObject<T>(BinaryReader reader)
		{
			var isNull = reader.ReadBoolean();
			if (isNull) return default;
			var binary = ReadByteList(reader);
			return DeserializeFromBytes<T>(binary);
		}
		private static void WriteObject<T>(T? item, BinaryWriter writer)
		{
			writer.Write(item is null);
			if (item is null) return;
			var binary = SerializeToBytes(item);
			WriteByteList(binary, writer);
		}

		private static T?[]? ReadArray<T>(BinaryReader reader)
		{
			var isNull = reader.ReadBoolean();
			if (isNull) return default;
			var len = reader.ReadInt32();
			T?[] result = new T[len];
			for(int i=0;i<len; i++)
			{
				result[i] = ReadObject<T>(reader);
			}
			return result;
		}
		private static void WriteArray<T>(IEnumerable<T> values, BinaryWriter writer)
		{
			writer.Write(values is null);
			if (values is null) return;
			var arr = values.ToArray();
			writer.Write(arr.Length);
			foreach (var item in arr)
			{
				WriteObject(item, writer);
			}
		}

		private static byte[] ReadByteList(BinaryReader reader)
		{
			var len = reader.ReadInt32();
			var arr = new byte[len];
			for(int i=0;i<len;i++) arr[i] = reader.ReadByte();
			return arr;
		}
		private static void WriteByteList(byte[] bytes, BinaryWriter writer)
		{
			writer.Write(bytes.Length);
			foreach(var item in bytes) writer.Write(item);
		}

		static readonly System.Runtime.Serialization.Formatters.Binary.BinaryFormatter formatter = new();
		private static byte[] SerializeToBytes<T>(T item)
		{
			using var memStrm = new MemoryStream();
#pragma warning disable SYSLIB0011 // Type or member is obsolete
			formatter.Serialize(memStrm, item);
#pragma warning restore SYSLIB0011 // Type or member is obsolete
			memStrm.Seek(0, SeekOrigin.Begin);
			return memStrm.ToArray();
		}
		private static T DeserializeFromBytes<T>(byte[] bytes)
		{
			using var stream = new MemoryStream(bytes);
#pragma warning disable SYSLIB0011 // Type or member is obsolete
			return (T)formatter.Deserialize(stream);
#pragma warning restore SYSLIB0011 // Type or member is obsolete
		}
		#endregion IO HELPERS
	}
}

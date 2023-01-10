using System;
using System.Collections.Generic;
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
		const int pullLimit = 100;

		public string? GroupID { get; init; }
		public Message[] Messages { get; set; }
		public Group? Group { get; set; }
		public DateTime? LastUpdated { get; private set; }

		public Dictionary<string, object> Properties { get; set; }

		private API api;

		private SaveFile(API api) {
			this.api = api;
		}
		private SaveFile(string groupID, API api) : this(api)
		{
			GroupID = groupID;
			Messages = Array.Empty<Message>();
			Group = null;
		}

		public void Update()
		{
			UpdateGroup();
			UpdateMessages();
			LastUpdated = DateTime.Now;
		}
		private void UpdateMessages()
		{
			if (GroupID is null) throw new ArgumentNullException(nameof(GroupID));

			IEnumerable<Message> allMessages = Messages;

			// Get messages predating list
			var minID = allMessages.Min(m => m.id);
			var olderMessages = api.GetAllMessagesBefore(GroupID, minID, pullLimit);
			allMessages = allMessages.Concat(olderMessages);

			// Get messages after list
			var maxID = allMessages.Max(m => m.id);
			var newerMessages = api.GetAllMessagesAfter(GroupID, maxID, pullLimit);
			allMessages = newerMessages.Concat(olderMessages);

			Messages = allMessages
				.DistinctBy(m => m.id)
				.OrderByDescending(m => m.created_at)
				.ToArray();
		}
		private void UpdateGroup()
		{
			if (GroupID is null) throw new ArgumentNullException(nameof(GroupID));

			Group = api.GetGroup(GroupID);
		}

		public static SaveFile Load(string filename, string groupID, API api)
		{
			if(!File.Exists(filename)) return new(groupID, api);

			using Stream fileStream = File.OpenRead(filename);
			using BinaryReader reader = new(fileStream);
			var fileVersion = reader.ReadString();
			if (fileVersion != Version) return new(groupID, api); // Do not attempt to load files from old versions

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
				return new(groupID, api);
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

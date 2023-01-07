using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace GroupMeAPI
{
	internal class SaveFile
	{
		const string Version = "1.0.0";

		public Message[] Messages { get; set; }
		public Group Group { get; set; }

		public SaveFile Load(string filename)
		{
			if(!File.Exists(filename)) return new();

			using Stream fileStream = File.OpenRead(filename);
			using BinaryReader reader = new(fileStream);
			var fileVersion = reader.ReadString();
			if (fileVersion != Version) return new(); // Do not attempt to load files from old versions

			SaveFile result = new()
			{
				Messages = ReadArray<Message>(reader),
				Group = ReadObject<Group>(reader),
			};

			return result;
		}

		public void Save(string filename)
		{
			using Stream fileStream = File.OpenWrite(filename);
			using BinaryWriter writer = new(fileStream);
			writer.Write(Version);

			WriteArray(Messages, writer);
			WriteObject(Group, writer);

		}

		private static T ReadObject<T>(BinaryReader reader)
		{
			var binary = ReadByteList(reader);
			return DeserializeFromBytes<T>(binary);
		}
		private static void WriteObject<T>(T item, BinaryWriter writer)
		{
			var binary = SerializeToBytes(item);
			WriteByteList(binary, writer);
		}

		private static T[] ReadArray<T>(BinaryReader reader)
		{
			var len = reader.ReadInt32();
			T[] result = new T[len];
			for(int i=0;i<len; i++)
			{
				result[i] = ReadObject<T>(reader);
			}
			return result;
		}
		private static void WriteArray<T>(IEnumerable<T> values, BinaryWriter writer)
		{
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
	}
}

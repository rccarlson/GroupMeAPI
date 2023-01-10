using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GroupMeAPI
{

	public struct Meta
	{
		public int code;
	}

	[Serializable]
	[DebuggerDisplay("{name,nq}")]
	public struct Group
	{
		public string id, group_id, name, phone_number, type, description, image_url, creator_user_id;
		public long created_at, updated_at;
		public long? muted_until;
		public bool office_mode;
		public GroupMessages messages;
		public int max_members;
		public string theme;
		public Icon? like_icon;
		public bool requires_approval, show_join_question;
		public string join_question;
		public int message_deletion_period;
		public string[] message_deletion_mode;
		public int children_count;
		public string share_url, share_qr_code_url;
		// public string[] directories;
		public GroupMember[] members;
		public int members_count;
		// locations, visibility, category_ids
	}

	[Serializable]
	public struct GroupMessages
	{
		public int count;
		public string last_message_id;
		public int last_message_created_at;
		public MessagePreview preview;
	}

	[Serializable]
	public struct MessagePreview
	{
		public string nickname, text, image_url;
		public MessageAttachment[] attachments;
	}

	[Serializable]
	public struct MessageAttachment
	{
		public string type;
		public string url; // image
		public bool IsImage => !string.IsNullOrWhiteSpace(url);
		public string poll_id; // poll
		public bool IsPoll => !string.IsNullOrWhiteSpace(poll_id);
		public string user_id, reply_id, base_reply_id; // reply
		public bool IsReply => !string.IsNullOrWhiteSpace(reply_id);
		public string[] user_ids;
		public bool IsMention => type is "mentions" && user_ids.Any(id => !string.IsNullOrWhiteSpace(id));
	}

	[Serializable]
	public struct Message
	{
		public override string ToString()
		{
			// special case for image with no text
			if (attachments.Length == 1 && attachments.First().IsImage && string.IsNullOrWhiteSpace(text))
				return $"{Utility.DateToString(CreatedAt)} {name}: {attachments.First().url}";

			StringBuilder sb = new();
			sb.Append(Utility.DateToString(CreatedAt))
				.Append(" ")
				.Append(name)
				.Append(": ");
			if (attachments.Any())
			{
				sb.Append("[Attachment]");
			}
			if (text is not null)
				sb.Append(text);
			return sb.ToString().Trim();
		}
		public MessageAttachment[] attachments;
		public string avatar_url;
		public long created_at;
		public DateTime CreatedAt => Utility.UnixTimeStampToDateTime(created_at);
		public string[] favorited_by;
		public string group_id, id, name, sender_id, sender_type, source_guid;
		public bool system;
		public string text;
		public string user_id;
		public Event @event;
		public string platform;
		public long? pinned_at;
		public string pinned_by;
	}

	[Serializable]
	public struct Event
	{
		public string type;
		public EventData data;
	}
	[Serializable]
	public struct EventData
	{
		public UserEvent removed_user;
		public UserEvent[] added_users;
		/// <summary> User who invited the new users </summary>
		public UserEvent adder_user;
		
		// calendar.event.created
		public CalendarEvent @event;
		public string url;
		public UserEvent user;

		// calendar.event.starting
		public string event_name, minutes;

		// calendar.event.updated
		public string[] updated_fields;

		// group.avatar_change
		public string avatar_url;

		// group.like_icon_set
		public Icon like_icon;

		// group.name_change
		public string name;

		// group.owner_changed
		public UserEvent old_owner, new_owner;

		// group.role_change_admin
		public string role;
		public UserEvent member;

		// group.theme_change
		public string theme_name;

		// group.topic_change
		public string topic;

		// message.deleted
		public string message_id;
		public long? deleted_id;
		public string deletion_actor, deleter_id;

		// poll.created
		public PollEvent poll;
		public ConversationEvent conversation;

		// poll.finished
		public PollOption[] options;

		// bot.add
		public string bot;
	}
	[Serializable]
	public struct CalendarEvent
	{
		public string id, name;
	}
	[Serializable]
	public struct ConversationEvent
	{
		public string id;
	}
	[Serializable]
	[DebuggerDisplay("{id,nq}: {nickname}")]
	public struct UserEvent
	{
		public long id;
		public string nickname;
	}

	[Serializable]
	public struct Icon
	{
		public string type;
		public int pack_id, pack_index;
	}

	[Serializable]
	[DebuggerDisplay("{name,nq} (\"{nickname,nq}\")")]
	public struct GroupMember
	{
		public string user_id, nickname, image_url, id;
		public bool muted, autokicked;
		public string[] roles;
		public string name;
	}

	[Serializable]
	public struct PollEvent {
		public long expiration;
		public string id, subject;
	}
	[Serializable]
	internal struct PollCollectionResponse
	{
		public Poll[] polls;
		public string continuation_token;
	}
	[Serializable]
	internal struct PollResponse
	{
		public Poll poll;
	}
	[Serializable]
	public struct Poll
	{
		public PollData data;
		public string user_vote;
		public string[] user_votes;
	}
	[DebuggerDisplay("{subject}")]
	[Serializable]
	public struct PollData
	{
		public string id, subject, owner_id, conversation_id;
		public long created_at, expiration;
		public string status;
		public PollOption[] options;
		public long last_modified;
		public string type, visibility;
	}
	[DebuggerDisplay("{title}")]
	[Serializable]
	public struct PollOption
	{
		public string id, title;
		public int votes;
		public string[] voter_ids;
	}
}

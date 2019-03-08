using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BFUlib
{
    public enum MessageType { Info, Warning, Error }

    public class Message
    {
        public MessageType Type { get; set; }
        public string Content { get; set; }

        public static Message Info(string txt) => new Message { Type = MessageType.Info, Content = txt };

        public static Message Warning(string txt) => new Message { Type = MessageType.Warning, Content = txt };

        public static Message Error(string txt) => new Message { Type = MessageType.Error, Content = txt };

        public static Message Error(string txt, Exception xcp) =>
            new Message { Type = MessageType.Error, Content = $"{txt}{Environment.NewLine}{xcp.Message}" };
    }

    public class MessageList
    {
        public List<Message> Messages { get; } = new List<Message>();

        public void Add(Message msg) => Messages.Add(msg);

        public void Add(MessageList ml) => Messages.AddRange(ml.Messages);

        public void Clear() => Messages.Clear();

        public static implicit operator MessageList(Message msg)
        {
            var ml = new MessageList();
            ml.Add(msg);
            return ml;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            foreach(var msg in Messages)
            {
                if(msg.Type != MessageType.Info)
                    sb.Append($"{msg.Type}: ");
                sb.AppendLine(msg.Content);
            }

            return sb.ToString();
        }

        public bool IsSuccess => Messages.All(x => x.Type != MessageType.Error);
    }
}
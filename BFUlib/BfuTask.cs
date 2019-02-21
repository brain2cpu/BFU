using System;
using System.Collections.Generic;

namespace BFUlib
{
    public enum MessageType { Info, Warning, Error }

    public enum CommandStatus { Pending, Success, Failed }

    public class Message
    {
        public MessageType Type { get; set; }
        public string Request { get; set; }
        public string Response { get; set; }
    }

    public class BfuTask
    {
        public CommandStatus Status { get; private set; } = CommandStatus.Pending;

        public List<Message> Messages { get; } = new List<Message>();

        private readonly Location _location;

        private readonly string _path;

        public BfuTask(Location location, string path)
        {
            _location = location;
            _path = path;
        }

        public void Execute()
        {

        }
    }
}

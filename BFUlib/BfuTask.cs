using System;
using System.Collections.Generic;

namespace BFUlib
{
    public enum MessageType { Info, Warning, Error }

    public enum CommandStatus { Pending, Running, Success, Failed }

    public class Message
    {
        public MessageType Type { get; set; }
        public string Content { get; set; }
    }

    public class BfuTask
    {
        public CommandStatus Status { get; private set; } = CommandStatus.Pending;

        public List<Message> Messages { get; } = new List<Message>();

        private readonly IConnection _connection;
        private readonly string _path;
        private readonly string _targetPath;

        public BfuTask(IConnection remote, string path, string targetPath)
        {
            _connection = remote;
            _path = path;
            _targetPath = targetPath;
        }

        public void Start()
        {
            Status = CommandStatus.Running;
            Messages.Clear();

            try
            {
                _connection.Upload(_path, _targetPath);
                Status = CommandStatus.Success;
            }
            catch(Exception xcp)
            {
                Messages.Add(new Message { Type = MessageType.Error, Content = xcp.Message });
                Status = CommandStatus.Failed;
            }
        }
    }
}

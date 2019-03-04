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
        private readonly Action<BfuTask> _onEnd;

        public Guid Guid { get; }

        public BfuTask(IConnection remote, string path, string targetPath, Action<BfuTask> onEnd)
        {
            _connection = remote;
            _path = path;
            _targetPath = targetPath;
            _onEnd = onEnd;

            Guid = Guid.NewGuid();
        }

        public BfuTask(BfuTask task)
        {
            _connection = task._connection;
            _path = task._path;
            _targetPath = task._targetPath;
            _onEnd = task._onEnd;

            Guid = Guid.NewGuid();
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

            _onEnd.Invoke(this);
        }
    }
}

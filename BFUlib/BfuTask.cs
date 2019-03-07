using System;

namespace BFUlib
{
    public enum CommandStatus { Pending, Running, Success, Failed }

    public class BfuTask
    {
        public CommandStatus Status { get; private set; } = CommandStatus.Pending;

        public MessageList Messages { get; } = new MessageList();

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

        public async void Start()
        {
            Status = CommandStatus.Running;
            Messages.Clear();

            try
            {
                Messages.Add(await _connection.UploadAsync(_path, _targetPath));
                Status = CommandStatus.Success;
            }
            catch(Exception xcp)
            {
                Messages.Add(Message.Error(xcp.Message));
                Status = CommandStatus.Failed;
            }

            _onEnd.Invoke(this);
        }
    }
}

namespace BFUlib
{
    public enum Operation {Add, Change, Delete}

    public class FileOperation
    {
        public string Path { get; set; }
        public Operation Operation { get; set; }
    }
}

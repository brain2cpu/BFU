using System;
using System.Runtime.Serialization;

namespace BFUlib
{
    public class BFUException : Exception
    {
        public BFUException()
        {
        }

        public BFUException(string message) : base(message)
        {
        }

        public BFUException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected BFUException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public BFUException(Location location, string message, Exception innerException) : 
            base($"{location.Name}: {message}{Environment.NewLine}{innerException.Message}", innerException)
        {
        }

        public BFUException(Location location, Exception innerException) :
            base($"{location.Name}{Environment.NewLine}{innerException.Message}", innerException)
        {
        }
    }
}

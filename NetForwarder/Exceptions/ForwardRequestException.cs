using System;

namespace NetForwarder.Exceptions
{
    public class ForwardRequestException : Exception
    {
        public ForwardRequestException(string message) : base(message)
        {
        }
    }
}
using System;

namespace WAHUKidsLearn
{
    public enum RuntimeIssueKind
    {
        None,
        PlatformUnavailable,
        ContentInvalid,
        DatabaseUnavailable
    }

    public sealed class RuntimeBootstrapIssue
    {
        public RuntimeIssueKind Kind { get; set; }
        public string ChildMessage { get; set; }
        public string TechnicalMessage { get; set; }
        public bool CanRecoverDatabase { get; set; }
    }

    public sealed class RuntimeBootstrapException : Exception
    {
        public RuntimeBootstrapException(RuntimeIssueKind kind, string childMessage, Exception inner)
            : base(childMessage, inner)
        {
            Kind = kind;
            ChildMessage = childMessage;
        }

        public RuntimeIssueKind Kind { get; private set; }
        public string ChildMessage { get; private set; }

        public RuntimeBootstrapIssue ToIssue()
        {
            return new RuntimeBootstrapIssue
            {
                Kind = Kind,
                ChildMessage = ChildMessage,
                TechnicalMessage = InnerException == null
                    ? GetType().Name + ": " + Message
                    : InnerException.GetType().Name + ": " + InnerException.Message,
                CanRecoverDatabase = Kind == RuntimeIssueKind.DatabaseUnavailable
            };
        }
    }
}

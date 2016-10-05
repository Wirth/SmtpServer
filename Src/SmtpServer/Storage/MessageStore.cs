using System;
using System.Threading;
using System.Threading.Tasks;
using SmtpServer.Mail;
using SmtpServer.Protocol;

namespace SmtpServer.Storage
{
    public abstract class MessageStore : IMessageStore
    {
        protected ISessionContext Context { get; }
        protected IMimeMessage Message { get; }

        protected MessageStore(ISessionContext context, IMimeMessage message)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (message == null) throw new ArgumentNullException(nameof(message));

            Context = context;
            Message = message;
        }

        public virtual void Dispose()
        {
        }

        public virtual Task<SmtpResponse> BeginWriteAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(SmtpResponse.Ok);
        }

        public virtual Task WriteAsync(string line, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public virtual Task<SmtpResponse> EndWriteAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(SmtpResponse.Ok);
        }
    }
}
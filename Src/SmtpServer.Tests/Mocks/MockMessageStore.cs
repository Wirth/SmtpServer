using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmtpServer.Mail;
using SmtpServer.Protocol;
using SmtpServer.Storage;

namespace SmtpServer.Tests.Mocks
{
    internal class MockMessageStore : MessageStore
    {
        public event EventHandler<MimeMessageEventArgs> Saved;

        public MockMessageStore(ISessionContext context, IMimeMessage message)
            : base(context, message)
        {
        }

        public override Task<SmtpResponse> EndWriteAsync(CancellationToken cancellationToken)
        {
            OnSaved(new MimeMessageEventArgs(Message));

            return base.EndWriteAsync(cancellationToken);
        }

        protected virtual void OnSaved(MimeMessageEventArgs e)
        {
            Saved?.Invoke(this, e);
        }
    }

    internal class MockMessageStoreFactory : IMessageStoreFactory
    {
        public List<IMimeMessage> Messages { get; } = new List<IMimeMessage>();

        public IMessageStore CreateInstance(ISessionContext context, IMimeMessage message)
        {
            var store = new MockMessageStore(context, message);
            store.Saved += Store_Saved;
            return store;
        }

        private void Store_Saved(object sender, MimeMessageEventArgs e)
        {
            Messages.Add(e.Message);
        }
    }

    internal class MimeMessageEventArgs : EventArgs
    {
        public IMimeMessage Message { get; }

        public MimeMessageEventArgs(IMimeMessage message)
        {
            Message = message;
        }
    }
}
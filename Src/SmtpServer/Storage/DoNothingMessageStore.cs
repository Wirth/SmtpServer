using SmtpServer.Mail;

namespace SmtpServer.Storage
{
    internal class DoNothingMessageStore : MessageStore
    {
        public DoNothingMessageStore(ISessionContext context, IMimeMessage message)
            : base(context, message)
        {
        }
    }

    internal class DoNothingMessageStoreFactory : IMessageStoreFactory
    {
        public IMessageStore CreateInstance(ISessionContext context, IMimeMessage message)
        {
            return new DoNothingMessageStore(context, message);
        }
    }
}
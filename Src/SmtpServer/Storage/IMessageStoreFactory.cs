using SmtpServer.Mail;

namespace SmtpServer.Storage
{
    public interface IMessageStoreFactory
    {
        IMessageStore CreateInstance(ISessionContext context, IMimeMessage message);
    }
}
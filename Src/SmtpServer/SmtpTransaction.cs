using System.Collections.ObjectModel;
using SmtpServer.Mail;

namespace SmtpServer
{
    internal sealed class SmtpTransaction : MimeMessage, ISmtpTransaction
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public SmtpTransaction()
        {
            Reset();
        }

        /// <summary>
        /// Reset the current transaction.
        /// </summary>
        public void Reset()
        {
            From = null;
            To = new Collection<IMailbox>();
        }
    }
}

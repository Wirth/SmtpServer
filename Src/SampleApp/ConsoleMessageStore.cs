using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SmtpServer;
using SmtpServer.Mail;
using SmtpServer.Protocol;
using SmtpServer.Storage;

namespace SampleApp
{
    internal class ConsoleMessageStore : MessageStore
    {
        private readonly StringBuilder _sb;

        public ConsoleMessageStore(ISessionContext context, IMimeMessage message) 
            : base(context, message)
        {
            _sb = new StringBuilder();
        }

        public override void Dispose()
        {
            base.Dispose();

            _sb.Clear();
        }

        public override Task WriteAsync(string line, CancellationToken cancellationToken)
        {
            _sb.AppendLine(line);
            return Task.FromResult(true);
        }

        public override Task<SmtpResponse> EndWriteAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("From: {0} ({1})", Message.From, Context.RemoteEndPoint);
            Console.WriteLine("To: {0}", string.Join(",", Message.To.Select(m => m.AsAddress())));
            Console.WriteLine(_sb.ToString());

            return base.EndWriteAsync(cancellationToken);
        }
    }

    public class ConsoleMessageStoreFactory : IMessageStoreFactory
    {
        public IMessageStore CreateInstance(ISessionContext context, IMimeMessage message)
        {
            return new ConsoleMessageStore(context, message);
        }
    }
}
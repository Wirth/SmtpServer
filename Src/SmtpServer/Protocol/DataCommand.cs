using System;
using System.Threading;
using System.Threading.Tasks;
using SmtpServer.Storage;

namespace SmtpServer.Protocol
{
    public sealed class DataCommand : SmtpCommand
    {
        private readonly IMessageStoreFactory _messageStoreFactory;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="messageStoreFactory">The message store factory.</param>
        public DataCommand(IMessageStoreFactory messageStoreFactory)
        {
            if (messageStoreFactory == null)
            {
                throw new ArgumentNullException(nameof(messageStoreFactory));
            }

            _messageStoreFactory = messageStoreFactory;
        }

        /// <summary>
        /// Execute the command.
        /// </summary>
        /// <param name="context">The execution context to operate on.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task which asynchronously performs the execution.</returns>
        public override async Task ExecuteAsync(ISmtpSessionContext context, CancellationToken cancellationToken)
        {
            if (context.Transaction.To.Count == 0)
            {
                await context.Text.ReplyAsync(SmtpResponse.NoValidRecipientsGiven, cancellationToken).ConfigureAwait(false);
                return;
            }

            await context.Text.ReplyAsync(new SmtpResponse(SmtpReplyCode.StartMailInput, "end with <CRLF>.<CRLF>"),
                                          cancellationToken)
                         .ConfigureAwait(false);

            try
            {
                using (var store = _messageStoreFactory.CreateInstance(context, context.Transaction))
                {
                    var response = await store.BeginWriteAsync(cancellationToken).ConfigureAwait(false);
                    if (response != SmtpResponse.Ok)
                    {
                        await context.Text.ReplyAsync(response, cancellationToken).ConfigureAwait(false);
                        return;
                    }

                    var emptyLine = false;
                    string text;
                    while ((text = await context.Text.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != ".")
                    {
                        // need to trim the '.' at the start of the line if it 
                        // exists as this would have been added for transparency
                        // http://tools.ietf.org/html/rfc5321#section-4.5.2

                        if (emptyLine) await store.WriteAsync(string.Empty, cancellationToken).ConfigureAwait(false);

                        if (text == string.Empty)
                        {
                            emptyLine = true;
                        }
                        else
                        {
                            var line = text.Length > 1 && text[0] == '.' ? text.Remove(0, 1) : text;
                            await store.WriteAsync(line, cancellationToken).ConfigureAwait(false);
                            emptyLine = false;
                        }
                    }

                    response = await store.EndWriteAsync(cancellationToken).ConfigureAwait(false);
                    await context.Text.ReplyAsync(response, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                await context.Text.ReplyAsync(new SmtpResponse(SmtpReplyCode.TransactionFailed), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
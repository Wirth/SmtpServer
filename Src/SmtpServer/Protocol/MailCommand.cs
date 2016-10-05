﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmtpServer.Mail;
using SmtpServer.Storage;

namespace SmtpServer.Protocol
{
    public sealed class MailCommand : SmtpCommand
    {
        readonly IMailbox _address;
        readonly IDictionary<string, string> _parameters;
        readonly IMailboxFilterFactory _mailboxFilterFactory;
        readonly int _maxMessageSize;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="parameters">The list of extended (ESMTP) parameters.</param>
        /// <param name="mailboxFilterFactory">The mailbox filter factory to create the filters from.</param>
        /// <param name="maxMessageSize">The maximum message size (0 for no limit).</param>
        public MailCommand(IMailbox address, IDictionary<string, string> parameters, IMailboxFilterFactory mailboxFilterFactory, int maxMessageSize = 0)
        {
            if (mailboxFilterFactory == null)
            {
                throw new ArgumentNullException(nameof(mailboxFilterFactory));
            }

            _address = address;
            _parameters = parameters;
            _mailboxFilterFactory = mailboxFilterFactory;
            _maxMessageSize = maxMessageSize;
        }

        /// <summary>
        /// Execute the command.
        /// </summary>
        /// <param name="context">The execution context to operate on.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task which asynchronously performs the execution.</returns>
        public override async Task ExecuteAsync(ISmtpSessionContext context, CancellationToken cancellationToken)
        {
            context.Transaction.Reset();

            // check if a size has been defined
            var size = GetMessageSize();

            // check against the server supplied maximum
            if (_maxMessageSize > 0 && size > _maxMessageSize)
            {
                await context.Text.ReplyAsync(SmtpResponse.SizeLimitExceeded, cancellationToken).ConfigureAwait(false);
                return;
            }

            using (var container = new DisposableContainer<IMailboxFilter>(_mailboxFilterFactory.CreateInstance(context)))
            {
                switch (await container.Instance.CanAcceptFromAsync(context, Address, size).ConfigureAwait(false))
                {
                    case MailboxFilterResult.Yes:
                        context.Transaction.From = _address;
                        await context.Text.ReplyAsync(SmtpResponse.Ok, cancellationToken).ConfigureAwait(false);
                        return;

                    case MailboxFilterResult.NoTemporarily:
                        await context.Text.ReplyAsync(SmtpResponse.MailboxUnavailable, cancellationToken).ConfigureAwait(false);
                        return;

                    case MailboxFilterResult.NoPermanently:
                        await context.Text.ReplyAsync(SmtpResponse.MailboxNameNotAllowed, cancellationToken).ConfigureAwait(false);
                        return;

                    case MailboxFilterResult.SizeLimitExceeded:
                        await context.Text.ReplyAsync(SmtpResponse.SizeLimitExceeded, cancellationToken).ConfigureAwait(false);
                        return;
                }
            }

            throw new NotSupportedException("The Acceptance state is not supported.");
        }

        /// <summary>
        /// Gets the estimated message size supplied from the ESMTP command extension.
        /// </summary>
        /// <returns>The estimated message size that was supplied by the client.</returns>
        int GetMessageSize()
        {
            string value;
            if (_parameters.TryGetValue("SIZE", out value) == false)
            {
                return 0;
            }

            int size;
            if (Int32.TryParse(value, out size) == false)
            {
                return 0;
            }

            return size;
        }

        /// <summary>
        /// Gets the address that the mail is from.
        /// </summary>
        public IMailbox Address
        {
            get { return _address; }
        }
    }
}

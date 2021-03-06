﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SmtpServer.Protocol;

namespace SmtpServer
{
    public class SmtpServer
    {
        /// <summary>
        /// Raised when a session has been created.
        /// </summary>
        public event EventHandler<SessionEventArgs> SessionCreated;

        /// <summary>
        /// Raised when a session has completed.
        /// </summary>
        public event EventHandler<SessionEventArgs> SessionCompleted;

        readonly ISmtpServerOptions _options;
        readonly TraceSwitch _logger = new TraceSwitch("SmtpServer", "The SMTP server.");

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="options">The SMTP server options.</param>
        public SmtpServer(ISmtpServerOptions options)
        {
            _options = options;
        }

        /// <summary>
        /// Raises the SessionCreated Event.
        /// </summary>
        /// <param name="args">The event data.</param>
        protected virtual void OnSessionCreated(SessionEventArgs args)
        {
            SessionCreated?.Invoke(this, args);
        }

        /// <summary>
        /// Raises the SessionCompleted Event.
        /// </summary>
        /// <param name="args">The event data.</param>
        protected virtual void OnSessionCompleted(SessionEventArgs args)
        {
            SessionCompleted?.Invoke(this, args);
        }

        /// <summary>
        /// Starts the SMTP server.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task which performs the operation.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInfo("Starting the SMTP Server");

            return Task.WhenAll(_options.Endpoints.Select(e => ListenAsync(e, cancellationToken)));
        }

        /// <summary>
        /// Listen for SMTP traffic on the given endpoint.
        /// </summary>
        /// <param name="endpoint">The endpoint to listen on.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task which performs the operation.</returns>
        async Task ListenAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
        {
            _logger.LogVerbose("Listening on port {0}", endpoint.Port);

            var tcpListener = new TcpListener(endpoint);
            tcpListener.Start();

            // keep track of the running tasks for disposal
            var sessions = new ConcurrentDictionary<SmtpSession, SmtpSession>();

            try
            {
                while (cancellationToken.IsCancellationRequested == false)
                {
                    // wait for a client connection
                    var tcpClient = await tcpListener.AcceptTcpClientAsync().WithCancellationAsync(cancellationToken).ConfigureAwait(false);
                    
                    _logger.LogVerbose("SMTP client accepted [{0}]", tcpClient.Client.RemoteEndPoint);

                    // create a new session to handle the connection
                    var session = CreateSession(tcpClient);
                    sessions.TryAdd(session, session);

                    OnSessionCreated(new SessionEventArgs(session.Context));

                    session.Run(cancellationToken);

                    #pragma warning disable 4014
                    session.Task
                        .ContinueWith(t =>
                        {
                            SmtpSession s;
                            sessions.TryRemove(session, out s);

                            OnSessionCompleted(new SessionEventArgs(session.Context));
                        },
                        cancellationToken);
                    #pragma warning restore 4014
                }

                // the server has been cancelled, wait for the tasks to complete
                await Task.WhenAll(sessions.Keys.Select(s => s.Task)).ConfigureAwait(false);
            }
            finally
            {
                tcpListener.Stop();
            }
        }

        /// <summary>
        /// Creates a new SMTP session.
        /// </summary>
        /// <param name="tcpClient">The TCP client that the session is reading & writing to.</param>
        /// <returns>The SMTP session.</returns>
        SmtpSession CreateSession(TcpClient tcpClient)
        {
            return new SmtpSession(_options, tcpClient, new SmtpStateMachine(_options, CreateCommandFactory()));
        }

        /// <summary>
        /// Create an instance of the SMTP Command Factory.
        /// </summary>
        /// <returns>An instance of the SMTP server command factory.</returns>
        SmtpCommandFactory CreateCommandFactory()
        {
            return new SmtpCommandFactory(_options, new SmtpParser());
        }
    }
}

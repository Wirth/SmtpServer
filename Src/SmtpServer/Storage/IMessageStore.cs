using System;
using System.Threading;
using System.Threading.Tasks;
using SmtpServer.Protocol;

namespace SmtpServer.Storage
{
    public interface IMessageStore : IDisposable
    {
        Task<SmtpResponse> BeginWriteAsync(CancellationToken cancellationToken);
        Task WriteAsync(string line, CancellationToken cancellationToken);
        Task<SmtpResponse> EndWriteAsync(CancellationToken cancellationToken);
    }
}
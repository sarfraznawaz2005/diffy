using System;
using System.Threading.Tasks;

namespace Diffy.Core.Interfaces;

public interface ISingleInstanceService : IDisposable
{
    void StartListening();
    void StopListening();
    Task SendArgsAsync(string args);
    event Action<string>? ArgumentsReceived;
}

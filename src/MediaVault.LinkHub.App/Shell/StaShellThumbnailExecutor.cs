using System.Collections.Concurrent;

namespace MediaVault.LinkHub.App.Shell;

/// <summary>
/// Ejecuta llamadas COM del Shell de Windows en un hilo STA (requerido por IShellItemImageFactory).
/// </summary>
internal static class StaShellThumbnailExecutor
{
    private static readonly BlockingCollection<Action> Queue = new();
    private static readonly Thread StaThread;

    static StaShellThumbnailExecutor()
    {
        StaThread = new Thread(ProcessQueue)
        {
            IsBackground = true,
            Name = "ShellThumbnailSTA"
        };
        StaThread.SetApartmentState(ApartmentState.STA);
        StaThread.Start();
    }

    public static Task<T> RunAsync<T>(Func<T> action)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        Queue.Add(() =>
        {
            try
            {
                tcs.SetResult(action());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }

    private static void ProcessQueue()
    {
        foreach (var action in Queue.GetConsumingEnumerable())
            action();
    }
}

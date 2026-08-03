using CommunityToolkit.Mvvm.ComponentModel;

namespace MediaVault.LinkHub.App.ViewModels.Base;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    protected async Task ExecuteBusyAsync(Func<Task> action, string? busyMessage = null)
    {
        if (IsBusy)
            return;

        await RunBusyCoreAsync(action, busyMessage).ConfigureAwait(true);
    }

    protected async Task<T?> ExecuteBusyAsync<T>(Func<Task<T>> action, string? busyMessage = null)
    {
        if (IsBusy)
            return default;

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            StatusMessage = busyMessage;
            return await action().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return default;
        }
        finally
        {
            IsBusy = false;
            StatusMessage = null;
        }
    }

    /// <summary>
    /// Ejecuta una acción con indicador de carga, permitiendo recargas anidadas desde dentro.
    /// </summary>
    protected async Task RunBusyCoreAsync(Func<Task> action, string? busyMessage = null)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            StatusMessage = busyMessage;
            await action().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            StatusMessage = null;
        }
    }
}

using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MediaVault.LinkHub.App.Shell;
using MediaVault.LinkHub.App.ViewModels.Base;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class ProducerManagerViewModel : ViewModelBase, INavigableViewModel
{
    private readonly IProducerService _producerService;
    private readonly IAppDialogService _appDialogService;

    public ProducerManagerViewModel(
        IProducerService producerService,
        IAppDialogService appDialogService)
    {
        _producerService = producerService;
        _appDialogService = appDialogService;
    }

    public string Title => "Productoras";

    public string Subtitle => "Productoras o fuentes asociadas a enlaces web (y opcionalmente a videos)";

    public ObservableCollection<Producer> Producers { get; } = [];

    [ObservableProperty]
    private Producer? _selectedProducer;

    [ObservableProperty]
    private string _producerName = string.Empty;

    public bool CanEditProducer => SelectedProducer is not null;

    public Task InitializeAsync() =>
        RunBusyCoreAsync(() => ReloadAsync(), "Cargando productoras...");

    private async Task ReloadAsync()
    {
        var producers = await _producerService.GetAllAsync().ConfigureAwait(true);

        Producers.Clear();
        foreach (var producer in producers)
            Producers.Add(producer);

        NotifyProducerCommands();
    }

    partial void OnSelectedProducerChanged(Producer? value)
    {
        ProducerName = value?.Name ?? string.Empty;
        NotifyProducerCommands();
    }

    private void NotifyProducerCommands()
    {
        OnPropertyChanged(nameof(CanEditProducer));
        RenameProducerCommand.NotifyCanExecuteChanged();
        DeleteProducerCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task AddProducerAsync()
    {
        if (string.IsNullOrWhiteSpace(ProducerName))
        {
            ErrorMessage = "Indique un nombre para la productora.";
            return;
        }

        try
        {
            ErrorMessage = null;
            await _producerService.CreateAsync(ProducerName).ConfigureAwait(true);
            ProducerName = string.Empty;
            SelectedProducer = null;
            await ReloadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditProducer))]
    private async Task RenameProducerAsync()
    {
        if (SelectedProducer is null || string.IsNullOrWhiteSpace(ProducerName))
            return;

        try
        {
            ErrorMessage = null;
            await _producerService.UpdateAsync(SelectedProducer.Id, ProducerName).ConfigureAwait(true);
            await ReloadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditProducer))]
    private async Task DeleteProducerAsync()
    {
        if (SelectedProducer is null)
            return;

        if (!_appDialogService.ConfirmYesNo(
                "Confirmar eliminación",
                $"¿Eliminar la productora «{SelectedProducer.Name}»?\n\nSe quitará de todos los videos.",
                AppDialogKind.Question))
            return;

        try
        {
            ErrorMessage = null;
            await _producerService.DeleteAsync(SelectedProducer.Id).ConfigureAwait(true);
            SelectedProducer = null;
            ProducerName = string.Empty;
            await ReloadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}

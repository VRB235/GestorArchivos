using System.Collections.ObjectModel;
using System.Net.Http;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MediaVault.LinkHub.App.Shell;
using MediaVault.LinkHub.App.ViewModels.Base;
using MediaVault.LinkHub.App.Views;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class ProducerManagerViewModel : ViewModelBase, INavigableViewModel
{
    private readonly IProducerService _producerService;
    private readonly IProducerLinkService _producerLinkService;
    private readonly IVideoScrapeService _videoScrapeService;
    private readonly IWebLinkService _webLinkService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAppDialogService _appDialogService;
    private readonly IBrowserLauncher _browserLauncher;
    private ProducerLinksWindow? _openLinksWindow;
    private int? _openLinksProducerId;

    public ProducerManagerViewModel(
        IProducerService producerService,
        IProducerLinkService producerLinkService,
        IVideoScrapeService videoScrapeService,
        IWebLinkService webLinkService,
        IHttpClientFactory httpClientFactory,
        IAppDialogService appDialogService,
        IBrowserLauncher browserLauncher)
    {
        _producerService = producerService;
        _producerLinkService = producerLinkService;
        _videoScrapeService = videoScrapeService;
        _webLinkService = webLinkService;
        _httpClientFactory = httpClientFactory;
        _appDialogService = appDialogService;
        _browserLauncher = browserLauncher;
    }

    public string Title => "Productoras";

    public string Subtitle => AppFeatureFlags.IsWebScrapingEnabled
        ? "Catálogo con logo del Link Manager · scrape de videos y etiquetado en Media Vault"
        : "Catálogo de productoras con logo del Link Manager y etiquetado en Media Vault";

    public ObservableCollection<ProducerCatalogItem> Catalog { get; } = [];

    [ObservableProperty]
    private ProducerCatalogItem? _selectedCatalogItem;

    [ObservableProperty]
    private string _producerName = string.Empty;

    public Producer? SelectedProducer => SelectedCatalogItem?.Producer;

    public bool CanEditProducer => SelectedCatalogItem is not null;

    public bool CanOpenCatalog => SelectedCatalogItem is not null;

    public Task InitializeAsync() =>
        RunBusyCoreAsync(() => ReloadAsync(), "Cargando productoras...");

    private async Task ReloadAsync()
    {
        var producers = await _producerService.GetAllAsync().ConfigureAwait(true);
        var webLinks = await _webLinkService.GetAllAsync().ConfigureAwait(true);

        var previousId = SelectedCatalogItem?.Id;
        Catalog.Clear();

        foreach (var producer in producers)
        {
            Catalog.Add(new ProducerCatalogItem
            {
                Producer = producer,
                PrimaryWebLink = ResolvePrimaryWebLink(producer, webLinks)
            });
        }

        SelectedCatalogItem = previousId is int id
            ? Catalog.FirstOrDefault(item => item.Id == id)
            : null;

        NotifyProducerCommands();
    }

    partial void OnSelectedCatalogItemChanged(ProducerCatalogItem? value)
    {
        ProducerName = value?.Name ?? string.Empty;
        NotifyProducerCommands();
    }

    private void NotifyProducerCommands()
    {
        OnPropertyChanged(nameof(CanEditProducer));
        OnPropertyChanged(nameof(CanOpenCatalog));
        OnPropertyChanged(nameof(SelectedProducer));
        RenameProducerCommand.NotifyCanExecuteChanged();
        DeleteProducerCommand.NotifyCanExecuteChanged();
        OpenCatalogLinksCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanOpenCatalog))]
    private void OpenCatalogLinks()
    {
        if (SelectedProducer is null)
            return;

        if (_openLinksWindow is { IsLoaded: true } && _openLinksProducerId == SelectedProducer.Id)
        {
            _openLinksWindow.Activate();
            return;
        }

        _openLinksWindow?.Close();

        var vm = new ProducerLinksViewModel(
            SelectedProducer,
            _producerLinkService,
            _videoScrapeService,
            _webLinkService,
            _httpClientFactory,
            _appDialogService,
            _browserLauncher);

        var window = new ProducerLinksWindow(vm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        _openLinksWindow = window;
        _openLinksProducerId = SelectedProducer.Id;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_openLinksWindow, window))
            {
                _openLinksWindow = null;
                _openLinksProducerId = null;
            }
        };

        window.Show();
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
            SelectedCatalogItem = null;
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
        if (SelectedCatalogItem is null || string.IsNullOrWhiteSpace(ProducerName))
            return;

        try
        {
            ErrorMessage = null;
            await _producerService.UpdateAsync(SelectedCatalogItem.Id, ProducerName).ConfigureAwait(true);
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
        if (SelectedCatalogItem is null)
            return;

        if (!_appDialogService.ConfirmYesNo(
                "Confirmar eliminación",
                $"¿Eliminar la productora «{SelectedCatalogItem.Name}»?\n\nSe quitará de todos los videos.",
                AppDialogKind.Question))
            return;

        try
        {
            ErrorMessage = null;
            await _producerService.DeleteAsync(SelectedCatalogItem.Id).ConfigureAwait(true);
            SelectedCatalogItem = null;
            ProducerName = string.Empty;
            await ReloadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private static WebLink? ResolvePrimaryWebLink(Producer producer, IReadOnlyList<WebLink> webLinks)
    {
        var associated = webLinks
            .Where(link => link.Producers.Any(item => item.Id == producer.Id))
            .ToList();

        if (associated.Count == 0)
        {
            var byName = webLinks.FirstOrDefault(link =>
                string.Equals(link.Nombre, producer.Name, StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
                return byName;

            return webLinks.FirstOrDefault(link =>
                link.Producers.Any(item =>
                    string.Equals(item.Name, producer.Name, StringComparison.OrdinalIgnoreCase)));
        }

        return associated.FirstOrDefault(link => !string.IsNullOrWhiteSpace(link.LogoPath))
            ?? associated.OrderBy(link => link.Nombre, StringComparer.CurrentCultureIgnoreCase).First();
    }
}

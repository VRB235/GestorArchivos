using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using MediaVault.LinkHub.App.Shell;

using MediaVault.LinkHub.App.ViewModels.Base;

using MediaVault.LinkHub.Application.Services;

using MediaVault.LinkHub.Domain.Entities;

using MediaVault.LinkHub.Domain.Enums;



namespace MediaVault.LinkHub.App.ViewModels;



public partial class LinkManagerViewModel : ViewModelBase, INavigableViewModel

{

    private readonly IWebLinkService _webLinkService;

    private readonly IAppDialogService _appDialogService;

    private List<WebLink> _allWebLinks = [];



    public LinkManagerViewModel(IWebLinkService webLinkService, IAppDialogService appDialogService)

    {

        _webLinkService = webLinkService;

        _appDialogService = appDialogService;

        Categories = new ObservableCollection<LinkCategory>(Enum.GetValues<LinkCategory>());

        CategoryFilters =

        [

            new CategoryFilterOption("Todas las categorías", null),

            new CategoryFilterOption("Oficial", LinkCategory.Oficial),

            new CategoryFilterOption("Descarga", LinkCategory.Descarga),

            new CategoryFilterOption("Gratis", LinkCategory.Gratis)

        ];

        SelectedCategoryFilter = CategoryFilters[0];

    }



    public string Title => "Link Manager";



    public string Subtitle => "Gestión de enlaces web con apertura en modo incógnito";



    public ObservableCollection<LinkCategory> Categories { get; }



    public ObservableCollection<CategoryFilterOption> CategoryFilters { get; }



    [ObservableProperty]

    private CategoryFilterOption _selectedCategoryFilter;



    public ObservableCollection<WebLink> WebLinks { get; } = [];



    [ObservableProperty]

    private WebLink? _selectedWebLink;



    [ObservableProperty]

    private string _nombre = string.Empty;



    [ObservableProperty]

    private string _url = string.Empty;



    [ObservableProperty]

    private string? _logoPath;



    [ObservableProperty]

    private LinkCategory _categoria = LinkCategory.Oficial;



    /// <summary>Fecha de visita del usuario (UTC en modelo, local en UI).</summary>

    [ObservableProperty]

    private DateTime? _fechaUltimaActualizacion;



    /// <summary>Solo fecha para el DatePicker (día local).</summary>

    [ObservableProperty]

    private DateTime? _fechaVisitaSeleccionada;



    public string FechaUltimaActualizacionTexto =>

        FechaUltimaActualizacion.HasValue

            ? FechaUltimaActualizacion.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")

            : "Sin registrar";



    public Task InitializeAsync() =>

        RunBusyCoreAsync(() => ReloadAsync(), "Cargando enlaces...");



    private async Task ReloadAsync()

    {

        var links = await _webLinkService.GetAllAsync().ConfigureAwait(true);

        _allWebLinks = SortByVisitDateOldestFirst(links).ToList();

        ApplyCategoryFilter();

    }



    partial void OnSelectedCategoryFilterChanged(CategoryFilterOption value) =>

        ApplyCategoryFilter();



    private void ApplyCategoryFilter()

    {

        var selectedId = SelectedWebLink?.Id;

        var filtered = SelectedCategoryFilter.Category is LinkCategory category

            ? _allWebLinks.Where(link => link.Categoria == category)

            : _allWebLinks;



        WebLinks.Clear();

        foreach (var link in filtered)

            WebLinks.Add(link);



        if (selectedId.HasValue)

            SelectedWebLink = WebLinks.FirstOrDefault(link => link.Id == selectedId.Value);

    }



    private static IEnumerable<WebLink> SortByVisitDateOldestFirst(IEnumerable<WebLink> links) =>

        links.OrderBy(link => link.FechaUltimaActualizacion ?? DateTime.MaxValue);



    partial void OnSelectedWebLinkChanged(WebLink? value)

    {

        if (value is null)

            return;



        Nombre = value.Nombre;

        Url = value.Url;

        LogoPath = value.LogoPath;

        Categoria = value.Categoria;

        FechaUltimaActualizacion = value.FechaUltimaActualizacion;

        FechaVisitaSeleccionada = value.FechaUltimaActualizacion?.ToLocalTime().Date;

        NotifyFechaVisitaChanged();

    }



    partial void OnFechaVisitaSeleccionadaChanged(DateTime? value)
    {

        if (value.HasValue)

        {

            var local = DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Local);

            FechaUltimaActualizacion = local.ToUniversalTime();

        }



        NotifyFechaVisitaChanged();

    }



    private void NotifyFechaVisitaChanged() =>

        OnPropertyChanged(nameof(FechaUltimaActualizacionTexto));



    [RelayCommand]

    private void ClearForm()

    {

        SelectedWebLink = null;

        Nombre = string.Empty;

        Url = string.Empty;

        LogoPath = null;

        Categoria = LinkCategory.Oficial;

        FechaUltimaActualizacion = null;

        FechaVisitaSeleccionada = null;

        NotifyFechaVisitaChanged();

    }



    [RelayCommand]

    private void BrowseLogo()

    {

        var dialog = new Microsoft.Win32.OpenFileDialog

        {

            Title = "Seleccionar logo o miniatura",

            Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.webp;*.ico;*.bmp|Todos los archivos|*.*"

        };



        if (dialog.ShowDialog() == true)

            LogoPath = dialog.FileName;

    }



    [RelayCommand]

    private async Task MarkAsVisitedAsync()

    {

        if (SelectedWebLink is null)

        {

            ErrorMessage = "Seleccione un enlace para marcar la visita.";

            return;

        }



        var linkId = SelectedWebLink.Id;



        await ExecuteBusyAsync(async () =>

        {

            await _webLinkService.MarkAsUserUpdatedAsync(linkId, DateTime.UtcNow).ConfigureAwait(true);

            await ReloadAsync().ConfigureAwait(true);

            SelectedWebLink = WebLinks.FirstOrDefault(link => link.Id == linkId);

        }, "Marcando visita...").ConfigureAwait(true);

    }



    [RelayCommand]

    private void ClearVisitDate()

    {

        FechaUltimaActualizacion = null;

        FechaVisitaSeleccionada = null;

        NotifyFechaVisitaChanged();

    }



    [RelayCommand]

    private async Task SaveAsync()

    {

        await ExecuteBusyAsync(async () =>

        {

            int? savedId;



            if (SelectedWebLink is null)

            {

                var created = await _webLinkService.CreateAsync(

                    Nombre,

                    Url,

                    Categoria,

                    LogoPath,

                    FechaUltimaActualizacion).ConfigureAwait(true);

                savedId = created.Id;

            }

            else

            {

                savedId = SelectedWebLink.Id;

                await _webLinkService.UpdateAsync(

                    savedId.Value,

                    Nombre,

                    Url,

                    Categoria,

                    LogoPath,

                    FechaUltimaActualizacion).ConfigureAwait(true);

            }



            await ReloadAsync().ConfigureAwait(true);

            SelectedWebLink = WebLinks.FirstOrDefault(link => link.Id == savedId);

        }, "Guardando enlace...").ConfigureAwait(true);

    }



    [RelayCommand]

    private async Task DeleteAsync()

    {

        if (SelectedWebLink is null)

            return;



        var linkName = SelectedWebLink.Nombre;

        if (!_appDialogService.ConfirmYesNo(

                "Confirmar eliminación",

                $"¿Eliminar el enlace «{linkName}»?\n\nEsta acción no se puede deshacer.",

                AppDialogKind.Warning))

            return;



        var linkId = SelectedWebLink.Id;



        await ExecuteBusyAsync(async () =>

        {

            await _webLinkService.DeleteAsync(linkId).ConfigureAwait(true);

            await ReloadAsync().ConfigureAwait(true);

            ClearForm();

        }, "Eliminando enlace...").ConfigureAwait(true);

    }



    [RelayCommand]

    private async Task OpenAsync(WebLink? link = null)

    {

        var target = link ?? SelectedWebLink;

        if (target is null)

            return;



        var linkId = target.Id;



        await ExecuteBusyAsync(async () =>

        {

            var opened = await _webLinkService.OpenInBrowserAsync(linkId).ConfigureAwait(true);

            if (!opened)

                throw new InvalidOperationException("No se pudo abrir el enlace en el navegador.");

        }, "Abriendo enlace...").ConfigureAwait(true);

    }

}



public sealed record CategoryFilterOption(string Label, LinkCategory? Category);



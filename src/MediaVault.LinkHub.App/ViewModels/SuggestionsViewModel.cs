using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MediaVault.LinkHub.App.Shell;
using MediaVault.LinkHub.App.ViewModels.Base;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Domain.Enums;

using Microsoft.Win32;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class SuggestionsViewModel : ViewModelBase, INavigableViewModel
{
    private readonly ISuggestionService _suggestionService;
    private readonly IAppDialogService _appDialogService;

    public SuggestionsViewModel(
        ISuggestionService suggestionService,
        IAppDialogService appDialogService)
    {
        _suggestionService = suggestionService;
        _appDialogService = appDialogService;
        KindOptions = Enum.GetValues<SuggestionKind>();
    }

    public string Title => "Sugerencias";

    public string Subtitle => "Mejoras, errores y capturas del aplicativo";

    public SuggestionKind[] KindOptions { get; }

    public ObservableCollection<Suggestion> Suggestions { get; } = [];

    public ObservableCollection<SuggestionAttachmentItem> Attachments { get; } = [];

    [ObservableProperty]
    private Suggestion? _selectedSuggestion;

    [ObservableProperty]
    private string _texto = string.Empty;

    [ObservableProperty]
    private SuggestionKind _tipo = SuggestionKind.Mejora;

    [ObservableProperty]
    private bool _resuelto;

    [ObservableProperty]
    private string? _fechaCreacionTexto;

    [ObservableProperty]
    private string? _fechaResueltoTexto;

    /// <summary>null = todas, false = pendientes, true = resueltas.</summary>
    [ObservableProperty]
    private bool? _filterResolved;

    [ObservableProperty]
    private DateTime? _filterFromDate;

    [ObservableProperty]
    private DateTime? _filterToDate;

    [ObservableProperty]
    private string _filterLabel = "Todas";

    public Task InitializeAsync() =>
        RunBusyCoreAsync(() => ReloadAsync(), "Cargando sugerencias...");

    private async Task ReloadAsync(int? reselectId = null)
    {
        var items = await _suggestionService.GetAllAsync(FilterResolved).ConfigureAwait(true);
        items = ApplyDateFilter(items);

        Suggestions.Clear();
        foreach (var item in items)
            Suggestions.Add(item);

        if (reselectId.HasValue)
            SelectedSuggestion = Suggestions.FirstOrDefault(item => item.Id == reselectId.Value);
        else if (SelectedSuggestion is not null)
            SelectedSuggestion = Suggestions.FirstOrDefault(item => item.Id == SelectedSuggestion.Id);

        UpdateFilterLabel();
    }

    private IReadOnlyList<Suggestion> ApplyDateFilter(IReadOnlyList<Suggestion> items)
    {
        if (!FilterFromDate.HasValue && !FilterToDate.HasValue)
            return items;

        var from = FilterFromDate?.Date;
        var to = FilterToDate?.Date;

        return items
            .Where(item =>
            {
                var createdLocal = ToLocalDate(item.FechaCreacion);
                if (from.HasValue && createdLocal < from.Value)
                    return false;
                if (to.HasValue && createdLocal > to.Value)
                    return false;
                return true;
            })
            .ToList();
    }

    private void UpdateFilterLabel()
    {
        var status = FilterResolved switch
        {
            true => "Resueltas",
            false => "Pendientes",
            _ => "Todas"
        };

        if (!FilterFromDate.HasValue && !FilterToDate.HasValue)
        {
            FilterLabel = status;
            return;
        }

        var fromText = FilterFromDate?.ToString("dd/MM/yyyy") ?? "…";
        var toText = FilterToDate?.ToString("dd/MM/yyyy") ?? "…";
        FilterLabel = $"{status} · {fromText} → {toText}";
    }

    partial void OnSelectedSuggestionChanged(Suggestion? value)
    {
        Attachments.Clear();

        if (value is null)
        {
            Texto = string.Empty;
            Tipo = SuggestionKind.Mejora;
            Resuelto = false;
            FechaCreacionTexto = null;
            FechaResueltoTexto = null;
            return;
        }

        Texto = value.Texto;
        Tipo = value.Tipo;
        Resuelto = value.Resuelto;
        FechaCreacionTexto = FormatLocal(value.FechaCreacion);
        FechaResueltoTexto = value.FechaResuelto.HasValue
            ? FormatLocal(value.FechaResuelto.Value)
            : null;

        foreach (var attachment in value.Attachments.OrderBy(item => item.FechaCreacion))
            Attachments.Add(new SuggestionAttachmentItem(attachment));
    }

    partial void OnFilterResolvedChanged(bool? value) =>
        _ = RunBusyCoreAsync(() => ReloadAsync(SelectedSuggestion?.Id), "Filtrando...");

    partial void OnFilterFromDateChanged(DateTime? value) =>
        _ = RunBusyCoreAsync(() => ReloadAsync(SelectedSuggestion?.Id), "Filtrando por fecha...");

    partial void OnFilterToDateChanged(DateTime? value) =>
        _ = RunBusyCoreAsync(() => ReloadAsync(SelectedSuggestion?.Id), "Filtrando por fecha...");

    [RelayCommand]
    private void ShowAll() => FilterResolved = null;

    [RelayCommand]
    private void ShowPending() => FilterResolved = false;

    [RelayCommand]
    private void ShowResolved() => FilterResolved = true;

    [RelayCommand]
    private void ClearDateFilter()
    {
        if (!FilterFromDate.HasValue && !FilterToDate.HasValue)
            return;

        FilterFromDate = null;
        FilterToDate = null;
    }

    [RelayCommand]
    private void ClearForm()
    {
        SelectedSuggestion = null;
        Texto = string.Empty;
        Tipo = SuggestionKind.Mejora;
        Resuelto = false;
        FechaCreacionTexto = null;
        FechaResueltoTexto = null;
        Attachments.Clear();
    }

    [RelayCommand]
    private void AddImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleccionar imagen",
            Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif|Todos|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
            return;

        foreach (var path in dialog.FileNames)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                continue;

            if (SelectedSuggestion is null)
            {
                Attachments.Add(new SuggestionAttachmentItem(path));
                continue;
            }

            var suggestionId = SelectedSuggestion.Id;
            _ = ExecuteBusyAsync(async () =>
            {
                await _suggestionService.AddAttachmentAsync(suggestionId, path).ConfigureAwait(true);
                await ReloadAsync(suggestionId).ConfigureAwait(true);
            }, "Adjuntando imagen...").ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void OpenAttachment(SuggestionAttachmentItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.FilePath))
            return;

        if (!File.Exists(item.FilePath))
        {
            ErrorMessage = "No se encontró el archivo de la imagen.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = item.FilePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo abrir la imagen: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RemoveAttachmentAsync(SuggestionAttachmentItem? item)
    {
        if (item is null)
            return;

        if (item.IsPending)
        {
            Attachments.Remove(item);
            return;
        }

        if (SelectedSuggestion is null)
            return;

        var suggestionId = SelectedSuggestion.Id;
        await ExecuteBusyAsync(async () =>
        {
            await _suggestionService.RemoveAttachmentAsync(item.Id).ConfigureAwait(true);
            await ReloadAsync(suggestionId).ConfigureAwait(true);
        }, "Eliminando imagen...").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Texto))
        {
            ErrorMessage = "Escriba el texto de la sugerencia o del error.";
            return;
        }

        await ExecuteBusyAsync(async () =>
        {
            if (SelectedSuggestion is null)
            {
                var pendingPaths = Attachments
                    .Where(item => item.IsPending)
                    .Select(item => item.FilePath)
                    .ToList();

                var created = await _suggestionService
                    .CreateAsync(Texto, Tipo, pendingPaths)
                    .ConfigureAwait(true);

                if (Resuelto)
                    created = await _suggestionService.SetResolvedAsync(created.Id, true).ConfigureAwait(true);

                await ReloadAsync(created.Id).ConfigureAwait(true);
            }
            else
            {
                var id = SelectedSuggestion.Id;
                await _suggestionService.UpdateAsync(id, Texto, Tipo).ConfigureAwait(true);

                if (SelectedSuggestion.Resuelto != Resuelto)
                    await _suggestionService.SetResolvedAsync(id, Resuelto).ConfigureAwait(true);

                await ReloadAsync(id).ConfigureAwait(true);
            }
        }, "Guardando sugerencia...").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ToggleResolvedAsync()
    {
        if (SelectedSuggestion is null)
            return;

        var id = SelectedSuggestion.Id;
        var next = !SelectedSuggestion.Resuelto;

        await ExecuteBusyAsync(async () =>
        {
            await _suggestionService.SetResolvedAsync(id, next).ConfigureAwait(true);
            await ReloadAsync(id).ConfigureAwait(true);
        }, next ? "Marcando como resuelto..." : "Marcando como pendiente...").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedSuggestion is null)
            return;

        if (!_appDialogService.ConfirmYesNo(
                "Eliminar sugerencia",
                "¿Eliminar esta sugerencia y sus imágenes adjuntas?\n\nEsta acción no se puede deshacer.",
                AppDialogKind.Warning))
            return;

        var id = SelectedSuggestion.Id;
        await ExecuteBusyAsync(async () =>
        {
            await _suggestionService.DeleteAsync(id).ConfigureAwait(true);
            ClearForm();
            await ReloadAsync().ConfigureAwait(true);
        }, "Eliminando sugerencia...").ConfigureAwait(true);
    }

    private static DateTime ToLocalDate(DateTime utcOrUnspecified)
    {
        var local = utcOrUnspecified.Kind == DateTimeKind.Utc
            ? utcOrUnspecified.ToLocalTime()
            : utcOrUnspecified;
        return local.Date;
    }

    private static string FormatLocal(DateTime utcOrUnspecified)
    {
        var local = utcOrUnspecified.Kind == DateTimeKind.Utc
            ? utcOrUnspecified.ToLocalTime()
            : utcOrUnspecified;
        return local.ToString("dd/MM/yyyy HH:mm");
    }
}

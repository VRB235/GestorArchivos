using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaVault.LinkHub.App.ViewModels.Base;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class ScratchpadViewModel : ViewModelBase, INavigableViewModel
{
    private readonly IQuickNoteService _quickNoteService;

    public ScratchpadViewModel(IQuickNoteService quickNoteService)
    {
        _quickNoteService = quickNoteService;
    }

    public string Title => "Scratchpad";

    public string Subtitle => "Notas rápidas de texto";

    public ObservableCollection<QuickNote> QuickNotes { get; } = [];

    [ObservableProperty]
    private QuickNote? _selectedNote;

    [ObservableProperty]
    private string _contenido = string.Empty;

    public Task InitializeAsync() =>
        RunBusyCoreAsync(() => ReloadAsync(), "Cargando notas...");

    private async Task ReloadAsync(int? reselectId = null)
    {
        var notes = await _quickNoteService.GetAllAsync().ConfigureAwait(true);
        QuickNotes.Clear();
        foreach (var note in notes)
            QuickNotes.Add(note);

        if (reselectId.HasValue)
            SelectedNote = QuickNotes.FirstOrDefault(note => note.Id == reselectId.Value);
    }

    partial void OnSelectedNoteChanged(QuickNote? value)
    {
        Contenido = value?.Contenido ?? string.Empty;
    }

    [RelayCommand]
    private void ClearForm()
    {
        SelectedNote = null;
        Contenido = string.Empty;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Contenido))
            return;

        await ExecuteBusyAsync(async () =>
        {
            if (SelectedNote is null)
            {
                var created = await _quickNoteService.CreateAsync(Contenido).ConfigureAwait(true);
                await ReloadAsync(created.Id).ConfigureAwait(true);
            }
            else
            {
                var noteId = SelectedNote.Id;
                await _quickNoteService.UpdateAsync(noteId, Contenido).ConfigureAwait(true);
                await ReloadAsync(noteId).ConfigureAwait(true);
            }
        }, "Guardando nota...").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedNote is null)
            return;

        var noteId = SelectedNote.Id;

        await ExecuteBusyAsync(async () =>
        {
            await _quickNoteService.DeleteAsync(noteId).ConfigureAwait(true);
            SelectedNote = null;
            Contenido = string.Empty;
            await ReloadAsync().ConfigureAwait(true);
        }, "Eliminando nota...").ConfigureAwait(true);
    }
}

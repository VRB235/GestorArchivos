using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;

using MediaVault.LinkHub.App.Shell;
using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class SuggestionAttachmentItem : ObservableObject
{
    public SuggestionAttachmentItem(SuggestionAttachment attachment)
    {
        Id = attachment.Id;
        FilePath = attachment.FilePath;
        OriginalFileName = attachment.OriginalFileName;
        Image = LocalImageLoader.TryLoad(attachment.FilePath, decodePixelWidth: 160);
    }

    public SuggestionAttachmentItem(string pendingSourcePath)
    {
        Id = 0;
        FilePath = pendingSourcePath;
        OriginalFileName = System.IO.Path.GetFileName(pendingSourcePath);
        IsPending = true;
        Image = LocalImageLoader.TryLoad(pendingSourcePath, decodePixelWidth: 160);
    }

    public int Id { get; }

    public string FilePath { get; }

    public string OriginalFileName { get; }

    public bool IsPending { get; }

    public ImageSource? Image { get; }
}

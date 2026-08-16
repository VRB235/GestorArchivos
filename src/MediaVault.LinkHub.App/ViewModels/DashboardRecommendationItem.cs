using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;

using MediaVault.LinkHub.Application.Media;
using MediaVault.LinkHub.Application.Models.Dashboard;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class DashboardRecommendationItem : ObservableObject
{
    public DashboardRecommendationItem(MediaFileViewStats video, bool showTierLabel)
    {
        Video = video;
        ShowTierLabel = showTierLabel;
    }

    public MediaFileViewStats Video { get; }

    public bool ShowTierLabel { get; }

    public int RankingStars => MediaFileRankingScale.ToDisplayStars(Video.RankingGlobal);

    public string OpenCountLabel
    {
        get
        {
            var opens = Video.VecesAbierto;
            return opens == 1 ? "1 apertura" : $"{opens} aperturas";
        }
    }

    public string TierLabel
    {
        get
        {
            if (!ShowTierLabel)
                return string.Empty;

            return RankingStars > 0
                ? $"Entre videos con {RankingStars} ★"
                : "Sin calificar (completar lote)";
        }
    }

    [ObservableProperty]
    private ImageSource? _thumbnail;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResolution))]
    [NotifyPropertyChangedFor(nameof(ResolutionDisplay))]
    private string? _resolutionLabel;

    public bool HasResolution => !string.IsNullOrWhiteSpace(ResolutionLabel);

    public string ResolutionDisplay => HasResolution
        ? $"Resolución: {ResolutionLabel}"
        : "Resolución: no disponible";
}

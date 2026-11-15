using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DauChat.Client.UI.Infrastructure;
using DauChat.Client.UI.Models;
using DauChat.Client.UI.Services;
using DauChat.Shared.Enums;

namespace DauChat.Client.UI.ViewModels;

public sealed class FilesLibraryViewModel : ObservableObject
{
    private readonly Action<string> _notify;
    private string _searchKeyword = string.Empty;
    private FileCategory _selectedCategory = FileCategory.All;
    private int _totalFilesCount;
    private int _recentFilesCount;
    private string _totalSizeLabel = "0 B";

    public FilesLibraryViewModel(Action<string> notify)
    {
        _notify = notify;
        SelectCategoryCommand = new AsyncRelayCommand(async param =>
        {
            if (param is string catStr && Enum.TryParse<FileCategory>(catStr, out var cat))
            {
                SelectedCategory = cat;
                await RefreshAsync();
            }
        });
        OpenFileCommand = new AsyncRelayCommand(async param =>
        {
            if (param is FileItemModel item)
            {
                var res = await ClientServiceHost.Current.FileStorage.OpenLocalFileAsync(item.LocalFullPath);
                _notify(res.Message);
            }
        });
        DeleteFileCommand = new AsyncRelayCommand(async param =>
        {
            if (param is FileItemModel item && ClientServiceHost.Current.AuthRoom.CurrentUser is { } user)
            {
                var confirm = MessageBox.Show(
                    $"Xóa tệp '{item.FileName}' khỏi kho lưu trữ Local?",
                    "Xóa tệp cục bộ",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;

                var res = await ClientServiceHost.Current.FileStorage.DeleteLocalFileAsync(user.UserId, item.FileId);
                _notify(res.Message);
                await RefreshAsync();
            }
        });
    }

    public ObservableCollection<FileItemModel> Files { get; } = new();

    public ICommand SelectCategoryCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand DeleteFileCommand { get; }

    public string SearchKeyword
    {
        get => _searchKeyword;
        set
        {
            if (SetProperty(ref _searchKeyword, value))
            {
                _ = RefreshAsync();
            }
        }
    }

    public FileCategory SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    public int TotalFilesCount
    {
        get => _totalFilesCount;
        set => SetProperty(ref _totalFilesCount, value);
    }

    public int RecentFilesCount
    {
        get => _recentFilesCount;
        set => SetProperty(ref _recentFilesCount, value);
    }

    public string TotalSizeLabel
    {
        get => _totalSizeLabel;
        set => SetProperty(ref _totalSizeLabel, value);
    }

    public async Task RefreshAsync()
    {
        var user = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (user is null) return;

        var host = ClientServiceHost.Current;
        var all = await host.FileStorage.GetAllLocalFilesAsync(user.UserId);
        TotalFilesCount = all.Count;
        RecentFilesCount = all.Count(f => f.DownloadedAtUtc >= DateTime.UtcNow.AddDays(-7));
        TotalSizeLabel = DemoFileStorageService.FormatBytes(all.Sum(f => f.FileSizeBytes));

        var filtered = await host.FileStorage.GetAllLocalFilesAsync(
            user.UserId,
            SelectedCategory == FileCategory.All ? null : SelectedCategory,
            SearchKeyword);

        var rooms = host.DemoStore.Rooms.ToDictionary(r => r.RoomId, r => r.RoomName);
        var users = host.DemoStore.Accounts.ToDictionary(a => a.User.UserId, a => a.User.FullName);

        Files.Clear();
        foreach (var f in filtered)
        {
            var (bg, fg) = f.Category switch
            {
                FileCategory.PDF => ("#FEF2F2", "#DC2626"),
                FileCategory.CAD => ("#EFF6FF", "#2563EB"),
                FileCategory.Archive => ("#FFFBEB", "#D97706"),
                FileCategory.Image => ("#F5F3FF", "#7C3AED"),
                _ => ("#ECFDF5", "#047857")
            };

            var convName = rooms.GetValueOrDefault(f.RoomOrPartnerId);
            if (convName is null && f.RoomOrPartnerId.StartsWith("dm:", StringComparison.Ordinal))
            {
                var parts = f.RoomOrPartnerId[3..].Split("::");
                var otherId = parts.FirstOrDefault(p => p != user.UserId) ?? parts.FirstOrDefault() ?? f.RoomOrPartnerId;
                convName = $"1-1: {users.GetValueOrDefault(otherId) ?? otherId}";
            }
            convName ??= users.GetValueOrDefault(f.RoomOrPartnerId) ?? f.RoomOrPartnerId;

            Files.Add(new FileItemModel
            {
                FileId = f.FileId,
                FileName = f.FileName,
                Category = f.Category,
                CategoryLabel = f.Category switch
                {
                    FileCategory.CAD => "CAD",
                    FileCategory.PDF => "PDF",
                    FileCategory.Archive => "ZIP",
                    FileCategory.Image => "ẢNH",
                    FileCategory.Document => "DOC",
                    _ => "TỆP"
                },
                SizeAndTimeText = $"{DemoFileStorageService.FormatBytes(f.FileSizeBytes)} • Gửi bởi {f.SenderName}",
                ConversationName = convName,
                SenderName = f.SenderName,
                Sha256Short = f.Sha256Checksum.Length >= 12 ? $"SHA-256 ✓ {f.Sha256Checksum[..12]}..." : "SHA-256 ✓",
                LocalFullPath = f.LocalFullPath,
                IconBgHex = bg,
                IconFgHex = fg
            });
        }
    }
}

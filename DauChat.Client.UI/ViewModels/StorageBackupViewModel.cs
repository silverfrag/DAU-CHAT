using System.Collections.ObjectModel;
using System.Windows.Input;
using DauChat.Client.UI.Infrastructure;
using DauChat.Client.UI.Models;
using DauChat.Client.UI.Services;
using DauChat.Shared.Enums;
using Microsoft.Win32;

namespace DauChat.Client.UI.ViewModels;

public sealed class StorageBackupViewModel : ObservableObject
{
    private readonly Action<string> _notify;
    private readonly Func<Task>? _onStorageDataChangedAsync;
    private bool _suppressSettingsSave;
    private string _usedCapacityText = "0 B";
    private string _quotaCapacityText = "10.00 GB";
    private string _documentsBytesText = "0 B";
    private string _imagesBytesText = "0 B";
    private string _messagesBytesText = "0 B";
    private string _otherBytesText = "0 B";
    private double _docsSharePercent = 45;
    private double _imagesSharePercent = 20;
    private double _messagesSharePercent = 12;
    private double _otherSharePercent = 8;
    private bool _autoClean90Days;
    private string _cleanupDaysText = "90";
    private string _lastBackupLabel = "Chưa sao lưu trong phiên này";
    private string _localFolderPathLabel = "LocalData/{UserId}/";

    public StorageBackupViewModel(Action<string> notify, Func<Task>? onStorageDataChangedAsync = null)
    {
        _notify = notify;
        _onStorageDataChangedAsync = onStorageDataChangedAsync;
        ExportBackupZipCommand = new AsyncRelayCommand(ExportBackupZipAsync);
        RestoreBackupZipCommand = new AsyncRelayCommand(RestoreBackupZipAsync);
        CleanupOldFilesCommand = new AsyncRelayCommand(CleanupOldFilesAsync);
    }

    public ObservableCollection<RoomStorageItemModel> RoomBreakdowns { get; } = new();

    public ICommand ExportBackupZipCommand { get; }
    public ICommand RestoreBackupZipCommand { get; }
    public ICommand CleanupOldFilesCommand { get; }

    public string UsedCapacityText { get => _usedCapacityText; set => SetProperty(ref _usedCapacityText, value); }
    public string QuotaCapacityText { get => _quotaCapacityText; set => SetProperty(ref _quotaCapacityText, value); }
    public string DocumentsBytesText { get => _documentsBytesText; set => SetProperty(ref _documentsBytesText, value); }
    public string ImagesBytesText { get => _imagesBytesText; set => SetProperty(ref _imagesBytesText, value); }
    public string MessagesBytesText { get => _messagesBytesText; set => SetProperty(ref _messagesBytesText, value); }
    public string OtherBytesText { get => _otherBytesText; set => SetProperty(ref _otherBytesText, value); }
    public double DocsSharePercent { get => _docsSharePercent; set => SetProperty(ref _docsSharePercent, value); }
    public double ImagesSharePercent { get => _imagesSharePercent; set => SetProperty(ref _imagesSharePercent, value); }
    public double MessagesSharePercent { get => _messagesSharePercent; set => SetProperty(ref _messagesSharePercent, value); }
    public double OtherSharePercent { get => _otherSharePercent; set => SetProperty(ref _otherSharePercent, value); }

    public bool AutoClean90Days
    {
        get => _autoClean90Days;
        set
        {
            if (SetProperty(ref _autoClean90Days, value) && !_suppressSettingsSave)
            {
                PersistAutoCleanSettings(allowDefaultDays: true);
            }
        }
    }

    public string CleanupDaysText
    {
        get => _cleanupDaysText;
        set
        {
            if (SetProperty(ref _cleanupDaysText, value) && !_suppressSettingsSave)
            {
                PersistAutoCleanSettings(allowDefaultDays: false);
            }
        }
    }

    public string LastBackupLabel
    {
        get => _lastBackupLabel;
        set => SetProperty(ref _lastBackupLabel, value);
    }

    public string LocalFolderPathLabel
    {
        get => _localFolderPathLabel;
        set => SetProperty(ref _localFolderPathLabel, value);
    }

    public async Task RefreshAsync()
    {
        var user = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (user is null) return;

        var (enabled, days) = ClientServiceHost.Current.DemoStore.GetAutoCleanConfig(user.UserId);
        _suppressSettingsSave = true;
        try
        {
            AutoClean90Days = enabled;
            CleanupDaysText = days.ToString();
        }
        finally
        {
            _suppressSettingsSave = false;
        }

        LocalFolderPathLabel = $"Thư mục cục bộ: LocalData/{user.UserId}/Messages & Files";
        var storage = ClientServiceHost.Current.FileStorage;
        var stats = await storage.GetStorageStatsAsync(user.UserId);
        var files = await storage.GetAllLocalFilesAsync(user.UserId);

        long docsBytes = files.Where(f => f.Category is FileCategory.PDF or FileCategory.CAD or FileCategory.Document).Sum(f => f.FileSizeBytes);
        long imgBytes = files.Where(f => f.Category == FileCategory.Image).Sum(f => f.FileSizeBytes);
        long otherBytes = files.Where(f => f.Category is FileCategory.Archive or FileCategory.Other).Sum(f => f.FileSizeBytes);
        long msgBytes = stats.TotalMessagesBytes;
        long total = Math.Max(1L, docsBytes + imgBytes + otherBytes + msgBytes);

        UsedCapacityText = DemoFileStorageService.FormatBytes(stats.TotalUsedBytes);
        DocumentsBytesText = DemoFileStorageService.FormatBytes(docsBytes);
        ImagesBytesText = DemoFileStorageService.FormatBytes(imgBytes);
        MessagesBytesText = DemoFileStorageService.FormatBytes(msgBytes);
        OtherBytesText = DemoFileStorageService.FormatBytes(otherBytes);

        // Chừa khoảng trống thị giác trên thanh tổng (tính theo tỉ lệ trên tổng 512MB quota demo để nhìn cân đối)
        const double demoBarCeiling = 260_000_000d;
        var denom = Math.Max(demoBarCeiling, (double)total);
        DocsSharePercent = Math.Max(4, docsBytes * 100d / denom);
        ImagesSharePercent = Math.Max(imgBytes > 0 ? 3 : 0, imgBytes * 100d / denom);
        MessagesSharePercent = Math.Max(2, msgBytes * 100d / denom);
        OtherSharePercent = Math.Max(otherBytes > 0 ? 6 : 0, otherBytes * 100d / denom);

        var barColors = new[] { "#CB0914", "#F59E0B", "#2563EB", "#7C3AED" };
        var maxRoom = Math.Max(1L, stats.BreakdownByConversation.Select(b => b.FilesBytes + b.MessagesBytes).DefaultIfEmpty(1L).Max());

        RoomBreakdowns.Clear();
        for (var i = 0; i < stats.BreakdownByConversation.Count; i++)
        {
            var b = stats.BreakdownByConversation[i];
            var sum = b.FilesBytes + b.MessagesBytes;
            RoomBreakdowns.Add(new RoomStorageItemModel
            {
                DisplayName = b.DisplayName,
                UsedText = DemoFileStorageService.FormatBytes(sum),
                DetailText = $"{b.FileCount} tệp • {DemoFileStorageService.FormatBytes(b.MessagesBytes)} log .jsonl",
                BarPercent = Math.Max(6, sum * 100d / maxRoom),
                BarHex = barColors[i % barColors.Length]
            });
        }
    }

    private async Task ExportBackupZipAsync()
    {
        var user = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (user is null) return;

        var dlg = new SaveFileDialog
        {
            Title = "Xuất gói sao lưu dữ liệu DAU Chat (.ZIP)",
            Filter = "Tệp nén ZIP (*.zip)|*.zip",
            FileName = $"DAUChat_Backup_{user.Username}_{DateTime.Now:yyyyMMdd}.zip"
        };

        if (dlg.ShowDialog() != true) return;

        var res = await ClientServiceHost.Current.FileStorage.ExportBackupZipAsync(user.UserId, dlg.FileName);
        if (res.Success)
        {
            LastBackupLabel = $"Lần cuối: {DateTime.Now:dd/MM/yyyy HH:mm}";
        }
        _notify(res.Message);
    }

    private async Task RestoreBackupZipAsync()
    {
        var user = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (user is null) return;

        var dlg = new OpenFileDialog
        {
            Title = "Chọn tệp sao lưu .ZIP để khôi phục về máy",
            Filter = "Tệp nén ZIP (*.zip)|*.zip"
        };

        if (dlg.ShowDialog() != true) return;

        var res = await ClientServiceHost.Current.FileStorage.RestoreBackupZipAsync(user.UserId, dlg.FileName);
        _notify(res.Message);
        if (res.Success)
        {
            await RefreshAsync();
            if (_onStorageDataChangedAsync is not null)
            {
                await _onStorageDataChangedAsync();
            }
        }
    }

    private async Task CleanupOldFilesAsync()
    {
        var user = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (user is null) return;

        var days = int.TryParse(CleanupDaysText.Trim(), out var d) && d > 0 ? d : 90;
        var res = await ClientServiceHost.Current.FileStorage.CleanupOldFilesAsync(user.UserId, olderThanDays: days);
        _notify(res.Message);
        await RefreshAsync();
        if (res.Success && res.Data is { DeletedFilesCount: > 0 } && _onStorageDataChangedAsync is not null)
        {
            await _onStorageDataChangedAsync();
        }
    }

    public async Task RunStartupAutoCleanIfEnabledAsync()
    {
        var user = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (user is null) return;

        var (enabled, days) = ClientServiceHost.Current.DemoStore.GetAutoCleanConfig(user.UserId);
        if (!enabled) return;

        var res = await ClientServiceHost.Current.FileStorage.CleanupOldFilesAsync(user.UserId, olderThanDays: days);
        if (res.Success && res.Data is { DeletedFilesCount: > 0 } data)
        {
            await RefreshAsync();
            if (_onStorageDataChangedAsync is not null)
            {
                await _onStorageDataChangedAsync();
            }
            _notify($"Tự động dọn {data.DeletedFilesCount} tệp cũ quá {days} ngày ({DemoFileStorageService.FormatBytes(data.FreedBytes)}).");
        }
    }

    private void PersistAutoCleanSettings(bool allowDefaultDays)
    {
        var cu = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (cu is null) return;

        if (int.TryParse(CleanupDaysText.Trim(), out var parsedDays) && parsedDays > 0)
        {
            ClientServiceHost.Current.DemoStore.SetAutoCleanConfig(cu.UserId, AutoClean90Days, parsedDays);
        }
        else if (allowDefaultDays)
        {
            var (_, currentDays) = ClientServiceHost.Current.DemoStore.GetAutoCleanConfig(cu.UserId);
            ClientServiceHost.Current.DemoStore.SetAutoCleanConfig(cu.UserId, AutoClean90Days, currentDays);
        }
    }
}


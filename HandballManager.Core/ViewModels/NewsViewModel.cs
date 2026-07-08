using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandballManager.Data;
using HandballManager.Models;
using Microsoft.EntityFrameworkCore;

namespace HandballManager.ViewModels;

public partial class NewsViewModel : BaseViewModel
{
    private readonly HandballDbContext _db;
    private readonly Func<Task>? _onNewsRead;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredItems))]
    private ObservableCollection<NewsItem> _items = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredItems))]
    private string _activeTab = "All";

    public IEnumerable<NewsItem> FilteredItems => ActiveTab switch
    {
        "Transfers" => Items.Where(n => n.NewsType == "Transfer"),
        "Retirements" => Items.Where(n => n.NewsType == "RetirementAnnouncement"),
        _ => Items
    };

    public NewsViewModel(HandballDbContext db, Func<Task>? onNewsRead = null)
    {
        _db = db;
        _onNewsRead = onNewsRead;
    }

    public async Task InitializeAsync()
    {
        var list = await _db.NewsItems
            .OrderByDescending(n => n.PublishedAt)
            .Take(100)
            .ToListAsync();
        Items = new ObservableCollection<NewsItem>(list);

        // Mark all as read and notify badge to refresh
        var unread = await _db.NewsItems.Where(n => !n.IsRead).ToListAsync();
        if (unread.Count > 0)
        {
            foreach (var item in unread) item.IsRead = true;
            await _db.SaveChangesAsync();
            if (_onNewsRead != null) await _onNewsRead();
        }
    }

    [RelayCommand]
    private void SetTab(string tab)
    {
        ActiveTab = tab;
    }
}
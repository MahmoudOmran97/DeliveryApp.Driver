using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeliveryApp.Driver.Models;
using DeliveryApp.Driver.Services;
using System.Collections.ObjectModel;

namespace DeliveryApp.Driver.ViewModels;

public partial class NotificationsViewModel : BaseViewModel
{
    readonly ApiService _api;

    [ObservableProperty] bool _isRefreshing;
    [ObservableProperty] int _unreadCount;

    public ObservableCollection<Notification> Notifications { get; } = new();

    public NotificationsViewModel(ApiService api) { _api = api; }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsRefreshing = true;
        try
        {
            var result = await _api.GetNotificationsAsync();
            Notifications.Clear();
            if (result?.Data != null)
            {
                foreach (var n in result.Data) Notifications.Add(n);
                UnreadCount = result.Data.Count(n => !n.IsRead);
            }
        }
        finally { IsRefreshing = false; }
    }

    [RelayCommand]
    async Task MarkAllReadAsync()
    {
        await _api.MarkAllReadAsync();
        foreach (var n in Notifications) n.IsRead = true;
        UnreadCount = 0;
    }
}
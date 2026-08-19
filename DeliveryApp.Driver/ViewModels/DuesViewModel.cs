using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeliveryApp.Driver.Models;
using DeliveryApp.Driver.Services;
using System.Collections.ObjectModel;

namespace DeliveryApp.Driver.ViewModels;

public partial class DuesViewModel : BaseViewModel
{
    readonly ApiService _api;

    [ObservableProperty] bool _isRefreshing;
    [ObservableProperty] DriverDuesSummary? _summary;
    [ObservableProperty] bool _hasDues;

    public ObservableCollection<DriverDue> Dues { get; } = new();

    public DuesViewModel(ApiService api)
    {
        _api = api;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsRefreshing = true;
        try
        {
            var summaryTask = _api.GetMyDuesSummaryAsync();
            var duesTask = _api.GetMyDuesAsync();
            await Task.WhenAll(summaryTask, duesTask);

            Summary = summaryTask.Result;

            Dues.Clear();
            foreach (var due in duesTask.Result ?? new List<DriverDue>())
                Dues.Add(due);

            HasDues = Dues.Count > 0;
        }
        finally { IsRefreshing = false; }
    }
}

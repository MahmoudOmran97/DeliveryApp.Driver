using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeliveryApp.Driver.Models;
using DeliveryApp.Driver.Services;
using System.Collections.ObjectModel;

namespace DeliveryApp.Driver.ViewModels;

public partial class EarningsViewModel : BaseViewModel
{
    readonly ApiService _api;

    [ObservableProperty] string _selectedPeriod = "today";
    [ObservableProperty] EarningsResult? _earnings;
    [ObservableProperty] bool _isRefreshing;

    public List<string> Periods { get; } = new() { "today", "week", "month" };
    public List<string> PeriodLabels { get; } = new() { "Today", "This Week", "This Month" };

    public string SelectedPeriodLabel => SelectedPeriod switch
    {
        "today" => "Today",
        "week" => "This Week",
        "month" => "This Month",
        _ => SelectedPeriod
    };

    public EarningsViewModel(ApiService api)
    {
        _api = api;
    }

    partial void OnSelectedPeriodChanged(string value)
        => MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsRefreshing = true;
        try
        {
            Earnings = await _api.GetEarningsAsync(SelectedPeriod);
        }
        finally { IsRefreshing = false; }
    }
}

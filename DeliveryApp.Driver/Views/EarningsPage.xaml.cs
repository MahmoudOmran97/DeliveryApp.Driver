using DeliveryApp.Driver.ViewModels;

namespace DeliveryApp.Driver.Views;

public partial class EarningsPage : ContentPage
{
    readonly EarningsViewModel _vm;

    public EarningsPage(EarningsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _vm.LoadCommand.ExecuteAsync(null);
        UpdatePeriodButtons();
    }

    void UpdatePeriodButtons()
    {
        var selected = Color.FromArgb("#FF5722");
        var unselected = Color.FromArgb("#F5F5F5");
        var selectedText = Colors.White;
        var unselectedText = Color.FromArgb("#757575");

        BtnToday.BackgroundColor = _vm.SelectedPeriod == "today" ? selected : unselected;
        BtnWeek.BackgroundColor = _vm.SelectedPeriod == "week" ? selected : unselected;
        BtnMonth.BackgroundColor = _vm.SelectedPeriod == "month" ? selected : unselected;
        BtnToday.TextColor = _vm.SelectedPeriod == "today" ? selectedText : unselectedText;
        BtnWeek.TextColor = _vm.SelectedPeriod == "week" ? selectedText : unselectedText;
        BtnMonth.TextColor = _vm.SelectedPeriod == "month" ? selectedText : unselectedText;
    }

    private void OnTodayTapped(object sender, EventArgs e)
    {
        _vm.SelectedPeriod = "today";
        UpdatePeriodButtons();
    }

    private void OnWeekTapped(object sender, EventArgs e)
    {
        _vm.SelectedPeriod = "week";
        UpdatePeriodButtons();
    }

    private void OnMonthTapped(object sender, EventArgs e)
    {
        _vm.SelectedPeriod = "month";
        UpdatePeriodButtons();
    }
}

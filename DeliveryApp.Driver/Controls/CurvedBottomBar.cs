using DeliveryApp.Driver.Services;
using Microsoft.Maui.Controls.Shapes;

namespace DeliveryApp.Driver.Controls;

public class CurvedBottomBar : ContentView
{
    public static readonly BindableProperty SelectedTabProperty =
        BindableProperty.Create(
            nameof(SelectedTab),
            typeof(string),
            typeof(CurvedBottomBar),
            "home",
            propertyChanged: (bindable, _, _) => ((CurvedBottomBar)bindable).RefreshState());

    private readonly GraphicsView _background;
    private readonly CurvedBarDrawable _drawable = new();
    private readonly List<(Border Bubble, Image Icon, Label Label)> _items = new();

    // Home in center; profile before settings (matches app tab order on the right side).
    private readonly (string Key, string Route, string Icon, string LabelKey)[] _tabs =
    {
        ("orders", "//AvailableOrdersPage", "tab_orders.svg", "Tab_Orders"),
        ("notifications", "//NotificationsPage", "tab_notifications.svg", "Tab_Notifications"),
        ("home", "//HomePage", "tab_home.svg", "Tab_Home"),
        ("profile", "//ProfilePage", "tab_profile.svg", "Tab_Profile"),
        ("settings", "//SettingsPage", "tab_settings.svg", "Tab_Settings")
    };

    public string SelectedTab
    {
        get => (string)GetValue(SelectedTabProperty);
        set => SetValue(SelectedTabProperty, value);
    }

    public CurvedBottomBar()
    {
        FlowDirection = FlowDirection.LeftToRight;
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.End;
        Padding = 0;
        Margin = 0;
        HeightRequest = 92;

        _background = new GraphicsView
        {
            Drawable = _drawable,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        var buttonGrid = new Grid
        {
            FlowDirection = FlowDirection.LeftToRight,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Padding = new Thickness(0, 8, 0, 0),
            ColumnSpacing = 0
        };

        for (int i = 0; i < 5; i++)
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        for (int index = 0; index < _tabs.Length; index++)
        {
            var bubble = new Border
            {
                WidthRequest = 54,
                HeightRequest = 54,
                StrokeShape = new RoundRectangle { CornerRadius = 27 },
                Stroke = Colors.Transparent,
                BackgroundColor = Colors.Transparent,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Start
            };

            var icon = new Image
            {
                Source = _tabs[index].Icon,
                WidthRequest = 21,
                HeightRequest = 21,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            bubble.Content = icon;

            var label = new Label
            {
                Text = LocalizationService.Get(_tabs[index].LabelKey),
                FontSize = 12,
                HorizontalOptions = LayoutOptions.Center,
                FontFamily = "CairoBold",
                Margin = new Thickness(0, 0, 0, 0)
            };

            var stack = new VerticalStackLayout
            {
                FlowDirection = FlowDirection.LeftToRight,
                Spacing = 1,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, -2, 0, 0),
                Children = { bubble, label }
            };

            var tap = new TapGestureRecognizer();
            var route = _tabs[index].Route;
            var tabKey = _tabs[index].Key;
            tap.Tapped += async (_, _) =>
            {
                if (string.Equals(SelectedTab, tabKey, StringComparison.OrdinalIgnoreCase))
                    return;

                if (Shell.Current is not null)
                    await Shell.Current.GoToAsync(route);
            };
            stack.GestureRecognizers.Add(tap);

            buttonGrid.Add(stack);
            Grid.SetColumn(stack, index);
            _items.Add((bubble, icon, label));
        }

        var layout = new Grid
        {
            FlowDirection = FlowDirection.LeftToRight,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star)
            }
        };
        layout.Children.Add(_background);
        layout.Children.Add(buttonGrid);

        Content = layout;
        RefreshState();
    }

    private void RefreshState()
    {
        var selectedIndex = Array.FindIndex(_tabs, t => string.Equals(t.Key, SelectedTab, StringComparison.OrdinalIgnoreCase));
        if (selectedIndex < 0) selectedIndex = 2;

        _drawable.SelectedIndex = selectedIndex;
        _background.Invalidate();

        for (int i = 0; i < _items.Count; i++)
        {
            var active = i == selectedIndex;
            _items[i].Bubble.BackgroundColor = active ? Color.FromArgb("#FF5722") : Colors.Transparent;
            _items[i].Label.TextColor = active ? Color.FromArgb("#FF5722") : Color.FromArgb("#D1D8DB");
            _items[i].Icon.Opacity = active ? 1 : 0.78;
        }
    }
}

internal sealed class CurvedBarDrawable : IDrawable
{
    public int SelectedIndex { get; set; } = 2;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();
        canvas.Antialias = true;

        var barColor = Color.FromArgb("#1F2A30");
        var topRadius = 24f;
        var top = 0f;
        var width = dirtyRect.Width;
        var height = dirtyRect.Height;
        const float sideInset = 20f;
        var itemWidth = (width - (sideInset * 2)) / 5f;
        var rawCenterX = sideInset + (itemWidth * SelectedIndex) + (itemWidth / 2f);
        var waveHalf = 34f;
        var waveDepth = 20f;
        var minCenter = waveHalf + 4f;
        var maxCenter = width - waveHalf - 4f;
        var cx = Math.Clamp(rawCenterX, minCenter, maxCenter);

        var path = new PathF();
        path.MoveTo(0, top + topRadius);
        path.QuadTo(0, top, topRadius, top);

        if (cx - waveHalf > topRadius)
            path.LineTo(cx - waveHalf, top);

        path.CurveTo(cx - 20f, top, cx - 18f, top + waveDepth, cx, top + waveDepth);
        path.CurveTo(cx + 18f, top + waveDepth, cx + 20f, top, cx + waveHalf, top);

        path.LineTo(width - topRadius, top);
        path.QuadTo(width, top, width, top + topRadius);
        path.LineTo(width, height);
        path.LineTo(0, height);
        path.Close();

        canvas.FillColor = barColor;
        canvas.FillPath(path);

        canvas.RestoreState();
    }
}

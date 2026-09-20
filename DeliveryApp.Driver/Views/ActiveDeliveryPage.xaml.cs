using DeliveryApp.Driver.Services;
using DeliveryApp.Driver.ViewModels;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace DeliveryApp.Driver.Views;

public partial class ActiveDeliveryPage : ContentPage
{
    readonly ActiveDeliveryViewModel _vm;

    bool _mapReady;
    bool _staticPinsDrawn;
    bool _routeBusy;

    // آخر مسار اترسم — بنستخدمهم عشان منطلبش OSRM مع كل تحديث لوكيشن
    // (نفس فكرة ShouldUpdateDriverRoute في تطبيق الكاستمر)
    string _lastRouteMode = "";
    string _lastRealRouteMode = "";
    double _lastRouteFromLat;
    double _lastRouteFromLng;
    DateTime _lastRouteTime = DateTime.MinValue;

    const string RouteId = "route";
    const string RouteColor = "#FF5722";
    const double RouteWidth = 5;

    static readonly HttpClient _routingHttp = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    static ActiveDeliveryPage()
    {
        // OSRM العام بيرفض الطلبات من غير User-Agent واضح (403) — نفس إصلاح الكاستمر.
        // غيّر الدومين ده بدومينك الحقيقي.
        _routingHttp.DefaultRequestHeaders.UserAgent.ParseAdd(
            "TalyDriverApp/1.0 (+https://your-domain.com)");
    }

    public ActiveDeliveryPage(ActiveDeliveryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // الـ WebView بيتعمله reload كامل كل مرة الصفحة تظهر (خريطة فاضية) —
        // فلازم نصفّر كل الحالات عشان الماركرز والمسار يترسموا من الأول.
        _mapReady = false;
        _staticPinsDrawn = false;
        _routeBusy = false;
        _lastRouteMode = "";
        _lastRealRouteMode = "";
        _lastRouteFromLat = 0;
        _lastRouteFromLng = 0;
        _lastRouteTime = DateTime.MinValue;

        // الاشتراك هنا (مش في الـ constructor) عشان يتعاد لما ترجع من صفحة الشات/المكالمة
        _vm.MapUpdated -= OnMapUpdated;
        _vm.MapUpdated += OnMapUpdated;
        _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm.PropertyChanged += OnVmPropertyChanged;

        MapWebView.Navigating -= MapWebView_Navigating;
        MapWebView.Navigating += MapWebView_Navigating;
        MapWebView.Source = new HtmlWebViewSource
        {
            Html = OpenFreeMapHtml.Create()
        };
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _mapReady = false;
        MapWebView.Navigating -= MapWebView_Navigating;
        _vm.MapUpdated -= OnMapUpdated;
        _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm.Cleanup();
    }

    // ── جسر الأحداث بين الـ WebView والـ C# ─────────────────────────────

    async void MapWebView_Navigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url == "app://map-ready")
        {
            e.Cancel = true;
            _mapReady = true;
            await RefreshMapAsync();
            return;
        }

        // مفيش تنقل فعلي: ده مجرد جسر أحداث جوه الصفحة
        if (e.Url.StartsWith("app://", StringComparison.OrdinalIgnoreCase))
            e.Cancel = true;
    }

    async void OnMapUpdated()
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(RefreshMapAsync);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Map] OnMapUpdated: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // لما الـ Order يتحدد (QueryProperty بيجي بعد الـ constructor) أو الحالة تتغير
    // (مثلاً OnTheWay) نحدّث الخريطة فوراً من غير ما نستنى تحديث اللوكيشن الجاي.
    void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ActiveDeliveryViewModel.Order))
            OnMapUpdated();
    }

    // ── رسم الخريطة ─────────────────────────────────────────────────────

    async Task RefreshMapAsync()
    {
        if (!_mapReady) return;

        try
        {
            var order = _vm.Order;
            if (order == null) return;

            bool hasCustomer = order.DeliveryLatitude != 0 && order.DeliveryLongitude != 0;
            bool hasRestaurant = order.RestaurantLat != 0 && order.RestaurantLng != 0;
            bool hasDriver = _vm.DriverLat != 0 && _vm.DriverLng != 0;

            // 1) ماركر العميل والمطعم (مرة واحدة في كل تحميل للخريطة)
            if (!_staticPinsDrawn && (hasCustomer || hasRestaurant))
            {
                _staticPinsDrawn = true;

                if (hasCustomer)
                    await SetMarkerAsync("customer", order.DeliveryLongitude, order.DeliveryLatitude,
                        "#2196F3", "user");

                if (hasRestaurant)
                    await SetMarkerAsync("restaurant", order.RestaurantLng, order.RestaurantLat,
                        "#4CAF50", "shop");

                if (hasCustomer && hasRestaurant)
                {
                    await FitToPointsAsync(new[]
                    {
                        new[] { order.RestaurantLng, order.RestaurantLat },
                        new[] { order.DeliveryLongitude, order.DeliveryLatitude }
                    });
                }
                else if (hasCustomer)
                {
                    await CenterOnAsync(order.DeliveryLongitude, order.DeliveryLatitude, 15);
                }
                else
                {
                    await CenterOnAsync(order.RestaurantLng, order.RestaurantLat, 15);
                }
            }

            // 2) ماركر الدريفر (بيتحدث مع كل تحديث لوكيشن)
            if (hasDriver)
            {
                await SetMarkerAsync("driver", _vm.DriverLng, _vm.DriverLat, "#FF5722", "driver");
            }

            // 3) المسار
            await UpdateRouteAsync(order, hasCustomer, hasRestaurant, hasDriver);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Map] RefreshMapAsync: {ex.GetType().Name}: {ex.Message}");
        }
    }

    async Task UpdateRouteAsync(
        Models.ActiveOrder order, bool hasCustomer, bool hasRestaurant, bool hasDriver)
    {
        string mode = "";
        List<(double lat, double lng)>? waypoints = null;

        if (hasDriver)
        {
            var driver = (_vm.DriverLat, _vm.DriverLng);

            if (order.IsOnTheWay && hasCustomer)
            {
                // الدريفر ماسك الأوردر ورايح للعميل
                mode = "driver-customer";
                waypoints = new()
                {
                    driver,
                    (order.DeliveryLatitude, order.DeliveryLongitude)
                };
            }
            else if (!order.IsOnTheWay && hasRestaurant && hasCustomer)
            {
                // الدريفر لسه رايح للمطعم ثم العميل
                mode = "driver-restaurant-customer";
                waypoints = new()
                {
                    driver,
                    (order.RestaurantLat, order.RestaurantLng),
                    (order.DeliveryLatitude, order.DeliveryLongitude)
                };
            }
        }
        else if (hasCustomer && hasRestaurant)
        {
            // لسه مفيش لوكيشن للدريفر: نعرض مسار المطعم ← العميل
            mode = "restaurant-customer";
            waypoints = new()
            {
                (order.RestaurantLat, order.RestaurantLng),
                (order.DeliveryLatitude, order.DeliveryLongitude)
            };
        }

        if (waypoints == null || !ShouldUpdateRoute(mode)) return;

        _lastRouteMode = mode;
        _lastRouteFromLat = _vm.DriverLat;
        _lastRouteFromLng = _vm.DriverLng;
        _lastRouteTime = DateTime.Now;

        await DrawRouteAsync(waypoints, mode, reportEta: mode == "driver-customer");
    }

    bool ShouldUpdateRoute(string mode)
    {
        if (_routeBusy) return false;
        if (mode != _lastRouteMode) return true;               // الحالة اتغيرت (مثلاً OnTheWay)
        if (mode == "restaurant-customer") return false;       // مسار ثابت، بيترسم مرة واحدة
        if ((DateTime.Now - _lastRouteTime).TotalSeconds > 15) return true;

        double dlat = _vm.DriverLat - _lastRouteFromLat;
        double dlng = _vm.DriverLng - _lastRouteFromLng;
        return Math.Sqrt(dlat * dlat + dlng * dlng) > 0.0005;  // ~55 متر
    }

    async Task DrawRouteAsync(
        IReadOnlyList<(double lat, double lng)> points, string mode, bool reportEta)
    {
        _routeBusy = true;
        try
        {
            var coordsParam = string.Join(";", points.Select(p =>
                string.Format(CultureInfo.InvariantCulture, "{0},{1}", p.lng, p.lat)));

            var url = $"https://router.project-osrm.org/route/v1/driving/{coordsParam}" +
                      "?overview=full&geometries=geojson";

            var json = await _routingHttp.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var routes = doc.RootElement.GetProperty("routes");
            if (routes.GetArrayLength() == 0)
                throw new InvalidOperationException("OSRM returned no routes");

            var route = routes[0];

            if (reportEta && route.TryGetProperty("duration", out var durationEl))
                _vm.UpdateDeliveryEta(durationEl.GetDouble());

            var coords = route.GetProperty("geometry").GetProperty("coordinates")
                .EnumerateArray()
                .Select(c => new[] { c[0].GetDouble(), c[1].GetDouble() })
                .ToList();

            if (coords.Count < 2)
                throw new InvalidOperationException("OSRM route has too few points");

            await SetRouteAsync(coords);
            _lastRealRouteMode = mode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Route:{mode}] {ex.GetType().Name}: {ex.Message}");

            // لو فيه مسار حقيقي لنفس الحالة مرسوم بالفعل، نسيبه بدل ما نبدله بخط مستقيم
            if (_lastRealRouteMode != mode)
            {
                var straight = points.Select(p => new[] { p.lng, p.lat }).ToList();
                await SetRouteAsync(straight);
            }
        }
        finally
        {
            _routeBusy = false;
        }
    }

    // ── أوامر الـ JavaScript ────────────────────────────────────────────

    Task SetMarkerAsync(string id, double lng, double lat, string color, string iconType)
    {
        var script = string.Format(
            CultureInfo.InvariantCulture,
            "setMarker('{0}',{1},{2},'{3}','{4}');",
            id, lng, lat, color, iconType);
        return ExecuteMapScriptAsync(script);
    }

    Task SetRouteAsync(List<double[]> coordinates)
    {
        var jsonCoords = JsonSerializer.Serialize(coordinates);
        var script = string.Format(
            CultureInfo.InvariantCulture,
            "setRoute('{0}',{1},'{2}',{3});",
            RouteId, jsonCoords, RouteColor, RouteWidth);
        return ExecuteMapScriptAsync(script);
    }

    Task FitToPointsAsync(double[][] points) =>
        ExecuteMapScriptAsync($"fitToPoints({JsonSerializer.Serialize(points)});");

    Task CenterOnAsync(double lng, double lat, int zoom) =>
        ExecuteMapScriptAsync(string.Format(
            CultureInfo.InvariantCulture,
            "centerOn({0},{1},{2});", lng, lat, zoom));

    Task<string> ExecuteMapScriptAsync(string script) =>
        _mapReady ? MapWebView.EvaluateJavaScriptAsync(script) : Task.FromResult(string.Empty);
}

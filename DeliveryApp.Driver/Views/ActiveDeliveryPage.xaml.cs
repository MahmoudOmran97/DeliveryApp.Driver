using DeliveryApp.Driver.ViewModels;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using static Microsoft.Maui.ApplicationModel.Permissions;

namespace DeliveryApp.Driver.Views;

public partial class ActiveDeliveryPage : ContentPage
{
    readonly ActiveDeliveryViewModel _vm;
    MemoryLayer? _driverLayer;
    MemoryLayer? _customerLayer;
    MemoryLayer? _restaurantLayer;
    MemoryLayer? _routeLayer;
    bool _mapInitialized;
    bool _markerSourcesReady;

    string _customerSvg = BuildUserMarkerSvg();
    string _restaurantSvg = BuildShopMarkerSvg();
    string _driverSvg = BuildDriverMarkerSvg();

    public ActiveDeliveryPage(ActiveDeliveryViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
        SetupMap();
        _ = LoadMarkerSourcesAsync();
        vm.MapUpdated += OnMapUpdated;
    }

    async Task LoadMarkerSourcesAsync()
    {
        _customerSvg = await TryReadBase64ImageSourceAsync("marker_user_img.png", _customerSvg);
        _restaurantSvg = await TryReadBase64ImageSourceAsync("marker_shop_img.png", _restaurantSvg);
        _driverSvg = await TryReadBase64ImageSourceAsync("marker_driver_img.png", _driverSvg);
        _markerSourcesReady = true;
        MainThread.BeginInvokeOnMainThread(OnMapUpdated);
    }

    static async Task<string> TryReadBase64ImageSourceAsync(string fileName, string fallback)
    {
        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());
            return $"base64-content://{base64}";
        }
        catch
        {
            return fallback;
        }
    }

    static string BuildDriverMarkerSvg() =>
        "svg-content://<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 64 64'>" +
        "<circle cx='32' cy='32' r='26' fill='#FF5722'/>" +
        "<circle cx='22' cy='40' r='7' fill='white'/><circle cx='22' cy='40' r='3.2' fill='#263238'/>" +
        "<circle cx='42' cy='40' r='7' fill='white'/><circle cx='42' cy='40' r='3.2' fill='#263238'/>" +
        "<path d='M19 33h19l6-7h-8l-3-7h-7l2 7h-9z' fill='#263238'/></svg>";

    static string BuildUserMarkerSvg() =>
        "svg-content://<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 64 64'>" +
        "<path d='M32 2C20.4 2 11 11.4 11 23c0 14 18.2 35.8 20.1 38.1.5.6 1.4.6 1.9 0C34.8 58.8 53 37 53 23 53 11.4 43.6 2 32 2z' fill='#2196F3'/>" +
        "<circle cx='32' cy='23' r='10' fill='white'/><circle cx='32' cy='20' r='4.6' fill='#2196F3'/>" +
        "<path d='M24.5 30.5c1.8-3 4.2-4.5 7.5-4.5s5.7 1.5 7.5 4.5' fill='none' stroke='#2196F3' stroke-width='3' stroke-linecap='round'/></svg>";

    static string BuildShopMarkerSvg() =>
        "svg-content://<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 64 64'>" +
        "<path d='M32 2C20.4 2 11 11.4 11 23c0 14 18.2 35.8 20.1 38.1.5.6 1.4.6 1.9 0C34.8 58.8 53 37 53 23 53 11.4 43.6 2 32 2z' fill='#4CAF50'/>" +
        "<rect x='20' y='16' width='24' height='18' rx='2' fill='white'/>" +
        "<path d='M20 22h24' stroke='#4CAF50' stroke-width='3'/>" +
        "<rect x='24' y='25' width='7' height='9' fill='#4CAF50'/>" +
        "<rect x='34' y='25' width='8' height='6' fill='#4CAF50'/></svg>";

    void SetupMap()
    {
        Mapsui.Logging.Logger.LogDelegate = (level, msg, ex) =>
            Debug.WriteLine($"[Mapsui/{level}] {msg} {ex?.Message}");

        MapControl.Map.Layers.Add(OpenStreetMap.CreateTileLayer());
        var (x, y) = SphericalMercator.FromLonLat(31.2357, 30.0444);
        MapControl.Map.Navigator.CenterOnAndZoomTo(
            new MPoint(x, y), MapControl.Map.Navigator.Resolutions[13]);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        MapControl.Refresh();

        // Draw initial map if order already set
        if (_vm.Order != null && !_mapInitialized)
            OnMapUpdated();
    }

    void OnMapUpdated()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (_vm.Order == null) return;

            bool hasCustomer = _vm.Order.DeliveryLatitude != 0 && _vm.Order.DeliveryLongitude != 0;
            bool hasRestaurant = _vm.Order.RestaurantLat != 0 && _vm.Order.RestaurantLng != 0;

            if (!_mapInitialized && (hasCustomer || hasRestaurant))
            {
                _mapInitialized = true;

                if (hasCustomer)
                    DrawImagePin(ref _customerLayer, "CustomerLayer",
                        _vm.Order.DeliveryLatitude, _vm.Order.DeliveryLongitude, _customerSvg, 0.04);

                if (hasRestaurant)
                    DrawImagePin(ref _restaurantLayer, "RestaurantLayer",
                        _vm.Order.RestaurantLat, _vm.Order.RestaurantLng, _restaurantSvg, 0.04);

                if (hasCustomer && hasRestaurant)
                {
                    FitBounds(
                        _vm.Order.RestaurantLat, _vm.Order.RestaurantLng,
                        _vm.Order.DeliveryLatitude, _vm.Order.DeliveryLongitude);
                }
                else
                {
                    double lat = hasCustomer ? _vm.Order.DeliveryLatitude : _vm.Order.RestaurantLat;
                    double lng = hasCustomer ? _vm.Order.DeliveryLongitude : _vm.Order.RestaurantLng;
                    var (cx, cy) = SphericalMercator.FromLonLat(lng, lat);
                    MapControl.Map.Navigator.CenterOnAndZoomTo(
                        new MPoint(cx, cy), MapControl.Map.Navigator.Resolutions[15]);
                }
            }

            // Draw driver current position
            if (_vm.DriverLat != 0 && _vm.DriverLng != 0)
            {
                DrawImagePin(ref _driverLayer, "DriverLayer",
                    _vm.DriverLat, _vm.DriverLng, _driverSvg, 0.065);

                var targetLat = _vm.Order.IsOnTheWay ? _vm.Order.DeliveryLatitude : _vm.Order.RestaurantLat;
                var targetLng = _vm.Order.IsOnTheWay ? _vm.Order.DeliveryLongitude : _vm.Order.RestaurantLng;
                if (_vm.Order.IsOnTheWay && targetLat != 0 && targetLng != 0)
                {
                    await DrawRouteAsync(
                        _vm.DriverLat, _vm.DriverLng,
                        targetLat, targetLng,
                        "#FF5722", 5, "RouteLayer",
                        layer => _routeLayer = layer);
                }
                else if (!_vm.Order.IsOnTheWay && hasRestaurant && hasCustomer)
                {
                    await DrawRouteByWaypointsAsync(
                        new[]
                        {
                            (_vm.DriverLat, _vm.DriverLng),
                            (_vm.Order.RestaurantLat, _vm.Order.RestaurantLng),
                            (_vm.Order.DeliveryLatitude, _vm.Order.DeliveryLongitude)
                        },
                        "#FF5722", 5, "RouteLayer",
                        layer => _routeLayer = layer);
                }
            }
            else if (hasCustomer && hasRestaurant)
            {
                await DrawRouteAsync(
                    _vm.Order.RestaurantLat, _vm.Order.RestaurantLng,
                    _vm.Order.DeliveryLatitude, _vm.Order.DeliveryLongitude,
                    "#FF5722", 5, "RouteLayer",
                    layer => _routeLayer = layer);
            }

            MapControl.Refresh();
        });
    }

    void DrawImagePin(ref MemoryLayer? existing, string name,
                      double lat, double lng, string svgSource, double scale)
    {
        if (existing != null) MapControl.Map.Layers.Remove(existing);

        var (x, y) = SphericalMercator.FromLonLat(lng, lat);
        var feature = new PointFeature(new MPoint(x, y));
        feature.Styles = new List<IStyle>
        {
            new ImageStyle
            {
                Image = svgSource,
                SymbolScale = scale,
                // Keep marker center locked to geographic coordinate.
                RelativeOffset = new RelativeOffset(0.0, 0.0)
            }
        };

        existing = new MemoryLayer
        {
            Name = name,
            Features = new[] { feature },
            Style = null
        };

        MapControl.Map.Layers.Add(existing);
    }

    async Task DrawRouteAsync(
        double fromLat, double fromLng,
        double toLat, double toLng,
        string colorHex, double width,
        string layerName,
        Action<MemoryLayer> onComplete)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var url = $"https://router.project-osrm.org/route/v1/driving/" +
                      $"{fromLng.ToString(CultureInfo.InvariantCulture)},{fromLat.ToString(CultureInfo.InvariantCulture)};" +
                      $"{toLng.ToString(CultureInfo.InvariantCulture)},{toLat.ToString(CultureInfo.InvariantCulture)}" +
                      $"?overview=full&geometries=geojson";

            var json = await http.GetStringAsync(url);
            var doc = JsonDocument.Parse(json);
            var route = doc.RootElement.GetProperty("routes")[0];

            var coords = route.GetProperty("geometry").GetProperty("coordinates");
            var points = new List<MPoint>();
            foreach (var c in coords.EnumerateArray())
            {
                var (mx, my) = SphericalMercator.FromLonLat(c[0].GetDouble(), c[1].GetDouble());
                points.Add(new MPoint(mx, my));
            }
            if (points.Count < 2) return;

            var existingByName = MapControl.Map.Layers.FirstOrDefault(l => l.Name == layerName);
            if (existingByName != null) MapControl.Map.Layers.Remove(existingByName);

            var line = new NetTopologySuite.Geometries.LineString(
                points.Select(p => new NetTopologySuite.Geometries.Coordinate(p.X, p.Y)).ToArray());
            var feature = new Mapsui.Nts.GeometryFeature(line);
            feature.Styles = new List<IStyle>
            {
                new VectorStyle { Line = new Pen(Mapsui.Styles.Color.FromArgb(50, 0, 0, 0), width + 4) },
                new VectorStyle { Line = new Pen(Mapsui.Styles.Color.FromString(colorHex), width) },
                new VectorStyle { Line = new Pen(Mapsui.Styles.Color.White, 1.5f) { PenStyle = PenStyle.Dash } }
            };

            var newLayer = new MemoryLayer
            {
                Name = layerName,
                Features = new[] { feature },
                Style = null
            };

            int insertIdx = Math.Min(1, Math.Max(0, MapControl.Map.Layers.Count - 1));
            MapControl.Map.Layers.Insert(insertIdx, newLayer);
            onComplete(newLayer);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Route] {ex.Message}");
            DrawStraightLine(fromLat, fromLng, toLat, toLng, colorHex, width, layerName, onComplete);
        }
    }

    async Task DrawRouteByWaypointsAsync(
        IEnumerable<(double lat, double lng)> waypoints,
        string colorHex, double width,
        string layerName,
        Action<MemoryLayer> onComplete)
    {
        try
        {
            var pointsList = waypoints.ToList();
            if (pointsList.Count < 2) return;

            var coordsParam = string.Join(";",
                pointsList.Select(p => $"{p.lng.ToString(System.Globalization.CultureInfo.InvariantCulture)},{p.lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var url = $"https://router.project-osrm.org/route/v1/driving/{coordsParam}?overview=full&geometries=geojson";
            var json = await http.GetStringAsync(url);
            var doc = JsonDocument.Parse(json);
            var route = doc.RootElement.GetProperty("routes")[0];
            var coords = route.GetProperty("geometry").GetProperty("coordinates");

            var points = new List<MPoint>();
            foreach (var c in coords.EnumerateArray())
            {
                var (mx, my) = SphericalMercator.FromLonLat(c[0].GetDouble(), c[1].GetDouble());
                points.Add(new MPoint(mx, my));
            }
            if (points.Count < 2) return;

            var existingByName = MapControl.Map.Layers.FirstOrDefault(l => l.Name == layerName);
            if (existingByName != null) MapControl.Map.Layers.Remove(existingByName);

            var line = new NetTopologySuite.Geometries.LineString(
                points.Select(p => new NetTopologySuite.Geometries.Coordinate(p.X, p.Y)).ToArray());
            var feature = new Mapsui.Nts.GeometryFeature(line);
            feature.Styles = new List<IStyle>
            {
                new VectorStyle { Line = new Pen(Mapsui.Styles.Color.FromArgb(50, 0, 0, 0), width + 4) },
                new VectorStyle { Line = new Pen(Mapsui.Styles.Color.FromString(colorHex), width) },
                new VectorStyle { Line = new Pen(Mapsui.Styles.Color.White, 1.5f) { PenStyle = PenStyle.Dash } }
            };

            var newLayer = new MemoryLayer
            {
                Name = layerName,
                Features = new[] { feature },
                Style = null
            };

            int insertIdx = Math.Min(1, Math.Max(0, MapControl.Map.Layers.Count - 1));
            MapControl.Map.Layers.Insert(insertIdx, newLayer);
            onComplete(newLayer);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RouteWaypoints] {ex.Message}");
            var pointsList = waypoints.ToList();
            if (pointsList.Count >= 2)
            {
                DrawStraightLine(pointsList[0].lat, pointsList[0].lng, pointsList[1].lat, pointsList[1].lng, colorHex, width, layerName, onComplete);
            }
        }
    }

    void DrawStraightLine(
        double fromLat, double fromLng,
        double toLat, double toLng,
        string colorHex, double width,
        string layerName,
        Action<MemoryLayer> onComplete)
    {
        var existingByName = MapControl.Map.Layers.FirstOrDefault(l => l.Name == layerName);
        if (existingByName != null) MapControl.Map.Layers.Remove(existingByName);

        var (x1, y1) = SphericalMercator.FromLonLat(fromLng, fromLat);
        var (x2, y2) = SphericalMercator.FromLonLat(toLng, toLat);
        var line = new NetTopologySuite.Geometries.LineString(new[]
        {
            new NetTopologySuite.Geometries.Coordinate(x1, y1),
            new NetTopologySuite.Geometries.Coordinate(x2, y2)
        });

        var feature = new Mapsui.Nts.GeometryFeature(line);
        feature.Styles = new List<IStyle>
        {
            new VectorStyle { Line = new Pen(Mapsui.Styles.Color.FromString(colorHex), width) }
        };

        var newLayer = new MemoryLayer
        {
            Name = layerName,
            Features = new[] { feature },
            Style = null
        };

        int insertIdx = Math.Min(1, Math.Max(0, MapControl.Map.Layers.Count - 1));
        MapControl.Map.Layers.Insert(insertIdx, newLayer);
        onComplete(newLayer);
    }

    void FitBounds(double lat1, double lng1, double lat2, double lng2)
    {
        var (x1, y1) = SphericalMercator.FromLonLat(lng1, lat1);
        var (x2, y2) = SphericalMercator.FromLonLat(lng2, lat2);

        double dist = Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
        int zoom = dist switch
        {
            < 3000 => 16,
            < 7000 => 15,
            < 15000 => 14,
            < 30000 => 13,
            _ => 12
        };

        MapControl.Map.Navigator.CenterOnAndZoomTo(
            new MPoint((x1 + x2) / 2, (y1 + y2) / 2),
            MapControl.Map.Navigator.Resolutions[Math.Max(zoom - 1, 0)]);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.MapUpdated -= OnMapUpdated;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");
}
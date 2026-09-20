namespace DeliveryApp.Driver.Services;

/// <summary>
/// HTML bridge used by MAUI WebViews to render OpenFreeMap through MapLibre GL JS.
/// </summary>
public static class OpenFreeMapHtml
{
    public static string Create(string initialMapScript = "")
    {
        // This is intentionally a non-interpolated raw string. JavaScript uses many
        // braces, and keeping it non-interpolated avoids C# treating them as fields.
        const string html = """
<!doctype html>
<html lang="ar">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no" />
  <link href="https://unpkg.com/maplibre-gl@5/dist/maplibre-gl.css" rel="stylesheet" />
  <style>
    html, body, #map { width:100%; height:100%; margin:0; padding:0; overflow:hidden; }
    .maplibregl-ctrl-attrib { font-size:10px; }
  </style>
</head>
<body>
  <div id="map"></div>
  <script src="https://unpkg.com/maplibre-gl@5/dist/maplibre-gl.js"></script>
  <script>
 maplibregl.setRTLTextPlugin(
  'https://unpkg.com/@mapbox/mapbox-gl-rtl-text@0.2.3/mapbox-gl-rtl-text.js',
  true // lazy: يحمّل الـ plugin بس أول ما يلاقي نص RTL في الخريطة
);
    const map = new maplibregl.Map({
      container: 'map',
      style: 'https://tiles.openfreemap.org/styles/liberty',
      center: [31.2357, 30.0444],
      zoom: 13,
      attributionControl: true
    });

    map.addControl(new maplibregl.NavigationControl({ showCompass: false }), 'top-right');
    let markers = {};
    let routes = {};

    // ✅ أيقونات ماركرز حقيقية (Pin) بدل الدايرة الملونة + رمز نصي القديمة —
    // نفس تصميم marker_user.svg / marker_shop.svg / marker_driver.svg
    // المستخدمة في باقي التطبيق، اترسمت هنا كـ inline SVG عشان الـ WebView
    // (اللي بيحمّل HTML خام) مينفعش يوصل لملفات الموارد الأصلية للتطبيق مباشرة.
    const markerIcons = {
      user: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="42" height="42">'
          + '<path d="M32 2C20.4 2 11 11.4 11 23c0 14 18.2 35.8 20.1 38.1.5.6 1.4.6 1.9 0C34.8 58.8 53 37 53 23 53 11.4 43.6 2 32 2z" fill="{C}"/>'
          + '<circle cx="32" cy="23" r="10" fill="#FFFFFF"/>'
          + '<circle cx="32" cy="20" r="4.6" fill="{C}"/>'
          + '<path d="M24.5 30.5c1.8-3 4.2-4.5 7.5-4.5s5.7 1.5 7.5 4.5" fill="none" stroke="{C}" stroke-width="3" stroke-linecap="round"/>'
          + '</svg>',
      pin: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="42" height="42">'
          + '<path d="M32 2C20.4 2 11 11.4 11 23c0 14 18.2 35.8 20.1 38.1.5.6 1.4.6 1.9 0C34.8 58.8 53 37 53 23 53 11.4 43.6 2 32 2z" fill="{C}"/>'
          + '<circle cx="32" cy="23" r="9" fill="#FFFFFF"/>'
          + '</svg>',
      shop: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="42" height="42">'
          + '<path d="M32 2C20.4 2 11 11.4 11 23c0 14 18.2 35.8 20.1 38.1.5.6 1.4.6 1.9 0C34.8 58.8 53 37 53 23 53 11.4 43.6 2 32 2z" fill="{C}"/>'
          + '<rect x="20" y="16" width="24" height="18" rx="2" fill="#FFFFFF"/>'
          + '<path d="M20 22h24" stroke="{C}" stroke-width="3"/>'
          + '<rect x="24" y="25" width="7" height="9" fill="{C}"/>'
          + '<rect x="34" y="25" width="8" height="6" fill="{C}"/>'
          + '</svg>',
      driver: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="40" height="40">'
          + '<circle cx="32" cy="32" r="30" fill="{C}"/>'
          + '<circle cx="22" cy="43" r="8" fill="#FFFFFF"/><circle cx="22" cy="43" r="3.5" fill="#263238"/>'
          + '<circle cx="44" cy="43" r="8" fill="#FFFFFF"/><circle cx="44" cy="43" r="3.5" fill="#263238"/>'
          + '<path d="M18 36h18l8-8h-9l-4-8h-7l3 8h-9z" fill="#263238"/>'
          + '<circle cx="41" cy="24" r="4" fill="#FFFFFF"/>'
          + '</svg>'
    };

    function setMarker(id, lng, lat, color, iconType) {
      if (markers[id]) markers[id].remove();
      const element = document.createElement('div');
      element.style.filter = 'drop-shadow(0 2px 4px rgba(0,0,0,0.45))';
      const template = markerIcons[iconType] || markerIcons.pin;
      element.innerHTML = template.split('{C}').join(color);
      const anchor = iconType === 'driver' ? 'center' : 'bottom';
      markers[id] = new maplibregl.Marker({ element: element, anchor: anchor })
        .setLngLat([lng, lat]).addTo(map);
    }

    function notify(url) { window.location.href = url; }
    function esc(value) { return encodeURIComponent(String(value)); }

    function removeMarker(id) {
      if (markers[id]) { markers[id].remove(); delete markers[id]; }
    }

    function setRoute(id, coordinates, color, width) {
      const sourceId = 'source-' + id;
      const layerId = 'route-' + id;
      const data = { type:'Feature', geometry:{ type:'LineString', coordinates:coordinates } };
      if (map.getSource(sourceId)) map.getSource(sourceId).setData(data);
      else map.addSource(sourceId, { type:'geojson', data:data });
      if (!map.getLayer(layerId)) map.addLayer({
        id: layerId, type:'line', source:sourceId,
        layout: { 'line-cap':'round', 'line-join':'round' },
        paint: { 'line-color':color, 'line-width':width, 'line-opacity':0.9 }
      });
      routes[id] = true;
    }

    function fitToPoints(points) {
      if (!points || points.length === 0) return;
      const bounds = new maplibregl.LngLatBounds(points[0], points[0]);
      points.forEach(p => bounds.extend(p));
      map.fitBounds(bounds, { padding: 80, maxZoom: 16, duration: 500 });
    }

    function centerOn(lng, lat, zoom) {
      map.easeTo({ center:[lng,lat], zoom:zoom, duration:400 });
    }

    map.on('load', () => {
      __INITIAL_MAP_SCRIPT__
      notify('app://map-ready');
    });

    map.on('click', (event) => {
      notify('app://map-click?lat=' + esc(event.lngLat.lat) + '&lng=' + esc(event.lngLat.lng));
    });
  </script>
</body>
</html>
""";

        return html.Replace("__INITIAL_MAP_SCRIPT__", initialMapScript,
            StringComparison.Ordinal);
    }
}

public static class WebViewMapQuery
{
    public static bool TryGetCoordinates(string url, out double lat, out double lng)
    {
        lat = lng = 0;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;

        var query = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries);
        string? latText = null;
        string? lngText = null;

        foreach (var item in query)
        {
            var pair = item.Split('=', 2);
            if (pair.Length != 2) continue;

            var key = Uri.UnescapeDataString(pair[0]);
            var value = Uri.UnescapeDataString(pair[1]);
            if (key == "lat") latText = value;
            if (key == "lng") lngText = value;
        }

        return double.TryParse(latText, System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture, out lat)
            && double.TryParse(lngText, System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture, out lng);
    }
}

public static class OpenFreeMapAttribution
{
    public const string Text = "OpenFreeMap © OpenMapTiles · Data from OpenStreetMap";
}

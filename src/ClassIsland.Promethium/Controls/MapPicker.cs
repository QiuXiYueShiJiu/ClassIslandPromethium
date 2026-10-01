// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace ClassIsland.Promethium.Controls;

/// <summary>地图上选好一个点时带出来的坐标。</summary>
public class MapLocationPickedEventArgs : EventArgs
{
    public MapLocationPickedEventArgs(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>纬度。</summary>
    public double Latitude { get; }

    /// <summary>经度。</summary>
    public double Longitude { get; }
}

/// <summary>
/// 一个能拖动、能滚轮缩放、点一下就取经纬度的地图控件。
/// </summary>
/// <remarks>
/// 没引第三方地图 SDK，就是老老实实按 Web 墨卡托自己算瓦片位置。
/// 这么做有几个实在好处：不引入额外的原生依赖、不会因为 SDK 只支持某个平台就挂掉，
/// 而且坐标换算全在明面上，出偏差一眼能查。
/// <para/>
/// 瓦片来自 OpenStreetMap。按它的使用条款，请求带了能识别来源的 User-Agent，
/// 并且只在内存里缓存——这是个设置页里的小地图，不是拿来做底图的。
/// </remarks>
public class MapPicker : Control
{
    private const int TileSize = 256;
    private const int MinZoom = 3;
    private const int MaxZoom = 18;

    private static readonly HttpClient Http = CreateClient();

    private static readonly IBrush PlaceholderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x22, 0x30));
    private static readonly IBrush MarkerBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF));
    private static readonly IBrush MarkerRingBrush = Brushes.White;
    private static readonly Pen MarkerPen = new(new SolidColorBrush(Color.FromRgb(0x0A, 0x0C, 0x14)), 1.5);
    private static readonly Typeface LabelTypeface = new("Microsoft YaHei UI, Noto Sans CJK SC, sans-serif");

    private readonly Dictionary<(int Z, int X, int Y), Bitmap?> _tiles = new();
    private readonly HashSet<(int Z, int X, int Y)> _pending = new();

    private double _centerLatitude = 39.9042;
    private double _centerLongitude = 116.4074;
    private int _zoom = 11;

    private double _markerLatitude = 39.9042;
    private double _markerLongitude = 116.4074;

    private Point? _dragStart;
    private double _dragStartWorldX;
    private double _dragStartWorldY;
    private bool _dragged;

    /// <summary>在地图上点选了一个点。</summary>
    public event EventHandler<MapLocationPickedEventArgs>? LocationPicked;

    public MapPicker()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("ClassIsland-Promethium", "1.0"));
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("(+https://github.com/QiuXiYueShiJiu/ClassIslandPromethium)"));
        return client;
    }

    /// <summary>当前标记点的纬度。</summary>
    public double MarkerLatitude => _markerLatitude;

    /// <summary>当前标记点的经度。</summary>
    public double MarkerLongitude => _markerLongitude;

    /// <summary>把地图移到指定位置。缩放为 null 时保持当前级别。</summary>
    public void SetView(double latitude, double longitude, int? zoom = null)
    {
        _markerLatitude = ClampLatitude(latitude);
        _markerLongitude = ClampLongitude(longitude);
        _centerLatitude = _markerLatitude;
        _centerLongitude = _markerLongitude;
        if (zoom.HasValue)
        {
            _zoom = Math.Clamp(zoom.Value, MinZoom, MaxZoom);
        }
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 320 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 240 : availableSize.Height);

    // ---------------- 渲染 ----------------

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        using var clip = context.PushClip(new Rect(size));
        context.FillRectangle(PlaceholderBrush, new Rect(size));

        var scale = 1 << _zoom;
        var originX = LongitudeToWorldX(_centerLongitude, scale) - size.Width / 2;
        var originY = LatitudeToWorldY(_centerLatitude, scale) - size.Height / 2;

        var firstX = (int)Math.Floor(originX / TileSize);
        var lastX = (int)Math.Floor((originX + size.Width) / TileSize);
        var firstY = (int)Math.Floor(originY / TileSize);
        var lastY = (int)Math.Floor((originY + size.Height) / TileSize);

        for (var tileX = firstX; tileX <= lastX; tileX++)
        {
            for (var tileY = firstY; tileY <= lastY; tileY++)
            {
                if (tileY < 0 || tileY >= scale)
                {
                    continue;
                }

                // 横向绕地球一圈，所以取模而不是丢弃
                var wrappedX = ((tileX % scale) + scale) % scale;
                var destination = new Rect(
                    tileX * TileSize - originX,
                    tileY * TileSize - originY,
                    TileSize, TileSize);

                var bitmap = GetTile(_zoom, wrappedX, tileY);
                if (bitmap != null)
                {
                    context.DrawImage(bitmap, destination);
                }
            }
        }

        DrawMarker(context, size, originX, originY, scale);
    }

    private void DrawMarker(DrawingContext context, Size size, double originX, double originY, int scale)
    {
        var x = LongitudeToWorldX(_markerLongitude, scale) - originX;
        var y = LatitudeToWorldY(_markerLatitude, scale) - originY;
        if (x < -20 || y < -20 || x > size.Width + 20 || y > size.Height + 20)
        {
            return;
        }

        var center = new Point(x, y);

        // 十字准星：先画细的两条，再压一个实心圆点上去
        context.DrawLine(MarkerPen, new Point(x - 14, y), new Point(x + 14, y));
        context.DrawLine(MarkerPen, new Point(x, y - 14), new Point(x, y + 14));
        context.DrawEllipse(MarkerRingBrush, MarkerPen, center, 7, 7);
        context.DrawEllipse(MarkerBrush, null, center, 4.5, 4.5);

        // 坐标贴在标记旁边，省得人选完还要去别处核对
        var text = new FormattedText(
            $"{_markerLatitude:0.0000}, {_markerLongitude:0.0000}",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, LabelTypeface, 11, Brushes.White);
        var textOrigin = new Point(
            Math.Clamp(x + 10, 2, Math.Max(2, size.Width - text.Width - 4)),
            Math.Clamp(y + 10, 2, Math.Max(2, size.Height - text.Height - 4)));
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(0xAA, 0, 0, 0)),
            new Rect(textOrigin.X - 3, textOrigin.Y - 1, text.Width + 6, text.Height + 2), 3);
        context.DrawText(text, textOrigin);
    }

    // ---------------- 瓦片 ----------------

    private Bitmap? GetTile(int zoom, int x, int y)
    {
        var key = (zoom, x, y);
        if (_tiles.TryGetValue(key, out var cached))
        {
            // null 表示这张已经失败过了，不再反复重试
            return cached;
        }

        RequestTile(key);
        return null;
    }

    private async void RequestTile((int Z, int X, int Y) key)
    {
        if (!_pending.Add(key))
        {
            return;
        }

        try
        {
            var url = $"https://tile.openstreetmap.org/{key.Z}/{key.X}/{key.Y}.png";
            var bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    using var stream = new MemoryStream(bytes);
                    _tiles[key] = new Bitmap(stream);
                }
                catch (Exception)
                {
                    _tiles[key] = null;
                }
                InvalidateVisual();
            });
        }
        catch (Exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _tiles[key] = null;
                InvalidateVisual();
            });
        }
        finally
        {
            _pending.Remove(key);
        }
    }

    // ---------------- 交互 ----------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetPosition(this);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragStart = point;
        _dragged = false;
        var scale = 1 << _zoom;
        _dragStartWorldX = LongitudeToWorldX(_centerLongitude, scale);
        _dragStartWorldY = LatitudeToWorldY(_centerLatitude, scale);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragStart == null)
        {
            return;
        }

        var point = e.GetPosition(this);
        var deltaX = point.X - _dragStart.Value.X;
        var deltaY = point.Y - _dragStart.Value.Y;
        if (!_dragged && Math.Abs(deltaX) + Math.Abs(deltaY) > 3)
        {
            _dragged = true;
        }

        if (!_dragged)
        {
            return;
        }

        var scale = 1 << _zoom;
        _centerLongitude = WorldXToLongitude(_dragStartWorldX - deltaX, scale);
        _centerLatitude = WorldYToLatitude(_dragStartWorldY - deltaY, scale);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragStart == null)
        {
            return;
        }

        var start = _dragStart.Value;
        _dragStart = null;
        e.Pointer.Capture(null);

        // 拖动过就是在平移地图，没拖动才算点选
        if (_dragged)
        {
            return;
        }

        var size = Bounds.Size;
        var scale = 1 << _zoom;
        var originX = LongitudeToWorldX(_centerLongitude, scale) - size.Width / 2;
        var originY = LatitudeToWorldY(_centerLatitude, scale) - size.Height / 2;

        _markerLongitude = ClampLongitude(WorldXToLongitude(originX + start.X, scale));
        _markerLatitude = ClampLatitude(WorldYToLatitude(originY + start.Y, scale));
        InvalidateVisual();
        LocationPicked?.Invoke(this, new MapLocationPickedEventArgs(_markerLatitude, _markerLongitude));
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var point = e.GetPosition(this);
        var size = Bounds.Size;

        var oldScale = 1 << _zoom;
        var oldOriginX = LongitudeToWorldX(_centerLongitude, oldScale) - size.Width / 2;
        var oldOriginY = LatitudeToWorldY(_centerLatitude, oldScale) - size.Height / 2;

        var anchorLongitude = WorldXToLongitude(oldOriginX + point.X, oldScale);
        var anchorLatitude = WorldYToLatitude(oldOriginY + point.Y, oldScale);
        var next = Math.Clamp(_zoom + (e.Delta.Y > 0 ? 1 : -1), MinZoom, MaxZoom);
        if (next == _zoom)
        {
            e.Handled = true;
            return;
        }

        _zoom = next;
        var newScale = 1 << _zoom;

        // 让光标下面那个地理点缩放前后停在原地，手感才对
        var newAnchorX = LongitudeToWorldX(anchorLongitude, newScale);
        var newAnchorY = LatitudeToWorldY(anchorLatitude, newScale);

        _centerLongitude = WorldXToLongitude(newAnchorX - point.X + size.Width / 2, newScale);
        _centerLatitude = WorldYToLatitude(newAnchorY - point.Y + size.Height / 2, newScale);

        InvalidateVisual();
        e.Handled = true;
    }

    // ---------------- 坐标换算 ----------------

    private static double LongitudeToWorldX(double longitude, int scale) =>
        (longitude + 180.0) / 360.0 * TileSize * scale;

    private static double LatitudeToWorldY(double latitude, int scale)
    {
        var clamped = Math.Clamp(latitude, -85.05112878, 85.05112878);
        var radians = clamped * Math.PI / 180.0;
        return (0.5 - Math.Log((1 + Math.Sin(radians)) / (1 - Math.Sin(radians))) / (4 * Math.PI))
               * TileSize * scale;
    }

    private static double WorldXToLongitude(double x, int scale) =>
        x / (TileSize * (double)scale) * 360.0 - 180.0;

    private static double WorldYToLatitude(double y, int scale)
    {
        var n = Math.PI - 2.0 * Math.PI * y / (TileSize * (double)scale);
        return 180.0 / Math.PI * Math.Atan(Math.Sinh(n));
    }

    private static double ClampLatitude(double latitude) => Math.Clamp(latitude, -85.0, 85.0);

    private static double ClampLongitude(double longitude) => Math.Clamp(longitude, -180.0, 180.0);
}

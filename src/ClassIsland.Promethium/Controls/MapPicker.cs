// Pm钷 —— ClassIsland 综合增强插件
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ClassIsland.Promethium.Services;

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
/// 一个能拖动、能滚轮或按钮缩放、点一下就取经纬度的地图控件。
/// </summary>
/// <remarks>
/// 没引第三方地图 SDK，就是老老实实按 Web 墨卡托自己算瓦片位置。
/// 这么做有几个实在好处：不引入额外的原生依赖、不会因为 SDK 只支持某个平台就挂掉，
/// 而且坐标换算全在明面上，出偏差一眼能查。
/// <para/>
/// <b>底图是可换的，而且失败会重试。</b>底图服务器没有全球都好用的——
/// OpenStreetMap 在国内经常连不上，天地图反过来只服务国内。所以来源做成可配，
/// 并且把「加载中 / 加载失败」直接画在图上：一片空白的方块没法排查，
/// 写上失败原因才有得查。
/// <para/>
/// 瓦片只在内存里缓存，不做磁盘持久化——这是设置页里的小地图，不是拿来做底图的。
/// </remarks>
public class MapPicker : Control
{
    private const int TileSize = 256;
    private const int MinZoom = 3;
    private const int MaxZoom = 18;

    /// <summary>同一张瓦片最多重试几次。</summary>
    private const int MaxAttempts = 4;

    /// <summary>两次重试之间至少隔这么久，别把人家服务器打爆。</summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(8);

    /// <summary>缓存超过这个数量就丢掉非当前缩放级别的瓦片，避免一直平移把内存吃满。</summary>
    private const int TileCacheLimit = 256;

    private static readonly HttpClient Http = CreateClient();

    private static readonly IBrush PlaceholderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x22, 0x30));
    private static readonly IBrush MarkerBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF));
    private static readonly IBrush MarkerRingBrush = Brushes.White;
    private static readonly Pen MarkerPen = new(new SolidColorBrush(Color.FromRgb(0x0A, 0x0C, 0x14)), 1.5);
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.FromArgb(0x33, 0x88, 0x99, 0xBB)), 1);
    private static readonly Typeface LabelTypeface = new("Microsoft YaHei UI, Noto Sans CJK SC, sans-serif");

    /// <summary>成功取到的瓦片。</summary>
    private readonly Dictionary<(int Z, int X, int Y), Bitmap> _tiles = new();

    /// <summary>失败过的瓦片：记录试了几次、上次什么时候试的，用来决定要不要重试。</summary>
    private readonly Dictionary<(int Z, int X, int Y), (int Attempts, DateTime LastAttempt)> _failures = new();

    private readonly HashSet<(int Z, int X, int Y)> _pending = new();

    private MapTileSpec _spec = new(string.Empty, string.Empty, string.Empty);
    private string _apiKey = string.Empty;

    private double _centerLatitude = 39.9042;
    private double _centerLongitude = 116.4074;
    private int _zoom = 11;

    private double _markerLatitude = 39.9042;
    private double _markerLongitude = 116.4074;

    private Point? _dragStart;
    private double _dragStartWorldX;
    private double _dragStartWorldY;
    private bool _dragged;

    private int _loadedCount;
    private int _failedCount;
    private string _lastError = string.Empty;

    /// <summary>在地图上点选了一个点。</summary>
    public event EventHandler<MapLocationPickedEventArgs>? LocationPicked;

    /// <summary>底图状态变了（加载中 / 失败 / 完成），界面可以据此更新提示。</summary>
    public event EventHandler? TileStatusChanged;

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

    /// <summary>当前缩放级别。</summary>
    public int ZoomLevel => _zoom;

    /// <summary>当前底图的版权署名。</summary>
    public string Attribution => _spec.Attribution;

    /// <summary>底图是不是还没弄好（加载中或者失败了）。</summary>
    public bool HasTileTrouble => _spec.HasBasemap && (_pending.Count > 0 || _loadedCount == 0);

    /// <summary>
    /// 换一套底图。换完会清掉已缓存的瓦片，包括失败记录。
    /// </summary>
    /// <param name="spec">取图规则。</param>
    /// <param name="apiKey">需要密钥的底图用它（天地图的 tk）。</param>
    public void Configure(MapTileSpec spec, string apiKey)
    {
        _spec = spec;
        _apiKey = apiKey ?? string.Empty;
        ClearTileCaches();
        InvalidateVisual();
        TileStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 把底图重新拉一遍。
    /// </summary>
    /// <remarks>
    /// 失败记录也要一起清掉——「重载」的意思就是当作没试过再来一次，
    /// 否则用户点了重载却发现什么都没变。
    /// </remarks>
    public void ReloadTiles()
    {
        ClearTileCaches();
        InvalidateVisual();
        TileStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>放大地图，给界面上的按钮用（有些场景滚轮会被外层滚动条吃掉）。</summary>
    public void ZoomIn() => ZoomBy(1);

    /// <summary>缩小地图。</summary>
    public void ZoomOut() => ZoomBy(-1);

    /// <summary>把地图移回标记点。</summary>
    public void ResetView()
    {
        _centerLatitude = _markerLatitude;
        _centerLongitude = _markerLongitude;
        InvalidateVisual();
    }

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

    /// <summary>当前底图状态的一句话说明，空串表示一切正常、不用提示。</summary>
    public string DescribeTileStatus()
    {
        if (!_spec.HasBasemap)
        {
            return "未使用底图，可直接在地图上点选或按经纬度填写";
        }

        if (_loadedCount == 0 && _failedCount > 0)
        {
            return string.IsNullOrEmpty(_lastError)
                ? "底图加载失败，可换一个底图来源或点「重载底图」"
                : $"底图加载失败：{_lastError}";
        }

        if (_pending.Count > 0)
        {
            return $"底图加载中…（已加载 {_loadedCount} 张）";
        }

        if (_failedCount > 0)
        {
            return $"部分底图瓦片加载失败（{_failedCount} 张），可点「重载底图」重试";
        }

        return string.Empty;
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 320 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 240 : availableSize.Height);

    private void ClearTileCaches()
    {
        // 不主动 Dispose 这些位图：渲染器可能还握着上一帧的场景，
        // 释放掉会有用到已释放对象的窗口期。就几张几十 KB 的图，交给 GC 更稳妥。
        _tiles.Clear();
        _failures.Clear();
        _pending.Clear();
        _loadedCount = 0;
        _failedCount = 0;
        _lastError = string.Empty;
    }

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

        // 经纬网格先画，瓦片盖在上面。这样底图没加载出来时也不是一片空白，
        // 至少能看出地图在动、能对着网格估个大概位置。
        DrawGraticule(context, size, originX, originY, scale);

        if (_spec.HasBasemap)
        {
            DrawTiles(context, size, originX, originY, scale);
        }

        DrawMarker(context, size, originX, originY, scale);
        DrawStatusBar(context, size);
    }

    private void DrawTiles(DrawingContext context, Size size, double originX, double originY, int scale)
    {
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
    }

    /// <summary>画经纬网格。间隔按当前缩放级别挑，保证屏幕上大约有 5~10 条线。</summary>
    private void DrawGraticule(DrawingContext context, Size size, double originX, double originY, int scale)
    {
        var left = WorldXToLongitude(originX, scale);
        var right = WorldXToLongitude(originX + size.Width, scale);
        var top = WorldYToLatitude(originY, scale);
        var bottom = WorldYToLatitude(originY + size.Height, scale);

        var step = PickGridStep(Math.Max(right - left, top - bottom));
        if (step <= 0)
        {
            return;
        }

        for (var longitude = Math.Ceiling(left / step) * step; longitude <= right; longitude += step)
        {
            var x = LongitudeToWorldX(longitude, scale) - originX;
            context.DrawLine(GridPen, new Point(x, 0), new Point(x, size.Height));
        }

        for (var latitude = Math.Ceiling(bottom / step) * step; latitude <= top; latitude += step)
        {
            var y = LatitudeToWorldY(latitude, scale) - originY;
            context.DrawLine(GridPen, new Point(0, y), new Point(size.Width, y));
        }
    }

    private static double PickGridStep(double spanDegrees)
    {
        double[] steps = { 0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 15, 30, 45 };
        foreach (var step in steps)
        {
            if (spanDegrees / step <= 10)
            {
                return step;
            }
        }

        return 90;
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
        DrawLabel(context, $"{_markerLatitude:0.0000}, {_markerLongitude:0.0000}", x + 10, y + 10, size);
    }

    /// <summary>在图上画一条状态提示。底图出问题时，这块字就是唯一的线索。</summary>
    private void DrawStatusBar(DrawingContext context, Size size)
    {
        var status = DescribeTileStatus();
        if (string.IsNullOrEmpty(status))
        {
            return;
        }

        var text = new FormattedText(status, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, LabelTypeface, 11, Brushes.White);
        var height = text.Height + 6;
        var origin = new Point(6, Math.Max(6, size.Height - height - 6));

        context.FillRectangle(new SolidColorBrush(Color.FromArgb(0xCC, 0x10, 0x14, 0x1E)),
            new Rect(origin.X - 4, origin.Y, Math.Min(text.Width + 10, size.Width - 8), height), 4);
        context.DrawText(text, new Point(origin.X + 1, origin.Y + 3));
    }

    private void DrawLabel(DrawingContext context, string value, double x, double y, Size size)
    {
        var text = new FormattedText(value, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, LabelTypeface, 11, Brushes.White);
        var origin = new Point(
            Math.Clamp(x, 2, Math.Max(2, size.Width - text.Width - 4)),
            Math.Clamp(y, 2, Math.Max(2, size.Height - text.Height - 4)));
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(0xAA, 0, 0, 0)),
            new Rect(origin.X - 3, origin.Y - 1, text.Width + 6, text.Height + 2), 3);
        context.DrawText(text, origin);
    }

    // ---------------- 瓦片 ----------------

    private Bitmap? GetTile(int zoom, int x, int y)
    {
        var key = (zoom, x, y);
        if (_tiles.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (ShouldRetry(key))
        {
            RequestTile(key);
        }

        return null;
    }

    /// <summary>
    /// 判断这张瓦片现在该不该去取。
    /// </summary>
    /// <remarks>
    /// 这里以前是「失败过就永久不再试」，那是个真缺陷：限流、DNS 抖动、
    /// 程序刚启动网络还没就绪，任何一次瞬时失败都会让那一格永远空着。
    /// 现在改成失败后隔一段时间重试有限次，试满才真正放弃。
    /// </remarks>
    private bool ShouldRetry((int Z, int X, int Y) key)
    {
        if (_pending.Contains(key))
        {
            return false;
        }

        if (_failures.TryGetValue(key, out var failure))
        {
            return failure.Attempts < MaxAttempts && DateTime.UtcNow - failure.LastAttempt >= RetryDelay;
        }

        return true;
    }

    private async void RequestTile((int Z, int X, int Y) key)
    {
        if (!_pending.Add(key))
        {
            return;
        }

        var url = MapTileCatalog.BuildUrl(_spec, _apiKey, key.Z, key.X, key.Y);
        if (url == null)
        {
            _pending.Remove(key);
            return;
        }

        try
        {
            var bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _pending.Remove(key);
                try
                {
                    using var stream = new MemoryStream(bytes);
                    _tiles[key] = new Bitmap(stream);
                    _failures.Remove(key);
                    _loadedCount++;
                    TrimCacheIfNeeded();
                }
                catch (Exception ex)
                {
                    MarkFailed(key, "图片解码失败：" + ex.GetType().Name);
                }

                InvalidateVisual();
                TileStatusChanged?.Invoke(this, EventArgs.Empty);
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _pending.Remove(key);
                MarkFailed(key, ex is HttpRequestException http ? $"HTTP {(int?)http.StatusCode}" : ex.GetType().Name);
                InvalidateVisual();
                TileStatusChanged?.Invoke(this, EventArgs.Empty);
            });
        }
    }

    private void MarkFailed((int Z, int X, int Y) key, string reason)
    {
        var attempts = _failures.TryGetValue(key, out var existing) ? existing.Attempts + 1 : 1;

        // 只有第一次失败才把它算进「失败张数」，重试失败不重复计数
        if (attempts == 1)
        {
            _failedCount++;
        }

        _failures[key] = (attempts, DateTime.UtcNow);
        _lastError = reason;
    }

    /// <summary>缓存太大时丢掉非当前缩放级别的瓦片，免得一路平移把内存吃满。</summary>
    private void TrimCacheIfNeeded()
    {
        if (_tiles.Count <= TileCacheLimit)
        {
            return;
        }

        foreach (var key in _tiles.Keys.Where(k => k.Z != _zoom).ToList())
        {
            _tiles.Remove(key);
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
        ZoomAt(e.GetPosition(this), e.Delta.Y > 0 ? 1 : -1);
        e.Handled = true;
    }

    /// <summary>以控件中心为准缩放，给按钮用。</summary>
    private void ZoomBy(int delta)
    {
        var size = Bounds.Size;
        ZoomAt(new Point(size.Width / 2, size.Height / 2), delta);
    }

    /// <summary>
    /// 以某个屏幕点为锚点缩放。
    /// </summary>
    /// <remarks>
    /// 锚点的意思是：光标底下那个地理点，缩放前后停在原地。
    /// 不这么做的话每滚一下地图都会跳一下，很难对准想选的地方。
    /// </remarks>
    private void ZoomAt(Point anchor, int delta)
    {
        var size = Bounds.Size;
        var next = Math.Clamp(_zoom + delta, MinZoom, MaxZoom);
        if (next == _zoom)
        {
            return;
        }

        var oldScale = 1 << _zoom;
        var oldOriginX = LongitudeToWorldX(_centerLongitude, oldScale) - size.Width / 2;
        var oldOriginY = LatitudeToWorldY(_centerLatitude, oldScale) - size.Height / 2;

        var anchorLongitude = WorldXToLongitude(oldOriginX + anchor.X, oldScale);
        var anchorLatitude = WorldYToLatitude(oldOriginY + anchor.Y, oldScale);

        _zoom = next;
        var newScale = 1 << _zoom;
        var newAnchorX = LongitudeToWorldX(anchorLongitude, newScale);
        var newAnchorY = LatitudeToWorldY(anchorLatitude, newScale);

        _centerLongitude = WorldXToLongitude(newAnchorX - anchor.X + size.Width / 2, newScale);
        _centerLatitude = WorldYToLatitude(newAnchorY - anchor.Y + size.Height / 2, newScale);

        InvalidateVisual();
        TileStatusChanged?.Invoke(this, EventArgs.Empty);
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

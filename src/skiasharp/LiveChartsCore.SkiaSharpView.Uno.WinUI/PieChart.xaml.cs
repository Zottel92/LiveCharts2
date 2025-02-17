﻿
// The MIT License(MIT)
//
// Copyright(c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Events;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.Motion;
using LiveChartsCore.SkiaSharpView.Drawing;
using LiveChartsCore.SkiaSharpView.SKCharts;
using LiveChartsCore.SkiaSharpView.WinUI.Helpers;
using LiveChartsCore.VisualElements;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace LiveChartsCore.SkiaSharpView.WinUI;

/// <inheritdoc cref="IPieChartView{TDrawingContext}"/>
public sealed partial class PieChart : UserControl, IPieChartView<SkiaSharpDrawingContext>
{
    private Chart<SkiaSharpDrawingContext>? _core;
    private MotionCanvas? _canvas;
    private readonly CollectionDeepObserver<ISeries> _seriesObserver;
    private readonly CollectionDeepObserver<ChartElement<SkiaSharpDrawingContext>> _visualsObserver;

    /// <summary>
    /// Initializes a new instance of the <see cref="PieChart"/> class.
    /// </summary>
    public PieChart()
    {
        LiveCharts.Configure(config => config.UseDefaults());

        InitializeComponent();

        _seriesObserver = new CollectionDeepObserver<ISeries>(
            (object? sender, NotifyCollectionChangedEventArgs e) => _core?.Update(),
            (object? sender, PropertyChangedEventArgs e) => _core?.Update());
        _visualsObserver = new CollectionDeepObserver<ChartElement<SkiaSharpDrawingContext>>(
            (object? sender, NotifyCollectionChangedEventArgs e) => _core?.Update(),
            (object? sender, PropertyChangedEventArgs e) => _core?.Update());

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        SetValue(SeriesProperty, new ObservableCollection<ISeries>());
        SetValue(VisualElementsProperty, new ObservableCollection<ChartElement<SkiaSharpDrawingContext>>());
        SetValue(SyncContextProperty, new object());
    }

    #region dependency properties

    /// <summary>
    /// The title property.
    /// </summary>
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(VisualElement<SkiaSharpDrawingContext>), typeof(PieChart), new PropertyMetadata(null));

    /// <summary>
    /// The series property
    /// </summary>
    public static readonly DependencyProperty SeriesProperty =
        DependencyProperty.Register(
            nameof(Series), typeof(IEnumerable<ISeries>), typeof(PieChart), new PropertyMetadata(null,
                (DependencyObject o, DependencyPropertyChangedEventArgs args) =>
                {
                    var chart = (PieChart)o;
                    var seriesObserver = chart._seriesObserver;
                    seriesObserver?.Dispose((IEnumerable<ISeries>)args.OldValue);
                    seriesObserver?.Initialize((IEnumerable<ISeries>)args.NewValue);
                    if (chart._core == null) return;
                    chart._core.Update();
                }));

    /// <summary>
    /// The visual elements property
    /// </summary>
    public static readonly DependencyProperty VisualElementsProperty =
        DependencyProperty.Register(
            nameof(VisualElements), typeof(IEnumerable<ChartElement<SkiaSharpDrawingContext>>), typeof(PieChart), new PropertyMetadata(null,
                (DependencyObject o, DependencyPropertyChangedEventArgs args) =>
                {
                    var chart = (PieChart)o;
                    var observer = chart._visualsObserver;
                    observer?.Dispose((IEnumerable<ChartElement<SkiaSharpDrawingContext>>)args.OldValue);
                    observer?.Initialize((IEnumerable<ChartElement<SkiaSharpDrawingContext>>)args.NewValue);
                    if (chart._core == null) return;
                    chart._core.Update();
                }));

    /// <summary>
    /// The sync context property
    /// </summary>
    public static readonly DependencyProperty SyncContextProperty =
        DependencyProperty.Register(
            nameof(SyncContext), typeof(object), typeof(PieChart), new PropertyMetadata(null,
                (DependencyObject o, DependencyPropertyChangedEventArgs args) =>
                {
                    var chart = (PieChart)o;
                    if (chart._canvas != null) chart.CoreCanvas.Sync = args.NewValue;
                    if (chart._core == null) return;
                    chart._core.Update();
                }));

    /// <summary>
    /// The IsClockwise property
    /// </summary>
    public static readonly DependencyProperty IsClockwiseProperty =
        DependencyProperty.Register(
            nameof(IsClockwise), typeof(bool), typeof(PieChart), new PropertyMetadata(true, OnDependencyPropertyChanged));

    /// <summary>
    /// The initial rotation property
    /// </summary>
    public static readonly DependencyProperty InitialRotationProperty =
        DependencyProperty.Register(
            nameof(InitialRotation), typeof(double), typeof(PieChart), new PropertyMetadata(0d, OnDependencyPropertyChanged));

    /// <summary>
    /// The maximum angle property
    /// </summary>
    public static readonly DependencyProperty MaxAngleProperty =
        DependencyProperty.Register(
            nameof(MaxAngle), typeof(double), typeof(PieChart), new PropertyMetadata(360d, OnDependencyPropertyChanged));

    /// <summary>
    /// The total property
    /// </summary>
    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.Register(
            nameof(MaxValue), typeof(double?), typeof(PieChart), new PropertyMetadata(null, OnDependencyPropertyChanged));

    /// <summary>
    /// The start property
    /// </summary>
    public static readonly DependencyProperty MinValueProperty =
        DependencyProperty.Register(
            nameof(MinValue), typeof(double), typeof(PieChart), new PropertyMetadata(0d, OnDependencyPropertyChanged));

    /// <summary>
    /// The draw margin property
    /// </summary>
    public static readonly DependencyProperty DrawMarginProperty =
       DependencyProperty.Register(
           nameof(DrawMargin), typeof(Margin), typeof(PieChart), new PropertyMetadata(null, OnDependencyPropertyChanged));

    /// <summary>
    /// The animations speed property
    /// </summary>
    public static readonly DependencyProperty AnimationsSpeedProperty =
        DependencyProperty.Register(
            nameof(AnimationsSpeed), typeof(TimeSpan), typeof(PieChart),
            new PropertyMetadata(LiveCharts.DefaultSettings.AnimationsSpeed, OnDependencyPropertyChanged));

    /// <summary>
    /// The easing function property
    /// </summary>
    public static readonly DependencyProperty EasingFunctionProperty =
        DependencyProperty.Register(
            nameof(EasingFunction), typeof(Func<float, float>), typeof(PieChart),
            new PropertyMetadata(LiveCharts.DefaultSettings.EasingFunction, OnDependencyPropertyChanged));

    /// <summary>
    /// The legend position property
    /// </summary>
    public static readonly DependencyProperty LegendPositionProperty =
        DependencyProperty.Register(
            nameof(LegendPosition), typeof(LegendPosition), typeof(PieChart),
            new PropertyMetadata(LiveCharts.DefaultSettings.LegendPosition, OnDependencyPropertyChanged));

    /// <summary>
    /// The legend background paint property
    /// </summary>
    public static readonly DependencyProperty LegendBackgroundPaintProperty =
        DependencyProperty.Register(
            nameof(LegendBackgroundPaint), typeof(IPaint<SkiaSharpDrawingContext>), typeof(PieChart),
            new PropertyMetadata(LiveCharts.DefaultSettings.LegendBackgroundPaint, OnDependencyPropertyChanged));

    /// <summary>
    /// The legend text paint property
    /// </summary>
    public static readonly DependencyProperty LegendTextPaintProperty =
        DependencyProperty.Register(
            nameof(LegendTextPaint), typeof(IPaint<SkiaSharpDrawingContext>), typeof(PieChart),
            new PropertyMetadata(LiveCharts.DefaultSettings.LegendTextPaint, OnDependencyPropertyChanged));

    /// <summary>
    /// The legend text size property
    /// </summary>
    public static readonly DependencyProperty LegendTextSizeProperty =
        DependencyProperty.Register(
            nameof(LegendTextSize), typeof(double?), typeof(PieChart),
            new PropertyMetadata(LiveCharts.DefaultSettings.LegendTextSize, OnDependencyPropertyChanged));

    /// <summary>
    /// The tool tip position property
    /// </summary>
    public static readonly DependencyProperty TooltipPositionProperty =
       DependencyProperty.Register(
           nameof(TooltipPosition), typeof(TooltipPosition), typeof(PieChart),
           new PropertyMetadata(LiveCharts.DefaultSettings.TooltipPosition, OnDependencyPropertyChanged));

    /// <summary>
    /// The tooltip background paint property
    /// </summary>
    public static readonly DependencyProperty TooltipBackgroundPaintProperty =
        DependencyProperty.Register(
            nameof(TooltipBackgroundPaint), typeof(IPaint<SkiaSharpDrawingContext>), typeof(PieChart),
            new PropertyMetadata(LiveCharts.DefaultSettings.TooltipBackgroundPaint, OnDependencyPropertyChanged));

    /// <summary>
    /// The tooltip text paint property
    /// </summary>
    public static readonly DependencyProperty TooltipTextPaintProperty =
        DependencyProperty.Register(
            nameof(TooltipTextPaint), typeof(IPaint<SkiaSharpDrawingContext>), typeof(PieChart),
            new PropertyMetadata(LiveCharts.DefaultSettings.TooltipTextPaint, OnDependencyPropertyChanged));

    /// <summary>
    /// The tooltip text size property
    /// </summary>
    public static readonly DependencyProperty TooltipTextSizeProperty =
        DependencyProperty.Register(
            nameof(TooltipTextSize), typeof(double?), typeof(PieChart),
            new PropertyMetadata(LiveCharts.DefaultSettings.TooltipTextSize, OnDependencyPropertyChanged));

    /// <summary>
    /// The update started command.
    /// </summary>
    public static readonly DependencyProperty UpdateStartedCommandProperty =
       DependencyProperty.Register(
           nameof(UpdateStartedCommand), typeof(ICommand), typeof(PieChart),
           new PropertyMetadata(null));

    /// <summary>
    /// The pointer pressed command.
    /// </summary>
    public static readonly DependencyProperty PointerPressedCommandProperty =
       DependencyProperty.Register(
           nameof(PointerPressedCommand), typeof(ICommand), typeof(PieChart),
           new PropertyMetadata(null));

    /// <summary>
    /// The pointer released command.
    /// </summary>
    public static readonly DependencyProperty PointerReleasedCommandProperty =
       DependencyProperty.Register(
           nameof(PointerReleasedCommand), typeof(ICommand), typeof(PieChart),
           new PropertyMetadata(null));

    /// <summary>
    /// The pointer move command.
    /// </summary>
    public static readonly DependencyProperty PointerMoveCommandProperty =
       DependencyProperty.Register(
           nameof(PointerMoveCommand), typeof(ICommand), typeof(PieChart),
           new PropertyMetadata(null));

    /// <summary>
    /// The data pointer down command property
    /// </summary>
    public static readonly DependencyProperty DataPointerDownCommandProperty =
        DependencyProperty.Register(
            nameof(DataPointerDownCommand), typeof(ICommand), typeof(PieChart), new PropertyMetadata(null));

    /// <summary>
    /// The data pointer down command property
    /// </summary>
    public static readonly DependencyProperty ChartPointPointerDownCommandProperty =
        DependencyProperty.Register(
            nameof(ChartPointPointerDownCommand), typeof(ICommand), typeof(PieChart), new PropertyMetadata(null));

    /// <summary>
    /// The chart point pointer down command property
    /// </summary>
    public static readonly DependencyProperty VisualElementsPointerDownCommandProperty =
        DependencyProperty.Register(
            nameof(VisualElementsPointerDownCommand), typeof(ICommand), typeof(PieChart), new PropertyMetadata(null));

    #endregion

    #region events

    /// <inheritdoc cref="IChartView{TDrawingContext}.Measuring" />
    public event ChartEventHandler<SkiaSharpDrawingContext>? Measuring;

    /// <inheritdoc cref="IChartView{TDrawingContext}.UpdateStarted" />
    public event ChartEventHandler<SkiaSharpDrawingContext>? UpdateStarted;

    /// <inheritdoc cref="IChartView{TDrawingContext}.UpdateFinished" />
    public event ChartEventHandler<SkiaSharpDrawingContext>? UpdateFinished;

    /// <inheritdoc cref="IChartView.DataPointerDown" />
    public event ChartPointsHandler? DataPointerDown;

    /// <inheritdoc cref="IChartView.ChartPointPointerDown" />
    public event ChartPointHandler? ChartPointPointerDown;

    /// <inheritdoc cref="IChartView{TDrawingContext}.VisualElementsPointerDown"/>
    public event VisualElementHandler<SkiaSharpDrawingContext>? VisualElementsPointerDown;

    #endregion

    #region properties

    /// <inheritdoc cref="IChartView.DesignerMode" />
    bool IChartView.DesignerMode => Windows.ApplicationModel.DesignMode.DesignModeEnabled;

    /// <inheritdoc cref="IChartView.CoreChart" />
    public IChart CoreChart => _core ?? throw new Exception("Core not set yet.");

    PieChart<SkiaSharpDrawingContext> IPieChartView<SkiaSharpDrawingContext>.Core
        => _core == null ? throw new Exception("core not found") : (PieChart<SkiaSharpDrawingContext>)_core;

    LvcColor IChartView.BackColor
    {
        get => Background is not SolidColorBrush b
                ? new LvcColor()
                : LvcColor.FromArgb(b.Color.A, b.Color.R, b.Color.G, b.Color.B);
        set => SetValue(BackgroundProperty, new SolidColorBrush(Windows.UI.Color.FromArgb(value.A, value.R, value.G, value.B)));
    }

    /// <inheritdoc cref="IChartView.SyncContext" />
    public object SyncContext
    {
        get => GetValue(SyncContextProperty);
        set => SetValue(SyncContextProperty, value);
    }

    /// <inheritdoc cref="IChartView{TDrawingContext}.Title" />
    public VisualElement<SkiaSharpDrawingContext>? Title
    {
        get => (VisualElement<SkiaSharpDrawingContext>?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="IPieChartView{TDrawingContext}.Series" />
    public IEnumerable<ISeries> Series
    {
        get => (IEnumerable<ISeries>)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    /// <inheritdoc cref="IChartView{TDrawingContext}.VisualElements" />
    public IEnumerable<ChartElement<SkiaSharpDrawingContext>> VisualElements
    {
        get => (IEnumerable<ChartElement<SkiaSharpDrawingContext>>)GetValue(VisualElementsProperty);
        set => SetValue(VisualElementsProperty, value);
    }

    /// <inheritdoc cref="IPieChartView{TDrawingContext}.IsClockwise" />
    public bool IsClockwise
    {
        get => (bool)GetValue(IsClockwiseProperty);
        set => SetValue(IsClockwiseProperty, value);
    }

    /// <inheritdoc cref="IPieChartView{TDrawingContext}.InitialRotation" />
    public double InitialRotation
    {
        get => (double)GetValue(InitialRotationProperty);
        set => SetValue(InitialRotationProperty, value);
    }

    /// <inheritdoc cref="IPieChartView{TDrawingContext}.MaxAngle" />
    public double MaxAngle
    {
        get => (double)GetValue(MaxAngleProperty);
        set => SetValue(MaxAngleProperty, value);
    }

    /// <inheritdoc cref="IPieChartView{TDrawingContext}.Total" />
    [Obsolete($"Use {nameof(MaxValue)} instead.")]
    public double? Total
    {
        get => (double?)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    /// <inheritdoc cref="IPieChartView{TDrawingContext}.MaxValue" />
    public double? MaxValue
    {
        get => (double?)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    /// <inheritdoc cref="IPieChartView{TDrawingContext}.MinValue" />
    public double MinValue
    {
        get => (double)GetValue(MinValueProperty);
        set => SetValue(MinValueProperty, value);
    }

    /// <inheritdoc cref="IChartView.DrawMargin" />
    public Margin DrawMargin
    {
        get => (Margin)GetValue(DrawMarginProperty);
        set => SetValue(DrawMarginProperty, value);
    }

    Margin? IChartView.DrawMargin
    {
        get => DrawMargin;
        set => SetValue(DrawMarginProperty, value);
    }

    LvcSize IChartView.ControlSize => _canvas == null
        ? throw new Exception("Canvas not found")
        : new LvcSize { Width = (float)_canvas.ActualWidth, Height = (float)_canvas.ActualHeight };

    /// <inheritdoc cref="IChartView{TDrawingContext}.CoreCanvas" />
    public MotionCanvas<SkiaSharpDrawingContext> CoreCanvas => _canvas == null ? throw new Exception("Canvas not found") : _canvas.CanvasCore;

    /// <inheritdoc cref="IChartView.AnimationsSpeed" />
    public TimeSpan AnimationsSpeed
    {
        get => (TimeSpan)GetValue(AnimationsSpeedProperty);
        set => SetValue(AnimationsSpeedProperty, value);
    }

    TimeSpan IChartView.AnimationsSpeed
    {
        get => AnimationsSpeed;
        set => SetValue(AnimationsSpeedProperty, value);
    }

    /// <inheritdoc cref="IChartView.EasingFunction" />
    public Func<float, float> EasingFunction
    {
        get => (Func<float, float>)GetValue(EasingFunctionProperty);
        set => SetValue(EasingFunctionProperty, value);
    }

    Func<float, float>? IChartView.EasingFunction
    {
        get => EasingFunction;
        set => SetValue(EasingFunctionProperty, value);
    }

    /// <inheritdoc cref="IChartView.LegendPosition" />
    public LegendPosition LegendPosition
    {
        get => (LegendPosition)GetValue(LegendPositionProperty);
        set => SetValue(LegendPositionProperty, value);
    }

    /// <inheritdoc cref="IChartView.TooltipPosition" />
    public TooltipPosition TooltipPosition
    {
        get => (TooltipPosition)GetValue(TooltipPositionProperty);
        set => SetValue(TooltipPositionProperty, value);
    }

    /// <inheritdoc cref="IChartView{TDrawingContext}.TooltipBackgroundPaint" />
    public IPaint<SkiaSharpDrawingContext>? TooltipBackgroundPaint
    {
        get => (IPaint<SkiaSharpDrawingContext>?)GetValue(TooltipBackgroundPaintProperty);
        set => SetValue(TooltipBackgroundPaintProperty, value);
    }

    /// <inheritdoc cref="IChartView{TDrawingContext}.TooltipTextPaint" />
    public IPaint<SkiaSharpDrawingContext>? TooltipTextPaint
    {
        get => (IPaint<SkiaSharpDrawingContext>?)GetValue(TooltipTextPaintProperty);
        set => SetValue(TooltipTextPaintProperty, value);
    }

    /// <inheritdoc cref="IChartView{TDrawingContext}.TooltipTextSize" />
    public double? TooltipTextSize
    {
        get => (double?)GetValue(TooltipTextSizeProperty);
        set => SetValue(TooltipTextSizeProperty, value);
    }

    /// <inheritdoc cref="IChartView{TDrawingContext}.Tooltip" />
    public IChartTooltip<SkiaSharpDrawingContext>? Tooltip { get; set; } = new SKDefaultTooltip();

    /// <inheritdoc cref="IChartView{TDrawingContext}.LegendBackgroundPaint" />
    public IPaint<SkiaSharpDrawingContext>? LegendBackgroundPaint
    {
        get => (IPaint<SkiaSharpDrawingContext>?)GetValue(LegendBackgroundPaintProperty);
        set => SetValue(LegendBackgroundPaintProperty, value);
    }

    /// <inheritdoc cref="IChartView{TDrawingContext}.LegendTextPaint" />
    public IPaint<SkiaSharpDrawingContext>? LegendTextPaint
    {
        get => (IPaint<SkiaSharpDrawingContext>?)GetValue(LegendTextPaintProperty);
        set => SetValue(LegendTextPaintProperty, value);
    }

    /// <inheritdoc cref="IChartView{TDrawingContext}.LegendTextSize" />
    public double? LegendTextSize
    {
        get => (double?)GetValue(LegendTextSizeProperty);
        set => SetValue(LegendTextSizeProperty, value);
    }

    /// <inheritdoc cref="IChartView{TDrawingContext}.Legend" />
    public IChartLegend<SkiaSharpDrawingContext>? Legend { get; set; } = new SKDefaultLegend();

    /// <inheritdoc cref="IChartView{TDrawingContext}.AutoUpdateEnabled" />
    public bool AutoUpdateEnabled { get; set; } = true;

    /// <inheritdoc cref="IChartView.UpdaterThrottler" />
    public TimeSpan UpdaterThrottler { get; set; } = LiveCharts.DefaultSettings.UpdateThrottlingTimeout;

    /// <summary>
    /// Gets or sets a command to execute when the chart update started.
    /// </summary>
    public ICommand? UpdateStartedCommand
    {
        get => (ICommand?)GetValue(UpdateStartedCommandProperty);
        set => SetValue(UpdateStartedCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets a command to execute when the pointer is pressed on the chart.
    /// </summary>
    public ICommand? PointerPressedCommand
    {
        get => (ICommand?)GetValue(PointerPressedCommandProperty);
        set => SetValue(PointerPressedCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets a command to execute when the pointer is released on the chart.
    /// </summary>
    public ICommand? PointerReleasedCommand
    {
        get => (ICommand?)GetValue(PointerReleasedCommandProperty);
        set => SetValue(PointerReleasedCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets a command to execute when the pointer moves over the chart.
    /// </summary>
    public ICommand? PointerMoveCommand
    {
        get => (ICommand?)GetValue(PointerMoveCommandProperty);
        set => SetValue(PointerMoveCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets a command to execute when the pointer goes down on a data or data points.
    /// </summary>
    public ICommand DataPointerDownCommand
    {
        get => (ICommand)GetValue(DataPointerDownCommandProperty);
        set => SetValue(DataPointerDownCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets a command to execute when the pointer goes down on a chart point.
    /// </summary>
    public ICommand ChartPointPointerDownCommand
    {
        get => (ICommand)GetValue(ChartPointPointerDownCommandProperty);
        set => SetValue(ChartPointPointerDownCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets a command to execute when the pointer goes down on a chart point.
    /// </summary>
    public ICommand? VisualElementsPointerDownCommand
    {
        get => (ICommand?)GetValue(VisualElementsPointerDownCommandProperty);
        set => SetValue(VisualElementsPointerDownCommandProperty, value);
    }

    #endregion

    /// <inheritdoc cref="IChartView{TDrawingContext}.GetPointsAt(LvcPoint, TooltipFindingStrategy)"/>
    public IEnumerable<ChartPoint> GetPointsAt(LvcPoint point, TooltipFindingStrategy strategy = TooltipFindingStrategy.Automatic)
    {
        if (_core is not PieChart<SkiaSharpDrawingContext> cc) throw new Exception("core not found");

        if (strategy == TooltipFindingStrategy.Automatic)
            strategy = cc.Series.GetTooltipFindingStrategy();

        return cc.Series.SelectMany(series => series.FindHitPoints(cc, point, strategy));
    }

    /// <inheritdoc cref="IChartView{TDrawingContext}.GetVisualsAt(LvcPoint)"/>
    public IEnumerable<VisualElement<SkiaSharpDrawingContext>> GetVisualsAt(LvcPoint point)
    {
        return _core is not PieChart<SkiaSharpDrawingContext> cc
            ? throw new Exception("core not found")
            : cc.VisualElements.SelectMany(visual => ((VisualElement<SkiaSharpDrawingContext>)visual).IsHitBy(_core, point));
    }

    void IChartView.InvokeOnUIThread(Action action)
    {
        UnoPlatformHelpers.InvokeOnUIThread(action, DispatcherQueue);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var canvas = (MotionCanvas)FindName("motionCanvas");
        _canvas = canvas;

        if (_core is null)
        {
            _core = new PieChart<SkiaSharpDrawingContext>(this, config => config.UseDefaults(), canvas.CanvasCore);

            if (SyncContext != null)
                _canvas.CanvasCore.Sync = SyncContext;

            if (_core == null) throw new Exception("Core not found!");
            _core.Update();

            _core.Measuring += OnCoreMeasuring;
            _core.UpdateStarted += OnCoreUpdateStarted;
            _core.UpdateFinished += OnCoreUpdateFinished;

            PointerPressed += OnPointerPressed;
            PointerReleased += PieChart_PointerReleased;
            SizeChanged += OnSizeChanged;
            PointerMoved += OnPointerMoved;
            PointerExited += OnPointerExited;
        }

        _core.Load();
        _core.Update();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_core == null) throw new Exception("Core not found!");
        _core.Update();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(this);

        if (PointerPressedCommand is not null)
        {
            var args = new PointerCommandArgs(this, new(p.Position.X, p.Position.Y), e);
            if (PointerPressedCommand.CanExecute(args)) PointerPressedCommand.Execute(args);
        }

        _core?.InvokePointerDown(new LvcPoint((float)p.Position.X, (float)p.Position.Y), false);
    }

    private void PieChart_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(this);
        if (PointerReleasedCommand is not null)
        {
            var args = new PointerCommandArgs(this, new(p.Position.X, p.Position.Y), e);
            if (PointerReleasedCommand.CanExecute(args)) PointerReleasedCommand.Execute(args);
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(motionCanvas);

        if (PointerMoveCommand is not null)
        {
            var args = new PointerCommandArgs(this, new(p.Position.X, p.Position.Y), e);
            if (PointerMoveCommand.CanExecute(args)) PointerMoveCommand.Execute(args);
        }

        _core?.InvokePointerMove(new LvcPoint((float)p.Position.X, (float)p.Position.Y));
    }

    private void OnCoreUpdateFinished(IChartView<SkiaSharpDrawingContext> chart)
    {
        UpdateFinished?.Invoke(this);
    }

    private void OnCoreUpdateStarted(IChartView<SkiaSharpDrawingContext> chart)
    {
        if (UpdateStartedCommand is not null)
        {
            var args = new ChartCommandArgs(this);
            if (UpdateStartedCommand.CanExecute(args)) UpdateStartedCommand.Execute(args);
        }

        UpdateStarted?.Invoke(this);
    }

    private void OnCoreMeasuring(IChartView<SkiaSharpDrawingContext> chart)
    {
        Measuring?.Invoke(this);
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _core?.InvokePointerLeft();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _core?.Unload();
    }

    private static void OnDependencyPropertyChanged(DependencyObject o, DependencyPropertyChangedEventArgs args)
    {
        var chart = (PieChart)o;
        if (chart._core == null) return;

        chart._core.Update();
    }

    void IChartView.OnDataPointerDown(IEnumerable<ChartPoint> points, LvcPoint pointer)
    {
        DataPointerDown?.Invoke(this, points);
        if (DataPointerDownCommand is not null && DataPointerDownCommand.CanExecute(points)) DataPointerDownCommand.Execute(points);

        var closest = points.FindClosestTo(pointer);
        ChartPointPointerDown?.Invoke(this, closest);
        if (ChartPointPointerDownCommand is not null && ChartPointPointerDownCommand.CanExecute(closest)) ChartPointPointerDownCommand.Execute(closest);
    }

    void IChartView<SkiaSharpDrawingContext>.OnVisualElementPointerDown(
       IEnumerable<VisualElement<SkiaSharpDrawingContext>> visualElements, LvcPoint pointer)
    {
        var args = new VisualElementsEventArgs<SkiaSharpDrawingContext>(CoreChart, visualElements, pointer);

        VisualElementsPointerDown?.Invoke(this, args);
        if (VisualElementsPointerDownCommand is not null && VisualElementsPointerDownCommand.CanExecute(args))
            VisualElementsPointerDownCommand.Execute(args);
    }

    void IChartView.Invalidate()
    {
        CoreCanvas.Invalidate();
    }
}

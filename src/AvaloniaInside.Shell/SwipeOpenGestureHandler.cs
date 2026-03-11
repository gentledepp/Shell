using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AvaloniaInside.Shell;

/// <summary>
/// Reusable swipe gesture handler for opening/closing side panes (SplitView-based).
/// Tracks the finger/pointer position during drag and manipulates OpenPaneLength for smooth tracking.
/// </summary>
public sealed class SwipeOpenGestureHandler : IDisposable
{
   private const double EdgeZoneFraction = 1.0 / 3.0;
   private const double DirectionLockThreshold = 10;
   private const double VelocityThreshold = 800;
   private const double OpenPositionThreshold = 0.4;
   private const double ClosePositionThreshold = 0.6;
   private const int SnapAnimationDurationMs = 200;
   private const int VelocitySampleCount = 5;

   private readonly Control _hitTestArea;
   private readonly Func<bool> _canSwipeOpen;
   private readonly Func<bool> _canSwipeClose;
   private readonly Func<bool> _isPaneOpen;
   private readonly Func<double> _targetWidth;
   private readonly Action<double> _setOpenPaneLength;
   private readonly Action<bool> _setPaneOpen;
   private readonly Action<bool> _commitState;

   private bool _isDragging;
   private bool _directionLocked;
   private bool _isSwipeToClose;
   private Point _startPoint;
   private double _currentDragWidth;
   private bool _attached;
   private bool _isAnimating;

   /// <summary>
   /// True while a swipe gesture is in progress (dragging or snap-animating).
   /// External code (e.g. SplitView.PaneClosing handlers) should check this
   /// and suppress their default behaviour when true.
   /// </summary>
   public bool IsGestureActive => _directionLocked || _isAnimating;

   // Velocity tracking
   private readonly List<(DateTime time, double x)> _velocitySamples = new();

   public SwipeOpenGestureHandler(
      Control hitTestArea,
      Func<bool> canSwipeOpen,
      Func<bool> canSwipeClose,
      Func<bool> isPaneOpen,
      Func<double> targetWidth,
      Action<double> setOpenPaneLength,
      Action<bool> setPaneOpen,
      Action<bool> commitState)
   {
      _hitTestArea = hitTestArea ?? throw new ArgumentNullException(nameof(hitTestArea));
      _canSwipeOpen = canSwipeOpen;
      _canSwipeClose = canSwipeClose;
      _isPaneOpen = isPaneOpen;
      _targetWidth = targetWidth;
      _setOpenPaneLength = setOpenPaneLength;
      _setPaneOpen = setPaneOpen;
      _commitState = commitState;
   }

   public void Attach()
   {
      if (_attached) return;
      _attached = true;

      _hitTestArea.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
      _hitTestArea.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
      _hitTestArea.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
      _hitTestArea.AddHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Tunnel);
      _hitTestArea.SizeChanged += OnSizeChanged;
   }

   public void Detach()
   {
      if (!_attached) return;
      _attached = false;

      _hitTestArea.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
      _hitTestArea.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
      _hitTestArea.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
      _hitTestArea.RemoveHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost);
      _hitTestArea.SizeChanged -= OnSizeChanged;
   }

   public void Dispose() => Detach();

   private bool IsRtl => _hitTestArea.FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;

   private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
   {
      if (_isDragging || _isAnimating) return;

      var pointerType = e.Pointer.Type;
      if (pointerType != PointerType.Touch && pointerType != PointerType.Mouse)
         return;

      var point = e.GetPosition(_hitTestArea);
      var isPaneCurrentlyOpen = _isPaneOpen();

      if (isPaneCurrentlyOpen)
      {
         if (!_canSwipeClose()) return;
         _isSwipeToClose = true;
      }
      else
      {
         if (!_canSwipeOpen()) return;

         // Check edge zone — left third of the screen (or right third for RTL)
         var controlWidth = _hitTestArea.Bounds.Width;
         var edgeZone = controlWidth * EdgeZoneFraction;
         bool inEdgeZone = IsRtl
            ? point.X >= controlWidth - edgeZone
            : point.X <= edgeZone;

         if (!inEdgeZone) return;
         _isSwipeToClose = false;
      }

      _startPoint = point;
      _isDragging = true;
      _directionLocked = false;
      _velocitySamples.Clear();
      RecordVelocitySample(point.X);

      // Do NOT capture here — let buttons and other controls handle the press.
      // We only capture once the direction is locked in OnPointerMoved.
   }

   private void OnPointerMoved(object? sender, PointerEventArgs e)
   {
      if (!_isDragging) return;

      var point = e.GetPosition(_hitTestArea);
      var deltaX = point.X - _startPoint.X;
      var deltaY = point.Y - _startPoint.Y;

      // Apply RTL inversion
      if (IsRtl) deltaX = -deltaX;

      if (!_directionLocked)
      {
         var absDx = Math.Abs(deltaX);
         var absDy = Math.Abs(deltaY);

         if (absDx < DirectionLockThreshold && absDy < DirectionLockThreshold)
            return;

         // Must be more horizontal than vertical
         if (absDy > absDx)
         {
            CancelDrag(e.Pointer);
            return;
         }

         // For swipe-to-open, must be moving right (positive deltaX after RTL inversion)
         if (!_isSwipeToClose && deltaX <= 0)
         {
            CancelDrag(e.Pointer);
            return;
         }

         // For swipe-to-close, must be moving left (negative deltaX after RTL inversion)
         if (_isSwipeToClose && deltaX >= 0)
         {
            CancelDrag(e.Pointer);
            return;
         }

         _directionLocked = true;

         // NOW capture — direction is confirmed as a swipe gesture
         e.Pointer.Capture(_hitTestArea);

         // On first direction-lock for swipe-to-open, open the pane at width 0
         if (!_isSwipeToClose)
         {
            _setOpenPaneLength(0);
            _setPaneOpen(true);
         }
      }

      RecordVelocitySample(point.X);

      var target = _targetWidth();
      if (target <= 0) return;

      if (_isSwipeToClose)
      {
         // Closing: reduce width from target by leftward drag distance
         var absDelta = Math.Abs(deltaX);
         _currentDragWidth = Math.Max(0, Math.Min(target, target - absDelta));
      }
      else
      {
         // Opening: increase width by rightward drag distance
         _currentDragWidth = Math.Max(0, Math.Min(target, deltaX));
      }

      _setOpenPaneLength(_currentDragWidth);
      e.Handled = true;
   }

   private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
   {
      if (!_isDragging) return;

      var point = e.GetPosition(_hitTestArea);
      RecordVelocitySample(point.X);

      if (!_directionLocked)
      {
         // Never locked direction — no gesture occurred.
         // Do NOT call Capture(null) here: we never captured,
         // so that would steal another control's (e.g. button's) capture.
         _isDragging = false;
         return;
      }

      // Clear _isDragging BEFORE releasing capture so that the synchronous
      // PointerCaptureLost handler (which also checks _isDragging) is a no-op.
      _isDragging = false;
      e.Pointer.Capture(null);

      var target = _targetWidth();
      var velocity = CalculateVelocity(); // px/s, positive = rightward (after RTL)

      bool shouldOpen;
      if (_isSwipeToClose)
      {
         // If fast leftward swipe or remaining width < 60% → close
         var effectiveVelocity = IsRtl ? velocity : -velocity;
         shouldOpen = !(effectiveVelocity > VelocityThreshold || _currentDragWidth < target * ClosePositionThreshold);
      }
      else
      {
         // If fast rightward swipe or position > 40% → open
         var effectiveVelocity = IsRtl ? -velocity : velocity;
         shouldOpen = effectiveVelocity > VelocityThreshold || _currentDragWidth > target * OpenPositionThreshold;
      }

      // _directionLocked stays true — keeps IsGestureActive true until animation finishes
      AnimateToState(shouldOpen, target);
   }

   private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
   {
      if (!_isDragging) return;

      if (_directionLocked)
      {
         // Snap back to previous state
         var wasOpen = _isSwipeToClose;
         _isDragging = false;
         AnimateToState(wasOpen, _targetWidth());
      }
      else
      {
         _isDragging = false;
      }
   }

   private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
   {
      if (_isDragging || _directionLocked)
      {
         // Cancel drag on resize
         var wasOpen = _isSwipeToClose;
         _isDragging = false;
         _directionLocked = false;

         var target = _targetWidth();
         if (wasOpen)
         {
            _setOpenPaneLength(target);
            _setPaneOpen(true);
            _commitState(true);
         }
         else
         {
            _setOpenPaneLength(0);
            _setPaneOpen(false);
            _commitState(false);
         }
      }
   }

   private void CancelDrag(IPointer pointer)
   {
      _isDragging = false;
      _directionLocked = false;
      pointer.Capture(null);
   }

   private void RecordVelocitySample(double x)
   {
      _velocitySamples.Add((DateTime.UtcNow, x));
      while (_velocitySamples.Count > VelocitySampleCount)
         _velocitySamples.RemoveAt(0);
   }

   private double CalculateVelocity()
   {
      if (_velocitySamples.Count < 2) return 0;

      var first = _velocitySamples[0];
      var last = _velocitySamples[^1];
      var dt = (last.time - first.time).TotalSeconds;
      if (dt <= 0) return 0;

      var dx = last.x - first.x;
      if (IsRtl) dx = -dx;
      return dx / dt;
   }

   private void AnimateToState(bool open, double targetWidth)
   {
      var from = _currentDragWidth;
      var to = open ? targetWidth : 0;

      if (Math.Abs(from - to) < 1)
      {
         // Already at target — just commit
         FinalizeState(open, targetWidth);
         return;
      }

      _isAnimating = true;
      var easing = new CubicEaseOut();
      var startTime = DateTime.UtcNow;
      var duration = TimeSpan.FromMilliseconds(SnapAnimationDurationMs);

      var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
      timer.Tick += (_, _) =>
      {
         var elapsed = DateTime.UtcNow - startTime;
         var progress = Math.Min(1.0, elapsed / duration);
         var easedProgress = easing.Ease(progress);
         var current = from + (to - from) * easedProgress;

         _setOpenPaneLength(Math.Max(0, current));

         if (progress >= 1.0)
         {
            timer.Stop();
            FinalizeState(open, targetWidth);
            _isAnimating = false;
            _directionLocked = false;
         }
      };
      timer.Start();
   }

   private void FinalizeState(bool open, double targetWidth)
   {
      if (open)
      {
         _setOpenPaneLength(targetWidth);
         _setPaneOpen(true);
         _commitState(true);
      }
      else
      {
         _setOpenPaneLength(0);
         _setPaneOpen(false);
         _commitState(false);
      }

      // If there was no animation (immediate finalize), clear direction lock
      // on next dispatcher tick so IsGestureActive stays true through any
      // SplitView PaneClosing events that fire on the same event cycle.
      if (!_isAnimating)
      {
         Dispatcher.UIThread.Post(() => _directionLocked = false, DispatcherPriority.Input);
      }
   }
}

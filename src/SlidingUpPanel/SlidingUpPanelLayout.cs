using System;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Util;
using Android.Views;
using Android.Views.Accessibility;
using Java.Interop;

namespace Cheesebaron.SlidingUpPanel
{
    public class SlidingUpPanelLayout : ViewGroup
    {
        private new const string Tag = "SlidingUpPanelLayout";
        private const int DefaultPanelHeight = 68;
        private const int DefaultShadowHeight = 4;
        private const int DefaultMinFlingVelocity = 400;
        private const bool DefaultOverlayFlag = false;
        private static readonly Color DefaultFadeColor = new Color(0, 0, 0, 99);
        private static readonly int[] DefaultAttrs = { Android.Resource.Attribute.Gravity };

        private readonly int _minFlingVelocity = DefaultMinFlingVelocity;
        private Color _coveredFadeColor = DefaultFadeColor;
        private readonly Paint _coveredFadePaint = new Paint();
        private int _panelHeight = -1;
        private readonly int _shadowHeight = -1;
        private readonly bool _isSlidingUp;
        private bool _canSlide;
        private View _dragView;
        private readonly int _dragViewResId = -1;
        private View _slideableView;
        private SlideState _slideState = SlideState.Collapsed;
        private float _slideOffset;
        private int _slideRange;
        private bool _isUnableToDrag;
        private readonly int _scrollTouchSlop;
        private float _initialMotionX;
        private float _initialMotionY;
        private float _anchorPoint;
        private readonly ViewDragHelper _dragHelper;
        private bool _firstLayout = true;
        private readonly Rect _tmpRect = new Rect();

        public event SlidingUpPanelSlideEventHandler PanelSlide;
        public event SlidingUpPanelEventHandler PanelCollapsed;
        public event SlidingUpPanelEventHandler PanelExpanded;
        public event SlidingUpPanelEventHandler PanelAnchored;

        public bool IsExpanded
        {
            get { return _slideState == SlideState.Expanded; }
        }

        public bool IsAnchored
        {
            get { return _slideState == SlideState.Anchored; }
        }

        public bool IsSlideable
        {
            get { return _canSlide; }
        }

        public Color CoveredFadeColor
        {
            get { return _coveredFadeColor; }
            set
            {
                _coveredFadeColor = value;
                Invalidate();
            }
        }

        public int PanelHeight
        {
            get { return _panelHeight; }
            set
            {
                _panelHeight = value;
                RequestLayout();
            }
        }

        public View DragView
        {
            get { return _dragView; }
            set { _dragView = value; }
        }

        public float AnchorPoint
        {
            get { return _anchorPoint; }
            set
            {
                if (value > 0 && value < 1)
                    _anchorPoint = value;
            }
        }

        public Drawable ShadowDrawable { get; set; }

        public bool SlidingEnabled { get; set; }

        public bool OverlayContent { get; set; }

        public bool IsUsingDragViewTouchEvents { get; set; }

        private int SlidingTop
        {
            get
            {
                if (_slideableView != null)
                {
                    return _isSlidingUp
                        ? MeasuredHeight - PaddingBottom - _slideableView.MeasuredHeight
                        : MeasuredHeight - PaddingBottom - (_slideableView.MeasuredHeight * 2);
                }

                return MeasuredHeight - PaddingBottom;
            }
        }

        public bool PaneVisible
        {
            get
            {
                if (ChildCount < 2)
                    return false;
                var slidingPane = GetChildAt(1);
                return slidingPane.Visibility == ViewStates.Visible;
            }
        }

        public SlidingUpPanelLayout(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer) { }

        public SlidingUpPanelLayout(Context context)
            : this(context, null) { }

        public SlidingUpPanelLayout(Context context, IAttributeSet attrs) 
            : this(context, attrs, 0) { }

        public SlidingUpPanelLayout(Context context, IAttributeSet attrs, int defStyle)
            : base(context, attrs, defStyle)
        {
            // not really relevan in Xamarin.Android but keeping for a possible
            // future update which will render layouts in the Designer.
            if (IsInEditMode) return; 

            if (attrs != null)
            {
                var defAttrs = context.ObtainStyledAttributes(attrs, DefaultAttrs);

                if (defAttrs.Length() > 0)
                {
                    var gravity = defAttrs.GetInt(0, (int)GravityFlags.NoGravity);
                    var gravityFlag = (GravityFlags) gravity;
                    if (gravityFlag != GravityFlags.Top && gravityFlag != GravityFlags.Bottom)
                        throw new ArgumentException("gravity must be set to either top or bottom");
                    _isSlidingUp = gravityFlag == GravityFlags.Bottom;
                }

                defAttrs.Recycle();

                var ta = context.ObtainStyledAttributes(attrs, Resource.Styleable.SlidingUpPanelLayout);

                if (ta.Length() > 0)
                {
                    _panelHeight = ta.GetDimensionPixelSize(Resource.Styleable.SlidingUpPanelLayout_collapsedHeight, -1);
                    _shadowHeight = ta.GetDimensionPixelSize(Resource.Styleable.SlidingUpPanelLayout_shadowHeight, -1);

                    _minFlingVelocity = ta.GetInt(Resource.Styleable.SlidingUpPanelLayout_flingVelocity,
                        DefaultMinFlingVelocity);
                    _coveredFadeColor = ta.GetColor(Resource.Styleable.SlidingUpPanelLayout_fadeColor, DefaultFadeColor);

                    _dragViewResId = ta.GetResourceId(Resource.Styleable.SlidingUpPanelLayout_dragView, -1);

                    OverlayContent = ta.GetBoolean(Resource.Styleable.SlidingUpPanelLayout_overlay, DefaultOverlayFlag);
                }

                ta.Recycle();
            }

            var density = context.Resources.DisplayMetrics.Density;
            if (_panelHeight == -1)
                _panelHeight = (int) (DefaultPanelHeight * density + 0.5f);
            if (_shadowHeight == -1)
                _shadowHeight = (int) (DefaultShadowHeight * density + 0.5f);

            SetWillNotDraw(false);

            _dragHelper = ViewDragHelper.Create(this, 0.5f, new DragHelperCallback(this));
            _dragHelper.MinVelocity = _minFlingVelocity * density;

            _canSlide = true;
            SlidingEnabled = true;

            var vc = ViewConfiguration.Get(context);
            _scrollTouchSlop = vc.ScaledTouchSlop;
        }

        protected override void OnFinishInflate()
        {
            base.OnFinishInflate();
            if (_dragViewResId != -1)
                _dragView = FindViewById(_dragViewResId);
        }

        private void OnPanelSlide(View panel)
        {
            if (PanelSlide != null)
                PanelSlide(this, new SlidingUpPanelSlideEventArgs {Panel = panel, SlideOffset = _slideOffset});
        }

        private void OnPanelCollapsed(View panel)
        {
            if (PanelCollapsed != null)
                PanelCollapsed(this, new SlidingUpPanelEventArgs { Panel = panel });
            SendAccessibilityEvent(EventTypes.WindowStateChanged);
        }

        private void OnPanelAnchored(View panel)
        {
            if (PanelAnchored != null)
                PanelAnchored(this, new SlidingUpPanelEventArgs { Panel = panel });
            SendAccessibilityEvent(EventTypes.WindowStateChanged);
        }

        private void OnPanelExpanded(View panel)
        {
            if (PanelExpanded != null)
                PanelExpanded(this, new SlidingUpPanelEventArgs { Panel = panel });
            SendAccessibilityEvent(EventTypes.WindowStateChanged);
        }

        private void UpdateObscuredViewVisibility()
        {
            if (ChildCount == 0) return;

            var leftBound = PaddingLeft;
            var rightBound = Width - PaddingLeft;
            var topBound = PaddingTop;
            var bottomBound = Height - PaddingBottom;
            int left;
            int right;
            int top;
            int bottom;

            if (_slideableView != null && HasOpaqueBackground(_slideableView))
            {
                left = _slideableView.Left;
                right = _slideableView.Right;
                top = _slideableView.Top;
                bottom = _slideableView.Bottom;
            }
            else
                left = right = top = bottom = 0;

            var child = GetChildAt(0);
            var clampedChildLeft = Math.Max(leftBound, child.Left);
            var clampedChildTop = Math.Max(topBound, child.Top);
            var clampedChildRight = Math.Max(rightBound, child.Right);
            var clampedChildBottom = Math.Max(bottomBound, child.Bottom);
            ViewStates vis;
            if (clampedChildLeft >= left && clampedChildTop >= top &&
                clampedChildRight <= right && clampedChildBottom <= bottom)
                vis = ViewStates.Invisible;
            else
                vis = ViewStates.Visible;
            child.Visibility = vis;
        }

        private void SetAllChildrenVisible()
        {
            for (var i = 0; i < ChildCount; i++)
            {
                var child = GetChildAt(i);
                if (child.Visibility == ViewStates.Invisible)
                    child.Visibility = ViewStates.Visible;
            }
        }

        private static bool HasOpaqueBackground(View view)
        {
            var bg = view.Background;
            if (bg != null)
                return bg.Opacity == (int) Format.Opaque;
            return false;
        }

        protected override void OnAttachedToWindow()
        {
            base.OnAttachedToWindow();
            _firstLayout = true;
        }

        protected override void OnDetachedFromWindow()
        {
            base.OnDetachedFromWindow();
            _firstLayout = true;
        }

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            var widthMode = MeasureSpec.GetMode(widthMeasureSpec);
            var widthSize = MeasureSpec.GetSize(widthMeasureSpec);
            var heightMode = MeasureSpec.GetMode(heightMeasureSpec);
            var heightSize = MeasureSpec.GetSize(heightMeasureSpec);

            if (widthMode != MeasureSpecMode.Exactly)
                throw new InvalidOperationException("Width must have an exact value or match_parent");
            if (heightMode != MeasureSpecMode.Exactly)
                throw new InvalidOperationException("Height must have an exact value or match_parent");

            var layoutHeight = heightSize - PaddingTop - PaddingBottom;
            var panelHeight = _panelHeight;

            if (ChildCount > 2)
                Log.Error(Tag, "OnMeasure: More than two child views are not supported.");
            else
                panelHeight = 0;

            _slideableView = null;
            _canSlide = false;

            for (var i = 0; i < ChildCount; i++)
            {
                var child = GetChildAt(i);
                var lp = (LayoutParams) child.LayoutParameters;

                var height = layoutHeight;
                if (child.Visibility == ViewStates.Gone)
                {
                    lp.DimWhenOffset = false;
                    continue;
                }

                if (i == 1)
                {
                    lp.Slideable = true;
                    lp.DimWhenOffset = true;
                    _slideableView = child;
                    _canSlide = true;
                }
                else
                {
                    if (!OverlayContent)
                        height -= panelHeight;
                }

                int childWidthSpec;
                if (lp.Width == ViewGroup.LayoutParams.WrapContent)
                    childWidthSpec = MeasureSpec.MakeMeasureSpec(widthSize, MeasureSpecMode.AtMost);
                else if (lp.Width == ViewGroup.LayoutParams.MatchParent)
                    childWidthSpec = MeasureSpec.MakeMeasureSpec(widthSize, MeasureSpecMode.Exactly);
                else
                    childWidthSpec = MeasureSpec.MakeMeasureSpec(lp.Width, MeasureSpecMode.Exactly);

                int childHeightSpec;
                if (lp.Height == ViewGroup.LayoutParams.WrapContent)
                    childHeightSpec = MeasureSpec.MakeMeasureSpec(height, MeasureSpecMode.AtMost);
                else if (lp.Height == ViewGroup.LayoutParams.MatchParent)
                    childHeightSpec = MeasureSpec.MakeMeasureSpec(height, MeasureSpecMode.Exactly);
                else
                    childHeightSpec = MeasureSpec.MakeMeasureSpec(lp.Height, MeasureSpecMode.Exactly);

                child.Measure(childWidthSpec, childHeightSpec);
            }
            SetMeasuredDimension(widthSize, heightSize);
        }

        protected override void OnLayout(bool changed, int l, int t, int r, int b)
        {
            if (_firstLayout)
            {
                switch (_slideState)
                {
                    case SlideState.Expanded:
                        _slideOffset = _canSlide ? 0.0f : 1.0f;
                        break;
                    case SlideState.Anchored:
                        _slideOffset = _canSlide ? _anchorPoint : 1.0f;
                        break;
                    case SlideState.Collapsed:
                        _slideOffset = 1.0f;
                        break;
                }
            }

            for (var i = 0; i < ChildCount; i++)
            {
                var child = GetChildAt(i);

                if (child.Visibility == ViewStates.Gone)
                    continue;

                var lp = (LayoutParams) child.LayoutParameters;
                var childHeight = child.MeasuredHeight;

                if (lp.Slideable)
                    _slideRange = childHeight - _panelHeight;

                int childTop;
                if (_isSlidingUp)
                    childTop = lp.Slideable ? SlidingTop + (int) (_slideRange * _slideOffset) : PaddingTop;
                else
                    childTop = lp.Slideable ? SlidingTop - (int)(_slideRange * _slideOffset) : PaddingTop + PanelHeight;

                var childBottom = childTop + childHeight;
                var childLeft = PaddingLeft;
                var childRight = childLeft + child.MeasuredWidth;

                child.Layout(childLeft, childTop, childRight, childBottom);
            }

            if (_firstLayout)
                UpdateObscuredViewVisibility();

            _firstLayout = false;
        }

        protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
        {
            base.OnSizeChanged(w, h, oldw, oldh);

            if (h != oldh)
                _firstLayout = true;
        }

        public override bool OnInterceptTouchEvent(MotionEvent ev)
        {
            var action = MotionEventCompat.GetActionMasked(ev);

            if (!_canSlide || !SlidingEnabled || (_isUnableToDrag && action != (int) MotionEventActions.Down))
            {
                _dragHelper.Cancel();
                return base.OnInterceptTouchEvent(ev);
            }

            if (action == (int) MotionEventActions.Cancel || action == (int) MotionEventActions.Up)
            {
                _dragHelper.Cancel();
                return false;
            }

            var x = ev.GetX();
            var y = ev.GetY();
            var interceptTap = false;

            switch (action)
            {
                case (int)MotionEventActions.Down:
                    _isUnableToDrag = false;
                    _initialMotionX = x;
                    _initialMotionY = y;
                    if (IsDragViewUnder((int) x, (int) y) && !IsUsingDragViewTouchEvents)
                        interceptTap = true;
                    break;
                case (int)MotionEventActions.Move:
                    var adx = Math.Abs(x - _initialMotionX);
                    var ady = Math.Abs(y - _initialMotionY);
                    var dragSlop = _dragHelper.TouchSlop;

                    if (IsUsingDragViewTouchEvents)
                    {
                        if (adx > _scrollTouchSlop && ady < _scrollTouchSlop)
                            return base.OnInterceptTouchEvent(ev);
                        if (ady > _scrollTouchSlop)
                            interceptTap = IsDragViewUnder((int) x, (int) y);
                    }

                    if ((ady > dragSlop && adx > ady) || !IsDragViewUnder((int) x, (int) y))
                    {
                        _dragHelper.Cancel();
                        _isUnableToDrag = true;
                        return false;
                    }
                    break;
            }

            var interceptForDrag = _dragHelper.ShouldInterceptTouchEvent(ev);

            return interceptForDrag || interceptTap;
        }

        public override bool OnTouchEvent(MotionEvent ev)
        {
            if (!_canSlide || !SlidingEnabled)
                return base.OnTouchEvent(ev);

            _dragHelper.ProcessTouchEvent(ev);
            var action = (int)ev.Action;

            switch (action & MotionEventCompat.ActionMask)
            {
                case (int)MotionEventActions.Down:
                {
                    var x = ev.GetX();
                    var y = ev.GetY();
                    _initialMotionX = x;
                    _initialMotionY = y;
                    break;
                }
                case (int)MotionEventActions.Up:
                {
                    var x = ev.GetX();
                    var y = ev.GetY();
                    var dx = x - _initialMotionX;
                    var dy = y - _initialMotionY;
                    var slop = _dragHelper.TouchSlop;
                    var dragView = _dragView ?? _slideableView;
                    if (dx * dx + dy * dy < slop * slop && IsDragViewUnder((int)x, (int)y))
                    {
                        dragView.PlaySoundEffect(SoundEffects.Click);
                        if (!IsExpanded && !IsAnchored)
                            ExpandPane(_anchorPoint);
                        else
                            CollapsePane();
                    }
                    break;
                }
            }

            return true;
        }

        private bool IsDragViewUnder(int x, int y)
        {
            var dragView = _dragView ?? _slideableView;
            if (dragView == null) return false;

            var viewLocation = new int[2];
            dragView.GetLocationOnScreen(viewLocation);
            var parentLocation = new int[2];
            GetLocationOnScreen(parentLocation);

            var screenX = parentLocation[0] + x;
            var screenY = parentLocation[1] + y;
            return screenX >= viewLocation[0] && screenX < viewLocation[0] + dragView.Width &&
                   screenY >= viewLocation[1] && screenY < viewLocation[1] + dragView.Height;
        }

        public bool CollapsePane()
        {
            if (_firstLayout || SmoothSlideTo(1.0f))
                return true;
            return false;
        }

        public bool ExpandPane()
        {
            return ExpandPane(0);
        }

        public bool ExpandPane(float slideOffset)
        {
            if (!PaneVisible)
                ShowPane();
            return _firstLayout || SmoothSlideTo(slideOffset);
        }

        public void ShowPane()
        {
            if (ChildCount < 2) return;

            var slidingPane = GetChildAt(1);
            slidingPane.Visibility = ViewStates.Visible;
            RequestLayout();
        }

        public void HidePane()
        {
            if (_slideableView == null) return;

            _slideableView.Visibility = ViewStates.Gone;
            RequestLayout();
        }

        private void OnPanelDragged(int newTop)
        {
            _slideOffset = _isSlidingUp
                ? (float) (newTop - SlidingTop) / _slideRange
                : (float) (SlidingTop - newTop) / _slideRange;
            OnPanelSlide(_slideableView);
        }

        protected override bool DrawChild(Canvas canvas, View child, long drawingTime)
        {
            var lp = (LayoutParams) child.LayoutParameters;
            var save = canvas.Save(SaveFlags.Clip);

            var drawScrim = false;

            if (_canSlide && !lp.Slideable && _slideableView != null)
            {
                if (!OverlayContent)
                {
                    canvas.GetClipBounds(_tmpRect);
                    if (_isSlidingUp)
                        _tmpRect.Bottom = Math.Min(_tmpRect.Bottom, _slideableView.Top);
                    else
                        _tmpRect.Top = Math.Max(_tmpRect.Top, _slideableView.Bottom);

                    canvas.ClipRect(_tmpRect);
                }

                if (_slideOffset < 1)
                    drawScrim = true;
            }

            var result = base.DrawChild(canvas, child, drawingTime);
            canvas.RestoreToCount(save);

            if (drawScrim)
            {
                var baseAlpha = (_coveredFadeColor.ToArgb() & 0xff000000) >> 24;
                var imag = (int) (baseAlpha * (1 - _slideOffset));
                var color = imag << 24 | (_coveredFadeColor.ToArgb() & 0xffffff);
                _coveredFadePaint.Color = new Color(color);
                canvas.DrawRect(_tmpRect, _coveredFadePaint);
            }

            return result;
        }

        private bool SmoothSlideTo(float slideOffset)
        {
            if (!_canSlide) return false;

            var y = _isSlidingUp
                ? (int) (SlidingTop + slideOffset * _slideRange)
                : (int) (SlidingTop - slideOffset * _slideRange);

            if (!_dragHelper.SmoothSlideViewTo(_slideableView, _slideableView.Left, y)) return false;

            SetAllChildrenVisible();
            ViewCompat.PostInvalidateOnAnimation(this);
            return true;
        }

        public override void ComputeScroll()
        {
            if (!_dragHelper.ContinueSettling(true)) return;

            if (!_canSlide)
            {
                _dragHelper.Abort();
                return;
            }

            ViewCompat.PostInvalidateOnAnimation(this);
        }

        public override void Draw(Canvas canvas)
        {
            base.Draw(canvas);

            if (_slideableView == null) return;
            if (ShadowDrawable == null) return;

            var right = _slideableView.Right;
            var left = _slideableView.Left;
            int top;
            int bottom;
            if (_isSlidingUp)
            {
                top = _slideableView.Top - _shadowHeight;
                bottom = _slideableView.Top;
            }
            else
            {
                top = _slideableView.Bottom;
                bottom = _slideableView.Bottom + _shadowHeight;
            }
            
            ShadowDrawable.SetBounds(left, top, right, bottom);
            ShadowDrawable.Draw(canvas);
        }

        protected bool CanScroll(View view, bool checkV, int dx, int x, int y)
        {
            var viewGroup = view as ViewGroup;
            if (viewGroup == null) return checkV && ViewCompat.CanScrollHorizontally(view, -dx);

            var scrollX = viewGroup.ScrollX;
            var scrollY = viewGroup.ScrollY;
            var count = viewGroup.ChildCount;

            for (var i = count - 1; i >= 0; i--)
            {
                var child = viewGroup.GetChildAt(i);
                if (x + scrollX >= child.Left && x + scrollX < child.Right &&
                    y + scrollY >= child.Top && y + scrollY < child.Bottom &&
                    CanScroll(child, true, dx, x + scrollX - child.Left, y + scrollY - child.Top))
                    return true;
            }
            return checkV && ViewCompat.CanScrollHorizontally(view, -dx);
        }

        protected override ViewGroup.LayoutParams GenerateDefaultLayoutParams()
        {
            return new LayoutParams();
        }

        protected override ViewGroup.LayoutParams GenerateLayoutParams(ViewGroup.LayoutParams p)
        {
            var param = p as MarginLayoutParams;
            return param != null ? new LayoutParams(param) : new LayoutParams(p);
        }

        protected override bool CheckLayoutParams(ViewGroup.LayoutParams p)
        {
            var param = p as LayoutParams;
            return param != null && base.CheckLayoutParams(p);
        }

        public override ViewGroup.LayoutParams GenerateLayoutParams(IAttributeSet attrs)
        {
            return new LayoutParams(Context, attrs);
        }

        public new class LayoutParams : MarginLayoutParams 
        {
            private static readonly int[] Attrs = {
                Android.Resource.Attribute.LayoutWidth
            };

            public bool Slideable { get; set; }

            public bool DimWhenOffset { get; set; }

            public Paint DimPaint { get; set; }

            public LayoutParams()
                : base(MatchParent, MatchParent) { }

            public LayoutParams(int width, int height)
                : base(width, height) { }

            public LayoutParams(ViewGroup.LayoutParams source)
                : base(source) { }

            public LayoutParams(MarginLayoutParams source)
                : base(source) { }

            public LayoutParams(LayoutParams source) 
                : base(source) { }

            public LayoutParams(Context c, IAttributeSet attrs) 
                : base(c, attrs) 
            {
                var a = c.ObtainStyledAttributes(attrs, Attrs);
                a.Recycle();
            }
        }

        private class DragHelperCallback : ViewDragHelper.Callback
        {
            //This class is a bit nasty, as C# does not allow calling variables directly
            //like stupid Java does.
            private readonly SlidingUpPanelLayout _panelLayout;

            public DragHelperCallback(SlidingUpPanelLayout layout)
            {
                _panelLayout = layout;
            }

            public override bool TryCaptureView(View child, int pointerId)
            {
                return !_panelLayout._isUnableToDrag && ((LayoutParams) child.LayoutParameters).Slideable;
            }

            public override void OnViewDragStateChanged(int state)
            {
                var anchoredTop = (int) (_panelLayout._anchorPoint * _panelLayout._slideRange);

                if (_panelLayout._dragHelper.ViewDragState == ViewDragHelper.StateIdle)
                {
                    if (FloatNearlyEqual(_panelLayout._slideOffset, 0))
                    {
                        if (_panelLayout._slideState != SlideState.Expanded)
                        {
                            _panelLayout.UpdateObscuredViewVisibility();
                            _panelLayout.OnPanelExpanded(_panelLayout._slideableView);
                            _panelLayout._slideState = SlideState.Expanded;
                        }
                    }
                    else if (FloatNearlyEqual(_panelLayout._slideOffset, (float)anchoredTop / _panelLayout._slideRange))
                    {
                        if (_panelLayout._slideState != SlideState.Anchored)
                        {
                            _panelLayout.UpdateObscuredViewVisibility();
                            _panelLayout.OnPanelAnchored(_panelLayout._slideableView);
                            _panelLayout._slideState = SlideState.Anchored;
                        }
                    }
                    else if (_panelLayout._slideState != SlideState.Collapsed)
                    {
                        _panelLayout.OnPanelCollapsed(_panelLayout._slideableView);
                        _panelLayout._slideState = SlideState.Collapsed;
                    }
                }
            }

            public override void OnViewCaptured(View capturedChild, int activePointerId)
            {
                _panelLayout.SetAllChildrenVisible();
            }

            public override void OnViewPositionChanged(View changedView, int left, int top, int dx, int dy)
            {
                _panelLayout.OnPanelDragged(top);
                _panelLayout.Invalidate();
            }

            public override void OnViewReleased(View releasedChild, float xvel, float yvel)
            {
                var top = _panelLayout._isSlidingUp
                    ? _panelLayout.SlidingTop
                    : _panelLayout.SlidingTop - _panelLayout._slideRange;

                if (!FloatNearlyEqual(_panelLayout._anchorPoint, 0))
                {
                    int anchoredTop;
                    float anchorOffset;

                    if (_panelLayout._isSlidingUp)
                    {
                        anchoredTop = (int) (_panelLayout._anchorPoint * _panelLayout._slideRange);
                        anchorOffset = (float) anchoredTop / _panelLayout._slideRange;
                    }
                    else
                    {
                        anchoredTop = _panelLayout._panelHeight -
                                      (int) (_panelLayout._anchorPoint * _panelLayout._slideRange);
                        anchorOffset = (float)(_panelLayout._panelHeight - anchoredTop) / _panelLayout._slideRange;
                    }

                    if (yvel > 0 || (FloatNearlyEqual(yvel, 0) && _panelLayout._slideOffset >= (1f + anchorOffset) / 2))
                        top += _panelLayout._slideRange;
                    else if (FloatNearlyEqual(yvel, 0) && _panelLayout._slideOffset < (1f + anchorOffset) / 2 &&
                             _panelLayout._slideOffset >= anchorOffset / 2)
                        top += (int) (_panelLayout._slideRange * _panelLayout._anchorPoint);
                }
                else if (yvel > 0 || (FloatNearlyEqual(yvel, 0) && _panelLayout._slideOffset > 0.5f))
                    top += _panelLayout._slideRange;

                _panelLayout._dragHelper.SettleCapturedViewAt(releasedChild.Left, top);
                _panelLayout.Invalidate();
            }

            public override int GetViewVerticalDragRange(View child)
            {
                return _panelLayout._slideRange;
            }

            public override int ClampViewPositionVertical(View child, int top, int dy)
            {
                int topBound;
                int bottomBound;
                if (_panelLayout._isSlidingUp)
                {
                    topBound = _panelLayout.SlidingTop;
                    bottomBound = topBound + _panelLayout._slideRange;
                }
                else
                {
                    bottomBound = _panelLayout.PaddingTop;
                    topBound = bottomBound - _panelLayout._slideRange;
                }

                return Math.Min(Math.Max(top, topBound), bottomBound);
            }
        }

        protected override IParcelable OnSaveInstanceState()
        {
            var superState = base.OnSaveInstanceState();

            var savedState = new SavedState(superState, _slideState);
            return savedState;
        }

        protected override void OnRestoreInstanceState(IParcelable state)
        {
            try
            {
                var savedState = (SavedState) state;
                base.OnRestoreInstanceState(savedState.SuperState);
                _slideState = savedState.State;
            }
            catch
            {
                base.OnRestoreInstanceState(state);    
            }
        }

        public class SavedState : BaseSavedState
        {
            public SlideState State { get; private set; }

            public SavedState(IParcelable superState, SlideState item)
                : base(superState)
            {
                State = item;
            }

            public SavedState(Parcel parcel)
                : base(parcel)
            {
                try
                {
                    State = (SlideState) parcel.ReadInt();
                }
                catch
                {
                    State = SlideState.Collapsed;
                }
            }

            public override void WriteToParcel(Parcel dest, ParcelableWriteFlags flags)
            {
                base.WriteToParcel(dest, flags);
                dest.WriteInt((int)State);
            }

            [ExportField("CREATOR")]
            public static SavedStateCreator InitializeCreator()
            {
                return new SavedStateCreator();
            }

            public class SavedStateCreator : Java.Lang.Object, IParcelableCreator
            {
                public Java.Lang.Object CreateFromParcel(Parcel source)
                {
                    return new SavedState(source);
                }

                public Java.Lang.Object[] NewArray(int size)
                {
                    return new SavedState[size];
                }
            }
        }

        public static bool FloatNearlyEqual(float a, float b, float epsilon) 
        {
            var absA = Math.Abs(a);
            var absB = Math.Abs(b);
            var diff = Math.Abs(a - b);

            if (a == b) // shortcut, handles infinities
                return true;
            if (a == 0 || b == 0 || diff < float.MinValue)
                // a or b is zero or both are extremely close to it
                // relative error is less meaningful here
                return diff < (epsilon * float.MinValue);

            // use relative error
            return diff / (absA + absB) < epsilon;
        }

        public static bool FloatNearlyEqual(float a, float b)
        {
            return FloatNearlyEqual(a, b, 0.00001f);
        }
    }
}

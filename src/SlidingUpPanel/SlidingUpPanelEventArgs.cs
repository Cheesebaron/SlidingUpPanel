using System;
using Android.Views;

namespace Cheesebaron.SlidingUpPanel
{
    public delegate void SlidingUpPanelEventHandler(object sender, SlidingUpPanelEventArgs args);
    public delegate void SlidingUpPanelSlideEventHandler(object sender, SlidingUpPanelSlideEventArgs args);

    public class SlidingUpPanelEventArgs : EventArgs
    {
        public View Panel { get; set; }
    }

    public class SlidingUpPanelSlideEventArgs : SlidingUpPanelEventArgs
    {
        public float SlideOffset { get; set; }
    }
}
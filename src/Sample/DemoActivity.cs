using Android.App;
using Android.Support.V7.App;
using Android.Text.Method;
using Android.Util;
using Android.OS;
using Android.Widget;
using Cheesebaron.SlidingUpPanel;

namespace Sample
{
    [Activity(Label = "SlidingUpPanel Sample", MainLauncher = true, Icon = "@drawable/ic_launcher",
        Theme = "@style/AppTheme")]
    public class DemoActivity : AppCompatActivity
    {
        private const string Tag = "DemoActivity";
        private const string SavedStateActionBarHidden = "saved_state_action_bar_hidden";

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            var layout = FindViewById<SlidingUpPanelLayout>(Resource.Id.sliding_layout);
            FindViewById<TextView>(Resource.Id.more_info).MovementMethod = new LinkMovementMethod();

            layout.ShadowDrawable = Resources.GetDrawable(Resource.Drawable.above_shadow);
            layout.AnchorPoint = 0.3f;
            layout.PanelExpanded += (s, e) => Log.Info(Tag, "PanelExpanded");
            layout.PanelCollapsed += (s, e) => Log.Info(Tag, "PanelCollapsed");
            layout.PanelAnchored += (s, e) => Log.Info(Tag, "PanelAnchored");
            layout.PanelSlide += (s, e) =>
            {
                if (e.SlideOffset < 0.2)
                {
                    if (SupportActionBar.IsShowing)
                        SupportActionBar.Hide();
                }
                else
                {
                    if (!SupportActionBar.IsShowing)
                        SupportActionBar.Show();
                }
            };

            var actionBarHidden = savedInstanceState != null &&
                                  savedInstanceState.GetBoolean(SavedStateActionBarHidden, false);
            if (actionBarHidden)
                SupportActionBar.Hide();
        }

        protected override void OnSaveInstanceState(Bundle outState)
        {
            base.OnSaveInstanceState(outState);
            outState.PutBoolean(SavedStateActionBarHidden, !SupportActionBar.IsShowing);
        }
    }
}


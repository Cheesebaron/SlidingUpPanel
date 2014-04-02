Start by creating or editing an existing AXML layout file. Add `cheesebaron.slidinguppanel.SlidingUpPanelLayout` as the root of this layout.

```
<?xml version="1.0" encoding="utf-8"?>
<cheesebaron.slidinguppanel.SlidingUpPanelLayout 
    xmlns:android="http://schemas.android.com/apk/res/android"
    android:id="@+id/sliding_layout"
    android:layout_width="match_parent"
    android:layout_height="match_parent"
    android:gravity="bottom">
    <!-- Main content -->
    <TextView
        android:id="@+id/main"
        android:layout_width="match_parent"
        android:layout_height="match_parent"
        android:text="Main Content"
        android:textSize="16sp" />
    <!-- Sliding panel -->
    <TextView
        android:id="@+id/main"
        android:layout_width="match_parent"
        android:layout_height="match_parent"
        android:text="Sliding Panel"
        android:textSize="16sp" />
</cheesebaron.slidinguppanel.SlidingUpPanelLayout>
```

The `SlidingUpPanelLayout` supports at most to nested children views. The first being the main content if you View, the second being the content of the sliding panel. The `android:gravity` attribute determines whether you have the menu on the `top` or the `bottom` of the screen. The two attributes `android:layout_width` and `android:layout_height`, for the root layout, both need to be set to `match_parent`. The same goes for the two nested child layouts.

As the `SlidingUpPanelLayout` only supports two nested Views, you will have to wrap additional Views in a container such as `LinearLayout`, `FrameLayout` or `RelativeLayout`

## Supported attributes

`SlidingUpPanelLayout` supports a variety of attributes to allow you to customize its behavior. Remember to add the namespace `xmlns:app="http://schemas.android.com/apk/res-auto"` to your layout.

Then you can use the following attributes:

- `collapsedHeight` sets the height of the drawer when it is collapsed. Use a dimension value `dp`, `sp`, `px` for this.
- `shadowHeight` sets the height of the shadow. Use a dimension value `dp`, `sp`, `px` for this.
- `fadeColor` sets the color to fade the main content with, when sliding the panel on top of it.
- `flingVelocity` set the velocity of which the panel allows to be flinged to either be opened or closed.
- `dragView` set the `id` of the view you want to restrict to allow dragging the panel.

### Usage of attributes

So a sample of using the described attributes would look as follows

```
<?xml version="1.0" encoding="utf-8"?>
<cheesebaron.slidinguppanel.SlidingUpPanelLayout 
    xmlns:android="http://schemas.android.com/apk/res/android"
    xmlns:app="http://schemas.android.com/apk/res-auto"
    android:id="@+id/sliding_layout"
    android:layout_width="match_parent"
    android:layout_height="match_parent"
    android:gravity="bottom"
    app:collapsedHeight="68dp"
    app:shadowHeight="4dp"
    app:fadeColor="#ffddaadd">
    
</cheesebaron.slidinguppanel.SlidingUpPanelLayout>
```

## Subscribing to events

The `SlidingUpPanelLayout` has 4 events which you can listen to in your app.

- `PanelExpanded` which triggers when the panel is expanded
- `PanelCollapsed` triggers when the panel is collapsed
- `PanelAnchored` triggers if you have set an `AnchorPoint` on the screen and the panel expands to that point
- `PanelSlide` triggers whenever the panel is dragged

As shown in the sample project, the `PanelSlide` event could be used to hide or show the `ActionBar` in an app whenever the sliding up panel has reached a certain point.

## Additional properties and methods

If you prefer, or want to override a value set in the layout, using a coded approach, several Properties are exposed for your usage.

- `PanelHeight` can be used to set the height of the panel
- `AnchorPoint` can be used to set a point in the middle of the screen to allow an intermediate expanded state
- `ShadowDrawable` can be used to set an alternative shadow
- `SlidingEnabled` can be used to allow or disallow dragging of the panel
- `CoveredFadeColor` can be used to set the fade color used on top of your main content
- `IsExpanded`, `IsSlidable`, `PaneVisible`, `IsAnchored` give you the state of the panel

You also have access to a couple methods to control the panel

- `ShowPane()` and `HidePane()` show and hide the pane triggered in code

For additional information refer to the Sample project

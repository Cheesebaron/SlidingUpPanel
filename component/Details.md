Add an awesome draggable panel that slides up from either the bottom or top of your screen. Use it to show more details, reveal music player controls or whatever you want. This type of panel is also used in apps such as Google Music and Rdio.

## Key features

- Customizable height
- Customizable shadow
- Restrict draggable area of panel to 
    - A visible view
    - An anchor point
- Listen to events when dragging the panel
- Switch between sliding from top or bottom

## Requirements

This library uses Android Support v4, and it is tested on Android 2.2 and above.

## Usage

To use it, add the component and in your layout simply wrap your layouts with `cheesebaron.slidinguppanel.SlidingUpPanelLayout`. It supports two children. The first child is your content layout. The second child is your layout for the sliding up panel. Both children should have their height set to
`match_parent`.

```
<cheesebaron.slidinguppanel.SlidingUpPanelLayout
    android:id="@+id/sliding_layout"
    android:layout_width="match_parent"
    android:layout_height="match_parent"
    android:gravity="bottom">

    <RelativeLayout
        android:layout_width="match_parent"
        android:layout_height="match_parent">
        <!-- Your main content inside here -->
    </RelativeLayout>

    <RelativeLayout
        android:layout_width="match_parent"
        android:layout_height="match_parent">
        <!-- Your main sliding panel inside here -->
    </RelativeLayout>
</cheesebaron.slidinguppanel.SlidingUpPanelLayout>
```

The project is Open Source and can be [forked on GitHub](https://github.com/Cheesebaron/SlidingUpPanel).

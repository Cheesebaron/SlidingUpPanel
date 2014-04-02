:: Grab xpkg from http://components.xamarin.com/submit/xpkg and put contents of the zip into xpkg

xpkg\xamarin-component.exe create-manually slidinguppanel-1.0.1.xam ^
	--name="SlidingUpPanel" ^
	--summary="A panel that slides out from the bottom or top of the screen." ^
	--website="https://github.com/Cheesebaron/SlidingUpPanel" ^
	--details="Details.md" ^
	--license="License.md" ^
	--getting-started="GettingStarted.md" ^
	--icon="icons/slidinguppanel_128x128.png" ^
	--icon="icons/slidinguppanel_512x512.png" ^
	--library="android":"../bin/Debug/Cheesebaron.SlidingUpPanel.dll" ^
	--publisher "Cheesebaron" ^
	--sample="Android Sample. Demonstrates SlidingUpPanel":"../src/SlidingUpPanel.sln"
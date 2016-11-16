#tool "nuget:?package=GitVersion.CommandLine"
#tool "nuget:?package=gitlink"
#addin "Cake.Xamarin"

var sln = new FilePath("src/SlidingUpPanel.sln");
var project = new FilePath("src/SlidingUpPanel/SlidingUpPanel.csproj");
var sample = new FilePath("src/Sample/Sample.csproj");
var binDir = new DirectoryPath("bin/Release");
var nuspec = new FilePath("slidinguppanel.nuspec");
var componentDir = new DirectoryPath("component");
var componentYaml = new FilePath("component/component.yaml");
var componentTool = new FilePath("component/xamarin-component.exe");
var outputDir = new DirectoryPath("artifacts");
var target = Argument("target", "Default");

var isRunningOnAppVeyor = AppVeyor.IsRunningOnAppVeyor;
var isPullRequest = AppVeyor.Environment.PullRequest.IsPullRequest;

Task("Clean").Does(() =>
{
    CleanDirectories("./**/bin");
    CleanDirectories("./**/obj");
	CleanDirectories(outputDir.FullPath);
});

GitVersion versionInfo = null;
Task("Version").Does(() => {
	GitVersion(new GitVersionSettings {
		UpdateAssemblyInfo = true,
		OutputType = GitVersionOutput.BuildServer
	});

	versionInfo = GitVersion(new GitVersionSettings{ OutputType = GitVersionOutput.Json });
	Information("VI:\t{0}", versionInfo.FullSemVer);
});

Task("Restore").Does(() => {
	NuGetRestore(sln);
});

Task("Build")
	.IsDependentOn("Clean")
	.IsDependentOn("Version")
	.IsDependentOn("Restore")
	.Does(() =>  {
	
	DotNetBuild(sln, 
		settings => settings.SetConfiguration("Release")
							.WithProperty("DebugSymbols", "true")
            				.WithProperty("DebugType", "Full")
							.WithTarget("Build")
	);
});

Task("GitLink")
	.IsDependentOn("Build")
	.WithCriteria(() => IsRunningOnWindows())
	.Does(() => {
	//pdbstr.exe and costura are not xplat currently
	GitLink(nuspec.GetDirectory(), new GitLinkSettings {
		ArgumentCustomization = args => args.Append("-ignore Sample")
	});
});

Task("Package")
	.IsDependentOn("GitLink")
	.Does(() => {

	EnsureDirectoryExists(outputDir);

	var dllDir = binDir + "/Cheesebaron.SlidingUpPanel.*";

	Information("Dll Dir: {0}", dllDir);

	var nugetContent = new List<NuSpecContent>();
	foreach(var dll in GetFiles(dllDir)){
	 	Information("File: {0}", dll.ToString());
		nugetContent.Add(new NuSpecContent {
			Target = "lib/MonoAndroid40",
			Source = dll.ToString()
		});
	}

	Information("File Count {0}", nugetContent.Count);

	NuGetPack(nuspec, new NuGetPackSettings {
		Authors = new [] { "Tomasz Cielecki" },
		Owners = new [] { "Tomasz Cielecki" },
		IconUrl = new Uri("http://i.imgur.com/V3983YY.png"),
		ProjectUrl = new Uri("https://github.com/Cheesebaron/SlidingUpPanel"),
		LicenseUrl = new Uri("https://github.com/Cheesebaron/SlidingUpPanel/blob/master/LICENSE"),
		Copyright = "Copyright (c) Tomasz Cielecki",
		RequireLicenseAcceptance = false,
		ReleaseNotes = ParseReleaseNotes("./releasenotes.md").Notes.ToArray(),
		Tags = new [] {"umano", "xamarin", "android", "panel", "sliding"},
		Version = versionInfo.NuGetVersion,
		Symbols = false,
		NoPackageAnalysis = true,
		OutputDirectory = outputDir,
		Verbosity = NuGetVerbosity.Detailed,
		Files = nugetContent,
		BasePath = "/."
	});
});

Task("DownloadComponentTool")
	.WithCriteria(() => !FileExists(componentTool))
	.Does(() => {
	var tool = DownloadFile("http://components.xamarin.com/submit/xpkg");
	Unzip(tool, componentDir);
	DeleteFile(tool);
});

Task("Component")
	.IsDependentOn("GitLink")
	.IsDependentOn("DownloadComponentTool")
	.Does(() => {
	
	EnsureDirectoryExists(outputDir);

	TransformTextFile(componentYaml)
		.WithToken("VERSION", versionInfo.SemVer)
		.Save(componentYaml);

	PackageComponent(
		componentDir,
		new XamarinComponentSettings() {
			ToolPath = componentTool
		}
	);

	MoveFiles("component/SlidingUpPanel-*.xam", outputDir);
});

Task("UploadAppVeyorArtifact")
	.IsDependentOn("Package")
	.IsDependentOn("Component")
	.WithCriteria(() => !isPullRequest)
	.WithCriteria(() => isRunningOnAppVeyor)
	.Does(() => {

	Information("Artifacts Dir: {0}", outputDir.FullPath);

	foreach(var file in GetFiles(outputDir.FullPath + "/*")) {
		Information("Uploading {0}", file.FullPath);
		AppVeyor.UploadArtifact(file.FullPath);
	}
});

Task("Default")
	.IsDependentOn("UploadAppVeyorArtifact")
	.Does(() => {

	Information("AppVeyor: {0}", isRunningOnAppVeyor);

	});

RunTarget(target);

#tool nuget:?package=GitVersion.CommandLine
#tool nuget:?package=vswhere

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
var configuration = Argument("configuration", "Release");

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

FilePath msBuildPath;
Task("ResolveBuildTools")
    .WithCriteria(() => IsRunningOnWindows())
    .Does(() => 
{
    var vsLatest = VSWhereLatest();
    msBuildPath = (vsLatest == null)
        ? null
        : vsLatest.CombineWithFilePath("./MSBuild/15.0/Bin/MSBuild.exe");
});

Task("Restore").Does(() => {
	NuGetRestore(sln);
});

Task("Build")
	.IsDependentOn("Clean")
	.IsDependentOn("Version")
	.IsDependentOn("Restore")
	.IsDependentOn("ResolveBuildTools")
	.Does(() => 
{	
	var settings = new MSBuildSettings 
	{
		Configuration = configuration
	};

	settings = settings.WithTarget("Build")
		.WithProperty("DebugSymbols", "True")
		.WithProperty("DebugType", "Full");

	if (msBuildPath != null)
		settings.ToolPath = msBuildPath;

	MSBuild(sln, settings);
});

Task("Package")
	.IsDependentOn("Build")
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

Task("UploadAppVeyorArtifact")
	.IsDependentOn("Package")
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
	.Does(() => 
{
	Information("AppVeyor: {0}", isRunningOnAppVeyor);
});

RunTarget(target);

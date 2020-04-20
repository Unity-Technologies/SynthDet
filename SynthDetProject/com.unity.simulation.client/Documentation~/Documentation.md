# USim C# Client Documentation.

## Classes
### API
The API class is the foundation for all the methods supported. Some additional higher level functionality is built on top of these methods for convenience. This class is static, and has a more or less one to one correspondence with the USim CLI.
```csharp
public static void Login();
```
>Authenticates with the USim service. Will retrieve an access token and save it to the USim configuration directory.

```csharp
public static void Refresh();
```
>Refreshes the access token, and writes it out to the USim configuration directory.

```csharp
public static SysParamDefinition[] GetSysParams();
```
>Retrieves the system parameters that are available. You will specify one system parameter when executing a run, to define what time of system resources will be available on each node in the cluster.

```csharp
public static string UploadBuild(string name, string location);
```
>Uploads a build from location. `location` must refer to a path to a directory containing an executable called `name`. All files in the location directory will be compressed into a zip file, which will be uploaded.

```csharp
public static void DownloadBuild(string id, string location);
```
>Downloads a previously uploaded build. `id` is the build id returned from uploading, and `location` is where the build will be downloaded.

```csharp
public static string UploadAppParam<T>(string name, T param) where T : struct;
```
>Converts any serializable struct into a JSON object and uploads as an app param. Returns the app param `id`

```csharp
public static T DownloadAppParam<T>(string id);
```
>Downloads the app param referenced by `id` and deserializes into a struct T. If any failure occurs, will either throw an exception, or return `default(T)`.

```csharp
public static string UploadRunDefinition(RunDefinition definition);
```
>Uploads a run definition and returns the run definition `id`.

```csharp
public static RunDefinition DownloadRunDefinition(string definitionId);
```
>Downloads a run definition identified by `definitionId`

```csharp
public static RunSummary Summarize(string executionId);
```
>Returns a `RunSummary` for a given `executionId`

```csharp
public static RunDescription Describe(string executionId);
```
>Returns a RunDescription for a given `executionId`

```csharp
public static Dictionary<int, ManifestEntry> GetManifest(string executionId);
```
>Retrieves the manifest for a given `executionId`

### Project
#### Properties

```csharp
public static string activeProjectId { get; }
```
>Returns the currently active `project id`. This is generally the cloud `project id` of the currently loaded project, but you can override this by activating a different `project id`.

#### Methods

```csharp
public static ProjectInfo[] GetProjects();
```
>Returns a list of all the projects you have available;

```csharp
public static void Activate(string projectId = null);
```
>Sets the active project id to `projectId`. This will only affect the calls to the USim service, and will noy change anything for your currently loaded project.

```csharp
public static void Deactivate();
```
>Deactivates the active project id and resets to the currently loaded cloud `project id`

```csharp
public static string[] GetOpenScenes();
```
>Returns a list of all the open scenes in the editor.

```csharp
public static string[] GetBuildSettingScenes();
```
>Returns a list of all the scenes added to the build settings dialog.

```csharp
public static void BuildProject(string savePath, string name, string[] scenes = null, BuildTarget target = BuildTarget.StandaloneLinux64, bool compress = true, bool launch = false);
```
>Builds the currently loaded project to `savePath` with `name`. A list of desired scenes to be included must be provided. You can specify the build target if you need to build for something other than Linux. By default the build is compressed into a zip file. You can also specify `launch: true` to launch the project, provided it was built for your host platform.

```csharp
public static void CompressProject(string path, string name);
```
>Compress a previously built project at `path` with `name`. This is called automatically by `BuildProject` unless overridden by passing `compress: false`

### Token
#### Properties

```csharp
public string accessToken { get; }
```
>Returns the access token that corresponds to the Token instance.

```csharp
public bool isExpired { get; }
```
>Returns true|false indicating whether or not the toke has expired.

#### Methods

```csharp
public static Token Load(string tokenFile = null, bool refreshIfExpired = true);
```
>Loads a token from a file, and optionally refreshes if `refreshIfExpired` is set to true. If no `tokenFile` is passed, the default location will be used. Returns a new instance of the Token class.

```csharp
public void Save(string tokenFile = null);
```
>Saves the token to a file. If no file path is specified, the default will be used.

```csharp
public void Refresh(int timeoutSeconds = 30);
```
>Refreshes the token.

### Run

#### Properties

```csharp
public string definitionId { get; }
```
>Returns the run definition id for this run.

```csharp
public string executionId { get; }
```
>Returns the run execution id for this run. This will only be valid after the run has been executed, and will be empty until then.

```csharp
public string buildLocation { get; set; }
```
>Specify the location of a build when getting ready to execute. The build at this location will be zipped and uploaded.

```csharp
public int instances { get; }
```
>The number of instances in the cluster as defined by the total of instances per app param.

```csharp
public bool completed { get; }
```
>Returns true|false whether or not the run has completed. Note that failed runs will also complete. To check outcome, see `Describe` or `Summarize`.

```csharp
public Dictionary<string, AppParam> appParameters;
```
>A container with the currently uploaded appParameters for this run. The key is the app param `id`.

#### Factory Methods
```csharp
public static Run Create(string name = null, string description = null);
```
>Create an empty run with optional name and description.

```csharp
public static Run CreateFromDefinitionId(string definitionId);
```
>Create a run from a previously uploaded run `definitionId`. The run will be populated with what can be gleaned from the run definition.

```csharp
public static Run CreateFromExecutionId(string executionId);
```
>Create a run from a previously uploaded run `executionId`. The run will be populated with what can be gleaned from the run execution.

#### Methods
```csharp
public void SetBuildLocation(string path);
```
>Set the build location for when the build is uploaded. This must be a path to a zip file. You can also use the property `buildLocation` above.

```csharp
public void SetSysParam(SysParamDefinition sysParam);
```
>Sets the system parameter to use with this run. This will define which hardware resources will be available in your cluster.

```csharp
public string SetAppParam<T>(string name, T param, int numInstances) where T : struct;
```
>Set an app parameter for a number of instances. This will upload the parameter and it will be added to the appParameters container keyed by the app param `id`.

```csharp
public T GetAppParam<T>(string name) where T : struct;
```
>Retrieve an app parameter that has been added by name. You can also enumerate the appParameters container if you need to find one by app param `id`.

```csharp
public void Execute();
```
>Execute your run definition.

## Examples

```csharp
[MenuItem("Simulation/Cloud/Build")]
public static void BuildProject()
{
    var scenes = new string[]
    {
        "Assets/Legacy/cluster.unity",
        "Assets/Legacy/test_scene.unity"
    };
    Project.BuildProject("./test_linux_build", "TestBuild", scenes);
}
```

```csharp
var run = Run.Create("test", "test run");
var sysParam = API.GetSysParams()[0];
run.SetSysParam(sysParam);
run.SetBuildLocation(zipPath);
run.SetAppParam("test", new TestAppParam(1), 1);
run.Execute();

while (!run.completed)
    ;

Debug.Log("Run completed.");
```

```csharp
[MenuItem("Simulation/Login")]
public static void Login()
{
    API.Login();
}

[MenuItem("Simulation/Build And Upload")]
public static void BuildAndUploadProject()
{
    var window = GetWindow<ClientDialog>(utility: true, title: "Build And Upload", focus: true);
    if (window != null)
    {
        window.minSize = new Vector2(kWindowWidth, kWindowHeight);
        window.maxSize = window.minSize;
        window.options = Option.Build | Option.Zip | Option.Upload | Option.HelpText | Option.Buttons;
        window.ShowUtility();
    }
}
```

## Parity With CLI

| CLI | C# |
| ----------- | ----------- |
| usim login auth | Auth.Login |
| usim refresh auth | Auth.Refresh |
| usim get projects | Project.GetProjects |
| usim describe project | N/A |
| usim activate project | Project.Activate |
| usim deactivate project | Project.Deactivate |
| usim get sys-params | API.GetSysParams |
| usim get app-params | N/A |
| usim upload app-param | API.UploadAppParam\<T\> |
| usim download app-param | API.DownloadAppParam\<T\> |
| usim get builds | N/A |
| usim zip build | Project.CompressBuild |
| usim upload build | API.UploadBuild |
| usim download build | API.DownloadBuild |
| usim get runs | N/A |
| usim describe run | N/A |
| usim define run | Run.Create |
| usim upload run | API.UploadRunDefinition |
| usim execute run | run.Execute |
| usim cancel run-execution | N/A |
| usim describe run-execution | Run.Describe |
| usim download manifest | Run.GetManifest |
| usim summarize run-execution | Run.Summarize |
| usim logs | N/A |

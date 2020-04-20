using System.Runtime.CompilerServices;

#if UNITY_EDITOR
[assembly: InternalsVisibleTo("Unity.SimViz.Editor.Tests")]
[assembly: InternalsVisibleTo("UnityEngine.SimViz.Content.Editor")]
#endif
[assembly: InternalsVisibleTo("Unity.SimViz.Content.Runtime.Tests")]
[assembly: InternalsVisibleTo("Untiy.SimViz.Tests.Scripts")]
[assembly: InternalsVisibleTo("Unity.SimViz.Runtime.Tests")]
[assembly: InternalsVisibleTo("Unity.SimViz.Scenario")]

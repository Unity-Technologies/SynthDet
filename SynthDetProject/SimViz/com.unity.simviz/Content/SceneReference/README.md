What is this?
---

A `SceneReference` wrapper class that uses [ISerializationCallbackReceiver](https://docs.unity3d.com/ScriptReference/ISerializationCallbackReceiver.html) and a custom `PropertyDrawer`to provide safe, user-friendly scene references in scripts.

![alt text][1]

Why is this needed?
---

Sooner or later Unity Developers will want to reference a Scene from script, so they can load it at runtime using the [SceneManager](https://docs.unity3d.com/ScriptReference/SceneManagement.SceneManager.html).

We can store a string of the scene's path (as the [SceneAsset documentation](https://docs.unity3d.com/ScriptReference/SceneAsset.html) suggests), but that is really not ideal for production: If the scene asset is ever moved or renamed, then our stored path is broken. 

Unity has already solved this problem for assets using [Object references][2]. Using [AssetDatabasse](https://docs.unity3d.com/ScriptReference/AssetDatabase.html) you can find the path for a given asset. The problem there unfortunately is that we don't have access to AssetDatabase at runtime, since it is part of the `UnityEditor` Assembly.

So to be able to reliably use a scene both in editor and at runtime we need two pieces of serialized information: A reference to the SceneAsset object, and a string path that can be passed into SceneManager at runtime.

We can create our own Wrapper class that uses [`ISerializationCallbackReceiver`](https://docs.unity3d.com/ScriptReference/ISerializationCallbackReceiver.html) to ensure that the stored path is always valid based on the specified SceneAsset Object.

This `SceneReference` class is an example implementation of this idea.

Key features
---

- Custom PropertyDrawer that displays the current Build Settings status, including [BuildIndex](https://docs.unity3d.com/ScriptReference/SceneManagement.Scene-buildIndex.html) and convenient buttons for managing it with destructive action confirmation dialogues.
- If (and only if) the serialized Object reference is invalid but path is still valid (for example if someone merged incorrectly) will recover object using path.
- Buttons collapse to smaller text if full text cannot be displayed.<br>![][3]
- Includes detailed tooltips and respects Version Control if build settings is not checked out (tested with [Perforce](https://docs.unity3d.com/Manual/perForceIntegration.html)).<br>![][4]
- It's a single drop-in script. You're welcome to split the Editor-only PropertyDrawer and helpers into their own Editor scripts if you'd like, just for convenience I've put it all in one self-contained file here.

----

For easy runtime verification I've also provided a testing Monobehaviour that lets you view and load scenes via buttons when in playmode:<br/>![][5]

  [1]: https://i.imgur.com/DSYi0kd.png
  [2]: https://unity3d.com/learn/tutorials/topics/best-practices/assets-objects-and-serialization
  [3]: https://i.imgur.com/BQLHrUt.png
  [4]: https://i.imgur.com/Mu4ISTp.png
  [5]: https://i.imgur.com/q2FQSES.png

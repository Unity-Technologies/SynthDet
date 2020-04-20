using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Unity.Simulation.Client
{
    /// <summary>
    /// This provied some pre-made functionality for an editor dialog to select and upload various things to the Unity Simularion service.
    /// </summary>
    public class ClientDialog : EditorWindow
    {
        // Public Members

        /// <summary>
        /// Enum of options that you can specify. They are mutually inclusive,
        /// so you can combine them as needed, however not all combinations will produce a useful dialog.
        /// </summary>
        [Flags]
        public enum Option
        {
            /// <summary>
            /// The Build option will present a list of either scenes added to build, or open scenes, if no scenes have been added to the build.
            /// </summary>
            Build = (1 << 0),

            /// <summary>
            /// The Zip option is currently unused, but in the future will be used to generate a build without zipping.
            /// </summary>
            Zip = (1 << 1),

            /// <summary>
            /// The Upload option is currently unused, but in the future will be used to generate a zipped build without uploading.
            /// </summary>
            Upload = (1 << 2),

            /// <summary>
            /// The Launch option is currently unused, but in the future will be used to launch a build once created.
            /// </summary>
            Launch = (1 << 3),

            /// <summary>
            /// The AppParam option is currently unused, but in the future will be used to add and edit AppParams in the dialog.
            /// </summary>
            AppParams = (1 << 4),

            /// <summary>
            /// The SysParam option will cause the dialog to present a list of Sys Param supported by Unity Simulation.
            /// </summary>
            SysParam = (1 << 5),

            /// <summary>
            /// The HelpText option displays a paragraph of help text at the top of the dialog.
            /// </summary>
            HelpText = (1 << 6),

            /// <summary>
            /// The Buttons option causes the dialog to show a button at the bottom, which will perform the build/zip/upload when pressed.
            /// </summary>
            Buttons = (1 << 7)
        }

        /// <summary>
        /// Options can be specified/retrieved via this property.
        /// </summary>
        public Option options { get; set; }

        // Non-Public Members

        bool HasOption(Option option)
        {
            return (options & option) == option;
        }

        void Awake()
        {
            EditorApplication.update += () =>
            {
                _buildAction?.Invoke();
            };
        }

        void _DrawSpace()
        {
            EditorGUILayout.Space();
            _totalHeight += GUILayoutUtility.GetLastRect().height;
        }

        void _DrawHelpText()
        {
            if (HasOption(Option.HelpText))
            {
                _DrawSpace();
                EditorGUILayout.LabelField(kMessageString, EditorStyles.wordWrappedLabel);
                _totalHeight += GUILayoutUtility.GetLastRect().height;
            }
        }

        void _DrawSysParams()
        {
            if (HasOption(Option.SysParam))
            {
                _DrawSpace();
                EditorGUILayout.LabelField(kSysParamsField, EditorStyles.boldLabel);
                _totalHeight += GUILayoutUtility.GetLastRect().height;
                var list = new List<string>(_sysParams.Length);
                foreach (var p in _sysParams)
                    if (p.allowed)
                        list.Add(p.description);
                _sysParamIndex = EditorGUILayout.Popup(_sysParamIndex, list.ToArray());
                _totalHeight += GUILayoutUtility.GetLastRect().height;
            }
        }

        void _DrawScenes()
        {
            _selectedScenesCount = 0;
            if (HasOption(Option.Build))
            {
                var scenes = (_buildSettingsScenes == null || _buildSettingsScenes.Length == 0) ? _openScenes : _buildSettingsScenes;
                {
                    EditorGUILayout.Space();
                    _totalHeight += GUILayoutUtility.GetLastRect().height;
                    EditorGUILayout.LabelField(kScenesInBuild, EditorStyles.boldLabel);
                    _totalHeight += GUILayoutUtility.GetLastRect().height;
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Width(kScrollViewWidth), GUILayout.Height(kScrollViewHeight));

                    if (scenes != null && scenes.Length > 0)
                    {
                        Array.Sort(scenes);
                        foreach (var sceneName in scenes)
                        {
                            var selected = false;
                            _selectedScenes.TryGetValue(sceneName, out selected);
                            selected = EditorGUILayout.ToggleLeft(sceneName, selected);
                            _selectedScenes[sceneName] = selected;
                            _selectedScenesCount += selected ? 1 : 0;
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField(kNoScenesText, EditorStyles.wordWrappedLabel);
                        _totalHeight += GUILayoutUtility.GetLastRect().height;
                    }

                    EditorGUILayout.EndScrollView();
                    EditorGUILayout.EndVertical();
                    _totalHeight += GUILayoutUtility.GetLastRect().height;
                }

                EditorGUILayout.Space();
                _totalHeight += GUILayoutUtility.GetLastRect().height;
                EditorGUIUtility.labelWidth = kFieldTextWidth;
                var location = Path.Combine("Assets", "..", "Build", string.IsNullOrEmpty(_buildName) ? "<BuildName>" : _buildName);
                EditorGUILayout.LabelField(kLocationText, location);
                _totalHeight += GUILayoutUtility.GetLastRect().height;
                GUI.SetNextControlName("_buildName");
                _buildName = EditorGUILayout.TextField(kFieldText, _buildName);
                _totalHeight += GUILayoutUtility.GetLastRect().height;
                EditorGUILayout.Space();
                _totalHeight += GUILayoutUtility.GetLastRect().height;
            }
        }

        void _DrawButtons()
        {
            if (HasOption(Option.Buttons))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_buildName) || _selectedScenesCount == 0);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(kButtonText, GUILayout.ExpandWidth(false)))
                {
                    var includedScenes = new List<string>(_selectedScenesCount);
                    foreach (var kv in _selectedScenes)
                        if (kv.Value == true)
                            includedScenes.Add(kv.Key);

                    _buildAction = () =>
                    {
                        _buildAction = null;
                        var buildLocation = Path.Combine(Application.dataPath, "..", "Build", _buildName);
                        Directory.CreateDirectory(buildLocation);
                        Project.BuildProject(buildLocation, _buildName, includedScenes.ToArray(), BuildTarget.StandaloneLinux64, compress: HasOption(Option.Zip), launch: false);
                        var id = API.UploadBuild(_buildName, $"{buildLocation}.zip");
                        Debug.Log($"Build {_buildName} uploaded with build id {id}");
                    };
                    Close();
                }
                _totalHeight += GUILayoutUtility.GetLastRect().height;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
                _totalHeight += GUILayoutUtility.GetLastRect().height;
                EditorGUILayout.Space();
                _totalHeight += GUILayoutUtility.GetLastRect().height;
                EditorGUI.FocusTextInControl("_buildName");
            }
        }

        protected virtual void OnGUI()
        {
            if (HasOption(Option.Build))
            {
                _buildSettingsScenes = Project.GetBuildSettingScenes();
                _openScenes          = Project.GetOpenScenes();
            }

            if (HasOption(Option.SysParam))
                _sysParams = API.GetSysParams();

            _totalHeight = 0;

            _DrawHelpText();
            _DrawSysParams();
            _DrawScenes();
            _DrawButtons();

            if (_totalHeight > 100 && _totalHeight < 500)
            {
                minSize = new Vector2(minSize.x, _totalHeight);
                maxSize = new Vector2(maxSize.x, _totalHeight);
            }
        }

        Action                     _buildAction;
        string                     _buildName;
        string[]                   _buildSettingsScenes;
        string[]                   _openScenes;
        SysParamDefinition[]       _sysParams;
        int                        _sysParamIndex;
        Vector2                    _scrollPosition;
        Dictionary<string, bool>   _selectedScenes = new Dictionary<string, bool>();
        int                        _selectedScenesCount;
        float                      _totalHeight;

        const int      kWindowWidth       = 500;
        const int      kWindowHeight      = 245;
        const int      kScrollViewWidth   = 490;
        const int      kScrollViewHeight  = 60;
        const string   kTitleString       = "Build Settings";
        const string   kSysParamsField    = "System Params";
        const string   kAppParamsField    = "App Params";
        const string   kMessageString     =
        "Create and upload a linux build for simulation. Note: we will include the scenes from your most recent build. " +
        "If you haven't done a build yet, we will include the open scenes in your project. " +
        "Specify a build name, and select the scenes you want to include from the list.";
        const string   kNoScenesText      = "No Scenes Loaded. Either load one or more scenes, or select scenes to include in the Build Settings dialog.";
        const string   kAddOpenScenesText = "Add Open Scenes";
        const string   kScenesInBuild     = "Scenes In Build";
        const string   kFieldText         = "Build Name";
        const string   kLocationText      = "Build Location";
        const int      kFieldTextWidth    = 90;
        const string   kButtonText        = "Build And Upload";
    }
}

#endif // UNITY_EDITOR

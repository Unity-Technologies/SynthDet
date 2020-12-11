using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    internal class PostprocessedILWindow : EditorWindow
    {
        private enum DisplayLanguage
        {
            CSharp,
            IL
        }

        private static readonly Dictionary<Type, string[]> GeneratedTypesAndDecompiledCSharpCode = new Dictionary<Type, string[]>();
        private static readonly Dictionary<Type, string[]> GeneratedTypesAndDecompiledIlCode = new Dictionary<Type, string[]>();
        private static readonly Dictionary<Type, TypeDefinition> TypesToTypeDefinitions = new Dictionary<Type, TypeDefinition>();
        
        private static readonly Type[] AllDOTSCompilerGeneratedTypes;
        private static readonly List<Type> FilteredTypes;

        private static DisplayLanguage s_currentDisplayLanguage = DisplayLanguage.CSharp;
        private static Type s_currentlySelectedType;
        private static string[] s_currentlyDisplayedDecompiledCode;
        
        private static Process s_decompilationProcess;
        private static ListView s_decompiledCodeField;
        private static Label s_decompilationStatusLabel;
        
        private static bool s_userMadeAtLeastOneSelection;
        
        private static DecompilationStatus s_currentDecompilationStatus;
        private static int s_decompilationDurationSoFar;

        private const int EstimatedNumFramesNeededForDecompilationToFinish = 15;

        private enum DecompilationStatus
        {
            InProgress,
            Complete
        }

        [MenuItem("DOTS/DOTS Compiler/Open Inspector...")]
        private static void ShowWindow()
        {
            var window = GetWindow<PostprocessedILWindow>();
            window.titleContent = new GUIContent("Postprocessed IL code");
        }
        
        static PostprocessedILWindow()
        {
            AllDOTSCompilerGeneratedTypes =
                TypeCache.GetTypesWithAttribute<DOTSCompilerGeneratedAttribute>()
                         .OrderBy(t => t.GetUserFriendlyName())
                         .ToArray();
            FilteredTypes = new List<Type>(AllDOTSCompilerGeneratedTypes);
        }

        public void OnEnable()
        {
            this.minSize = new Vector2(1500f, 400f);

            ImportVisualTree();
            ImportStyleSheet();
            CacheElements();
            RegisterCallbacks();
        }

        private void ImportVisualTree()
        {
            VisualTreeAsset visualTree =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.unity.entities/Unity.Entities.Editor/PostprocessedILInspector/ILPostProcessorWindow.uxml");
            visualTree.CloneTree(rootVisualElement);
        }

        private void ImportStyleSheet()
        {
            StyleSheet styleSheet = 
                AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.entities/Unity.Entities.Editor/PostprocessedILInspector/ILPostProcessorWindow.uss");
            rootVisualElement.styleSheets.Add(styleSheet);
        }

        private void RegisterCallbacks()
        {
            Button copyCodeButton = rootVisualElement.Q<Button>("Copy Code");
            copyCodeButton.clicked += () =>
            {
                EditorGUIUtility.systemCopyBuffer =
                    string.Join(Environment.NewLine, (string[]) s_decompiledCodeField.itemsSource);
            };

            var generatedTypesListView = rootVisualElement.Q<ListView>("Scripts ListView");
            SetupListView(generatedTypesListView, FilteredTypes, 15, MakeScriptLabel, BindScriptLabel);
            generatedTypesListView.selectionType = SelectionType.Single;
#if UNITY_2020_1_OR_NEWER
            generatedTypesListView.onSelectionChange += OnScriptSelected;
#else
            generatedTypesListView.onSelectionChanged += OnScriptSelected;
#endif

            var searchField = rootVisualElement.Q<ToolbarSearchField>("Script Search Field");
            searchField.RegisterCallback<ChangeEvent<string>, ListView>(OnFilter, generatedTypesListView);
    
            SetupListView(s_decompiledCodeField, s_currentlyDisplayedDecompiledCode, 15, MakeScriptLineLabel, BindScriptLineLabel);
            
            var language = rootVisualElement.Q<EnumField>("Language Popup");
            language.Init(defaultValue: DisplayLanguage.CSharp);
            language.RegisterValueChangedCallback(changeEvent =>
            {
                s_currentDisplayLanguage = (DisplayLanguage)changeEvent.newValue;
                StartDecompilationOrDisplayDecompiledCode();
            });
            
            var fontSizes = new PopupField<int>(
                choices: Enumerable.Range(start: 12, count: 7).ToList(),
                defaultIndex: 0,
                formatSelectedValueCallback: GetFontSizeHeader,
                formatListItemCallback: GetFontSize
            );
            fontSizes.RegisterValueChangedCallback(changeEvent =>
            {
                s_decompiledCodeField.style.fontSize = changeEvent.newValue;
                s_decompiledCodeField.itemHeight = Mathf.CeilToInt(changeEvent.newValue * 1.5f);
            });
            
            VisualElement fontSizeRoot = rootVisualElement.Q("Fontsize Popup");
            fontSizeRoot.Add(fontSizes);

            s_decompilationStatusLabel.binding = new DecompilationStatusLabelBinding();
        }
        
        private static string GetFontSizeHeader(int size)
        {
            return "Font size: " + size;
        }

        private static string GetFontSize(int size)
        {
            return size.ToString(CultureInfo.InvariantCulture);
        }

        private static void OnScriptSelected(IEnumerable<object> o)
        {
            s_currentlySelectedType = (Type)o.Single();
            s_userMadeAtLeastOneSelection = true;
            StartDecompilationOrDisplayDecompiledCode();
        }

        private static void OnFilter(ChangeEvent<string> changeEvent, ListView listView)
        {
            FilteredTypes.Clear();

            if (string.IsNullOrEmpty(changeEvent.newValue))
            {
                FilteredTypes.AddRange(AllDOTSCompilerGeneratedTypes);    
            }
            else
            {
                FilteredTypes.AddRange(
                    AllDOTSCompilerGeneratedTypes.Where(t => 
                        CultureInfo.InvariantCulture.CompareInfo.IndexOf(
                            t.GetUserFriendlyName(), changeEvent.newValue, CompareOptions.IgnoreCase) >= 0));
            }
            
            listView.Refresh();
        }

        private static void SetupListView(ListView listView, IList source, int itemHeight, Func<VisualElement> makeItem, Action<VisualElement, int> bindItem)
        {
            listView.itemsSource = source;
            listView.itemHeight = itemHeight;
            listView.makeItem = makeItem;
            listView.bindItem = bindItem;
        }

        private void CacheElements()
        {
             s_decompiledCodeField = rootVisualElement.Q<ListView>("Decompiled Script ListView");
             s_decompilationStatusLabel = rootVisualElement.Q<Label>(className:"status-label");
        }

        private static VisualElement MakeScriptLabel()
        {
            var label = new Label();
            label.AddToClassList("script-label");
            
            return label;
        }

        private static void BindScriptLabel(VisualElement element, int index)
        {
            if (element is Label label)
            {
                label.text = FilteredTypes[index].GetUserFriendlyName();
            }
        }

        private static VisualElement MakeScriptLineLabel()
        {
            var label = new Label();
            label.AddToClassList("script-line-label");
            
            return label;
        }
        
        private static void BindScriptLineLabel(VisualElement element, int index)
        {
            if (element is Label label)
            {
                label.text = $"{index}\t{s_currentlyDisplayedDecompiledCode[index]}";
            }
        }

        private static void StartDecompilationOrDisplayDecompiledCode()
        {
            TypeDefinition typeDefinition = GetTypeDefinition(s_currentlySelectedType);
            switch (s_currentDisplayLanguage)
            {
                case DisplayLanguage.CSharp:
                {
                    if (GeneratedTypesAndDecompiledCSharpCode.ContainsKey(s_currentlySelectedType))
                    {
                        DisplayDecompiledCode(DisplayLanguage.CSharp);
                        break;
                    }
                    s_decompilationProcess = 
                        Decompiler.StartDecompilationProcesses(typeDefinition, DecompiledLanguage.CSharpOnly)
                            .DecompileIntoCSharpProcess;
                    s_currentDecompilationStatus = DecompilationStatus.InProgress;
                    break;
                }
                case DisplayLanguage.IL:
                {
                    if (GeneratedTypesAndDecompiledIlCode.ContainsKey(s_currentlySelectedType))
                    {
                        DisplayDecompiledCode(DisplayLanguage.IL);
                        break;
                    }
                    s_decompilationProcess = 
                        Decompiler.StartDecompilationProcesses(typeDefinition, DecompiledLanguage.ILOnly)
                            .DecompileIntoILProcess;
                    s_currentDecompilationStatus = DecompilationStatus.InProgress;
                    break;
                }
            }
        }

        private static void DisplayDecompiledCode(DisplayLanguage displayLanguage)
        {
            s_currentlyDisplayedDecompiledCode =
                displayLanguage == DisplayLanguage.CSharp
                    ? GeneratedTypesAndDecompiledCSharpCode[s_currentlySelectedType]
                    : GeneratedTypesAndDecompiledIlCode[s_currentlySelectedType];
            
            s_decompiledCodeField.itemsSource = s_currentlyDisplayedDecompiledCode;
            s_currentDecompilationStatus = DecompilationStatus.Complete;
        }

        private static TypeDefinition GetTypeDefinition(Type type)
        {
            if (TypesToTypeDefinitions.ContainsKey(type))
            {
                return TypesToTypeDefinitions[type];
            }
            
            ModuleDefinition moduleDefinition = CreateAssemblyDefinitionFor(type).MainModule;

            string fullName = type.FullName?.Replace(oldValue: "+", "/");
            TypeDefinition typeDefinition =
                moduleDefinition.GetType(fullName) ?? moduleDefinition.GetType(GetCorrectedTypeName(fullName));
            
            TypesToTypeDefinitions.Add(type, typeDefinition);
            
            return typeDefinition;
        }
        
        /*
         * Previously, selecting the Samples.Boids.BoidSchoolSpawnSystem/<>c__DisplayClass_OnUpdate_LambdaJob0 type for decompilation
         * causes a NullReferenceException to be thrown, because its corresponding TypeDefinition could not be found inside the module
         * that supposedly contains it. It turns out that the module stores the type as:
         * 
         *     Samples.Boids.BoidSchoolSpawnSystem/Samples.Boids.<>c__DisplayClass_OnUpdate_LambdaJob0 (notice the Samples.Boids. infix)
         *
         * instead of:
         *
         *     Samples.Boids.BoidSchoolSpawnSystem/<>c__DisplayClass_OnUpdate_LambdaJob0.
         *
         * Several other types throw the same exception. This method is intended correct the type name so that it can be found inside the
         * module that contains it.
         * 
         */
        static string GetCorrectedTypeName(string name)
        {
            // An example that requires correction is: Samples.Boids.BoidSchoolSpawnSystem/<>c__DisplayClass_OnUpdate_LambdaJob0
            string[] splitName = name.Split('/');
            (string parentTypeName, string nestedTypeName) = (splitName[0], splitName[1]);
            
            // Retrieve the namespace ("Samples.Boids.") from the parentTypeName ("Samples.Boids.BoidSchoolSpawnSystem")
            string nameSpace = string.Concat(parentTypeName.Reverse().SkipWhile(c => c != '.').Reverse());
            
            // Correct it to: Samples.Boids.BoidSchoolSpawnSystem/Samples.Boids.<>c__DisplayClass_OnUpdate_LambdaJob0 
            return $"{parentTypeName}/{nameSpace}{nestedTypeName}";
        }
        
        private static AssemblyDefinition CreateAssemblyDefinitionFor(Type type)
        {
            var assemblyLocation = type.Assembly.Location;

            var assemblyDefinition =
                AssemblyDefinition.ReadAssembly(
                    new MemoryStream(
                        buffer: File.ReadAllBytes(assemblyLocation)),
                        new ReaderParameters(ReadingMode.Immediate)
                        {
                            ReadSymbols = true,
                            ThrowIfSymbolsAreNotMatching = true,
                            SymbolReaderProvider = new PortablePdbReaderProvider(),
                            AssemblyResolver = new OnDemandResolver(),
                            SymbolStream = CreatePdbStreamFor(assemblyLocation)
                        }
                );

            if (!assemblyDefinition.MainModule.HasSymbols)
            {
                throw new Exception("NoSymbols");
            }
            return assemblyDefinition;
        }

        private static MemoryStream CreatePdbStreamFor(string assemblyLocation)
        {
            string pdbFilePath = Path.ChangeExtension(assemblyLocation, ".pdb");
            return !File.Exists(pdbFilePath) ? null : new MemoryStream(File.ReadAllBytes(pdbFilePath));
        }
        
        private class OnDemandResolver : IAssemblyResolver
        {
            public void Dispose()
            {
            }

            public AssemblyDefinition Resolve(AssemblyNameReference name)
            {
                return Resolve(name, new ReaderParameters(ReadingMode.Deferred));
            }

            public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                string assemblyLocation = 
                    AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == name.Name).Location;
                
                parameters.AssemblyResolver = this;
                parameters.SymbolStream = CreatePdbStreamFor(assemblyLocation);

                return AssemblyDefinition.ReadAssembly(
                    new MemoryStream(File.ReadAllBytes(assemblyLocation)), parameters);
            }
        }

        private class DecompilationStatusLabelBinding : IBinding
        {
             public void PreUpdate()
             {
             }
     
             public void Update()
             {
                if (!s_userMadeAtLeastOneSelection)
                {
                    return;
                }
            
                switch (s_currentDecompilationStatus)
                {
                    case DecompilationStatus.Complete:
                    {
                        s_decompilationStatusLabel.text =
                            $"Currently displaying decompiled {(s_currentDisplayLanguage == DisplayLanguage.CSharp ? "C#" : "IL")} code.";
                        return;
                    }
                    case DecompilationStatus.InProgress when s_decompilationDurationSoFar < EstimatedNumFramesNeededForDecompilationToFinish:
                    {
                        s_decompilationStatusLabel.text = "Decompilation in progress. Please be patient...";
                        s_decompilationDurationSoFar++;
                        return;
                    }
                    default:
                    {
                        switch (s_currentDisplayLanguage)
                        {
                            case DisplayLanguage.CSharp:
                            {
                                GeneratedTypesAndDecompiledCSharpCode.Add(
                                    s_currentlySelectedType,
                                    s_decompilationProcess.StandardOutput
                                                         .ReadToEnd()
                                                         .Split(new[] {Environment.NewLine}, StringSplitOptions.None));
                        
                                DisplayDecompiledCode(DisplayLanguage.CSharp);
                                break;
                            }

                            case DisplayLanguage.IL:
                            {
                                GeneratedTypesAndDecompiledIlCode.Add(
                                    s_currentlySelectedType,
                                    s_decompilationProcess.StandardOutput
                                                         .ReadToEnd()
                                                         .Split(new[] {Environment.NewLine}, StringSplitOptions.None));
                        
                                DisplayDecompiledCode(DisplayLanguage.IL);
                                break;
                            }
                        }

                        s_decompilationDurationSoFar = 0;
                        break;
                    }
                }
             }
             
             public void Release()
             {
             }
        }
    }
}
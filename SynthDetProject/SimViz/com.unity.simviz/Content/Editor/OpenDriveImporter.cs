using System;
using System.Collections.Generic;
using UnityEditor.Experimental.AssetImporters;
using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using UnityEngine.SimViz.Content.MapElements;

namespace UnityEngine.SimViz.Content
{
    [ScriptedImporter(3, "xodr")]
    public class OpenDriveImporter : ScriptedImporter
    {
        public bool validateXmlSchema;
        
        private const string _openDriveSchema = "OpenDRIVE_1.5M.xsd";
        internal int _numValidationErrors;
        internal HashSet<string> _errMsgs;
        
        private static string FindOpenDriveSchema()
        {
            var rootDir = Directory.GetCurrentDirectory();
            var packagePath = Path.Combine(new string[] {rootDir, "Packages", "com.unity.simviz", "Content"});
            string contentDir;

            if (Directory.Exists(packagePath))
            {
                contentDir = packagePath;
            }
            else if (rootDir.EndsWith("UnityScenarios"))
            {
                // We're in the simviz-av-template project, navigate to simviz in the package directory...
                contentDir = Path.Combine(new string[] {rootDir, "Packages", "com.unity.simviz", "Content"});
            }
            else if (rootDir.EndsWith("simviz"))
            {
                // We're in the root of the SimViz package...
                contentDir = Path.Combine(new string[] {rootDir, "Content"});
            }
            else
            {
                throw new DirectoryNotFoundException("Couldn't identify the cwd: " + rootDir);
            }

            return Path.Combine(new string[] {contentDir, _openDriveSchema});
        }


        private static XmlSchemaSet LoadOpenDriveSchema()
        {
            var schemaPath = FindOpenDriveSchema();
            Debug.Log("Loading OpenDrive schema from " + schemaPath);
            var schemaReader = new StreamReader(schemaPath);
            var schema = new XmlSchemaSet();
            schema.Add("", XmlReader.Create(schemaReader));
            return schema;
        }

        [STAThread]
        public override void OnImportAsset(AssetImportContext context)
        {
            Debug.Log($"Importing {context.assetPath}...");
            var timeImportStart = Time.realtimeSinceStartup;
            XmlReader openDriveReader;
            if (validateXmlSchema)
            {
                var openDriveSchema = LoadOpenDriveSchema();
                var readerSettings = new XmlReaderSettings
                {
                    ValidationType = ValidationType.Schema,
                    Schemas = openDriveSchema,
                };
                readerSettings.ValidationEventHandler += ValidationCallback;

                openDriveReader = XmlReader.Create(context.assetPath, readerSettings);

                Debug.Log("Attempting to parse and validate " + context.assetPath);
                _numValidationErrors = 0;
                _errMsgs = new HashSet<string>();
            }
            else
            {
                openDriveReader = XmlReader.Create(context.assetPath);    
            }

            var openDriveDoc = XDocument.Load(openDriveReader);
            
            // If the message is empty, no errors were recorded
            if (_numValidationErrors == 0)
            {
                Debug.Log("Successfully parsed and validated file!");
            }
            else
            {
                var consoleMsg = $"Found {_numValidationErrors} total errors when validating {context.assetPath}: " +
                    Environment.NewLine + string.Join(Environment.NewLine, _errMsgs);
                                                 
                Debug.LogWarning(consoleMsg);
            }
            
            var timeValidateFinish = Time.realtimeSinceStartup;
            Debug.Log($"XML validation finished - took {timeValidateFinish - timeImportStart} seconds.");

            var factory = new OpenDriveMapElementFactory();
            if (!factory.TryCreateRoadNetworkDescription(openDriveDoc, out var roadNetwork))
            {
                throw new UnityException("Failed to create a valid road network for " + openDriveReader);
            }

            context.AddObjectToAsset("Road Network Description", roadNetwork);
            context.SetMainObject(roadNetwork);
            DontDestroyOnLoad(roadNetwork);

            var timeConstructFinish = Time.realtimeSinceStartup;
            Debug.Log($"Construction finished - took {timeConstructFinish - timeValidateFinish} seconds");
            Debug.Log($"Total time to import {context.assetPath}: {timeValidateFinish - timeImportStart} seconds.");
        }
        
        public void ValidationCallback(object sender, ValidationEventArgs eventArgs)
        {
            lock (_errMsgs)
                _errMsgs.Add(eventArgs.Message);
            Interlocked.Increment(ref _numValidationErrors);
        }
    }
}

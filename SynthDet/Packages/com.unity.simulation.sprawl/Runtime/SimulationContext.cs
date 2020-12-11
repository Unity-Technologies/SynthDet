using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Sprawl {

    public class SimulationContext : ISimulationContext {

        private static readonly string kSprawlConfigFlagPrefix = "--sprawl.config=";
        private static readonly string kSprawlConfigDefault = "config.json";

        private static  SimulationContext instance_;

        public static SimulationContext GetInstance()
        {
            if(instance_ == null)
                instance_ = new SimulationContext();
            
            return instance_;
        }

        private Pipeline pipeline_ = null;
        private ISimulationContext pipeline_entry_context_ = null;
        private UnityEntrypoint entrypoint_ = null;

        private static string GetConfigFileFromCommandline() {
            string[] args = System.Environment.GetCommandLineArgs();
            foreach (string arg in args) {
                if (arg.StartsWith(kSprawlConfigFlagPrefix, StringComparison.Ordinal)) {
                    string[] parts = arg.Split(new char[] { '=' });
                    if (parts.Length == 2) {
                        return parts[1];
                    }
                }
            }
            return kSprawlConfigDefault;
        }

        public bool Initialize(string config_path = null) {
            ThreadPool.SetMinThreads(4, 4);
            ThreadPool.SetMaxThreads(8, 8);

            if (pipeline_ != null) {
                Debug.LogError("SimulationContext already initialized.");
                return false;
            }

            if (config_path == null) {
                config_path = GetConfigFileFromCommandline();
            }

            if (config_path == null) {
                Debug.LogError("Cannot resolve config file.");
                return false;
            }

            pipeline_ = Pipeline.Load(config_path);

            if (pipeline_ == null) {
                Debug.LogError(string.Format("Cannot load pipeline: {0}", config_path));
                return false;
            }

            if (!pipeline_.Initialize()) {
                Debug.LogError(string.Format("Cannot Initialize pipeline: {0}", config_path));
                return false;
            }

            Pipeline.Node entrypoint_node = pipeline_.EntryNode;

            entrypoint_node.AddInput(new PipelineServerInput(pipeline_.Port, pipeline_.Marshaller, pipeline_.MaxPendingRequests), "request");

            entrypoint_ = entrypoint_node.NodeImpl as UnityEntrypoint;

            if (entrypoint_ == null) {
                Debug.LogError("Cannot find UnityEntrypoint in pipeline.");
                return false;
            }

            if (!entrypoint_node.Initialize(pipeline_.Context)) {
                Debug.LogError("Cannot initialize pipeline entrypoint.");
                return false;
            }

            pipeline_entry_context_ = entrypoint_.SimulationContext;

            return true;
        }

        public Pipeline GetPipeline() {
            return pipeline_;
        }

        public JObject GetConfig() {
            return entrypoint_.Config;
        }

        public int InputsCount() {
            return pipeline_entry_context_.InputsCount();
        }

        public bool HasInput(string name) {
            return pipeline_entry_context_.HasInput(name);
        }

        public IInput GetInput(int index) {
            return pipeline_entry_context_.GetInput(index);
        }

        public IInput GetInput(string name) {
            return pipeline_entry_context_.GetInput(name);
        }

        public IList<KeyValuePair<string, IInput>> GetInputs() {
            return pipeline_entry_context_.GetInputs();
        }

        public int OutputsCount() {
            return pipeline_entry_context_.OutputsCount();
        }

        public bool HasOutput(string name) {
            return pipeline_entry_context_.HasOutput(name);
        }

        public IOutput GetOutput(int index) {
            return pipeline_entry_context_.GetOutput(index);
        }

        public IOutput GetOutput(string name) {
            return pipeline_entry_context_.GetOutput(name);
        }

        public IList<KeyValuePair<string, IOutput>> GetOutputs() {
            return pipeline_entry_context_.GetOutputs();
        }

        public int SharedEntitiesCount() {
            throw new System.NotImplementedException();
        }

        public bool HasSharedEntity(string name) {
            throw new System.NotImplementedException();
        }

        public ISharedEntity GetSharedEntity(int index) {
            throw new System.NotImplementedException();
        }

        public ISharedEntity GetSharedEntity(string name) {
            throw new System.NotImplementedException();
        }

        public IList<KeyValuePair<string, ISharedEntity>> GetSharedEntities() {
            throw new System.NotImplementedException();
        }

        public int CommunicationChannelsCount() {
            throw new System.NotImplementedException();
        }

        public bool HasCommunicationChannel(string name) {
            throw new System.NotImplementedException();
        }

        public ICommunicationChannel GetCommunicationChannel(int index) {
            throw new System.NotImplementedException();
        }

        public ICommunicationChannel GetCommunicationChannel(string name) {
            throw new System.NotImplementedException();
        }

        public IList<KeyValuePair<string, ICommunicationChannel>> GetCommunicationChannels() {
            throw new System.NotImplementedException();
        }

        public void LogDebug(string format, params object[] objects) {
            pipeline_entry_context_.LogDebug(format, objects);
        }

        public void LogInfo(string format, params object[] objects) {
            pipeline_entry_context_.LogInfo(format, objects);
        }

        public void LogWarning(string format, params object[] objects) {
            pipeline_entry_context_.LogWarning(format, objects);
        }

        public void LogError(string format, params object[] objects) {
            pipeline_entry_context_.LogError(format, objects);
        }

    }

}

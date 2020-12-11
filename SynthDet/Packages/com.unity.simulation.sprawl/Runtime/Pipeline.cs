using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Sprawl {

    public class Pipeline {

        public enum PipelineType {
            PYTHON,
            UNITY
        };

        public enum PipelineMode {
            LOOP,
            REQUEST
        };

        public static PipelineType ParsePipelineType(string type) {
            if ("PipelineType.PYTHON".Equals(type)) {
                return PipelineType.PYTHON;
            } else if ("PipelineType.UNITY".Equals(type)) {
                return PipelineType.UNITY;
            } else {
                Debug.LogError(string.Format("Unknown pipeline type: {0}.", type));
                return PipelineType.UNITY;
            }
        }

        public static PipelineMode ParsePipelineMode(string mode) {
            if ("PipelineMode.LOOP".Equals(mode)) {
                return PipelineMode.LOOP;
            } else if ("PipelineMode.REQUEST".Equals(mode)) {
                return PipelineMode.REQUEST;
            } else {
                Debug.LogError(string.Format("Unknown pipeline mode: {0}.", mode));
                return PipelineMode.LOOP;
            }
        }

        public class PipelineContext {
            public string Name { get; set; } = "";
            public string ContainerName { get; set; } = "";
            public MarshallerType Marshaller { get; set; } = MarshallerType.PROTOBUF;

            public PipelineContext(string name, string container_name, MarshallerType marshaller) {
                Name = name;
                ContainerName = container_name;
                Marshaller = marshaller;
            }

            private void LogLevel(string log_level, string format, params object[] objects) {
                if (objects.Length > 0) {
                    string message = string.Format(format, objects);
                    string date = DateTime.Now.ToString("yyyyMMddHHmmss.fff");
                    Console.WriteLine(string.Format("{0}:{1}:{2}", log_level, date, message));
                } else {
                    string date = DateTime.Now.ToString("yyyyMMddHHmmss.fff");
                    Console.WriteLine(string.Format("{0}:{1}:{2}", log_level, date, format));
                }
            }

            public void LogDebug(string format, params object[] objects) {
                LogLevel("DBG", format, objects);
            }

            public void LogInfo(string format, params object[] objects) {
                LogLevel("INF", format, objects);
            }

            public void LogWarning(string format, params object[] objects) {
                LogLevel("WRN", format, objects);
            }

            public void LogError(string format, params object[] objects) {
                LogLevel("ERR", format, objects);
            }
        }

        public class NodeExecutionContext : ISimulationContext {

            private class OutputWrapper : IOutput {

                private Node node_ = null;

                public OutputWrapper(Node node) {
                    node_ = node;
                }

                public void Push(Message message) {
                    node_.Execute(message);
                }

                public Message PushAcknowledged(Message message) {
                    return node_.Execute(message);
                }

                public bool PushAcknowledged(Message message, float timeout, out Message response) {
                    throw new NotImplementedException();
                }
            }

            private class InputWrapper : IInput {

                private INodeInput node_input_ = null;
                private NodeExecutionContext context_ = null;

                public InputWrapper(INodeInput node_input, NodeExecutionContext context) {
                    node_input_ = node_input;
                    context_ = context;
                }

                public void Acknowledge(Message response) {
                    node_input_.Acknowledge(context_, response);
                }

                public Message Get() {
                    return node_input_.Get(context_);
                }

                public Message Get(float timeout) {
                    return node_input_.Get(context_, timeout);
                }

                public Message TryGet() {
                    return node_input_.TryGet(context_);
                }
            }

            public PipelineContext PipelineContext { get; set; } = null;
            public int Level { get; set; } = 0;
            public Message Payload { get; set; } = null;

            public List<KeyValuePair<string, IOutput>> OutputWrappers { get; } = new List<KeyValuePair<string, IOutput>>();
            public Dictionary<string, IOutput> OutputWrappersDict { get; } = new Dictionary<string, IOutput>();

            public List<KeyValuePair<string, IInput>> InputWrappers { get; } = new List<KeyValuePair<string, IInput>>();
            public Dictionary<string, IInput> InputWrapperDict { get; } = new Dictionary<string, IInput>();

            private Node node_ = null;
            private string indent_ = "";

            public NodeExecutionContext(Node node, PipelineContext pipeline_context, int level) {
                node_ = node;
                PipelineContext = pipeline_context;
                Level = level;

                for (int i = 0; i < Level; ++i) {
                    indent_ += "  ";
                }

                foreach (KeyValuePair<string, Node> output_node in node.Outputs) {
                    OutputWrappers.Add(new KeyValuePair<string, IOutput>(output_node.Key, new OutputWrapper(output_node.Value)));
                }

                foreach (KeyValuePair<string, IOutput> output_node in OutputWrappers) {
                    OutputWrappersDict.Add(output_node.Key, output_node.Value);
                }

                foreach (KeyValuePair<string, INodeInput> node_input in node.Inputs) {
                    InputWrappers.Add(new KeyValuePair<string, IInput>(node_input.Key, new InputWrapper(node_input.Value, this)));
                }

                foreach (KeyValuePair<string, IInput> input in InputWrappers) {
                    InputWrapperDict.Add(input.Key, input.Value);
                }
            }

            public JObject GetConfig() {
                return null;
            }

            public int InputsCount() {
                return InputWrappers.Count;
            }

            public bool HasInput(string name) {
                return InputWrapperDict.ContainsKey(name);
            }

            public IInput GetInput(int index) {
                return InputWrappers[index].Value;
            }

            public IInput GetInput(string name) {
                return InputWrapperDict[name];
            }

            public IList<KeyValuePair<string, IInput>> GetInputs() {
                return InputWrappers;
            }

            public int OutputsCount() {
                return OutputWrappers.Count;
            }

            public bool HasOutput(string name) {
                return OutputWrappersDict.ContainsKey(name);
            }

            public IOutput GetOutput(int index) {
                return OutputWrappers[index].Value;
            }

            public IOutput GetOutput(string name) {
                return OutputWrappersDict[name];
            }

            public IList<KeyValuePair<string, IOutput>> GetOutputs() {
                return OutputWrappers;
            }

            public int SharedEntitiesCount() {
                throw new NotImplementedException();
            }

            public bool HasSharedEntity(string name) {
                throw new NotImplementedException();
            }

            public ISharedEntity GetSharedEntity(int index) {
                throw new NotImplementedException();
            }

            public ISharedEntity GetSharedEntity(string name) {
                throw new NotImplementedException();
            }

            public IList<KeyValuePair<string, ISharedEntity>> GetSharedEntities() {
                throw new NotImplementedException();
            }

            public int CommunicationChannelsCount() {
                throw new NotImplementedException();
            }

            public bool HasCommunicationChannel(string name) {
                throw new NotImplementedException();
            }

            public ICommunicationChannel GetCommunicationChannel(int index) {
                throw new NotImplementedException();
            }

            public ICommunicationChannel GetCommunicationChannel(string name) {
                throw new NotImplementedException();
            }

            public IList<KeyValuePair<string, ICommunicationChannel>> GetCommunicationChannels() {
                throw new NotImplementedException();
            }

            private string FormatIndented(string format, params object[] objects) {
                if (objects.Length > 0) {
                    string message = string.Format(format, objects);
                    return string.Format("{0}{1}", indent_, message);
                } else {
                    return string.Format("{0}{1}", indent_, format);
                }
            }

            public void LogDebug(string format, params object[] objects) {
                PipelineContext.LogDebug(FormatIndented(format, objects));
            }

            public void LogInfo(string format, params object[] objects) {
                PipelineContext.LogInfo(FormatIndented(format, objects));
            }

            public void LogWarning(string format, params object[] objects) {
                PipelineContext.LogWarning(FormatIndented(format, objects));
            }

            public void LogError(string format, params object[] objects) {
                PipelineContext.LogError(FormatIndented(format, objects));
            }
        }

        public class Node {
            public string Name { get; set; } = "";
            public INodeImplementation NodeImpl { get; set; } = null;

            public List<KeyValuePair<string, Node>> Outputs { get; } = new List<KeyValuePair<string, Node>>();
            public List<KeyValuePair<string, INodeInput>> Inputs { get; } = new List<KeyValuePair<string, INodeInput>>();

            public List<Node> Dependencies { get; } = new List<Node>();
            public NodeExecutionContext Context { get; set; } = null;

            public Node(string name, INodeImplementation node_impl) {
                Name = name;
                NodeImpl = node_impl;
            }

            public void AddOutput(Node other, string name = null) {
                if (name == null) {
                    name = string.Format("__output_{0}", Outputs.Count);
                }
                Outputs.Add(new KeyValuePair<string, Node>(name, other));
            }

            public void AddDependency(Node other) {
                Dependencies.Add(other);
            }

            public void AddInput(INodeInput input, string name = null) {
                if (name == null) {
                    name = string.Format("__input_{0}", Inputs.Count);
                }
                Inputs.Add(new KeyValuePair<string, INodeInput>(name, input));
            }

            public bool Initialize(PipelineContext pipeline_context, int level = 0) {
                foreach (KeyValuePair<string, Node> output in Outputs) {
                    if (!output.Value.Initialize(pipeline_context, level + 1)) {
                        pipeline_context.LogError(string.Format("Failed to initialize node: {0}.", Name));
                        return false;
                    }
                }

                foreach (KeyValuePair<string, INodeInput> input in Inputs) {
                    if (!input.Value.Initialize(pipeline_context)) {

                    }
                }

                Context = new NodeExecutionContext(this, pipeline_context, level);

                if (!NodeImpl.Initialize(Context)) {
                    pipeline_context.LogError(string.Format("Failed to initialize node implementation in node: {0}.", Name));
                    return false;
                }

                return true;
            }

            public Message Execute(Message payload) {
                Context.Payload = payload;
                return NodeImpl.Execute(Context);
            }
        }

        public string ContainerName { get; set; } = "";
        public int ContainerRank { get; set; } = 0;
        public int ContainerRankSize { get; set; } = 1;

        public string Name { get; set; } = "";
        public int PipelineRank { get; set; } = 0;
        public int PipelineRankSize { get; set; } = 1;

        public PipelineType Type { get; set; } = PipelineType.UNITY;
        public PipelineMode Mode { get; set; } = PipelineMode.LOOP;
        public MarshallerType Marshaller { get; set; } = MarshallerType.PROTOBUF;

        public int[] GPUs { get; set; } = new int[0];
        public int Port { get; set; } = 0;
        public int MaxPendingRequests { get; set; } = 1;

        public Node EntryNode { get; set; } = null;
        public PipelineContext Context { get; set; } = null;

        private readonly Dictionary<string, Node> nodes_ = new Dictionary<string, Node>();

        public static Pipeline Load(string config_file) {
            Pipeline pipeline = new Pipeline();

            if (!pipeline.InitializeFromConfig(config_file)) {
                Debug.LogError(string.Format("Cannot load: {0}", config_file));
                return null;
            }

            return pipeline;
        }

        private bool InitializeFromConfig(string config_file) {
            JObject config = JObject.Parse(File.ReadAllText(config_file));

            ContainerName = config["container_name"].ToString();
            ContainerRank = config["container_rank"].ToObject<int>();
            ContainerRankSize = config["container_rank_size"].ToObject<int>();

            Name = config["name"].ToString();
            PipelineRank = config["pipeline_rank"].ToObject<int>();
            PipelineRankSize = config["pipeline_rank_size"].ToObject<int>();

            Type = ParsePipelineType(config["type"].ToString());
            Mode = ParsePipelineMode(config["mode"].ToString());
            Marshaller = MarshallerUtils.ParseMarshallerType(config["marshaller"].ToString());
            GPUs = config["gpus"].ToObject<int[]>();
            Port = config["port"].ToObject<int>();
            MaxPendingRequests = config["max_pending_requests"].ToObject<int>();


            if (GPUs.Length != 1 && GPUs.Length != 0) {
                Debug.Log(string.Format("Unity pipeline needs one or none gpus configured, got: {0}.", GPUs.Length));
                return false;
            }

            if (!PipelineType.UNITY.Equals(Type)) {
                Debug.Log(string.Format("Only UNITY pipeline type supported, got: {0}.", Type));
                return false;
            }

            Debug.Log(string.Format("Container name: {0}, pipeline: {1}, mode: {2}, marshaller: {3}.",
                ContainerName, Name, Mode, Marshaller));

            foreach (int gpu in GPUs) {
                Debug.Log(string.Format("GPU: {0}.", gpu));
            }

            foreach (JObject node in config["nodes"]) {
                if (!InitializeNodeFromConfig(node)) {
                    Debug.LogError(string.Format("Failed to add node: {0}.", node));
                    return false;
                }
            }

            foreach (JObject node in config["nodes"]) {
                if (!InitializeOutputsFromConfig(node)) {
                    Debug.LogError(string.Format("Failed to add outputs for node: {0}.", node));
                    return false;
                }
            }

            return true;
        }

        private Node FindEntryNode() {
            List<Node> candidates = new List<Node>();
            foreach (KeyValuePair<string, Node> node in nodes_) {
                if (node.Value.Dependencies.Count == 0) {
                    candidates.Add(node.Value);
                }
            }

            if (candidates.Count != 1) {
                Debug.LogError("Cannot find entry node.");
            }

            return candidates[0];
        }

        public bool Initialize() {
            Context = new PipelineContext(Name, ContainerName, Marshaller);
            EntryNode = FindEntryNode();
            return true;
        }

        private bool InitializeOutputsFromConfig(JObject node_config) {
            string node_name = node_config["name"].ToString();
            Node node = GetNode(node_name);

            if (node == null) {
                return false;
            }

            Dictionary<int, string> index_names = new Dictionary<int, string>();

            foreach (JObject name_index in node_config["output_names"]) {
                string name = name_index["name"].ToString();
                int i = name_index["index"].ToObject<int>();
                index_names.Add(i, name);
            }

            int index = 0;
            foreach (JToken destination in node_config["outputs"]) {
                Node destination_node = GetNode(destination.ToString());

                if (destination_node == null) {
                    return false;
                }

                if (index_names.ContainsKey(index)) {
                    node.AddOutput(destination_node, index_names[index]);
                } else {
                    node.AddOutput(destination_node);
                }
                destination_node.AddDependency(node);
                index++;
            }
            return true;
        }

        private bool InitializeNodeFromConfig(JObject node_config) {
            string node_name = node_config["name"].ToString();
            JObject node_impl_config = node_config["node_impl"] as JObject;

            if (node_impl_config == null) {
                Debug.LogError("No \"node_impl\" in the node config.");
                return false;
            }

            string node_impl_type = node_impl_config["type"].ToString();
            JObject node_impl_parameters = node_impl_config["params"] as JObject;

            if (node_impl_parameters == null) {
                Debug.LogError("No \"params\" in the node_impl config.");
                return false;
            }

            INodeImplementation node_impl = PipelineFactory.CreateNodeImplementationFromConfig(node_impl_type, node_impl_parameters);

            if (node_impl == null) {
                Debug.LogError("Could not InitializeNodeFromConfig.");
                return false;
            }

            Node node = AddNode(node_name, node_impl);

            Dictionary<int, string> index_names = new Dictionary<int, string>();

            foreach (JObject name_index in node_config["input_names"]) {
                string name = name_index["name"].ToString();
                int i = name_index["index"].ToObject<int>();
                index_names.Add(i, name);
            }

            int index = 0;
            foreach (JObject input_config in node_config["inputs"]) {
                string node_input_type = input_config["type"].ToString();
                JObject nde_input_params = input_config["params"] as JObject;

                if (nde_input_params == null) {
                    Debug.LogError("No \"params\" in the input config.");
                    return false;
                }

                INodeInput node_input = PipelineFactory.CreateNodeInputFromConfig(node_input_type, nde_input_params);

                if (index_names.ContainsKey(index)) {
                    node.AddInput(node_input, index_names[index]);
                } else {
                    node.AddInput(node_input);
                }
            }

            return true;
        }

        public Node AddNode(string node_name, INodeImplementation node_impl) {
            if (nodes_.ContainsKey(node_name)) {
                Debug.LogError(string.Format("Node already in pipeline: {0}.", node_name));
                return null;
            }
            Node node = new Node(node_name, node_impl);
            nodes_[node_name] = node;
            return node;
        }

        public Node GetNode(string node_name) {
            if (!nodes_.ContainsKey(node_name)) {
                Debug.LogError(string.Format("Cannot find node: {0}.", node_name));
                return null;
            }
            return nodes_[node_name];
        }

    }

}

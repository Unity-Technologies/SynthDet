using System.Collections.Generic;
using System.Text;

namespace Sprawl {

    public class Message {
        public Dictionary<string, int> IntValues { get; } = new Dictionary<string, int>();
        public Dictionary<string, long> LongValues { get; } = new Dictionary<string, long>();
        public Dictionary<string, bool> BoolValues { get; } = new Dictionary<string, bool>();
        public Dictionary<string, float> FloatValues { get; } = new Dictionary<string, float>();
        public Dictionary<string, double> DoubleValues { get; } = new Dictionary<string, double>();
        public Dictionary<string, string> StringValues { get; } = new Dictionary<string, string>();
        public Dictionary<string, Tensor> TensorValues { get; } = new Dictionary<string, Tensor>();
        public Dictionary<string, Message> MessageValues { get; } = new Dictionary<string, Message>();

        public void AddIntValues(IDictionary<string, int> dict) {
            foreach (KeyValuePair<string, int> int_value in dict) {
                IntValues.Add(int_value.Key, int_value.Value);
            }
        }

        public void AddLongValues(IDictionary<string, long> dict) {
            foreach (KeyValuePair<string, long> long_value in dict) {
                LongValues.Add(long_value.Key, long_value.Value);
            }
        }

        public void AddBoolValues(IDictionary<string, bool> dict) {
            foreach (KeyValuePair<string, bool> bool_value in dict) {
                BoolValues.Add(bool_value.Key, bool_value.Value);
            }
        }

        public void AddFloatValues(IDictionary<string, float> dict) {
            foreach (KeyValuePair<string, float> float_value in dict) {
                FloatValues.Add(float_value.Key, float_value.Value);
            }
        }

        public void AddDoubleValues(IDictionary<string, double> dict) {
            foreach (KeyValuePair<string, double> double_value in dict) {
                DoubleValues.Add(double_value.Key, double_value.Value);
            }
        }

        public void AddStringValues(IDictionary<string, string> dict) {
            foreach (KeyValuePair<string, string> string_value in dict) {
                StringValues.Add(string_value.Key, string_value.Value);
            }
        }

        public void AddMessageValue(string key, Message value) {
            MessageValues.Add(key, value);
        }

        public void AddTensorValue(string key, Tensor value) {
            TensorValues.Add(key, value);
        }

        private string GetIndent(int level) {
            string indent = "";
            for (int i = 0; i < level; ++i) {
                indent += "  ";
            }
            return indent;
        }

        public string DebugString(int level = 0) {
            StringBuilder builder = new StringBuilder();
            string indent = GetIndent(level);
            
            foreach (KeyValuePair<string, int> i in IntValues) {
                builder.AppendFormat("{0}int {1} = {2}\n", indent, i.Key, i.Value);
            }
            foreach (KeyValuePair<string, long> i in LongValues) {
                builder.AppendFormat("{0}long {1} = {2}\n", indent, i.Key, i.Value);
            }
            foreach (KeyValuePair<string, bool> i in BoolValues) {
                builder.AppendFormat("{0}bool {1} = {2}\n", indent, i.Key, i.Value);
            }
            foreach (KeyValuePair<string, float> i in FloatValues) {
                builder.AppendFormat("{0}float {1} = {2}\n", indent, i.Key, i.Value);
            }
            foreach (KeyValuePair<string, double> i in DoubleValues) {
                builder.AppendFormat("{0}double {1} = {2}\n", indent, i.Key, i.Value);
            }
            foreach (KeyValuePair<string, string> i in StringValues) {
                builder.AppendFormat("{0}string {1} = \"{2}\"\n", indent, i.Key, i.Value);
            }
            foreach (KeyValuePair<string, Tensor> i in TensorValues) {
                builder.AppendFormat("{0}tensor {1} = {{\n", indent, i.Key);
                builder.Append(i.Value.DebugString(level + 1));
                builder.AppendFormat("{0}}}\n", indent);
            }
            foreach (KeyValuePair<string, Message> i in MessageValues) {
                builder.AppendFormat("{0}message {1} = {{\n", indent, i.Key);
                builder.Append(i.Value.DebugString(level + 1));
                builder.AppendFormat("{0}}}\n", indent);
            }
            return builder.ToString();
        }
    }
}
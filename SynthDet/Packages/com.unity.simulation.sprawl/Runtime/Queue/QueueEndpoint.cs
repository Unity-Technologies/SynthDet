using System.Diagnostics;
using StackExchange.Redis;
using Debug = UnityEngine.Debug;

namespace Sprawl {
    public class QueueEndpoint {

        public string Hostname { get; set; } = "";
        public int Port { get; set; } = 0;
        public IMessageMarshaller Marshaller { get; set; } = null;
        
        private static float _redisConnectionTimeoutMilliseconds = 5000;

        private ConnectionMultiplexer redis_connection_ = null;
        private IDatabase redis_db_ = null;

        public QueueEndpoint(string hostname, int port, MarshallerType marshaller_type) {
            Hostname = hostname.Equals("localhost") ? "127.0.0.1" : hostname;
            Port = port;
            Marshaller = MarshallerFactory.CreateMarshaller(marshaller_type);
        }

        private void ResetConnection() {
            if (redis_connection_ != null) {
                redis_connection_.Close();
            }

            var sw = new Stopwatch();
            sw.Start();

            while (true) {
                try {
                    redis_connection_ = ConnectionMultiplexer.Connect(string.Format("{0}:{1},connectTimeout=1000,syncTimeout=3000", Hostname, Port));
                    redis_db_ = redis_connection_.GetDatabase();
                    Debug.Log(string.Format("Connected to Redis: {0}:{1}", Hostname, Port));
                    if (sw.ElapsedMilliseconds > _redisConnectionTimeoutMilliseconds)
                    {
                        Debug.LogError("Timeout occurred while connecting to Redis..");
                        break;
                    }
                    return;
                } catch (RedisConnectionException) {
                    Debug.LogWarning(string.Format("Trying to connect to Redis failed: {0}:{1}", Hostname, Port));
                }
            }
        }

        public long QueueElementsCount(Pipeline.NodeExecutionContext context) {
            while (true) {
                try {
                    return redis_db_.ListLength("q");
                } catch (RedisTimeoutException) {
                    context.LogWarning("Timed out, reseting Redis.");
                    ResetConnection();
                }
            }
        }

        public void Push(Message message, Pipeline.NodeExecutionContext context) {
            byte[] message_serialized = this.Marshaller.Serialize(message);
            while (true) {
                try {
                    redis_db_.ListLeftPush("q", message_serialized);
                    return;
                } catch (RedisTimeoutException) {
                    context.LogWarning("Timed out, reseting Redis.");
                    ResetConnection();
                }
            }
        }

        public Message Get(Pipeline.NodeExecutionContext context) {
            while (true) {
                try {
                    byte[] message = redis_db_.ListRightPop("q");
                    if (message != null) {
                        return this.Marshaller.Deserialize(message);
                    } else {
                        return null;
                    }
                } catch (RedisTimeoutException) {
                    context.LogWarning("Timed out, reseting Redis.");
                    ResetConnection();
                }
            }
        }

        public bool Initialize() {
            ConnectionMultiplexer.SetFeatureFlag("preventthreadtheft", true);
            ResetConnection();
            return true;
        }
    }
}
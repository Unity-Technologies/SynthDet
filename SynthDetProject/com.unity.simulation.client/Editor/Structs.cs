using System;

namespace Unity.Simulation.Client
{
    #pragma warning disable 0649

    [Serializable]
    public struct ProjectInfo
    {
        public string name;
        public string guid;
        public long   org_foreign_key;
        public string org_name;
        public bool   archived;
        public bool   active;
        public string created_at;
        public string updated_at;
    }

    [Serializable]
    internal struct ProjectArray
    {
        public ProjectInfo[] projects;
    }

    [Serializable]
    internal struct TokenData
    {
        public string access_token;
        public int    expires_in;
        public string refresh_token;
        public string timestamp;
        public string token_type;
    }

    [Serializable]
    internal struct UploadUrlData
    {
        public string id;
        public string upload_uri;
    }

    [Serializable]
    internal struct UploadInfo
    {
        public string name;
        public string description;
        public UploadInfo(string name, string description)
        {
            this.name = name;
            this.description = description;
        }
    }

    [Serializable]
    internal struct DownloadDetails
    {
        public string name;
        public string download_uri;
    }

    [Serializable]
    public struct SysParamDefinition
    {
        public string id;
        public string description;
        public bool   allowed;
    }

    [Serializable]
    internal struct SysParamArray
    {
        public SysParamDefinition[] sys_params;
    }

    [Serializable]
    public struct AppParam
    {
        public string id;
        public string name;
        public int    num_instances;
    }

    [Serializable]
    public struct RunDefinition
    {
        public string name;
        public string description;
        public string build_id;
        public string sys_param_id;
        public AppParam[] app_params;
    }

    [Serializable]
    public struct RunDescription
    {
        public string project_id;
        public string build_id;
        public string definition_id;
        public string name;
        public string description;
        public string created_at;
        public AppParam[] app_params;
        public string sys_param_id;
        public RunResult[] runs;
    }

    [Serializable]
    internal struct RunDefinitionId
    {
        public string definition_id;
    }

    [Serializable]
    internal struct RunExecutionId
    {
        public string execution_id;
    }

    [Serializable]
    public struct RunResult
    {
        public string execution_id;
        public string created_at;
        public string status;
        public string source;
        public string message;
        public string updated_at;
    }

    [Serializable]
    public struct RunState
    {
        public string code;
        public string source;
    }

    [Serializable]
    public struct RunSummary
    {
        public int num_success;
        public int num_in_progress;
        public int num_failures;
        public int num_not_run;
        public RunState state;
    }

    [Serializable]
    public struct ManifestEntry
    {
        public string executionId;
        public string appParamId;
        public int    instanceId;
        public int    attemptId;
        public string fileName;
        public string downloadUri;
    }

    [Serializable]
    internal struct LogChunk
    {
      public int    chunk_id;
      public string content_type;
      public int    content_length;
      public string created_at;
      public string download_uri;
    }

    [Serializable]
    internal struct LogInfo
    {
        public string     project_id;
        public string     definition_id;
        public string     execution_id;
        public string     app_param_id;
        public int        instance_id;
        public int        attempt_id;
        public LogChunk   min_log_chunk;
        public LogChunk   max_log_chunk;
        public LogChunk[] log_chunks;
    }

    #pragma warning restore 0649
}

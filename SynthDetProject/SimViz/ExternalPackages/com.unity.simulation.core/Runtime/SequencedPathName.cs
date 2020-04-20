using System.IO;

using UnityEngine;
using System.Threading;

namespace Unity.Simulation
{
    /// <summary>
    /// Utility class to generate filenames with a sequence number.
    /// </summary>
    public struct SequencedPathName
    {
        string _path;
        string _pathWithoutExtension;
        string _extension;
        int    _sequence;
        bool   _addSequence;

        /// <summary>
        /// Constructs a SequencePathName object with path, and optional sequence number.
        /// </summary>
        /// <param name="path">The path to the file on the local file system..</param>
        /// <param name="addSequenceNumber">When true, appends an auto incrementing sequence number before the extension.</param>
        public SequencedPathName(string path, bool addSequenceNumber)
        {
            Debug.Assert(!string.IsNullOrEmpty(path));
            _path = path;
            _pathWithoutExtension = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + "_");
            _extension   = Path.GetExtension(path);
            _sequence    = 0;
            _addSequence = addSequenceNumber;
        }

        /// <summary>
        /// Get a full path to the file with sequence number appended if addSequence number is enabled.
        /// </summary>
        /// <returns>string of the path to the file</returns>
        public string GetPath()
        {
            if (_addSequence)
            {
                int seq = Interlocked.Increment(ref _sequence);
                var path = _pathWithoutExtension + seq.ToString() + _extension;
                return path;
            }
            else
                return _path;
        }
    }
}

using System.IO;

using UnityEngine;
using System.Threading;

namespace Unity.AI.Simulation
{
    public struct SequencedPathName
    {
        string _path;
        string _pathWithoutExtension;
        string _extension;
        int    _sequence;
        bool   _addSequence;

        public SequencedPathName(string path, bool addSequenceNumber)
        {
            Debug.Assert(!string.IsNullOrEmpty(path));
            _path = path;
            _pathWithoutExtension = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + "_");
            _extension   = Path.GetExtension(path);
            _sequence    = 0;
            _addSequence = addSequenceNumber;
        }

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

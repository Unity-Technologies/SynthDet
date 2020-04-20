#if INCLUDE_SHARED_MEMORY_IN_SDK
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Simulation
{
    public class SharedMemory
    {
        [Flags]
        public enum Options
        {
            Create    = (1<<0),
            Read      = (1<<2),
            Write     = (1<<3),
            ReadWrite = (1<<4)
        }

        Options m_Options;
        string  m_Name;
        IntPtr  m_Buffer;
        int     m_LengthInBytes;

        public string Name
        {
            get { return m_Name; }
        }

        public IntPtr Buffer
        {
            get { return m_Buffer; }
        }

        public int LengthInBytes
        {
            get { return m_LengthInBytes; }
        }

        public SharedMemory(string name, int lengthInBytes, Options options)
        {
            m_Options = options;
            m_Name = name;
            m_LengthInBytes = lengthInBytes;
            m_Buffer = MapSharedMemory(m_Name, m_LengthInBytes, options);
        }

        unsafe public NativeArray<T> CreateNativeArray<T>() where T : struct
        {
            var count = m_LengthInBytes / Marshal.SizeOf(typeof(T));
            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>((void*)m_Buffer, count, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safety = AtomicSafetyHandle.Create();
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, safety);
            AtomicSafetyHandle.SetAllowReadOrWriteAccess(safety, true);
#endif
            return array;
        }

        static IntPtr MapSharedMemory(string name, int length, Options options)
        {
            var create = ((options & Options.Create) == Options.Create) ? m_OptionsMap[Options.Create] : new NativeFlags();

            options &= ~Options.Create;
            // Remaining options are mutually exclusive.
            Debug.Assert((int)options > 0 && ((int)options & ((int)options - 1)) == 0);

            var flags = m_OptionsMap[options];

	        var fd = shm_open(
                name,
                create.open | flags.open,
                flags.perms
            );

            if (fd == -1)
            {
                throw new InvalidOperationException("shm_open failed.");
            }

            if (ftruncate(fd, length) != 0)
            {
                shm_unlink(name);
                close(fd);
            }

            var address = mmap(
                IntPtr.Zero,
                length,
                flags.prot,
                MapFlags.MAP_SHARED,
                fd,
                0
            );

            if ((MapFlags)(int)address == MapFlags.MAP_FAILED)
            {
                shm_unlink(name);
                close(fd);
                return IntPtr.Zero;
            }

            close(fd);

            return address;
        }

        static void ReleaseSharedMemory(string name, IntPtr memory, int length)
        {
            munmap(memory, length);
            shm_unlink(name);
        }

        struct NativeFlags
        {
            public OpenFlags   open;
            public Permissions perms;
            public Protection  prot;
            public NativeFlags(OpenFlags openFlags = OpenFlags.None, Permissions perms = Permissions.None, Protection prot = Protection.PROT_NONE)
            {
                this.open  = openFlags;
                this.perms = perms;
                this.prot  = prot;
            }
        }

        readonly static Dictionary<Options, NativeFlags> m_OptionsMap = new Dictionary<Options, NativeFlags>()
        {
            {Options.Create,    new NativeFlags(OpenFlags.O_CREAT)},
            {Options.Read,      new NativeFlags(OpenFlags.O_RDONLY, Permissions.S_IRUSR | Permissions.S_IRGRP, Protection.PROT_READ)},
            {Options.Write,     new NativeFlags(OpenFlags.O_WRONLY, Permissions.S_IWUSR | Permissions.S_IWGRP, Protection.PROT_WRITE)},
            {Options.ReadWrite, new NativeFlags(OpenFlags.O_RDWR,   Permissions.S_IRUSR | Permissions.S_IRGRP | Permissions.S_IWUSR | Permissions.S_IWGRP, Protection.PROT_READ | Protection.PROT_WRITE)}
        };

        [Flags]
        enum OpenFlags
        {
            None     = 0,
            O_CREAT	 = 0x0200, /* create if nonexistant */
            O_TRUNC	 = 0x0400, /* truncate to zero length */
            O_EXCL	 = 0x0800, /* error if already exists */
            O_RDONLY = 0x0000, /* open for reading only */
            O_WRONLY = 0x0001, /* open for writing only */
            O_RDWR	 = 0x0002, /* open for reading and writing */
        }

        [Flags]
        enum Permissions
        {
            None    = 0,
            /* Read, write, execute/search by owner */
            S_IRWXU = 0000700, /* [XSI] RWX mask for owner */
            S_IRUSR = 0000400, /* [XSI] R for owner */
            S_IWUSR = 0000200, /* [XSI] W for owner */
            S_IXUSR = 0000100, /* [XSI] X for owner */
            /* Read, write, execute/search by group */
            S_IRWXG = 0000070, /* [XSI] RWX mask for group */
            S_IRGRP = 0000040, /* [XSI] R for group */
            S_IWGRP = 0000020, /* [XSI] W for group */
            S_IXGRP = 0000010, /* [XSI] X for group */
            /* Read, write, execute/search by others */
            S_IRWXO = 0000007, /* [XSI] RWX mask for other */
            S_IROTH = 0000004, /* [XSI] R for other */
            S_IWOTH = 0000002, /* [XSI] W for other */
            S_IXOTH = 0000001, /* [XSI] X for other */
        }

        [Flags]
        enum Protection
        {
            PROT_NONE  = 0, /* Pages may not be accessed. */
            PROT_READ  = 1, /* Pages may be read. */
            PROT_WRITE = 2, /* Pages may be written. */
            PROT_EXEC  = 4, /* Pages may be executed. */
        }

        [Flags]
        enum MapFlags
        {
            MAP_FAILED = -1,
            MAP_SHARED = 1,
        }

        [DllImport ("__Internal")]
        static extern int shm_open(string name, OpenFlags oflag, Permissions mode);

        [DllImport ("__Internal")]
        static extern int ftruncate(int fd, int length);

        [DllImport ("__Internal")]
        static extern int shm_unlink(string name);

        [DllImport ("__Internal")]
        static extern int close(int fd);

        [DllImport ("__Internal")]
        static extern IntPtr mmap(IntPtr addr, int length, Protection prot, MapFlags flags, int fd, int offset);
        
        [DllImport ("__Internal")]
        static extern int munmap(IntPtr addr, int length);
    }
}
#endif//INCLUDE_SHARED_MEMORY_IN_SDK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Mappics.ProcessWatcher
{
    public static class IrfanWatcher
    {
        static Process _irfanProcess;
        static Process IrfanProcess
        {
            get
            {
                bool update = false;
                if (_irfanProcess == null)
                {
                    update = true;
                }
                else if (_irfanProcess.HasExited == true)
                {
                    update = true;
                }

                if (update)
                {
                    _irfanProcess = tryToFindIrfanViewProcess();
                    if (_irfanProcess != null)
                    {
                        // Register for some events
                        _irfanProcess.Exited += _irfanProcess_Exited;
                    }
                    else
                    {
                        MainWindow.setImageReplacement("No irfan process could be found!");
                    }
                }
                else
                {
                    _irfanProcess.Refresh();
                }

                return _irfanProcess;
            }
        }
        
        static string lastTimesWindowTitle = "";
        public static bool updateNeeded()
        {
            // Check if process still exists
            if (IrfanProcess == null)
            {
                return false;
            }

            // Check if the name changed
            if (IrfanProcess.MainWindowTitle == lastTimesWindowTitle)
            {
                return false;
            }
            lastTimesWindowTitle = IrfanProcess.MainWindowTitle;

            return true;
        }

        static private void _irfanProcess_Exited(object sender, System.EventArgs e)
        {
            _irfanProcess = null;
        }

        static private Process tryToFindIrfanViewProcess()
        {
            Process[] possibleProcesses = Process.GetProcessesByName("i_view32");
            if (possibleProcesses.Length == 0)
            {
                return null;
            }
            return possibleProcesses[0];
        }

        static IntPtr m_ipProcessHwnd = IntPtr.Zero;
        public static string getFilePathToCurrentlyOpenedFileInIrfan()
        {
            if (IrfanProcess == null)
            {
                return "";
            }
            string completeTitle = IrfanProcess.MainWindowTitle;
            if (String.IsNullOrEmpty(completeTitle))
            {
                return "";
            }
            int positionOfIrfanText = completeTitle.IndexOf(" - IrfanView");
            if (positionOfIrfanText == -1)
            {
                return "";
            }
            string pictureName = completeTitle.Substring(0, positionOfIrfanText);
            string strTemp = "";
            Console.WriteLine("pictureName: " + pictureName);
            m_ipProcessHwnd = Win32API.OpenProcess(Win32API.ProcessAccessFlags.DupHandle, false, IrfanProcess.Id);
            List<Win32API.SYSTEM_HANDLE_INFORMATION> lstHandles = CustomAPI.GetHandles(IrfanProcess);
            for (int nIndex = 0; nIndex < lstHandles.Count; nIndex++)
            {
                strTemp = GetFileDetails(lstHandles[nIndex]).Name;
                if (strTemp != "")
                {
                    // We do not want the device stuff
                    // Later we should get a good regex for this
                    if (strTemp.ElementAt(0) != '\\')
                    {
                        // get the file attributes for file or directory
                        FileAttributes attr = File.GetAttributes(@strTemp);
                        //detect whether its a directory or file
                        if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                        {
                            string[] foundFiles = Directory.GetFiles(@strTemp, pictureName);
                            if (foundFiles.Length > 0)
                            {
                                Console.WriteLine("Picture found in '" + strTemp + "\\" + pictureName);
                                return strTemp + "\\" + pictureName;
                            }
                        }
                    }
                }
            }
            return "";
        }


        private static FileDetails GetFileDetails(Win32API.SYSTEM_HANDLE_INFORMATION sYSTEM_HANDLE_INFORMATION)
        {
            FileDetails fd = new FileDetails();
            fd.Name = "";
            IntPtr ipHandle = IntPtr.Zero;
            Win32API.OBJECT_BASIC_INFORMATION objBasic = new Win32API.OBJECT_BASIC_INFORMATION();
            IntPtr ipBasic = IntPtr.Zero;
            Win32API.OBJECT_TYPE_INFORMATION objObjectType = new Win32API.OBJECT_TYPE_INFORMATION();
            IntPtr ipObjectType = IntPtr.Zero;
            Win32API.OBJECT_NAME_INFORMATION objObjectName = new Win32API.OBJECT_NAME_INFORMATION();
            IntPtr ipObjectName = IntPtr.Zero;
            string strObjectTypeName = "";
            string strObjectName = "";
            int nLength = 0;
            int nReturn = 0;
            IntPtr ipTemp = IntPtr.Zero;

            //OpenProcessForHandle(sYSTEM_HANDLE_INFORMATION.ProcessID);
            if (!Win32API.DuplicateHandle(m_ipProcessHwnd, sYSTEM_HANDLE_INFORMATION.Handle, Win32API.GetCurrentProcess(), out ipHandle, 0, false, Win32API.DUPLICATE_SAME_ACCESS)) return fd;

            ipBasic = Marshal.AllocHGlobal(Marshal.SizeOf(objBasic));
            Win32API.NtQueryObject(ipHandle, (int)Win32API.ObjectInformationClass.ObjectBasicInformation, ipBasic, Marshal.SizeOf(objBasic), ref nLength);
            objBasic = (Win32API.OBJECT_BASIC_INFORMATION)Marshal.PtrToStructure(ipBasic, objBasic.GetType());
            Marshal.FreeHGlobal(ipBasic);


            ipObjectType = Marshal.AllocHGlobal(objBasic.TypeInformationLength);
            nLength = objBasic.TypeInformationLength;
            while ((uint)(nReturn = Win32API.NtQueryObject(ipHandle, (int)Win32API.ObjectInformationClass.ObjectTypeInformation, ipObjectType, nLength, ref nLength)) == Win32API.STATUS_INFO_LENGTH_MISMATCH)
            {
                Marshal.FreeHGlobal(ipObjectType);
                ipObjectType = Marshal.AllocHGlobal(nLength);
            }

            objObjectType = (Win32API.OBJECT_TYPE_INFORMATION)Marshal.PtrToStructure(ipObjectType, objObjectType.GetType());
            if (Is64Bits())
            {
                ipTemp = new IntPtr(Convert.ToInt64(objObjectType.Name.Buffer.ToString(), 10) >> 32);
            }
            else
            {
                ipTemp = objObjectType.Name.Buffer;
            }

            strObjectTypeName = Marshal.PtrToStringUni(ipTemp, objObjectType.Name.Length >> 1);
            Marshal.FreeHGlobal(ipObjectType);
            if (strObjectTypeName != "File") return fd;

            nLength = objBasic.NameInformationLength;
            ipObjectName = Marshal.AllocHGlobal(nLength);
            while ((uint)(nReturn = Win32API.NtQueryObject(ipHandle, (int)Win32API.ObjectInformationClass.ObjectNameInformation, ipObjectName, nLength, ref nLength)) == Win32API.STATUS_INFO_LENGTH_MISMATCH)
            {
                Marshal.FreeHGlobal(ipObjectName);
                ipObjectName = Marshal.AllocHGlobal(nLength);
            }
            objObjectName = (Win32API.OBJECT_NAME_INFORMATION)Marshal.PtrToStructure(ipObjectName, objObjectName.GetType());

            if (Is64Bits())
            {
                ipTemp = new IntPtr(Convert.ToInt64(objObjectName.Name.Buffer.ToString(), 10) >> 32);
            }
            else
            {
                ipTemp = objObjectName.Name.Buffer;
            }

            byte[] baTemp = new byte[nLength];
            Win32API.CopyMemory(baTemp, ipTemp, (uint)nLength);

            if (Is64Bits())
            {
                strObjectName = Marshal.PtrToStringUni(new IntPtr(ipTemp.ToInt64()));
            }
            else
            {
                strObjectName = Marshal.PtrToStringUni(new IntPtr(ipTemp.ToInt32()));
            }

            Marshal.FreeHGlobal(ipObjectName);
            Win32API.CloseHandle(ipHandle);

            fd.Name = GetRegularFileNameFromDevice(strObjectName);
            return fd;
        }

        public static string GetRegularFileNameFromDevice(string strRawName)
        {
            string strFileName = strRawName;
            foreach (string strDrivePath in Environment.GetLogicalDrives())
            {
                StringBuilder sbTargetPath = new StringBuilder(Win32API.MAX_PATH);
                if (Win32API.QueryDosDevice(strDrivePath.Substring(0, 2), sbTargetPath, Win32API.MAX_PATH) == 0)
                {
                    return strRawName;
                }
                string strTargetPath = sbTargetPath.ToString();
                if (strFileName.StartsWith(strTargetPath))
                {
                    strFileName = strFileName.Replace(strTargetPath, strDrivePath.Substring(0, 2));
                    break;
                }
            }
            return strFileName;
        }

        static bool Is64Bits()
        {
            return false; // Marshal.SizeOf(typeof(IntPtr)) == 8 ? true : false;
        }

        static int m_nLastProcId = 0;
        private static void OpenProcessForHandle(int p)
        {
            if (p != m_nLastProcId)
            {
                Win32API.CloseHandle(m_ipProcessHwnd);
                m_ipProcessHwnd = Win32API.OpenProcess(Win32API.ProcessAccessFlags.DupHandle, false, p);
                m_nLastProcId = p;
            }
        }
    }

    class FileDetails
    {
        public string Name { get; set; }
    }

    public class Win32API
    {
        [DllImport("ntdll.dll")]
        public static extern int NtQueryObject(IntPtr ObjectHandle, int
            ObjectInformationClass, IntPtr ObjectInformation, int ObjectInformationLength,
            ref int returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint QueryDosDevice(string lpDeviceName, StringBuilder lpTargetPath, int ucchMax);

        [DllImport("ntdll.dll")]
        public static extern uint NtQuerySystemInformation(int
            SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength,
            ref int returnLength);

        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        public static extern void CopyMemory(byte[] Destination, IntPtr Source, uint Length);

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);
        [DllImport("kernel32.dll")]
        public static extern int CloseHandle(IntPtr hObject);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DuplicateHandle(IntPtr hSourceProcessHandle,
           ushort hSourceHandle, IntPtr hTargetProcessHandle, out IntPtr lpTargetHandle,
           uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwOptions);
        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentProcess();

        public enum ObjectInformationClass : int
        {
            ObjectBasicInformation = 0,
            ObjectNameInformation = 1,
            ObjectTypeInformation = 2,
            ObjectAllTypesInformation = 3,
            ObjectHandleInformation = 4
        }

        [Flags]
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VMOperation = 0x00000008,
            VMRead = 0x00000010,
            VMWrite = 0x00000020,
            DupHandle = 0x00000040,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            Synchronize = 0x00100000
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct OBJECT_BASIC_INFORMATION
        { // Information Class 0
            public int Attributes;
            public int GrantedAccess;
            public int HandleCount;
            public int PointerCount;
            public int PagedPoolUsage;
            public int NonPagedPoolUsage;
            public int Reserved1;
            public int Reserved2;
            public int Reserved3;
            public int NameInformationLength;
            public int TypeInformationLength;
            public int SecurityDescriptorLength;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreateTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct OBJECT_TYPE_INFORMATION
        { // Information Class 2
            public UNICODE_STRING Name;
            public int ObjectCount;
            public int HandleCount;
            public int Reserved1;
            public int Reserved2;
            public int Reserved3;
            public int Reserved4;
            public int PeakObjectCount;
            public int PeakHandleCount;
            public int Reserved5;
            public int Reserved6;
            public int Reserved7;
            public int Reserved8;
            public int InvalidAttributes;
            public GENERIC_MAPPING GenericMapping;
            public int ValidAccess;
            public byte Unknown;
            public byte MaintainHandleDatabase;
            public int PoolType;
            public int PagedPoolUsage;
            public int NonPagedPoolUsage;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct OBJECT_NAME_INFORMATION
        { // Information Class 1
            public UNICODE_STRING Name;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct UNICODE_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GENERIC_MAPPING
        {
            public int GenericRead;
            public int GenericWrite;
            public int GenericExecute;
            public int GenericAll;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SYSTEM_HANDLE_INFORMATION
        { // Information Class 16
            public int ProcessID;
            public byte ObjectTypeNumber;
            public byte Flags; // 0x01 = PROTECT_FROM_CLOSE, 0x02 = INHERIT
            public ushort Handle;
            public int Object_Pointer;
            public UInt32 GrantedAccess;
        }

        public const int MAX_PATH = 260;
        public const uint STATUS_INFO_LENGTH_MISMATCH = 0xC0000004;
        public const int DUPLICATE_SAME_ACCESS = 0x2;
    }

    public class CustomAPI
    {
        const int CNST_SYSTEM_HANDLE_INFORMATION = 16;
        const uint STATUS_INFO_LENGTH_MISMATCH = 0xc0000004;

        public static List<Win32API.SYSTEM_HANDLE_INFORMATION> GetHandles(Process process)
        {
            uint nStatus;
            int nHandleInfoSize = 0x10000;
            IntPtr ipHandlePointer = Marshal.AllocHGlobal(nHandleInfoSize);
            int nLength = 0;
            IntPtr ipHandle = IntPtr.Zero;

            while ((nStatus = Win32API.NtQuerySystemInformation(CNST_SYSTEM_HANDLE_INFORMATION, ipHandlePointer, nHandleInfoSize, ref nLength)) == STATUS_INFO_LENGTH_MISMATCH)
            {
                nHandleInfoSize = nLength;
                Marshal.FreeHGlobal(ipHandlePointer);
                ipHandlePointer = Marshal.AllocHGlobal(nLength);
            }

            byte[] baTemp = new byte[nLength];
            Win32API.CopyMemory(baTemp, ipHandlePointer, (uint)nLength);

            long lHandleCount = 0;
            if (Is64Bits())
            {
                lHandleCount = Marshal.ReadInt64(ipHandlePointer);
                ipHandle = new IntPtr(ipHandlePointer.ToInt64() + 8);
            }
            else
            {
                lHandleCount = Marshal.ReadInt32(ipHandlePointer);
                ipHandle = new IntPtr(ipHandlePointer.ToInt32() + 4);
            }

            Win32API.SYSTEM_HANDLE_INFORMATION shHandle;
            List<Win32API.SYSTEM_HANDLE_INFORMATION> lstHandles = new List<Win32API.SYSTEM_HANDLE_INFORMATION>();

            for (long lIndex = 0; lIndex < lHandleCount; lIndex++)
            {
                shHandle = new Win32API.SYSTEM_HANDLE_INFORMATION();
                if (Is64Bits())
                {
                    shHandle = (Win32API.SYSTEM_HANDLE_INFORMATION)Marshal.PtrToStructure(ipHandle, shHandle.GetType());
                    ipHandle = new IntPtr(ipHandle.ToInt64() + Marshal.SizeOf(shHandle) + 8);
                }
                else
                {
                    ipHandle = new IntPtr(ipHandle.ToInt64() + Marshal.SizeOf(shHandle));
                    shHandle = (Win32API.SYSTEM_HANDLE_INFORMATION)Marshal.PtrToStructure(ipHandle, shHandle.GetType());
                }
                if (shHandle.ProcessID != process.Id) continue;
                lstHandles.Add(shHandle);
            }
            return lstHandles;

        }

        static bool Is64Bits()
        {
            return false; // Marshal.SizeOf(typeof(IntPtr)) == 8 ? true : false;
        }
    }
}

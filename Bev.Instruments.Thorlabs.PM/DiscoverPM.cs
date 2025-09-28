using Thorlabs.TLPM_32.Interop;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace Bev.Instruments.Thorlabs.PM
{
    public class DiscoverPM
    {
        private readonly List<string> foundDevices = new List<string>();

        public DiscoverPM()
        {
            HandleRef instrumentHandle = new HandleRef();
            TLPM tlpm = new TLPM(instrumentHandle.Handle);
            try
            {
                tlpm.findRsrc(out uint count);
                if (count>0)
                {
                    for (uint i = 0; i < count; i++)
                    {
                        StringBuilder sb = new StringBuilder(1024);
                        tlpm.getRsrcName(i, sb);
                        foundDevices.Add(sb.ToString());
                    }
                }
            }
            catch {}
            tlpm.Dispose();
        }

        public string[] NamesOfDevices => foundDevices.ToArray();
        public int NumberOfDevices => foundDevices.Count;
        public string FirstDevice => GetFirstDevice();
        public string LastDevice => GetLastDevice();

        private string GetLastDevice()
        {
            if (NumberOfDevices > 0) return NamesOfDevices[NamesOfDevices.Length-1];
            return string.Empty;
        }

        private string GetFirstDevice()
        {
            if (NumberOfDevices > 0) return NamesOfDevices[0];
            return string.Empty;
        }
    }
}

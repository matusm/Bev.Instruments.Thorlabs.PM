using System;
using System.Globalization;
using Bev.Instruments.Thorlabs.PM;

namespace SetAdapter
{
    class Program
    {
        static int Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            
            Console.WriteLine("Searching for devices ...");
            DiscoverPM dpm = new DiscoverPM();

            if (dpm.NumberOfDevices == 0)
            {
                Console.WriteLine("No powermeter found!");
                return 1;
            }
            Console.WriteLine($"Number of detected devices: {dpm.NumberOfDevices}");
            Console.WriteLine();

            Adapter adapter = Adapter.Current;
            if (args.Length == 1)
            {
                string option = args[0].Trim(new char[] { '-', '/' });
                //if (option.Contains("p")) adapter = Adapter.Pyro; // dangerous!
                //if (option.Contains("P")) adapter = Adapter.Pyro;
                if (option.Contains("c")) adapter = Adapter.Current;
                if (option.Contains("C")) adapter = Adapter.Current;
                if (option.Contains("v")) adapter = Adapter.Voltage;
                if (option.Contains("V")) adapter = Adapter.Voltage;
            }

            ThorlabsPM[] pms = new ThorlabsPM[dpm.NumberOfDevices];

            for (int i = 0; i < pms.Length; i++)
            {
                pms[i] = new ThorlabsPM(dpm.NamesOfDevices[i]);
                Console.WriteLine($"InstrumentID: {pms[i].InstrumentID}");
                if (pms[i].SensorSubtype == SensorSubtype.Adapter)
                {
                    if (adapter == Adapter.Current)
                    {
                        Console.WriteLine("=> setting adapter to current mode.");
                        pms[i].SetAdapterPhotodiode();
                    }
                    if (adapter == Adapter.Voltage)
                    {
                        Console.WriteLine("=> setting adapter to voltage mode.");
                        pms[i].SetAdapterThermal();
                    }
                    if (adapter == Adapter.Pyro)
                    {
                        Console.WriteLine("=> setting adapter to pyro mode.");
                        pms[i].SetAdapterPyro();
                    }
                }
                else
                {
                    Console.WriteLine("=> no adapter connected to instrument.");
                }
                Console.WriteLine();
            }
            return 0;
        }
    }

    public enum Adapter
    {
        None,
        Current,
        Voltage,
        Pyro
    }
}

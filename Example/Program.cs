using System;
using System.Globalization;
using System.IO;
using System.Threading;
using Bev.Instruments.Thorlabs.PM;

namespace Example
{
    class Program
    {
        static int Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            string pm100D_1 = "USB0::0x1313::0x8078::PM003835::INSTR";  // console instrument
            string pm16_120 = "USB0::0x1313::0x807B::230104202::INSTR"; // USB power sensor

            Console.WriteLine("Searching for devices ...");
            DiscoverPM dpm = new DiscoverPM(); // time expensive!
            if(dpm.NumberOfDevices==0)
            {
                Console.WriteLine("No powermeter found!");
                return 1;
            }
            Console.WriteLine($"Number of detected devices: {dpm.NumberOfDevices}");
            foreach (var s in dpm.NamesOfDevices)
                Console.WriteLine($"{s}");
            Console.WriteLine();

            ThorlabsPM pm = new ThorlabsPM(dpm.LastDevice);
            
            Console.WriteLine();

            string csvFileName =$"X_TLPM_{pm.DetectorSerialNumber}.csv";
            StreamWriter streamWriter = new StreamWriter(csvFileName, false);

            int minWl = (int)pm.GetMinimumWavelength();
            int maxWl = (int)pm.GetMaximumWavelength();

            LogAndDisplay($"# Filename:                  {csvFileName}");
            LogAndDisplay($"# DriverRevision:            {pm.DriverRevision}");
            LogAndDisplay($"# InstrumentID:              {pm.InstrumentID}");
            LogAndDisplay($"# InstrumentManufacturer:    {pm.InstrumentManufacturer}");
            LogAndDisplay($"# InstrumentType:            {pm.InstrumentType}");
            LogAndDisplay($"# InstrumentSerialNumber:    {pm.InstrumentSerialNumber}");
            LogAndDisplay($"# InstrumentFirmwareVersion: {pm.InstrumentFirmwareVersion}");
            LogAndDisplay($"# DetectorType:              {pm.DetectorType}");
            LogAndDisplay($"# DetectorSerialNumber:      {pm.DetectorSerialNumber}");
            LogAndDisplay($"# DetectorCalibration:       {pm.DetectorCalibration}");
            LogAndDisplay($"# SensorType:                {pm.SensorType} - {pm.SensorSubtype} - {pm.SensorFlags}");
            LogAndDisplay($"# WavelengthRange:           {pm.GetMinimumWavelength()} nm - {pm.GetMaximumWavelength()} nm");
            LogAndDisplay($"# PowerRange-Range:          {pm.GetMinimumRange()} W - {pm.GetMaximumRange()} W");
            //LogAndDisplay($"# CurrentRanges:             {pm.GetCurrentRanges().Length}");
            LogAndDisplay("##############################################################"); 
            LogOnly($"wavelength (nm), responsivity ({pm.ResponsivityUnit})");

            //for (int w = minWl; w <= maxWl; w += 1)
            //{
            //    pm.SetWavelength(w);
            //    DisplayOnly($"{pm.GetWavelength(),4} nm  ->  {pm.GetResponsivity():F7} {pm.ResponsivityUnit}");
            //    LogOnly($"{pm.GetWavelength()}, {pm.GetResponsivity()}");
            //}

            streamWriter.Close();

            SCPIquery("*IDN?");
            SCPIquery("*OPC?");
            SCPIquery("SYSTEM:SENSOR:IDN?");
            SCPIquery("SYSTEM:VERSION?");
            //SCPIwrite("SENSE:CORRECTION:WAVELENGTH 632.8 nm");
            //SCPIquery("SENSE:CORRECTION:WAVELENGTH?");
            //SCPIquery("SENSE:CURRENT:DC:RANGE:UPPER?");
            //SCPIquery("SENSE:CURRENT:DC:RANGE:UPPER? MIN");
            //SCPIquery("SENSE:CURRENT:DC:RANGE:UPPER? MAX");
            //SCPIquery("SENSE:POWER:DC:RANGE:UPPER?");
            //SCPIquery("SENSE:POWER:DC:RANGE:UPPER? MIN");
            //SCPIquery("SENSE:POWER:DC:RANGE:UPPER? MAX");
            //SCPIquery("STATUS:OPERATION:EVENT?");
            //SCPIquery("STATUS:OPERATION:CONDITION?");
            //SCPIquery("SENSE:VOLTAGE:DC:RANGE:UPPER?");
            //SCPIquery("SENSE:VOLTAGE:DC:RANGE:UPPER? MIN");
            //SCPIquery("SENSE:VOLTAGE:DC:RANGE:UPPER? MAX");
            //TestSCPIquery("SENSE:AVERAGE:COUNT?");
            SCPIquery("SYSTEM:DATE?");
            //SCPIwrite("*CLS");
            //SCPIquery("*TST?");
            //SCPIwrite("*RST");
            //Thread.Sleep(5000);
            SCPIquery("STATUS:OPERATION:EVENT?");
            SCPIquery("STATUS:OPERATION:CONDITION?");
            SCPIquery("SENSE:CURRENT:DC:RANGE:AUTO?");
            SCPIquery("SENSE:CURRENT:DC:RANGE:UPPER?");

            SCPIwrite("SENSE:CURRENT:DC:RANGE:UPPER 5.0e-6");
            SCPIquery("SENSE:CURRENT:DC:RANGE:UPPER?");
            SCPIquery("SENSE:CURRENT:DC:RANGE:UPPER? MINIMUM");
            SCPIquery("SENSE:CURRENT:DC:RANGE:UPPER? MAXIMUM");

            pm.SetWavelength(633);

            Console.WriteLine();
            for (int i = 0; i < 3; i++)
            {
                Thread.Sleep(500);
                Console.WriteLine();
                DisplayOnly($"power:       {pm.MeasurePower()} W");
                DisplayOnly($"current:     {pm.MeasureCurrent()} A");
                DisplayOnly($"voltage:     {pm.MeasureVoltage()} V");
                //DisplayOnly($"temperature: {pm.MeasureTemperature()} °C");
                //DisplayOnly($"energy:      {pm.MeasureEnergy()} J");
                //DisplayOnly($"frequency:   {pm.MeasureFrequency()} Hz");
            }

            return 0;

            /***************************************************/
            void LogAndDisplay(string line)
            {
                DisplayOnly(line);
                LogOnly(line);
            }
            /***************************************************/
            void LogOnly(string line)
            {
                streamWriter.WriteLine(line);
                streamWriter.Flush();
            }
            /***************************************************/
            void DisplayOnly(string line)
            {
                Console.WriteLine(line);
            }
            /***************************************************/
            void SCPIquery(string command)
            {
                Console.WriteLine($"'{command}' -> '{pm.ScpiWriteRead(command)}'");
            }
            /***************************************************/
            void SCPIwrite(string command)
            {
                pm.ScpiWrite(command);
                Console.WriteLine($"'{command}'");
            }
            /***************************************************/
        }
    }
}

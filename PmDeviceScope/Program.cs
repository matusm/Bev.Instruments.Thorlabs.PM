using Bev.Instruments.Thorlabs.PM;
using System;
using System.Globalization;
using System.IO;

namespace PmDeviceScope
{
    internal class Program
    {
        static int Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Console.WriteLine("Searching for devices ...");
            DiscoverPM dpm = new DiscoverPM(); // time expensive!
            if (dpm.NumberOfDevices == 0)
            {
                Console.WriteLine("No powermeter found!");
                return 1;
            }
            Console.WriteLine($"Number of detected devices: {dpm.NumberOfDevices}");
            foreach (var s in dpm.NamesOfDevices)
                Console.WriteLine($"   {s}");
            Console.WriteLine();

            ThorlabsPM pm = new ThorlabsPM(dpm.LastDevice);

            string csvFileName = $"TLPM_{pm.DetectorType}_{pm.DetectorSerialNumber}.csv";
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
            LogAndDisplay($"# CurrentRange-Range:        {pm.GetMinimumCurrentRange()} A - {pm.GetMaximumCurrentRange()} A");
            //LogAndDisplay($"# CurrentRanges:             {pm.GetCurrentRanges().Length}");
            LogAndDisplay($"# Wavelength:                {pm.GetWavelength()} nm");
            LogAndDisplay("##############################################################");
            LogOnly($"wavelength (nm), responsivity ({pm.ResponsivityUnit})");

            for (int w = minWl; w <= maxWl; w += 1)
            {
                pm.SetWavelength(w);
                DisplayOnly($"{pm.GetWavelength(),5} nm  ->  {pm.GetResponsivity():F7} {pm.ResponsivityUnit}");
                LogOnly($"{pm.GetWavelength()}, {pm.GetResponsivity()}");
            }

            streamWriter.Close();
            pm.SetWavelength(633);
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


        }
    }
}

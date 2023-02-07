using Thorlabs.TLPM_32.Interop;
using System.Text;
using System;
using System.Threading;

namespace Bev.Instruments.Thorlabs.PM
{
    public class ThorlabsPM
    {
        private const int MillisecondsTimeout = 10;     // between SCPI write and read
        private const int Capacity = 1024;              // of StringBuilder buffers
        private readonly TLPM pm;

        public ThorlabsPM(string resourceName)
        {
            pm = new TLPM(resourceName, true, true);
            DevicePort = resourceName;
            UpdateInstrumentInfo();
            UpdateSensorInfo();
            UpdateDriverRevision();
        }

        public string DevicePort { get; }
        public string DriverRevision { get; private set; }
        public string InstrumentManufacturer { get; private set; }
        public string InstrumentType { get; private set; }
        public string InstrumentSerialNumber { get; private set; }
        public string InstrumentFirmwareVersion { get; private set; }
        public string InstrumentID => $"{InstrumentType} v{InstrumentFirmwareVersion} SN:{InstrumentSerialNumber} @ {DevicePort}";
        public string DetectorSerialNumber { get; private set; }
        public string DetectorType { get; private set; }
        public string DetectorCalibration { get; private set; }
        public string ResponsivityUnit => GetResponsivityUnit();
        public SensorType SensorType { get; private set; }
        public SensorSubtype SensorSubtype { get; private set; }
        public SensorFlags SensorFlags { get; private set; }

        public void SetAdapterPhotodiode() => SetAdapter("PHOTODIODE"); // you must quit after this command!

        public void SetAdapterThermal() => SetAdapter("THERMAL"); // you must quit after this command!

        public void SetAdapterPyro() => SetAdapter("PYRO"); // you must quit after this command!

        private void SetAdapter(string v)
        {
            ScpiWrite($"INPUT:ADAPTER:TYPE {v}");
        } // you must quit after this command!

        public double MeasurePower()
        {
            if (SensorFlags.HasFlag(SensorFlags.IsPowerSensor))
            {
                //pm.measPower(out double value);
                //return value;
                ScpiWrite("CONFIGURE:SCALAR:POWER");
                if (ScpiError()) return double.NaN;
                return ConvertScpiInf(ParseMyDouble(ScpiWriteRead("READ?")));
            }
            return double.NaN;
        }

        public double MeasureEnergy()
        {
            if (SensorFlags.HasFlag(SensorFlags.IsEnergySensor))
            {
                // pm.measEnergy(out double value);
                ScpiWrite("CONFIGURE:SCALAR:ENERGY");
                if (ScpiError()) return double.NaN;
                return ConvertScpiInf(ParseMyDouble(ScpiWriteRead("READ?")));
            }
            return double.NaN;
        }

        public double MeasureCurrent()
        {
            if (SensorType == SensorType.Photodiode)
            {
                // pm.measCurrent(out double value);
                ScpiWrite("CONFIGURE:SCALAR:CURRENT");
                if (ScpiError()) return double.NaN;
                return ConvertScpiInf(ParseMyDouble(ScpiWriteRead("READ?")));
            }
            return double.NaN;
        }

        public double MeasureVoltage()
        {
            if (SensorType == SensorType.Thermopile || SensorType == SensorType.Pyroelectric)
            {
                // pm.measVoltage(out double value);
                ScpiWrite("CONFIGURE:SCALAR:VOLTAGE");
                if (ScpiError()) return double.NaN;
                return ConvertScpiInf(ParseMyDouble(ScpiWriteRead("READ?")));
            }
            return double.NaN;
        }

        public double MeasureTemperature()
        {
            if (SensorSubtype == SensorSubtype.HasTemperatureSensor || SensorFlags.HasFlag(SensorFlags.HasTemperatureSensor))
            {
                ScpiWrite("CONFIGURE:SCALAR:TEMPERATURE");
                if (ScpiError()) return double.NaN;
                return ConvertScpiInf(ParseMyDouble(ScpiWriteRead("READ?")));
            }
            return double.NaN;
        }

        public double MeasureFrequency()
        {
            ScpiWrite("CONFIGURE:SCALAR:FREQUENCY");
            if (ScpiError()) return double.NaN;
            return ConvertScpiInf(ParseMyDouble(ScpiWriteRead("READ?")));
        }

        public void SetWavelength(double wavelength)
        {
            if (SensorFlags.HasFlag(SensorFlags.IsWavelengthSettable))
                //pm.setWavelength(wavelength);
                ScpiWrite($"SENSE:CORRECTION:WAVELENGTH {wavelength}");
        }

        public double GetWavelength() => GetWavelength(0);

        public double GetMinimumWavelength() => GetWavelength(1);

        public double GetMaximumWavelength() => GetWavelength(2);

        public double GetResponsivity()
        {
            double value = double.NaN;
            if (SensorType == SensorType.Photodiode)
                pm.getPhotodiodeResponsivity(0, out value);
            if (SensorType == SensorType.Thermopile)
                pm.getPhotodiodeResponsivity(0, out value);
            if (SensorType == SensorType.Pyroelectric)
                pm.getPyrosensorResponsivity(0, out value);
            return value;
        }

        public double GetResponsivityForWavelength(double wavelength)
        {
            double oldWavelength = GetWavelength();
            SetWavelength(wavelength);
            double responsivity = GetResponsivity();
            SetWavelength(oldWavelength);
            return responsivity;
        }

        public double GetRange() => GetRange(0);

        public double GetMinimumRange() => GetRange(1);

        public double GetMaximumRange() => GetRange(2);

        // TODO this does not work!
        public double[] GetCurrentRanges()
        {
            double[] values = new double[32];
            pm.getCurrentRanges(values, out ushort rangeCount);
            Console.WriteLine($">>>>>>> {rangeCount}");
            return values; ;
        }

        // TODO this does not work!
        public bool GetFilterPosition()
        {
            pm.getFilterPosition(out bool value);
            return value;
        }

        public void ScpiWrite(string command) => pm.writeRaw(command);

        public string ScpiRead()
        {
            uint bufferSize = Capacity;
            StringBuilder sb = new StringBuilder((int)bufferSize);
            pm.readRaw(sb, bufferSize, out uint returnSize);
            return sb.ToString().Trim(new char[] { '\r', '\n', ' ' });
        }

        public string ScpiWriteRead(string command) => ScpiWriteRead(command, true);

        public string ScpiWriteRead(string command, bool deviceClear)
        {
            try
            {
                if(deviceClear) ScpiWrite("*CLS");
                ScpiWrite(command);
                Thread.Sleep(MillisecondsTimeout);
                return ScpiRead();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public bool ScpiError() => !ScpiGetErrorStatus().Contains("+0,");

        private string ScpiGetErrorStatus() => ScpiWriteRead("SYSTEM:ERROR:NEXT?", false);

        private double GetWavelength(short attribute)
        {
            //pm.getWavelength(attribute, out double value);
            if (!SensorFlags.HasFlag(SensorFlags.IsWavelengthSettable))
                return double.NaN;
            switch (attribute)
            {
                case 0:
                    return ParseMyDouble(ScpiWriteRead("SENSE:CORRECTION:WAVELENGTH?"));
                case 1:
                    return ParseMyDouble(ScpiWriteRead("SENSE:CORRECTION:WAVELENGTH? MINIMUM"));
                case 2:
                    return ParseMyDouble(ScpiWriteRead("SENSE:CORRECTION:WAVELENGTH? MAXIMUM"));
                default:
                    return double.NaN;
            }
        }

        private double GetRange(short attribute)
        {
            if (SensorFlags.HasFlag(SensorFlags.IsPowerSensor))
            {
                pm.getPowerRange(attribute, out double value);
                return value;
            }
            if (SensorFlags.HasFlag(SensorFlags.IsEnergySensor))
            {
                pm.getEnergyRange(attribute, out double value);
                return value;
            }
            return double.NaN;
        }

        //private void UpdateInstrumentInfo()
        //{
        //    StringBuilder sb1 = new StringBuilder(Capacity);
        //    StringBuilder sb2 = new StringBuilder(Capacity);
        //    StringBuilder sb3 = new StringBuilder(Capacity);
        //    StringBuilder sb4 = new StringBuilder(Capacity);
        //    pm.identificationQuery(sb1, sb2, sb3, sb4);
        //    InstrumentManufacturer = sb1.ToString();
        //    InstrumentType = sb2.ToString();
        //    InstrumentSerialNumber = sb3.ToString();
        //    InstrumentFirmwareVersion = sb4.ToString();
        //}

        private void UpdateInstrumentInfo()
        {
            string str = ScpiWriteRead("*IDN?");
            string[] token = str.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (token.Length != 4)
                return;
            InstrumentManufacturer = token[0];
            InstrumentType = token[1];
            InstrumentSerialNumber = token[2];
            InstrumentFirmwareVersion = token[3];
        }

        //private void UpdateSensorInfo()
        //{
        //    StringBuilder sb1 = new StringBuilder(Capacity);
        //    StringBuilder sb2 = new StringBuilder(Capacity);
        //    StringBuilder sb3 = new StringBuilder(Capacity);
        //    pm.getSensorInfo(sb1, sb2, sb3, out short sensorType, out short sensorSubtype, out short sensorFlags);
        //    DetectorType = sb1.ToString();
        //    DetectorSerialNumber = sb2.ToString();
        //    DetectorCalibration = sb3.ToString();
        //    SensorType = (SensorType)sensorType;
        //    SensorSubtype = (SensorSubtype)sensorSubtype;
        //    SensorFlags = (SensorFlags)sensorFlags;
        //}

        private void UpdateSensorInfo()
        {
            string str = ScpiWriteRead("SYSTEM:SENSOR:IDN?");
            string[] token = str.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (token.Length != 6)
                return;
            DetectorType = token[0];
            DetectorSerialNumber = token[1];
            DetectorCalibration = token[2];
            SensorType = (SensorType)short.Parse(token[3]);
            SensorSubtype = (SensorSubtype)short.Parse(token[4]);
            SensorFlags = (SensorFlags)short.Parse(token[5]);
        }

        private void UpdateDriverRevision()
        {
            StringBuilder sb1 = new StringBuilder(Capacity);
            StringBuilder sb2 = new StringBuilder(Capacity);
            pm.revisionQuery(sb1, sb2);
            DriverRevision = $"{sb1.ToString().Trim(new char[] { '\r', '\n' })}";
        }

        private string GetResponsivityUnit()
        {
            string unit = "a.u.";
            if (SensorType == SensorType.Photodiode)
                unit = "A/W";
            if (SensorType == SensorType.Thermopile)
                unit = "V/W";
            if (SensorType == SensorType.Pyroelectric)
                unit = "V/J";
            return unit;
        }

        private double ParseMyDouble(string str)
        {
            if (double.TryParse(str, out double value))
                return value;
            return double.NaN;
        }

        private double ConvertScpiInf(double value)
        {
            if (value >= 9.9E37) 
                return double.PositiveInfinity;
            if (value <= -9.9E37)
                return double.NegativeInfinity;
            return value;
        }
    }
}

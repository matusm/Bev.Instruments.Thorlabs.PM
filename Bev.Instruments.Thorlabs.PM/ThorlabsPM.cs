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

        #region syntactic sugar to mimic a P9710 for calibration as a current meter
        public double GetCurrent() => MeasureCurrent();

        public MeasurementRange GetMeasurementRange() => EstimateMeasurementRange(GetCurrentRange());

        public MeasurementRange EstimateMeasurementRange(double current)
        {
            if (double.IsNaN(current)) return MeasurementRange.Unknown;
            current = Math.Abs(current);
            if (current > 5.5E-3) return MeasurementRange.RangeOverflow;
            if (current > 5.5E-4) return MeasurementRange.Range03;
            if (current > 5.5E-5) return MeasurementRange.Range04;
            if (current > 5.5E-6) return MeasurementRange.Range05;
            if (current > 5.5E-7) return MeasurementRange.Range06;
            if (current > 5.5E-8) return MeasurementRange.Range07;
            return MeasurementRange.Range08;
        }

        public void SetMeasurementRange(MeasurementRange measurementRange) => SetCurrentRange(measurementRange);

        public void SelectAutoRange() => ScpiWrite("CURRENT:RANGE:AUTO ON");

        public void DeselectAutoRange() => ScpiWrite("CURRENT:RANGE:AUTO OFF");
        #endregion

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
                ScpiWrite($"SENSE:CORRECTION:WAVELENGTH {wavelength}");
        }

        public double GetWavelength() => GetWavelength(0);

        public double GetMinimumWavelength() => GetWavelength(1);

        public double GetMaximumWavelength() => GetWavelength(2);

        public double GetResponsivity()  //!!!
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

        public double GetCurrentRange() => GetCurrentRange(0);

        public double GetMinimumCurrentRange() => GetCurrentRange(1);

        public double GetMaximumCurrentRange() => GetCurrentRange(2);

        public void SetCurrentRange(double value)
        {
            if (SensorType == SensorType.Photodiode)
                ScpiWrite($"SENSE:CURRENT:RANGE:UPPER {value}");
        }

        public void SetCurrentRange(MeasurementRange measurementRange)
        {
            // this works for the PM100D only
            double upperValue = 5.5e-3;
            switch (measurementRange)
            {
                case MeasurementRange.Unknown:
                    break;
                case MeasurementRange.RangeOverflow:
                    break;
                case MeasurementRange.Range03:
                    upperValue = 5.5e-3;
                    break;
                case MeasurementRange.Range04:
                    upperValue = 5.5e-4;
                    break;
                case MeasurementRange.Range05:
                    upperValue = 5.5e-5;
                    break;
                case MeasurementRange.Range06:
                    upperValue = 5.5e-6;
                    break;
                case MeasurementRange.Range07:
                    upperValue = 5.5e-7;
                    break;
                case MeasurementRange.Range08:
                    upperValue = 5.5e-8;
                    break;
                default:
                    break;
            }
            SetCurrentRange(upperValue);
        }

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

        public void ScpiWrite(string command) => pm.writeRaw(command);  //!!!

        public string ScpiRead()  //!!!
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
                if (deviceClear) ScpiWrite("*CLS");
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

        private double GetCurrentRange(short attribute)
        {
            if (SensorType != SensorType.Photodiode)
                return double.NaN;
            switch (attribute)
            {
                case 0:
                    return ParseMyDouble(ScpiWriteRead("SENSE:CURRENT:DC:RANGE:UPPER?"));
                case 1:
                    return ParseMyDouble(ScpiWriteRead("SENSE:CURRENT:DC:RANGE:UPPER? MINIMUM"));
                case 2:
                    return ParseMyDouble(ScpiWriteRead("SENSE:CURRENT:DC:RANGE:UPPER? MAXIMUM"));
                default:
                    return double.NaN;
            }
        }

        private double GetRange(short attribute)  //!!!
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

        private void UpdateDriverRevision() //!!!
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
            if (value == 9.9E37)
                return double.PositiveInfinity;
            if (value == -9.9E37)
                return double.NegativeInfinity;
            if (value == 9.91E37)
                return double.NaN;
            return value;
        }
    }
}

using System;

namespace Bev.Instruments.Thorlabs.PM
{
    public enum SensorType : short
    {
        None = 0x00,
        Photodiode = 0x01,
        Thermopile = 0x02,
        Pyroelectric = 0x03
    }

    public enum SensorSubtype : short
    {
        None = 0x00,                // no detector
        Adapter = 0x01,             // Detector adapter
        Standard = 0x02,            // Detector
        HasFilter = 0x03,           // Photodiode sensor with integrated filter identified by position
        HasTemperatureSensor = 0x12 // Detector with temperature sensor
    }

    [Flags]
    public enum SensorFlags : short
    {
        None = 0,
        IsPowerSensor = 0x0001,             // Power sensor 
        IsEnergySensor = 0x0002,            // Energy sensor 
        IsResponsivitySettable = 0x0010,    // Responsivity settable 
        IsWavelengthSettable = 0x0020,      // Wavelength settable 
        IsTauSettable = 0x0040,             // Time constant settable 
        HasTemperatureSensor = 0x0100       // With Temperature sensor
    }
}

using CSJ2K.Icc.Types;

namespace CSJ2K.Icc.Tags;

public class ICCMeasurementType : ICCTag
{
    public XYZNumber backing;
    public int flare;
    public int geometry;
    public int illuminant;
    public int observer;
    public int reserved;
    public new int type;

    /// <summary> Construct this tag from its constituant parts</summary>
    /// <param name="signature">tag id</param>
    /// <param name="data">array of bytes</param>
    /// <param name="offset">to data in the data array</param>
    /// <param name="length">of data in the data array</param>
    protected internal ICCMeasurementType(int signature, byte[] data, int offset, int length)
        : base(signature, data, offset, offset + 2 * ICCProfile.int_size)
    {
        type = ICCProfile.getInt(data, offset);
        reserved = ICCProfile.getInt(data, offset + ICCProfile.int_size);
        observer = ICCProfile.getInt(data, offset + ICCProfile.int_size);
        backing = ICCProfile.getXYZNumber(data, offset + ICCProfile.int_size);
        geometry = ICCProfile.getInt(data, offset + ICCProfile.int_size * 3);
        flare = ICCProfile.getInt(data, offset + ICCProfile.int_size);
        illuminant = ICCProfile.getInt(data, offset + ICCProfile.int_size);
    }
}
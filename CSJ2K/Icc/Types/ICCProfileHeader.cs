/// <summary>**************************************************************************
/// 
/// $Id: ICCProfileHeader.java,v 1.1 2002/07/25 14:56:31 grosbois Exp $
/// 
/// Copyright Eastman Kodak Company, 343 State Street, Rochester, NY 14650
/// $Date $
/// ***************************************************************************
/// </summary>

using System;
using System.IO;
using System.Text;

namespace CSJ2K.Icc.Types;

/// <summary>
///     An ICC profile contains a 128-byte header followed by a variable
///     number of tags contained in a tag table. This class models the header
///     portion of the profile.  Most fields in the header are ints.  Some, such
///     as data and version are aggregations of ints. This class provides an api to
///     those fields as well as the definition of standard constants which are used
///     in the header.
/// </summary>
/// <seealso cref="jj2000.j2k.icc.ICCProfile">
/// </seealso>
/// <version>
///     1.0
/// </version>
/// <author>
///     Bruce A. Kern
/// </author>
public class ICCProfileHeader
{
    private const string kdwInputProfile = "scnr";
    private const string kdwDisplayProfile = "mntr";
    private const string kdwRGBData = "RGB ";
    private const string kdwGrayData = "GRAY";
    private const string kdwXYZData = "XYZ ";
    private const string kdwGrayTRCTag = "kTRC";
    private const string kdwRedColorantTag = "rXYZ";
    private const string kdwGreenColorantTag = "gXYZ";
    private const string kdwBlueColorantTag = "bXYZ";
    private const string kdwRedTRCTag = "rTRC";
    private const string kdwGreenTRCTag = "gTRC";

    private const string kdwBlueTRCTag = "bTRC";

    //UPGRADE_NOTE: Final was removed from the declaration of 'eol '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
    private static readonly string eol = Environment.NewLine;

    /// <summary>ICCProfile header byte array. </summary>
    //private byte[] header = null;


    /* Define the set of standard signature and type values. Only
    * those codes required for Restricted ICC use are defined here.
    */
    /// <summary>Profile header signature </summary>
    private static int kdwProfileSignature = ICCProfile.getInt(Encoding.ASCII.GetBytes("acsp"), 0);

    /// <summary>Profile header signature </summary>
    public static int kdwProfileSigReverse = ICCProfile.getInt(Encoding.ASCII.GetBytes("psca"), 0);

    /* Offsets into ICCProfile header byte array. */

    private static readonly int offProfileSize = 0;
    private static readonly int offCMMTypeSignature = offProfileSize + ICCProfile.int_size;
    private static readonly int offProfileVersion = offCMMTypeSignature + ICCProfile.int_size;
    private static readonly int offProfileClass = offProfileVersion + ICCProfileVersion.size;
    private static readonly int offColorSpaceType = offProfileClass + ICCProfile.int_size;
    private static readonly int offPCSType = offColorSpaceType + ICCProfile.int_size;
    private static readonly int offDateTime = offPCSType + ICCProfile.int_size;
    private static readonly int offProfileSignature = offDateTime + ICCDateTime.size;
    private static readonly int offPlatformSignature = offProfileSignature + ICCProfile.int_size;
    private static readonly int offCMMFlags = offPlatformSignature + ICCProfile.int_size;
    private static readonly int offDeviceManufacturer = offCMMFlags + ICCProfile.int_size;
    private static readonly int offDeviceModel = offDeviceManufacturer + ICCProfile.int_size;
    private static readonly int offDeviceAttributes1 = offDeviceModel + ICCProfile.int_size;
    private static readonly int offDeviceAttributesReserved = offDeviceAttributes1 + ICCProfile.int_size;
    private static readonly int offRenderingIntent = offDeviceAttributesReserved + ICCProfile.int_size;
    private static readonly int offPCSIlluminant = offRenderingIntent + ICCProfile.int_size;
    private static readonly int offCreatorSig = offPCSIlluminant + XYZNumber.size;
    private static readonly int offReserved = offCreatorSig + ICCProfile.int_size;

    /// <summary>Size of the header </summary>
    public static int size = offReserved + 44 * ICCProfile.byte_size;

    /// <summary>Header field </summary>
    // Version of the profile format on which
    public ICCDateTime dateTime;

    /// <summary>Header field </summary>
    // Primary platform for which this profile was created
    public int dwCMMFlags;

    /// <summary>Header field </summary>
    // Size of the entire profile in bytes	
    public int dwCMMTypeSignature;

    /// <summary>Header field </summary>
    // Profile/Device class signature
    public int dwColorSpaceType;

    /// <summary>Header field </summary>
    // Desired rendering intent for this profile
    public int dwCreatorSig;

    /// <summary>Header field </summary>
    // Signature of device model
    public int dwDeviceAttributes1;

    /// <summary>Header field </summary>
    // Attributes of the device
    public int dwDeviceAttributesReserved;

    /// <summary>Header field </summary>
    // Flags to indicate various hints for the CMM
    public int dwDeviceManufacturer;

    /// <summary>Header field </summary>
    // Signature of device manufacturer
    public int dwDeviceModel;

    /// <summary>Header field </summary>
    // Colorspace signature
    public int dwPCSType;

    /// <summary>Header field </summary>
    // Must be 'acsp' (0x61637370)
    public int dwPlatformSignature;

    /// <summary>Header field </summary>
    // The preferred CMM for this profile
    public int dwProfileClass;

    /// <summary>Header field </summary>
    // PCS type signature
    public int dwProfileSignature;

    /// <summary>Header field </summary>
    /* Header fields mapped to primitive types. */
    public int dwProfileSize;

    /// <summary>Header field </summary>
    public int dwRenderingIntent;

    /// <summary>Header field </summary>
    // Date and time of profile creation// this profile is based
    public XYZNumber PCSIlluminant; // Illuminant used for this profile

    /// <summary>Header field </summary>
    /* Header fields mapped to ggregate types. */
    public ICCProfileVersion profileVersion;

    /// <summary>Header field </summary>
    // Profile creator signature
    public byte[] reserved = new byte[44]; // 


    /// <summary>Construct and empty header </summary>
    public ICCProfileHeader()
    {
    }

    /// <summary> Construct a header from a complete ICCProfile</summary>
    /// <param name="byte">
    ///     [] -- holds ICCProfile contents
    /// </param>
    public ICCProfileHeader(byte[] data)
    {
        dwProfileSize = ICCProfile.getInt(data, offProfileSize);
        dwCMMTypeSignature = ICCProfile.getInt(data, offCMMTypeSignature);
        dwProfileClass = ICCProfile.getInt(data, offProfileClass);
        dwColorSpaceType = ICCProfile.getInt(data, offColorSpaceType);
        dwPCSType = ICCProfile.getInt(data, offPCSType);
        dwProfileSignature = ICCProfile.getInt(data, offProfileSignature);
        dwPlatformSignature = ICCProfile.getInt(data, offPlatformSignature);
        dwCMMFlags = ICCProfile.getInt(data, offCMMFlags);
        dwDeviceManufacturer = ICCProfile.getInt(data, offDeviceManufacturer);
        dwDeviceModel = ICCProfile.getInt(data, offDeviceModel);
        dwDeviceAttributes1 = ICCProfile.getInt(data, offDeviceAttributesReserved);
        dwDeviceAttributesReserved = ICCProfile.getInt(data, offDeviceAttributesReserved);
        dwRenderingIntent = ICCProfile.getInt(data, offRenderingIntent);
        dwCreatorSig = ICCProfile.getInt(data, offCreatorSig);
        profileVersion = ICCProfile.getICCProfileVersion(data, offProfileVersion);
        dateTime = ICCProfile.getICCDateTime(data, offDateTime);
        PCSIlluminant = ICCProfile.getXYZNumber(data, offPCSIlluminant);

        for (var i = 0; i < reserved.Length; ++i)
            reserved[i] = data[offReserved + i];
    }

    /// <summary> Write out this ICCProfile header to a RandomAccessFile</summary>
    /// <param name="raf">
    ///     sink for data
    /// </param>
    /// <exception cref="IOException">
    /// </exception>
    //UPGRADE_TODO: Class 'java.io.RandomAccessFile' was converted to 'System.IO.FileStream' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javaioRandomAccessFile'"
    public virtual void write(FileStream raf)
    {
        raf.Seek(offProfileSize, SeekOrigin.Begin);
        raf.WriteByte((byte)dwProfileSize);
        raf.Seek(offCMMTypeSignature, SeekOrigin.Begin);
        raf.WriteByte((byte)dwCMMTypeSignature);
        raf.Seek(offProfileVersion, SeekOrigin.Begin);
        profileVersion.write(raf);
        raf.Seek(offProfileClass, SeekOrigin.Begin);
        raf.WriteByte((byte)dwProfileClass);
        raf.Seek(offColorSpaceType, SeekOrigin.Begin);
        raf.WriteByte((byte)dwColorSpaceType);
        raf.Seek(offPCSType, SeekOrigin.Begin);
        raf.WriteByte((byte)dwPCSType);
        raf.Seek(offDateTime, SeekOrigin.Begin);
        dateTime.write(raf);
        raf.Seek(offProfileSignature, SeekOrigin.Begin);
        raf.WriteByte((byte)dwProfileSignature);
        raf.Seek(offPlatformSignature, SeekOrigin.Begin);
        raf.WriteByte((byte)dwPlatformSignature);
        raf.Seek(offCMMFlags, SeekOrigin.Begin);
        raf.WriteByte((byte)dwCMMFlags);
        raf.Seek(offDeviceManufacturer, SeekOrigin.Begin);
        raf.WriteByte((byte)dwDeviceManufacturer);
        raf.Seek(offDeviceModel, SeekOrigin.Begin);
        raf.WriteByte((byte)dwDeviceModel);
        raf.Seek(offDeviceAttributes1, SeekOrigin.Begin);
        raf.WriteByte((byte)dwDeviceAttributes1);
        raf.Seek(offDeviceAttributesReserved, SeekOrigin.Begin);
        raf.WriteByte((byte)dwDeviceAttributesReserved);
        raf.Seek(offRenderingIntent, SeekOrigin.Begin);
        raf.WriteByte((byte)dwRenderingIntent);
        raf.Seek(offPCSIlluminant, SeekOrigin.Begin);
        PCSIlluminant.write(raf);
        raf.Seek(offCreatorSig, SeekOrigin.Begin);
        raf.WriteByte((byte)dwCreatorSig);
        raf.Seek(offReserved, SeekOrigin.Begin);
        raf.Write(reserved, 0, reserved.Length);
        //SupportClass.RandomAccessFileSupport.WriteRandomFile(reserved, raf);
    }


    /// <summary>String representation of class </summary>
    public override string ToString()
    {
        var rep = new StringBuilder("[ICCProfileHeader: ");

        rep.Append(eol + "         ProfileSize: " + Convert.ToString(dwProfileSize, 16));
        rep.Append(eol + "    CMMTypeSignature: " + Convert.ToString(dwCMMTypeSignature, 16));
        rep.Append(eol + "        ProfileClass: " + Convert.ToString(dwProfileClass, 16));
        rep.Append(eol + "      ColorSpaceType: " + Convert.ToString(dwColorSpaceType, 16));
        rep.Append(eol + "           dwPCSType: " + Convert.ToString(dwPCSType, 16));
        rep.Append(eol + "  dwProfileSignature: " + Convert.ToString(dwProfileSignature, 16));
        rep.Append(eol + " dwPlatformSignature: " + Convert.ToString(dwPlatformSignature, 16));
        rep.Append(eol + "          dwCMMFlags: " + Convert.ToString(dwCMMFlags, 16));
        rep.Append(eol + "dwDeviceManufacturer: " + Convert.ToString(dwDeviceManufacturer, 16));
        rep.Append(eol + "       dwDeviceModel: " + Convert.ToString(dwDeviceModel, 16));
        rep.Append(eol + " dwDeviceAttributes1: " + Convert.ToString(dwDeviceAttributes1, 16));
        rep.Append(eol + "   dwRenderingIntent: " + Convert.ToString(dwRenderingIntent, 16));
        rep.Append(eol + "        dwCreatorSig: " + Convert.ToString(dwCreatorSig, 16));
        rep.Append(eol + "      profileVersion: " + profileVersion);
        rep.Append(eol + "            dateTime: " + dateTime);
        rep.Append(eol + "       PCSIlluminant: " + PCSIlluminant);
        return rep.Append("]").ToString();
    }

    /* end class ICCProfileHeader */
}
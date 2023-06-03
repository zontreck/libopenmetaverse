/// <summary>**************************************************************************
/// 
/// $Id: ICCTag.java,v 1.1 2002/07/25 14:56:37 grosbois Exp $
/// 
/// Copyright Eastman Kodak Company, 343 State Street, Rochester, NY 14650
/// $Date $
/// ***************************************************************************
/// </summary>

using System;
using System.Text;

namespace CSJ2K.Icc.Tags;

/// <summary>
///     An ICC profile contains a 128-byte header followed by a variable
///     number of tags contained in a tag table. Each tag is a structured
///     block of ints. The tags share a common format on disk starting with
///     a signature, an offset to the tag data, and a length of the tag data.
///     The tag data itself is found at the given offset in the file and
///     consists of a tag type int, followed by a reserved int, followed by
///     a data block, the structure of which is unique to the tag type.
///     <p>
///         This class is the abstract super class of all tags. It models that
///         part of the structure which is common among tags of all types.
///         <p>
///             It also contains the definitions of the various tag types.
/// </summary>
/// <seealso cref="jj2000.j2k.icc.tags.ICCTagTable">
/// </seealso>
/// <version>
///     1.0
/// </version>
/// <author>
///     Bruce A. Kern
/// </author>
public abstract class ICCTag
{
    // Tag Signature Strings
    private const string sdwCprtSignature = "cprt";
    private const string sdwDescSignature = "desc";
    private const string sdwWtPtSignature = "wtpt";
    private const string sdwBkPtSignature = "bkpt";
    private const string sdwRXYZSignature = "rXYZ";
    private const string sdwGXYZSignature = "gXYZ";
    private const string sdwBXYZSignature = "bXYZ";
    private const string sdwKXYZSignature = "kXYZ";
    private const string sdwRTRCSignature = "rTRC";
    private const string sdwGTRCSignature = "gTRC";
    private const string sdwBTRCSignature = "bTRC";
    private const string sdwKTRCSignature = "kTRC";
    private const string sdwDmndSignature = "dmnd";
    private const string sdwDmddSignature = "dmdd";

    // Tag Type Strings
    private const string sdwTextDescType = "desc";
    private const string sdwTextType = "text";
    private const string sdwCurveType = "curv";
    private const string sdwCurveTypeReverse = "vruc";
    private const string sdwXYZType = "XYZ ";
    private const string sdwXYZTypeReverse = " ZYX";
    private const string sdwMeasurementType = "meas";
    private const string sdwSignatureType = "sig ";
    private const string sdwViewType = "view";
    private const string sdwDataType = "data";

    // Tag Signatures
    private static readonly int kdwCprtSignature;
    private static readonly int kdwDescSignature;
    private static readonly int kdwWtPtSignature;
    private static readonly int kdwBkPtSignature;
    private static readonly int kdwRXYZSignature;
    private static readonly int kdwGXYZSignature;
    private static readonly int kdwBXYZSignature;
    private static readonly int kdwKXYZSignature;
    private static readonly int kdwRTRCSignature;
    private static readonly int kdwGTRCSignature;
    private static readonly int kdwBTRCSignature;
    private static readonly int kdwKTRCSignature;
    private static readonly int kdwDmndSignature;
    private static readonly int kdwDmddSignature;

    // Tag Types
    private static readonly int kdwTextDescType;
    private static readonly int kdwTextType;
    private static readonly int kdwCurveType;
    private static readonly int kdwCurveTypeReverse;
    private static readonly int kdwXYZType;
    private static readonly int kdwXYZTypeReverse;
    private static readonly int kdwMeasurementType;
    private static readonly int kdwSignatureType;
    private static readonly int kdwViewType;
    private static readonly int kdwDataType;

    /// <summary>size of the tag data in the array </summary>
    public int count;

    /// <summary>Tag data                          </summary>
    public byte[] data;

    /// <summary>offset to tag data in the array   </summary>
    // Tag type
    public int offset;

    /// <summary>Tag id                            </summary>
    public int signature;

    /// <summary>Tag type                          </summary>
    // Tag signature
    public int type;

    /* end class ICCTag */
    static ICCTag()
    {
        kdwCprtSignature = GetTagInt(sdwCprtSignature);
        kdwDescSignature = GetTagInt(sdwDescSignature);
        kdwWtPtSignature = GetTagInt(sdwWtPtSignature);
        kdwBkPtSignature = GetTagInt(sdwBkPtSignature);
        kdwRXYZSignature = GetTagInt(sdwRXYZSignature);
        kdwGXYZSignature = GetTagInt(sdwGXYZSignature);
        kdwBXYZSignature = GetTagInt(sdwBXYZSignature);
        kdwKXYZSignature = GetTagInt(sdwKXYZSignature);
        kdwRTRCSignature = GetTagInt(sdwRTRCSignature);
        kdwGTRCSignature = GetTagInt(sdwGTRCSignature);
        kdwBTRCSignature = GetTagInt(sdwBTRCSignature);
        kdwKTRCSignature = GetTagInt(sdwKTRCSignature);
        kdwDmndSignature = GetTagInt(sdwDmndSignature);
        kdwDmddSignature = GetTagInt(sdwDmddSignature);
        kdwTextDescType = GetTagInt(sdwTextDescType);
        kdwTextType = GetTagInt(sdwTextType);
        kdwCurveType = GetTagInt(sdwCurveType);
        kdwCurveTypeReverse = GetTagInt(sdwCurveTypeReverse);
        kdwXYZType = GetTagInt(sdwXYZType);
        kdwXYZTypeReverse = GetTagInt(sdwXYZTypeReverse);
        kdwMeasurementType = GetTagInt(sdwMeasurementType);
        kdwSignatureType = GetTagInt(sdwSignatureType);
        kdwViewType = GetTagInt(sdwViewType);
        kdwDataType = GetTagInt(sdwDataType);
    }


    /// <summary> Ued by subclass initialization to store the state common to all tags</summary>
    /// <param name="signature">
    ///     tag being created
    /// </param>
    /// <param name="data">
    ///     byte array containg embedded tag data
    /// </param>
    /// <param name="offset">
    ///     to tag data in the array
    /// </param>
    /// <param name="count">
    ///     size of tag data in bytes
    /// </param>
    protected internal ICCTag(int signature, byte[] data, int offset, int count)
    {
        this.signature = signature;
        this.data = data;
        this.offset = offset;
        this.count = count;
        type = ICCProfile.getInt(data, offset);
    }

    /// <summary> Create a string representation of the tag type</summary>
    /// <param name="type">
    ///     input
    /// </param>
    /// <returns>
    ///     String representation of the type
    /// </returns>
    public static string typeString(int type)
    {
        if (type == kdwTextDescType)
            return sdwTextDescType;
        if (type == kdwTextType)
            return sdwTextDescType;
        if (type == kdwCurveType)
            return sdwCurveType;
        if (type == kdwCurveTypeReverse)
            return sdwCurveTypeReverse;
        if (type == kdwXYZType)
            return sdwXYZType;
        if (type == kdwXYZTypeReverse)
            return sdwXYZTypeReverse;
        if (type == kdwMeasurementType)
            return sdwMeasurementType;
        if (type == kdwSignatureType)
            return sdwSignatureType;
        if (type == kdwViewType)
            return sdwViewType;
        if (type == kdwDataType)
            return sdwDataType;
        return "bad tag type";
    }


    /// <summary> Create a string representation of the signature</summary>
    /// <param name="signature">
    ///     input
    /// </param>
    /// <returns>
    ///     String representation of the signature
    /// </returns>
    public static string signatureString(int signature)
    {
        if (signature == kdwCprtSignature)
            return sdwCprtSignature;
        if (signature == kdwDescSignature)
            return sdwDescSignature;
        if (signature == kdwWtPtSignature)
            return sdwWtPtSignature;
        if (signature == kdwBkPtSignature)
            return sdwBkPtSignature;
        if (signature == kdwRXYZSignature)
            return sdwRXYZSignature;
        if (signature == kdwGXYZSignature)
            return sdwGXYZSignature;
        if (signature == kdwBXYZSignature)
            return sdwBXYZSignature;
        if (signature == kdwRTRCSignature)
            return sdwRTRCSignature;
        if (signature == kdwGTRCSignature)
            return sdwGTRCSignature;
        if (signature == kdwBTRCSignature)
            return sdwBTRCSignature;
        if (signature == kdwKTRCSignature)
            return sdwKTRCSignature;
        if (signature == kdwDmndSignature)
            return sdwDmndSignature;
        if (signature == kdwDmddSignature)
            return sdwDmddSignature;
        return "bad tag signature";
    }


    /// <summary> Factory method for creating a tag of a specific type.</summary>
    /// <param name="signature">
    ///     tag to create
    /// </param>
    /// <param name="data">
    ///     byte array containg embedded tag data
    /// </param>
    /// <param name="offset">
    ///     to tag data in the array
    /// </param>
    /// <param name="count">
    ///     size of tag data in bytes
    /// </param>
    /// <returns>
    ///     specified ICCTag
    /// </returns>
    public static ICCTag createInstance(int signature, byte[] data, int offset, int count)
    {
        var type = ICCProfile.getInt(data, offset);

        if (type == kdwTextDescType)
            return new ICCTextDescriptionType(signature, data, offset, count);
        if (type == kdwTextType)
            return new ICCTextType(signature, data, offset, count);
        if (type == kdwXYZType)
            return new ICCXYZType(signature, data, offset, count);
        if (type == kdwXYZTypeReverse)
            return new ICCXYZTypeReverse(signature, data, offset, count);
        if (type == kdwCurveType)
            return new ICCCurveType(signature, data, offset, count);
        if (type == kdwCurveTypeReverse)
            return new ICCCurveTypeReverse(signature, data, offset, count);
        if (type == kdwMeasurementType)
            return new ICCMeasurementType(signature, data, offset, count);
        if (type == kdwSignatureType)
            return new ICCSignatureType(signature, data, offset, count);
        if (type == kdwViewType)
            return new ICCViewType(signature, data, offset, count);
        if (type == kdwDataType)
            return new ICCDataType(signature, data, offset, count);
        throw new ArgumentException("bad tag type: " + Encoding.ASCII.GetString(BitConverter.GetBytes(type)) + "(" +
                                    type + ")");
    }

    public override string ToString()
    {
        return signatureString(signature) + ":" + typeString(type);
    }

    private static int GetTagInt(string tag)
    {
        var tagBytes = Encoding.ASCII.GetBytes(tag);
        Array.Reverse(tagBytes);
        return BitConverter.ToInt32(tagBytes, 0);
    }
}
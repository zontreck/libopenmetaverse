/// <summary>**************************************************************************
/// 
/// $Id: ICCTagTable.java,v 1.1 2002/07/25 14:56:37 grosbois Exp $
/// 
/// Copyright Eastman Kodak Company, 343 State Street, Rochester, NY 14650
/// $Date $
/// ***************************************************************************
/// </summary>

using System;
using System.Collections;
using System.IO;
using System.Text;
using CSJ2K.Color;
using CSJ2K.Icc.Types;

namespace CSJ2K.Icc.Tags;

/// <summary>
///     This class models an ICCTagTable as a HashTable which maps
///     ICCTag signatures (as Integers) to ICCTags.
///     On disk the tag table exists as a byte array conventionally aggragted into a
///     structured sequence of types (bytes, shorts, ints, and floats.  The first four bytes
///     are the integer count of tags in the table.  This is followed by an array of triplets,
///     one for each tag. The triplets each contain three integers, which are the tag signature,
///     the offset of the tag in the byte array and the length of the tag in bytes.
///     The tag data follows.  Each tag consists of an integer (4 bytes) tag type, a reserved integer
///     and the tag data, which varies depending on the tag.
/// </summary>
/// <seealso cref="jj2000.j2k.icc.tags.ICCTag">
/// </seealso>
/// <version>
///     1.0
/// </version>
/// <author>
///     Bruce A. Kern
/// </author>
[Serializable]
public class ICCTagTable : Hashtable
{
    //UPGRADE_NOTE: Final was removed from the declaration of 'eol '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
    private static readonly string eol = Environment.NewLine;

    //UPGRADE_NOTE: Final was removed from the declaration of 'offTagCount '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
    //UPGRADE_NOTE: The initialization of  'offTagCount' was moved to static method 'icc.tags.ICCTagTable'. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1005'"
    private static readonly int offTagCount;

    //UPGRADE_NOTE: Final was removed from the declaration of 'offTags '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
    //UPGRADE_NOTE: The initialization of  'offTags' was moved to static method 'icc.tags.ICCTagTable'. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1005'"
    private static readonly int offTags;

    private int tagCount;

    //UPGRADE_NOTE: Final was removed from the declaration of 'trios '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
    private ArrayList trios = ArrayList.Synchronized(new ArrayList(10));

    /* end class ICCTagTable */
    static ICCTagTable()
    {
        offTagCount = ICCProfileHeader.size;
        offTags = offTagCount + ICCProfile.int_size;
    }


    /// <summary> Ctor used by factory method.</summary>
    /// <param name="byte">
    ///     raw tag data
    /// </param>
    protected internal ICCTagTable(byte[] data)
    {
        tagCount = ICCProfile.getInt(data, offTagCount);

        var offset = offTags;
        for (var i = 0; i < tagCount; ++i)
        {
            var signature = ICCProfile.getInt(data, offset);
            var tagOffset = ICCProfile.getInt(data, offset + ICCProfile.int_size);
            var length = ICCProfile.getInt(data, offset + 2 * ICCProfile.int_size);
            trios.Add(new Triplet(signature, tagOffset, length));
            offset += 3 * ICCProfile.int_size;
        }


        var Enum = trios.GetEnumerator();
        //UPGRADE_TODO: Method 'java.util.Enumeration.hasMoreElements' was converted to 'System.Collections.IEnumerator.MoveNext' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javautilEnumerationhasMoreElements'"
        while (Enum.MoveNext())
        {
            //UPGRADE_TODO: Method 'java.util.Enumeration.nextElement' was converted to 'System.Collections.IEnumerator.Current' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javautilEnumerationnextElement'"
            var trio = (Triplet)Enum.Current;
            var tag = ICCTag.createInstance(trio.signature, data, trio.offset, trio.count);
            object tempObject;
            tempObject = this[tag.signature];
            this[tag.signature] = tag;
            var generatedAux2 = tempObject;
        }
    }

    /// <summary> Representation of a tag table</summary>
    /// <returns>
    ///     String
    /// </returns>
    //UPGRADE_NOTE: The equivalent of method 'java.util.Hashtable.toString' is not an override method. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1143'"
    public override string ToString()
    {
        var rep = new StringBuilder("[ICCTagTable containing " + tagCount + " tags:");
        var body = new StringBuilder("  ");
        var keys = Keys.GetEnumerator();
        //UPGRADE_TODO: Method 'java.util.Enumeration.hasMoreElements' was converted to 'System.Collections.IEnumerator.MoveNext' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javautilEnumerationhasMoreElements'"
        while (keys.MoveNext())
        {
            //UPGRADE_TODO: Method 'java.util.Enumeration.nextElement' was converted to 'System.Collections.IEnumerator.Current' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javautilEnumerationnextElement'"
            var key = (int)keys.Current;
            var tag = (ICCTag)this[key];
            body.Append(eol).Append(tag);
        }

        rep.Append(ColorSpace.indent("  ", body));
        return rep.Append("]").ToString();
    }


    /// <summary> Factory method for creating a tag table from raw input.</summary>
    /// <param name="byte">
    ///     array of unstructured data representing a tag
    /// </param>
    /// <returns>
    ///     ICCTagTable
    /// </returns>
    public static ICCTagTable createInstance(byte[] data)
    {
        var tags = new ICCTagTable(data);
        return tags;
    }


    /// <summary> Output the table to a disk</summary>
    /// <param name="raf">
    ///     RandomAccessFile which receives the table.
    /// </param>
    /// <exception cref="IOException">
    /// </exception>
    //UPGRADE_TODO: Class 'java.io.RandomAccessFile' was converted to 'System.IO.FileStream' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javaioRandomAccessFile'"
    public virtual void write(FileStream raf)
    {
        var ntags = trios.Count;

        var countOff = ICCProfileHeader.size;
        var tagOff = countOff + ICCProfile.int_size;
        var dataOff = tagOff + 3 * ntags * ICCProfile.int_size;

        raf.Seek(countOff, SeekOrigin.Begin);
        BinaryWriter temp_BinaryWriter;
        temp_BinaryWriter = new BinaryWriter(raf);
        temp_BinaryWriter.Write(ntags);

        var currentTagOff = tagOff;
        var currentDataOff = dataOff;

        var enum_Renamed = trios.GetEnumerator();
        //UPGRADE_TODO: Method 'java.util.Enumeration.hasMoreElements' was converted to 'System.Collections.IEnumerator.MoveNext' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javautilEnumerationhasMoreElements'"
        while (enum_Renamed.MoveNext())
        {
            //UPGRADE_TODO: Method 'java.util.Enumeration.nextElement' was converted to 'System.Collections.IEnumerator.Current' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javautilEnumerationnextElement'"
            var trio = (Triplet)enum_Renamed.Current;
            var tag = (ICCTag)this[trio.signature];

            raf.Seek(currentTagOff, SeekOrigin.Begin);
            BinaryWriter temp_BinaryWriter2;
            temp_BinaryWriter2 = new BinaryWriter(raf);
            temp_BinaryWriter2.Write(tag.signature);
            BinaryWriter temp_BinaryWriter3;
            temp_BinaryWriter3 = new BinaryWriter(raf);
            temp_BinaryWriter3.Write(currentDataOff);
            BinaryWriter temp_BinaryWriter4;
            temp_BinaryWriter4 = new BinaryWriter(raf);
            temp_BinaryWriter4.Write(tag.count);
            currentTagOff += 3 * Triplet.size;

            raf.Seek(currentDataOff, SeekOrigin.Begin);
            raf.Write(tag.data, tag.offset, tag.count);
            currentDataOff += tag.count;
        }
    }


    private class Triplet
    {
        /// <summary>size of an entry            </summary>
        //UPGRADE_NOTE: Final was removed from the declaration of 'size '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        //UPGRADE_NOTE: The initialization of  'size' was moved to static method 'icc.tags.ICCTagTable.Triplet'. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1005'"
        public static readonly int size;

        /// <summary>length of tag data          </summary>
        internal readonly int count;

        /// <summary>absolute offset of tag data </summary>
        internal readonly int offset;

        /// <summary>Tag identifier              </summary>
        internal readonly int signature;

        static Triplet()
        {
            size = 3 * ICCProfile.int_size;
        }


        internal Triplet(int signature, int offset, int count)
        {
            this.signature = signature;
            this.offset = offset;
            this.count = count;
        }
    }
}
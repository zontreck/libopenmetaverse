/// <summary>**************************************************************************
/// 
/// $Id: ICCDateTime.java,v 1.1 2002/07/25 14:56:31 grosbois Exp $
/// 
/// Copyright Eastman Kodak Company, 343 State Street, Rochester, NY 14650
/// $Date $
/// ***************************************************************************
/// </summary>

using System;
using System.IO;

namespace CSJ2K.Icc.Types;

/// <summary>
///     Date Time format for tags
/// </summary>
/// <version>
///     1.0
/// </version>
/// <author>
///     Bruce A. Kern
/// </author>
public class ICCDateTime
{
    //UPGRADE_NOTE: Final was removed from the declaration of 'size '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
    //UPGRADE_NOTE: The initialization of  'size' was moved to static method 'icc.types.ICCDateTime'. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1005'"
    public static readonly int size;

    /// <summary>Day datum.    </summary>
    // Number of the month (1-12)
    public short wDay;

    /// <summary>Hour datum.   </summary>
    // Number of the day
    public short wHours;

    /// <summary>Minute datum. </summary>
    // Number of hours (0-23)
    public short wMinutes;

    /// <summary>Month datum.  </summary>
    // Number of the actual year (i.e. 1994)
    public short wMonth;

    /// <summary>Second datum. </summary>
    // Number of minutes (0-59)
    public short wSeconds; // Number of seconds (0-59)

    /// <summary>Year datum.   </summary>
    public short wYear;

    /* end class ICCDateTime*/
    static ICCDateTime()
    {
        size = 6 * ICCProfile.short_size;
    }

    /// <summary>Construct an ICCDateTime from parts </summary>
    public ICCDateTime(short year, short month, short day, short hour, short minute, short second)
    {
        wYear = year;
        wMonth = month;
        wDay = day;
        wHours = hour;
        wMinutes = minute;
        wSeconds = second;
    }

    /// <summary>Write an ICCDateTime to a file. </summary>
    //UPGRADE_TODO: Class 'java.io.RandomAccessFile' was converted to 'System.IO.FileStream' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javaioRandomAccessFile'"
    public virtual void write(FileStream raf)
    {
        BinaryWriter temp_BinaryWriter;
        temp_BinaryWriter = new BinaryWriter(raf);
        temp_BinaryWriter.Write(wYear);
        BinaryWriter temp_BinaryWriter2;
        temp_BinaryWriter2 = new BinaryWriter(raf);
        temp_BinaryWriter2.Write(wMonth);
        BinaryWriter temp_BinaryWriter3;
        temp_BinaryWriter3 = new BinaryWriter(raf);
        temp_BinaryWriter3.Write(wDay);
        BinaryWriter temp_BinaryWriter4;
        temp_BinaryWriter4 = new BinaryWriter(raf);
        temp_BinaryWriter4.Write(wHours);
        BinaryWriter temp_BinaryWriter5;
        temp_BinaryWriter5 = new BinaryWriter(raf);
        temp_BinaryWriter5.Write(wMinutes);
        BinaryWriter temp_BinaryWriter6;
        temp_BinaryWriter6 = new BinaryWriter(raf);
        temp_BinaryWriter6.Write(wSeconds);
    }

    /// <summary>Return a ICCDateTime representation. </summary>
    public override string ToString()
    {
        //System.String rep = "";
        return Convert.ToString(wYear) + "/" + Convert.ToString(wMonth) + "/" + Convert.ToString(wDay) + " " +
               Convert.ToString(wHours) + ":" + Convert.ToString(wMinutes) + ":" + Convert.ToString(wSeconds);
    }
}
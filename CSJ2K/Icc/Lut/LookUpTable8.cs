/// <summary>**************************************************************************
/// 
/// $Id: LookUpTable8.java,v 1.1 2002/07/25 14:56:48 grosbois Exp $
/// 
/// Copyright Eastman Kodak Company, 343 State Street, Rochester, NY 14650
/// $Date $
/// ***************************************************************************
/// </summary>

using System.Text;
using CSJ2K.Icc.Tags;

namespace CSJ2K.Icc.Lut;

/// <summary>
///     Toplevel class for a byte [] lut.
/// </summary>
/// <version>
///     1.0
/// </version>
/// <author>
///     Bruce A. Kern
/// </author>
public abstract class LookUpTable8 : LookUpTable
{
    /// <summary>Maximum output value of the LUT </summary>
    //UPGRADE_NOTE: Final was removed from the declaration of 'dwMaxOutput '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
    protected internal byte dwMaxOutput;

    /// <summary>The lut values.                 </summary>
    // Maximum output value of the LUT
    //UPGRADE_NOTE: Final was removed from the declaration of 'lut '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
    protected internal byte[] lut;

    protected internal LookUpTable8(int dwNumInput, byte dwMaxOutput) : base(null, dwNumInput)
    {
        lut = new byte[dwNumInput];
        this.dwMaxOutput = dwMaxOutput;
    }


    /// <summary> Create the string representation of a 16 bit lut.</summary>
    /// <returns>
    ///     the lut as a String
    /// </returns>
    protected internal LookUpTable8(ICCCurveType curve, int dwNumInput, byte dwMaxOutput) : base(curve, dwNumInput)
    {
        this.dwMaxOutput = dwMaxOutput;
        lut = new byte[dwNumInput];
    }


    /// <summary> Create an abbreviated string representation of a 16 bit lut.</summary>
    /// <returns>
    ///     the lut as a String
    /// </returns>
    public override string ToString()
    {
        var rep = new StringBuilder("[LookUpTable8 ");
        //int row, col;
        rep.Append("max= " + dwMaxOutput);
        rep.Append(", nentries= " + dwMaxOutput);
        return rep.Append("]").ToString();
    }


    public virtual string toStringWholeLut()
    {
        var rep = new StringBuilder("LookUpTable8" + eol);
        rep.Append("maxOutput = " + dwMaxOutput + eol);
        for (var i = 0; i < dwNumInput; ++i)
            rep.Append("lut[" + i + "] = " + lut[i] + eol);
        return rep.Append("]").ToString();
    }

    /// <summary> lut accessor</summary>
    /// <param name="index">
    ///     of the element
    /// </param>
    /// <returns>
    ///     the lut [index]
    /// </returns>
    public byte elementAt(int index)
    {
        return lut[index];
    }

    /* end class LookUpTable8 */
}
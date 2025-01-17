/// <summary>**************************************************************************
/// 
/// $Id: RestrictedICCProfile.java,v 1.1 2002/07/25 14:56:56 grosbois Exp $
/// 
/// Copyright Eastman Kodak Company, 343 State Street, Rochester, NY 14650
/// $Date $
/// ***************************************************************************
/// </summary>

using System;
using CSJ2K.Icc.Tags;

namespace CSJ2K.Icc;

/// <summary>
///     This profile is constructed by parsing an ICCProfile and
///     is the profile actually applied to the image.
/// </summary>
/// <seealso cref="jj2000.j2k.icc.ICCProfile">
/// </seealso>
/// <version>
///     1.0
/// </version>
/// <author>
///     Bruce A. Kern
/// </author>
public abstract class RestrictedICCProfile
{
    /// <summary>input type enumerator </summary>
    public const int kMonochromeInput = 0;

    /// <summary>input type enumerator </summary>
    public const int kThreeCompInput = 1;

    //UPGRADE_NOTE: Final was removed from the declaration of 'eol '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
    protected internal static readonly string eol = Environment.NewLine;

    /// <summary>Component index       </summary>
    //UPGRADE_NOTE: Final was removed from the declaration of 'GRAY '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
    //UPGRADE_NOTE: The initialization of  'GRAY' was moved to static method 'icc.RestrictedICCProfile'. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1005'"
    protected internal static readonly int GRAY;

    /// <summary>Component index       </summary>
    //UPGRADE_NOTE: Final was removed from the declaration of 'RED '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
    //UPGRADE_NOTE: The initialization of  'RED' was moved to static method 'icc.RestrictedICCProfile'. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1005'"
    protected internal static readonly int RED;

    /// <summary>Component index       </summary>
    //UPGRADE_NOTE: Final was removed from the declaration of 'GREEN '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
    //UPGRADE_NOTE: The initialization of  'GREEN' was moved to static method 'icc.RestrictedICCProfile'. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1005'"
    protected internal static readonly int GREEN;

    /// <summary>Component index       </summary>
    //UPGRADE_NOTE: Final was removed from the declaration of 'BLUE '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
    //UPGRADE_NOTE: The initialization of  'BLUE' was moved to static method 'icc.RestrictedICCProfile'. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1005'"
    protected internal static readonly int BLUE;

    /// <summary>Colorant data </summary>
    public ICCXYZType[] colorant;

    /// <summary>Curve data    </summary>
    public ICCCurveType[] trc;

    /* end class RestrictedICCProfile */
    static RestrictedICCProfile()
    {
        GRAY = ICCProfile.GRAY;
        RED = ICCProfile.RED;
        GREEN = ICCProfile.GREEN;
        BLUE = ICCProfile.BLUE;
    }

    /// <summary> Construct the common state of all gray RestrictedICCProfiles</summary>
    /// <param name="gcurve">
    ///     curve data
    /// </param>
    protected internal RestrictedICCProfile(ICCCurveType gcurve)
    {
        trc = new ICCCurveType[1];
        colorant = null;
        trc[GRAY] = gcurve;
    }

    /// <summary>
    ///     Construct the common state of all 3 component RestrictedICCProfiles
    /// </summary>
    /// <param name="rcurve">
    ///     red curve
    /// </param>
    /// <param name="gcurve">
    ///     green curve
    /// </param>
    /// <param name="bcurve">
    ///     blue curve
    /// </param>
    /// <param name="rcolorant">
    ///     red colorant
    /// </param>
    /// <param name="gcolorant">
    ///     green colorant
    /// </param>
    /// <param name="bcolorant">
    ///     blue colorant
    /// </param>
    protected internal RestrictedICCProfile(ICCCurveType rcurve, ICCCurveType gcurve, ICCCurveType bcurve,
        ICCXYZType rcolorant, ICCXYZType gcolorant, ICCXYZType bcolorant)
    {
        trc = new ICCCurveType[3];
        colorant = new ICCXYZType[3];

        trc[RED] = rcurve;
        trc[GREEN] = gcurve;
        trc[BLUE] = bcurve;

        colorant[RED] = rcolorant;
        colorant[GREEN] = gcolorant;
        colorant[BLUE] = bcolorant;
    }

    /// <summary>Returns the appropriate input type enum. </summary>
    public abstract int Type { get; }

    /// <summary>
    ///     Factory method for creating a RestrictedICCProfile from
    ///     3 component curve and colorant data.
    /// </summary>
    /// <param name="rcurve">
    ///     red curve
    /// </param>
    /// <param name="gcurve">
    ///     green curve
    /// </param>
    /// <param name="bcurve">
    ///     blue curve
    /// </param>
    /// <param name="rcolorant">
    ///     red colorant
    /// </param>
    /// <param name="gcolorant">
    ///     green colorant
    /// </param>
    /// <param name="bcolorant">
    ///     blue colorant
    /// </param>
    /// <returns>
    ///     MatrixBasedRestrictedProfile
    /// </returns>
    public static RestrictedICCProfile createInstance(ICCCurveType rcurve, ICCCurveType gcurve, ICCCurveType bcurve,
        ICCXYZType rcolorant, ICCXYZType gcolorant, ICCXYZType bcolorant)
    {
        return MatrixBasedRestrictedProfile.createInstance(rcurve, gcurve, bcurve, rcolorant, gcolorant, bcolorant);
    }

    /// <summary>
    ///     Factory method for creating a RestrictedICCProfile from
    ///     gray curve data.
    /// </summary>
    /// <param name="gcurve">
    ///     gray curve
    /// </param>
    /// <returns>
    ///     MonochromeInputRestrictedProfile
    /// </returns>
    public static RestrictedICCProfile createInstance(ICCCurveType gcurve)
    {
        return MonochromeInputRestrictedProfile.createInstance(gcurve);
    }
}
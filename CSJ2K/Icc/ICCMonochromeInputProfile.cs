/// <summary>**************************************************************************
/// 
/// $Id: ICCMonochromeInputProfile.java,v 1.1 2002/07/25 14:56:54 grosbois Exp $
/// 
/// Copyright Eastman Kodak Company, 343 State Street, Rochester, NY 14650
/// $Date $
/// ***************************************************************************
/// </summary>

using CSJ2K.Color;

namespace CSJ2K.Icc;

/// <summary>
///     The monochrome ICCProfile.
/// </summary>
/// <version>
///     1.0
/// </version>
/// <author>
///     Bruce A. Kern
/// </author>
public class ICCMonochromeInputProfile : ICCProfile
{
    /// <summary> Construct a ICCMonochromeInputProfile corresponding to the profile file</summary>
    /// <param name="f">
    ///     disk based ICCMonochromeInputProfile
    /// </param>
    /// <returns>
    ///     theICCMonochromeInputProfile
    /// </returns>
    /// <exception cref="ColorSpaceException">
    /// </exception>
    /// <exception cref="ICCProfileInvalidException">
    /// </exception>
    protected internal ICCMonochromeInputProfile(ColorSpace csm) : base(csm)
    {
    }

    /// <summary> Return the ICCProfile embedded in the input image</summary>
    /// <param name="in">
    ///     jp2 image with embedded profile
    /// </param>
    /// <returns>
    ///     ICCMonochromeInputProfile
    /// </returns>
    /// <exception cref="ColorSpaceICCProfileInvalidExceptionException">
    /// </exception>
    /// <exception cref="">
    /// </exception>
    public static ICCMonochromeInputProfile createInstance(ColorSpace csm)
    {
        return new ICCMonochromeInputProfile(csm);
    }

    /* end class ICCMonochromeInputProfile */
}
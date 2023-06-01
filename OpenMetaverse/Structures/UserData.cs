using System.Collections.Generic;

namespace OpenMetaverse.Structures;

public class UserData
{
    public UUID Id;
    public string FirstName;
    public string LastName;
    public string HomeURL;
    public Dictionary<string, object> ServerURLs;
    public bool IsUnknownUser;
    public bool HasGridUserTried;
    public bool IsLocal;
    public double LastWebFail = -1;
    public DisplayName UserDisplayName = new DisplayName();
}

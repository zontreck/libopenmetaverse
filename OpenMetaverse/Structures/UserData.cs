using System.Collections.Generic;

namespace OpenMetaverse.Structures;

public class UserData
{
    public string FirstName;
    public bool HasGridUserTried;
    public string HomeURL;
    public UUID Id;
    public bool IsLocal;
    public bool IsUnknownUser;
    public string LastName;
    public double LastWebFail = -1;
    public Dictionary<string, object> ServerURLs;
    public DisplayName UserDisplayName = new();
}
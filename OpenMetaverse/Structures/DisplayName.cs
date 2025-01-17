using System;

namespace OpenMetaverse.Structures;

/// <summary>
///     A miniaturized data structure for the user data only.
///     You should probably be using AgentDisplayName
/// </summary>
public class DisplayName
{
    public string Current;

    public DateTime LastChange = DateTime.Now;

    public bool isModified
    {
        get
        {
            if (Current != "") return true;
            return false;
        }
    }
}
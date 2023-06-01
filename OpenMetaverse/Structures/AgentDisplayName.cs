using System;
using OpenMetaverse;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse.Structures;

public class AgentDisplayName
{
    /// <summary> Agent UUID </summary>
    public UUID ID;

    /// <summary> Username </summary>
    public string UserName;

    /// <summary> Display name </summary>
    public string DisplayName;

    /// <summary> First name (legacy) </summary>
    public string LegacyFirstName;

    /// <summary> Last name (legacy) </summary>
    public string LegacyLastName;

    /// <summary> Full name (legacy) </summary>
    public string LegacyFullName
    {
        get { return string.Format("{0} {1}", LegacyFirstName, LegacyLastName); }
    }

    /// <summary> Is display name default display name </summary>
    public bool IsDefaultDisplayName;

    /// <summary> Cache display name until </summary>
    public DateTime NextUpdate;

    /// <summary> Last updated timestamp </summary>
    public DateTime Updated;

    /// <summary>
    /// Creates AgentDisplayName object from OSD
    /// </summary>
    /// <param name="data">Incoming OSD data</param>
    /// <returns>AgentDisplayName object</returns>
    public static AgentDisplayName FromOSD(OSD data)
    {
        AgentDisplayName ret = new AgentDisplayName();

        OSDMap map = (OSDMap)data;
        ret.ID = map["id"];
        ret.UserName = map["username"];
        ret.DisplayName = map["display_name"];
        ret.LegacyFirstName = map["legacy_first_name"];
        ret.LegacyLastName = map["legacy_last_name"];
        ret.IsDefaultDisplayName = map["is_display_name_default"];
        ret.NextUpdate = map["display_name_next_update"];
        ret.Updated = map["last_updated"];

        return ret;
    }

    /// <summary>
    /// Return object as OSD map
    /// </summary>
    /// <returns>OSD containing agent's display name data</returns>
    public OSD GetOSD()
    {
        OSDMap map = new OSDMap();

        map["id"] = ID;
        map["username"] = UserName;
        map["display_name"] = DisplayName;
        map["legacy_first_name"] = LegacyFirstName;
        map["legacy_last_name"] = LegacyLastName;
        map["is_display_name_default"] = IsDefaultDisplayName;
        map["display_name_next_update"] = NextUpdate;
        map["last_updated"] = Updated;

        return map;
    }

    public AgentDisplayName()
    {
        
    }

    public AgentDisplayName(UserData data)
    {
        ID = data.Id;
        LegacyFirstName = data.FirstName;
        LegacyLastName = data.LastName;
        IsDefaultDisplayName = !data.UserDisplayName.isModified;
        NextUpdate = data.UserDisplayName.LastChange.AddHours(1); // TODO: Make this able to be customized at the server level
        Updated = data.UserDisplayName.LastChange;
        UserName = $"{LegacyFirstName}.${LegacyLastName}".ToLower();
        DisplayName = (data.UserDisplayName.isModified ? data.UserDisplayName.Current : LegacyFullName);
    }

    public override string ToString()
    {
        return Helpers.StructToString(this);
    }

    public DisplayNameUpdateMessage CreateUpdateMessage(string oldName)
    {
        return new DisplayNameUpdateMessage()
        {
            OldDisplayName = oldName,
            DisplayName = this
        };
    }
}